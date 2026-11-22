using UnityEngine;
using UnityEngine.InputSystem;

namespace Play
{
    /// <summary>
    /// 按 M（Map）切换关卡里的「Black」遮罩：默认开启，按一次隐藏，再按一次恢复。
    /// 关卡是叠加加载的，切关后会自动重新找到新关卡里的 Black。
    /// </summary>
    public class MapToggle : MonoBehaviour
    {
        [Tooltip("要开关的物体名（关卡里压在 Grid 下的 Black）")]
        [SerializeField]
        private string targetName = "Black";

        [Tooltip("找不到目标时，多久重试一次（秒）")]
        [SerializeField]
        private float retryInterval = 0.3f;

        private PlayerInputController _input;
        private GameObject _target;
        private bool _hidden;
        private float _retryTimer;

        private void Awake()
        {
            _input = new PlayerInputController();
        }

        private void OnEnable()
        {
            _input.Gameplay.Enable();
            _input.Gameplay.Map.started += OnMapPressed;
        }

        private void OnDisable()
        {
            _input.Gameplay.Map.started -= OnMapPressed;
            _input.Gameplay.Disable();
        }

        private void Update()
        {
            // 目标被销毁（切关）时重新找
            if (_target != null) return;

            _retryTimer -= Time.deltaTime;
            if (_retryTimer > 0f) return;

            _retryTimer = retryInterval;
            ResolveTarget();
        }

        private void OnMapPressed(InputAction.CallbackContext context)
        {
            if (_target == null) ResolveTarget();
            if (_target == null) return;

            _hidden = !_hidden;
            _target.SetActive(!_hidden);
        }

        private void ResolveTarget()
        {
            var all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var t in all)
            {
                if (t.name != targetName) continue;

                _target = t.gameObject;
                _hidden = false;
                _target.SetActive(true);
                return;
            }
        }
    }
}
