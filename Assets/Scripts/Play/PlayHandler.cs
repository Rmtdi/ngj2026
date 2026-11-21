using System.Collections.Generic;
using Framework;
using Play.General;
using UnityEngine;
using UnityEngine.InputSystem;
using MoveDirection = Tools.MoveDirection;

namespace Play
{
    public class PlayHandler : MonoBehaviour
    {

        public PlayerInputController _inputControler;
        public Rigidbody2D rb;
        public float speed;

        [Header("探测墙体")] [Tooltip("从角色中心算起的探测距离（单位=格）")] [SerializeField]
        private float detectDistance;

        [Tooltip("射线只检测这些层。默认 Everything；建议只勾 Wall 层，或者排除 Player 层")]
        [SerializeField]
        private LayerMask hitMask = ~0;

        [Header("采集墙体")]
        [Tooltip("采集检测距离（策划案 3.3 三：向面前 0-1 个身位检查）")]
        [SerializeField]
        private float collectDistance = 1f;

        //用栈来存输入的按键，保证后输入的为当前方向
        private readonly List<MoveDirection> _pressed = new List<MoveDirection>();
        private MoveDirection? _current;
        private Vector3 _faceDir = Vector3.down; // 默认朝下，松手后保持上一次朝向
        private Collider2D _selfCollider;

        public void OnEnable()
        {
            _inputControler.Enable();
        }

        public void OnDisable()
        {
            _inputControler.Disable();
        }

        public void Awake()
        {
            _inputControler = new PlayerInputController();
            // 用基类 Collider2D，Box / Circle / Capsule 都能取到
            _selfCollider = GetComponent<Collider2D>();
            rb = GetComponent<Rigidbody2D>();

            if (_selfCollider == null)
            {
                Debug.LogWarning("[PlayHandler] 没找到 Collider2D：射线会从角色中心发出，可能先打到自己。请给角色加 BoxCollider2D。", this);
            }

            // 用来对账：确认跑的是哪个对象、拿到的到底是哪个值
            Debug.Log($"[PlayHandler] {name}（实例 {GetInstanceID()}）detectDistance={detectDistance}，hitMask={hitMask.value}，scale={transform.lossyScale}", this);

            Bind();
        }
        public void FixedUpdate()
        {
            switch (_current)
            {
                case MoveDirection.Up : _faceDir = new Vector3(0, 1); break;
                case MoveDirection.Down: _faceDir = new Vector3(0, -1); break;
                case MoveDirection.Left: _faceDir = new Vector3(-1, 0); break;
                case MoveDirection.Right: _faceDir = new Vector3(1, 0); break;
            }
            rb.velocity = _current.HasValue ? (Vector2)_faceDir * speed : Vector2.zero;
        }

        void Bind()
        {
            var g = _inputControler.Gameplay;
            g.Up.performed += _ => Press(MoveDirection.Up);
            g.Down.performed += _ => Press(MoveDirection.Down);
            g.Left.performed += _ => Press(MoveDirection.Left);
            g.Right.performed += _ => Press(MoveDirection.Right);

            g.Up.canceled += _ => Release(MoveDirection.Up);
            g.Down.canceled += _ => Release(MoveDirection.Down);
            g.Left.canceled += _ => Release(MoveDirection.Left);
            g.Right.canceled += _ => Release(MoveDirection.Right);

            g.Detect.started += DetectWall;
            g.Collect.started += CollectWall;
        }

        private void DetectWall(InputAction.CallbackContext obj)
        {
            var dir = new Vector2(_faceDir.x, _faceDir.y);
            if (dir == Vector2.zero) return;

            var origin = GetRayOrigin(dir, out var edge);

            // detectDistance 以“角色中心”为准；起点已经在碰撞箱边缘，所以要扣掉自身半宽
            var reach = Mathf.Max(0f, detectDistance - edge);

            var hit = default(RaycastHit2D);
            if (reach > 0f) hit = Physics2D.Raycast(origin, dir, reach, hitMask);

            Debug.DrawRay(transform.position, dir * detectDistance, hit ? Color.green : Color.cyan, 0.5f);

            if (!hit)
            {
                Debug.Log($"[Detect] {name}（实例 {GetInstanceID()}）没命中：detectDistance={detectDistance}，自身半宽 {edge:F2}，实际射线长 {reach:F2}", this);
                return;
            }

            Debug.Log($"[Detect] 命中 {hit.collider.name} tag={hit.collider.tag}，distance={hit.distance:F2}（距中心 {edge + hit.distance:F2}）", hit.collider);

            switch (hit.collider.tag)
            {
                case "RockWall": Debug.Log("RockWall");
                    AudioManager.Instance.PlayByDistance("Wall_Rock", reach);
                    break;
                case "LooseWall": Debug.Log("LooseWall");
                    AudioManager.Instance.PlayByDistance("Wall_Loose", reach);
                    break;
                case "EnergyWall": Debug.Log("EnergyWall");
                    AudioManager.Instance.PlayByDistance("Wall_Energy", reach);
                    break;
            }
        }

        private void CollectWall(InputAction.CallbackContext obj)
        {
            var dir = new Vector2(_faceDir.x, _faceDir.y);
            if (dir == Vector2.zero) return;

            var origin = GetRayOrigin(dir, out var edge);
            var reach = Mathf.Max(0f, collectDistance - edge);

            var hit = default(RaycastHit2D);
            if (reach > 0f) hit = Physics2D.Raycast(origin, dir, reach, hitMask);

            if (!hit)
            {
                Debug.Log("[Collect] 前方没有可采集的物体");
                return;
            }

            // 命中点在碰撞面上，往角色方向收一点，保证 WorldToCell 落在被击中的那一格
            var hitPoint = hit.point - dir * 0.02f;

            Debug.Log($"[Collect] 命中 {hit.collider.name}（{hit.collider.GetType().Name}），distance={hit.distance:F2}，hitPoint={hitPoint}", hit.collider);

            // 只把“打中的是哪一个”放进事件，由每面墙自己决定要不要响应
            EventBus.Publish(new WallCollectedEvent(hit.collider.gameObject, hitPoint));
        }

        /// <summary>沿 dir 把射线起点推到自身碰撞箱边缘外一点，并返回这段偏移长度。</summary>
        private Vector2 GetRayOrigin(Vector2 dir, out float edge)
        {
            if (_selfCollider == null)
            {
                edge = 0f;
                return transform.position;
            }

            var bounds = _selfCollider.bounds;
            // 轴对齐盒：中心沿 dir 到边界的距离 = |dx|*extents.x + |dy|*extents.y，+0.02 是防抖余量
            edge = Mathf.Abs(dir.x) * bounds.extents.x + Mathf.Abs(dir.y) * bounds.extents.y + 0.02f;
            return (Vector2)bounds.center + dir * edge;
        }

        void Press(MoveDirection dir)
        {
            _pressed.Remove(dir); // 重复按下（比如 W 和 ↑ 都映射 Up）也保证它排到最后
            _pressed.Add(dir);
            _current = dir;
        }

        void Release(MoveDirection dir)
        {
            _pressed.Remove(dir);
            _current = _pressed.Count > 0 ? _pressed[_pressed.Count - 1] : (MoveDirection?)null;
        }
        
    }
}