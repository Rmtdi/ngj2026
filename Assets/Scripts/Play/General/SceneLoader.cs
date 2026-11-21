using System;
using System.Collections;
using Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Play.General
{
    /// <summary>
    /// 场景管理器：异步加载 / 卸载场景，可选的淡入淡出，加载完成后把玩家放到指定位置。
    ///
    /// 用法：
    ///   SceneLoader.Instance.LoadScene("Level_02", new Vector3(3, 1, 0));      // 带渐变 + 传送
    ///   SceneLoader.Instance.LoadScene("Level_02", null, fade: false);          // 不渐变、玩家位置不动
    ///   SceneLoader.Instance.UnloadScene("Level_01");                           // 卸载并渐变
    ///
    /// 需要挂在一个场景根节点下的空物体上（会 DontDestroyOnLoad 常驻）。
    /// </summary>
    [AddComponentMenu("NGJ2026/场景管理器 SceneLoader")]
    [DisallowMultipleComponent]
    public class SceneLoader : MonoSingleton<SceneLoader>
    {
        [Header("渐变")]
        [SerializeField]
        private Color fadeColor = Color.black;

        [SerializeField, Min(0f)]
        private float fadeOutDuration = 0.25f;

        [SerializeField, Min(0f)]
        private float fadeInDuration = 0.25f;

        [Header("玩家")]
        [Tooltip("玩家 Tag。留空则按 PlayHandler 组件找")]
        [SerializeField]
        private string playerTag = "Player";

        private CanvasGroup _fadeGroup;
        private string _currentScene;

        /// <summary>是否正在加载/卸载中。加载期间重复调用会被忽略。</summary>
        public bool IsLoading { get; private set; }

        /// <summary>0~1 的加载进度，可以做进度条。</summary>
        public float Progress { get; private set; }

        /// <summary>当前场景名。</summary>
        public string CurrentScene => _currentScene;

        /// <summary>场景加载完成（已放置玩家、淡入之前）时触发。</summary>
        public event Action<string> SceneLoaded;

        protected override void OnInstanceReady()
        {
            EnsureFadeCanvas();
            _currentScene = SceneManager.GetActiveScene().name;
        }

        /// <summary>
        /// 异步加载场景（叠加模式），并把当前记录的场景卸掉。
        /// </summary>
        /// <param name="sceneName">场景名（必须在 Build Settings 里）</param>
        /// <param name="spawnPosition">玩家出生位置；传 null 则不移动玩家</param>
        /// <param name="fade">是否渐变</param>
        public void LoadScene(string sceneName, Vector3? spawnPosition = null, bool fade = true)
        {
            if (IsLoading)
            {
                Debug.LogWarning($"[SceneLoader] 正在加载中，忽略本次请求：{sceneName}", this);
                return;
            }

            StartCoroutine(LoadRoutine(sceneName, spawnPosition, fade));
        }

        /// <summary>异步卸载场景，可带渐变。</summary>
        public void UnloadScene(string sceneName, bool fade = true)
        {
            if (IsLoading)
            {
                Debug.LogWarning($"[SceneLoader] 正在加载中，忽略本次卸载请求：{sceneName}", this);
                return;
            }

            StartCoroutine(UnloadRoutine(sceneName, fade));
        }

        /// <summary>卸载当前记录的场景。</summary>
        public void UnloadCurrentScene(bool fade = true)
        {
            if (!string.IsNullOrEmpty(_currentScene)) UnloadScene(_currentScene, fade);
        }

        private IEnumerator LoadRoutine(string sceneName, Vector3? spawnPosition, bool fade)
        {
            IsLoading = true;
            Progress = 0f;
            SetInputBlocked(true);

            // 1. 淡出
            if (fade) yield return Fade(1f, fadeOutDuration);

            // 2. 卸载当前场景
            if (!string.IsNullOrEmpty(_currentScene) && _currentScene != sceneName)
            {
                var current = SceneManager.GetSceneByName(_currentScene);
                if (current.isLoaded)
                {
                    var unload = SceneManager.UnloadSceneAsync(current);
                    while (unload != null && !unload.isDone) yield return null;
                }
            }

            // 3. 异步加载新场景（先激活关闭，等过渡完再切）
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError($"[SceneLoader] 加载失败：{sceneName} 不在 Build Settings 里？", this);
                yield return Fade(0f, fadeInDuration);
                SetInputBlocked(false);
                IsLoading = false;
                yield break;
            }

            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                Progress = Mathf.Clamp01(op.progress / 0.9f);
                yield return null;
            }

            Progress = 1f;
            op.allowSceneActivation = true;

            while (!op.isDone) yield return null;

            var loaded = SceneManager.GetSceneByName(sceneName);
            if (loaded.IsValid() && loaded.isLoaded)
            {
                SceneManager.SetActiveScene(loaded);
            }

            _currentScene = sceneName;

            // 4. 把玩家放到目标位置
            if (spawnPosition.HasValue) PlacePlayer(spawnPosition.Value);

            SceneLoaded?.Invoke(sceneName);

            // 5. 淡入
            if (fade) yield return Fade(0f, fadeInDuration);

            SetInputBlocked(false);
            IsLoading = false;
        }

        private IEnumerator UnloadRoutine(string sceneName, bool fade)
        {
            IsLoading = true;
            SetInputBlocked(true);

            if (fade) yield return Fade(1f, fadeOutDuration);

            var scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                var op = SceneManager.UnloadSceneAsync(scene);
                while (op != null && !op.isDone) yield return null;
            }
            else
            {
                Debug.LogWarning($"[SceneLoader] 场景未加载，无法卸载：{sceneName}", this);
            }

            if (_currentScene == sceneName) _currentScene = null;

            if (fade) yield return Fade(0f, fadeInDuration);

            SetInputBlocked(false);
            IsLoading = false;
        }

        /// <summary>找到玩家并把它放到目标位置（同时清掉刚体残留速度）。</summary>
        private void PlacePlayer(Vector3 position)
        {
            var player = FindPlayer();
            if (player == null)
            {
                Debug.LogWarning("[SceneLoader] 没找到玩家，跳过传送。可以设置 playerTag 或确认场景里有 PlayHandler。", this);
                return;
            }

            player.transform.position = position;

            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.position = position;
                rb.velocity = Vector2.zero;
            }
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
                    // Tag 没在 TagManager 里定义，忽略，走下面的备用方案
                }
            }

            var handler = FindFirstObjectByType<PlayHandler>(FindObjectsInactive.Exclude);
            return handler != null ? handler.gameObject : null;
        }

        private void SetInputBlocked(bool blocked)
        {
            if (_fadeGroup == null) return;

            _fadeGroup.blocksRaycasts = blocked;
            _fadeGroup.interactable = false;
        }

        private IEnumerator Fade(float target, float duration)
        {
            if (_fadeGroup == null) yield break;

            if (duration <= 0f)
            {
                _fadeGroup.alpha = target;
                yield break;
            }

            var start = _fadeGroup.alpha;
            var time = 0f;

            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                _fadeGroup.alpha = Mathf.Lerp(start, target, time / duration);
                yield return null;
            }

            _fadeGroup.alpha = target;
        }

        /// <summary>运行时生成一张全屏黑图做渐变，不需要美术资源。</summary>
        private void EnsureFadeCanvas()
        {
            if (_fadeGroup != null) return;

            var canvasGo = new GameObject("[SceneLoaderFade]");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            _fadeGroup = canvasGo.AddComponent<CanvasGroup>();
            _fadeGroup.alpha = 0f;
            _fadeGroup.blocksRaycasts = false;
            _fadeGroup.interactable = false;

            var imageGo = new GameObject("Fade");
            imageGo.transform.SetParent(canvasGo.transform, false);

            var image = imageGo.AddComponent<Image>();
            image.color = fadeColor;
            image.raycastTarget = false;

            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
