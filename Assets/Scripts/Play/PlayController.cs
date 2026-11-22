using Framework;
using UnityEngine;
using MoveDirection = Tools.MoveDirection;

namespace Play
{
    /// <summary>
    /// 玩家动画逻辑：把 PlayHandler 的输入状态转成 Animator 参数。
    ///
    /// Animator 状态（三组 × 上/右/下，左用右的动画 + flipX 翻转）：
    ///   RobotIdleUp / RobotIdleRight / RobotIdleDown
    ///   RobotWalkUp / RobotWalkRight / RobotWalkDown
    ///   RobotAttackUp / RobotAttackRight / RobotAttackDown   ← 收集
    ///
    /// 参数：
    ///   Up / Down / Left / Right : bool，当前朝向（同一时间只有一个为 true）
    ///   Move                     : bool，是否在移动
    ///   Collect                  : bool，收集动作进行中
    /// </summary>
    public class PlayController : MonoBehaviour
    {
        [Header("收集动作")]
        [Tooltip("收集动画播放时长（Attack1/3 约 0.55 秒，Attack2 约 0.45 秒）")]
        [SerializeField]
        private float collectDuration = 0.55f;

        private Animator _animator;
        private PlayHandler _playHandler;
        private SpriteRenderer _spriteRenderer;

        // 朝向：没有输入时保持上一次朝向
        private bool _isUp;
        private bool _isDown = true;
        private bool _isLeft;
        private bool _isRight;

        private bool _collecting;
        private float _collectTimer;

        public void Awake()
        {
            _animator = GetComponent<Animator>();
            _playHandler = GetComponent<PlayHandler>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void OnEnable()
        {
            EventBus.Subscribe<WallCollectedEvent>(WallCollect);
        }

        public void OnDisable()
        {
            // EventBus 是静态的，必须成对退订
            EventBus.Unsubscribe<WallCollectedEvent>(WallCollect);
        }

        public void Update()
        {
            SetAnimator();
            UpdateCollect();
        }

        public void SetAnimator()
        {
            var pressed = _playHandler.pressed;

            // 有输入才更新朝向；松手时保留上一次朝向，避免朝向参数全变 false
            if (pressed.Count > 0)
            {
                switch (pressed[pressed.Count - 1])
                {
                    case MoveDirection.Up:
                        _isUp = true; _isDown = _isLeft = _isRight = false; break;
                    case MoveDirection.Down:
                        _isDown = true; _isUp = _isLeft = _isRight = false; break;
                    case MoveDirection.Left:
                        _isLeft = true; _isUp = _isDown = _isRight = false; break;
                    case MoveDirection.Right:
                        _isRight = true; _isUp = _isDown = _isLeft = false; break;
                }
            }

            var moving = pressed.Count > 0;

            // 左向复用右向动画，靠 SpriteRenderer.flipX 翻转
            var animRight = _isRight || _isLeft;

            _animator.SetBool("Up", _isUp);
            _animator.SetBool("Down", _isDown);
            _animator.SetBool("Left", _isLeft);
            _animator.SetBool("Right", animRight);
            _animator.SetBool("Move", moving);

            if (_spriteRenderer != null)
            {
                _spriteRenderer.flipX = _isLeft;
            }
        }

        /// <summary>收到采集事件：进入收集动作（同一时间只播一次）。</summary>
        private void WallCollect(WallCollectedEvent obj)
        {
            if (_collecting) return;

            _collecting = true;
            _collectTimer = Mathf.Max(0.1f, collectDuration);
            _animator.SetBool("Collect", true);
        }

        /// <summary>收集动画播完，退出收集状态，Animator 会自动回到待机/移动。</summary>
        private void UpdateCollect()
        {
            if (!_collecting) return;

            _collectTimer -= Time.deltaTime;
            if (_collectTimer > 0f) return;

            _collecting = false;
            _animator.SetBool("Collect", false);
        }
    }
}
