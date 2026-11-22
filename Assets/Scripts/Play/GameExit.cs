using UnityEngine;
using UnityEngine.InputSystem;

namespace Play
{
    /// <summary>
    /// 全局退出：按 Exit（默认 Esc）直接退出游戏。
    /// 第一条场景加载后自动创建一个常驻实例，切场景/卸载场景都不会丢。
    /// </summary>
    public class GameExit : MonoBehaviour
    {
        private PlayerInputController _input;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var existing = FindFirstObjectByType<GameExit>(FindObjectsInactive.Include);
            if (existing != null) return;

            var go = new GameObject("[GameExit]");
            DontDestroyOnLoad(go);
            go.AddComponent<GameExit>();
        }

        private void Awake()
        {
            _input = new PlayerInputController();
        }

        private void OnEnable()
        {
            _input.Gameplay.Enable();
            _input.Gameplay.Exit.started += OnExit;
        }

        private void OnDisable()
        {
            _input.Gameplay.Exit.started -= OnExit;
            _input.Gameplay.Disable();
        }

        private void OnExit(InputAction.CallbackContext context)
        {
            Quit();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
