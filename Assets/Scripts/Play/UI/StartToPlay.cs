using System.Collections;
using Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Play.UI
{
    /// <summary>
    /// 主菜单 → 第一关。
    /// 流程：后台并行加载 Main + 关卡 → 放玩家/对准相机 → 淡出 UI → 菜单里的机器人换成真实姿态
    ///      → 关菜单相机 → 卸载 Menu。
    /// </summary>
    public class StartToPlay : MonoBehaviour
    {
        [Header("UI 渐变")]
        [SerializeField]
        private CanvasGroup menuCanvas;

        [SerializeField, Min(0f)]
        private float fadeDuration = 0.3f;

        [Header("菜单里的预览")]
        [SerializeField]
        private Camera menuCamera;

        [SerializeField]
        private SpriteRenderer menuRobot;

        [SerializeField]
        private SpriteRenderer menuArrow;

        [Header("场景")]
        [SerializeField]
        private string persistentScene = "Main";

        [SerializeField]
        private string firstScene = "The First Level";

        [SerializeField]
        private Vector3 spawnPosition = Vector3.zero;

        [SerializeField]
        private string playerTag = "Player";

        private bool _loading;
        private string _menuSceneName;

        private void Awake()
        {
            _menuSceneName = gameObject.scene.name;
        }

        public void LoadFirstScene()
        {
            if (_loading) return;

            _loading = true;
            StartCoroutine(LoadRoutine());
        }

        private IEnumerator LoadRoutine()
        {
            // Menu 的 EventSystem 用完就让位，否则和 Main 的 EventSystem 同时存在会一直警告
            DisableMenuEventSystem();

            // 关卡如果之前已经加载过，先卸掉，保证每次都是干净的
            var oldLevel = SceneManager.GetSceneByName(firstScene);
            if (oldLevel.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(oldLevel);
                while (unload != null && !unload.isDone) yield return null;
            }

            // Main 已经加载过就不重复加载（Unity 不允许同一场景加载两次）
            var mainLoaded = SceneManager.GetSceneByName(persistentScene).isLoaded;
            var mainOp = mainLoaded
                ? null
                : SceneManager.LoadSceneAsync(persistentScene, LoadSceneMode.Additive);

            var levelOp = SceneManager.LoadSceneAsync(firstScene, LoadSceneMode.Additive);
            if (levelOp == null)
            {
                Debug.LogError($"[StartToPlay] 关卡加载失败：{firstScene} 不在 Build Settings 里？", this);
                _loading = false;
                yield break;
            }

            while ((mainOp != null && !mainOp.isDone) || !levelOp.isDone) yield return null;

            var level = SceneManager.GetSceneByName(firstScene);
            if (level.IsValid() && level.isLoaded) SceneManager.SetActiveScene(level);

            var player = FindPlayer();
            PlacePlayer(player, spawnPosition);
            SnapCameraTo(spawnPosition);

            yield return FadeOutMenu();

            CopyPlayerLook(player);

            if (menuCamera != null) menuCamera.enabled = false;

            yield return null;

            var menu = SceneManager.GetSceneByName(_menuSceneName);
            if (menu.IsValid() && menu.isLoaded)
            {
                var unloadOp = SceneManager.UnloadSceneAsync(menu);
                while (unloadOp != null && !unloadOp.isDone) yield return null;
            }

            _loading = false;
        }

        /// <summary>关掉 Menu 场景里的 EventSystem，避免和 Main 里的同时存在。</summary>
        private void DisableMenuEventSystem()
        {
            var menu = SceneManager.GetSceneByName(_menuSceneName);
            if (!menu.IsValid()) return;

            foreach (var root in menu.GetRootGameObjects())
            {
                var eventSystem = root.GetComponentInChildren<EventSystem>(true);
                if (eventSystem == null) continue;

                eventSystem.enabled = false;

                if (EventSystem.current == eventSystem) EventSystem.current = null;

                break;
            }
        }

        private IEnumerator FadeOutMenu()
        {
            var startAlpha = menuCanvas != null ? menuCanvas.alpha : 0f;
            var startArrow = menuArrow != null ? menuArrow.color.a : 0f;
            var time = 0f;

            while (time < fadeDuration)
            {
                time += Time.unscaledDeltaTime;
                var t = fadeDuration <= 0f ? 1f : time / fadeDuration;

                if (menuCanvas != null) menuCanvas.alpha = Mathf.Lerp(startAlpha, 0f, t);

                if (menuArrow != null)
                {
                    var c = menuArrow.color;
                    c.a = Mathf.Lerp(startArrow, 0f, t);
                    menuArrow.color = c;
                }

                yield return null;
            }

            if (menuCanvas != null)
            {
                menuCanvas.alpha = 0f;
                menuCanvas.blocksRaycasts = false;
                menuCanvas.interactable = false;
            }

            if (menuArrow != null)
            {
                var c = menuArrow.color;
                c.a = 0f;
                menuArrow.color = c;
                menuArrow.enabled = false;
            }
        }

        private void CopyPlayerLook(GameObject player)
        {
            if (menuRobot == null || player == null) return;

            var playerRenderer = player.GetComponent<SpriteRenderer>();
            if (playerRenderer == null) return;

            menuRobot.sprite = playerRenderer.sprite;
            menuRobot.flipX = playerRenderer.flipX;
            menuRobot.flipY = playerRenderer.flipY;
            menuRobot.sortingOrder = playerRenderer.sortingOrder;

            menuRobot.transform.position = player.transform.position;
            menuRobot.transform.localScale = player.transform.lossyScale;
        }

        private GameObject FindPlayer()
        {
            if (!string.IsNullOrEmpty(playerTag))
            {
                try
                {
                    var byTag = GameObject.FindWithTag(playerTag);
                    if (byTag != null) return byTag;
                }
                catch (UnityException)
                {
                }
            }

            var handler = FindFirstObjectByType<PlayHandler>(FindObjectsInactive.Exclude);
            return handler != null ? handler.gameObject : null;
        }

        private void PlacePlayer(GameObject player, Vector3 position)
        {
            if (player == null)
            {
                Debug.LogWarning("[StartToPlay] 没找到玩家，跳过传送。", this);
                return;
            }

            player.transform.position = position;

            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                var interpolation = rb.interpolation;
                rb.interpolation = RigidbodyInterpolation2D.None;
                rb.position = position;
                rb.velocity = Vector2.zero;
                Physics2D.SyncTransforms();
                rb.interpolation = interpolation;
            }
        }

        private void SnapCameraTo(Vector3 position)
        {
            var brain = FindFirstObjectByType<CinemachineBrain>(FindObjectsInactive.Exclude);
            if (brain == null) return;

            var cam = brain.GetComponent<Camera>();
            if (cam == null) return;

            var p = cam.transform.position;
            cam.transform.position = new Vector3(position.x, position.y, p.z);
        }
    }
}
