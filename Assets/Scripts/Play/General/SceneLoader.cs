using System;
using System.Collections;
using Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Play.General
{
    /// <summary>
    /// 场景管理器：异步加载 / 卸载关卡，可选的淡入淡出，加载完成后把玩家放到指定位置。
    ///
    /// 场景结构约定（推荐）：
    ///   Main（常驻：玩家、主相机、AudioManager、SceneLoader、常驻 UI）
    ///     └─ 通过 LoadScene 叠加 The First Level / The Second Level ...
    /// 加载新关卡时，会自动卸掉"除常驻场景和目标场景以外的所有已加载场景"。
    ///
    /// 用法：
    ///   SceneLoader.Instance.LoadScene("The First Level", new Vector3(3, 1, 0));
    ///   SceneLoader.Instance.LoadScene("The Second Level", 出生点, fade: false);
    ///   SceneLoader.Instance.UnloadScene("The First Level");
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

        [Header("场景")]
        [Tooltip("常驻场景名（放 SceneLoader 的那个，比如 Main）。它永远不会被自动卸载")]
        [SerializeField]
        private string persistentScene = "Main";

        [Tooltip("允许卸载常驻场景。一般不要开，否则玩家/相机/管理器会一起没")]
        [SerializeField]
        private bool allowUnloadPersistent = false;

        [Header("玩家")]
        [Tooltip("玩家 Tag。留空则按 PlayHandler 组件找")]
        [SerializeField]
        private string playerTag = "Player";

        private CanvasGroup _fadeGroup;

        /// <summary>是否正在加载/卸载中。加载期间重复调用会被忽略。</summary>
        public bool IsLoading { get; private set; }

        /// <summary>0~1 的加载进度，可以做进度条。</summary>
        public float Progress { get; private set; }

        /// <summary>当前关卡场景名（没加载关卡时返回当前活动场景）。</summary>
        public string CurrentScene => SceneManager.GetActiveScene().name;

        /// <summary>常驻场景名。</summary>
        public string PersistentScene => persistentScene;

        /// <summary>场景加载完成（玩家已放好、淡入之前）时触发。</summary>
        public event Action<string> SceneLoaded;

        protected override void OnInstanceReady()
        {
            EnsureFadeCanvas();
        }

        /// <summary>场景里忘了放 SceneLoader 时兜底创建一个，避免调用方空引用。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;

            var go = new GameObject("[SceneLoader]（自动创建）");
            go.AddComponent<SceneLoader>();

            Debug.LogWarning("[SceneLoader] 场景里没有 SceneLoader，已自动创建。建议在场景里手动放一个并配置参数。", go);
        }

        // ------------------------------------------------------------------
        // 公开接口
        // ------------------------------------------------------------------

        /// <summary>
        /// 异步加载关卡（叠加模式）。会自动卸掉除常驻场景外的其它场景。
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

            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[SceneLoader] 场景名为空。", this);
                return;
            }

            StartCoroutine(LoadRoutine(sceneName, spawnPosition, fade));
        }

        /// <summary>异步卸载场景，可带渐变。常驻场景默认不允许卸载。</summary>
        public void UnloadScene(string sceneName, bool fade = true)
        {
            if (IsLoading)
            {
                Debug.LogWarning($"[SceneLoader] 正在加载中，忽略本次卸载请求：{sceneName}", this);
                return;
            }

            if (!allowUnloadPersistent && sceneName == persistentScene)
            {
                Debug.LogWarning($"[SceneLoader] {sceneName} 是常驻场景，默认不卸载。真要卸请勾 allowUnloadPersistent。", this);
                return;
            }

            StartCoroutine(UnloadRoutine(sceneName, fade));
        }

        /// <summary>卸载当前关卡（不是常驻场景时才有意义）。</summary>
        public void UnloadCurrentScene(bool fade = true)
        {
            var current = CurrentScene;
            if (string.IsNullOrEmpty(current) || current == persistentScene)
            {
                Debug.LogWarning("[SceneLoader] 当前就是常驻场景，没有可卸载的关卡。", this);
                return;
            }

            UnloadScene(current, fade);
        }

        // ------------------------------------------------------------------
        // 加载 / 卸载流程
        // ------------------------------------------------------------------

        private IEnumerator LoadRoutine(string sceneName, Vector3? spawnPosition, bool fade)
        {
            IsLoading = true;
            Progress = 0f;
            SetInputBlocked(true);

            // 让调用方的事件链先跑完，再开始卸场景
            yield return null;

            // 1. 淡出
            if (fade) yield return Fade(1f, fadeOutDuration);

            // 2. 卸掉除"常驻场景"和"目标场景"以外的所有已加载场景
            yield return UnloadOtherScenes(sceneName);

            // 3. 异步加载目标场景（先关激活，等过渡完再切）
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

            if (fade) yield return Fade(0f, fadeInDuration);

            SetInputBlocked(false);
            IsLoading = false;
        }

        /// <summary>卸掉除常驻场景、目标场景、DontDestroyOnLoad 之外的所有已加载场景。</summary>
        private IEnumerator UnloadOtherScenes(string keepScene)
        {
            // 倒序遍历：卸载会改变场景列表
            for (var i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded) continue;
                if (scene.name == keepScene || scene.name == persistentScene) continue;
                if (scene.name == "DontDestroyOnLoad") continue;

                Debug.Log($"[SceneLoader] 卸载场景：{scene.name}", this);

                var op = SceneManager.UnloadSceneAsync(scene);
                while (op != null && !op.isDone) yield return null;
            }
        }

        // ------------------------------------------------------------------
        // 玩家
        // ------------------------------------------------------------------

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

        // ------------------------------------------------------------------
        // 渐变
        // ------------------------------------------------------------------

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
