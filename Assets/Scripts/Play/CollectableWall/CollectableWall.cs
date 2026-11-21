using Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Play.DestructibleWall
{
    /// <summary>
    /// 可采集墙体的抽象模板类。
    ///
    /// 它负责所有"被 R 采集"的公共流程，子类只关心"采集之后发生什么"：
    ///   1. 订阅 / 退订 WallCollectedEvent（EventBus 是静态的，必须成对）
    ///   2. 过滤掉"打中的不是我"的事件，避免一张图上的墙一起响应
    ///   3. 提供命中格查找、删格、补碰撞体这些可复用逻辑
    ///
    /// 子类只要重写 <see cref="OnCollected"/>：
    ///   LooseWall  → 删掉命中的那一格（塌方）
    ///   EnergyWall → 通知上层"拿到能源了"（胜利）
    ///   以后的岩浆壁 / 电石 / 萤石也都继承它。
    /// </summary>
    public abstract class CollectableWall : MonoBehaviour
    {
        [Header("命中检测")]
        [Tooltip("身上没有 Collider2D 时自动补一个 TilemapCollider2D，否则 R 的射线打不到这面墙")]
        [SerializeField]
        private bool ensureCollider = true;

        [Header("删格容错")]
        [Tooltip("命中点所在格没有 tile 时，向周围再搜几格（0 = 不搜）。一般 1 就够")]
        [SerializeField]
        private int cellSearchRadius = 1;

        /// <summary>墙体的 Tilemap；不是 tilemap 画的墙时为 null。</summary>
        protected Tilemap Tilemap { get; private set; }

        protected virtual void Awake()
        {
            Tilemap = GetComponent<Tilemap>();
            EnsureCollider();
        }

        protected virtual void OnEnable()
        {
            EventBus.Subscribe<WallCollectedEvent>(OnWallCollected);
        }

        protected virtual void OnDisable()
        {
            // 一定要退订：EventBus 是静态的，对象销毁后还留着旧的委托会报空引用
            EventBus.Unsubscribe<WallCollectedEvent>(OnWallCollected);
        }

        /// <summary>子类实现：被采集后做什么。只有"打中的是自己"时才会进来。</summary>
        protected abstract void OnCollected(Vector3 hitPoint);

        /// <summary>事件入口：过滤目标 → 交给子类。</summary>
        private void OnWallCollected(WallCollectedEvent evt)
        {
            if (!IsMe(evt.Target)) return;

            OnCollected(evt.HitPoint);
        }

        /// <summary>被击中的对象是不是自己（允许碰撞体挂在子物体上）。</summary>
        protected bool IsMe(GameObject target)
        {
            if (target == null) return false;

            return target == gameObject || target.transform.IsChildOf(transform);
        }

        /// <summary>
        /// 默认的"摧毁自己"：tilemap 墙只删命中的那一格，普通物体才取消激活。
        /// 绝不对 tilemap 墙用 SetActive(false)，那会把整面墙一起关掉。
        /// </summary>
        /// <returns>是否真的处理掉了</returns>
        protected bool BreakSelf(Vector3 hitPoint)
        {
            if (Tilemap != null) return EraseHitTile(hitPoint);

            gameObject.SetActive(false);
            return true;
        }

        /// <summary>删除命中点所在的那一格 tile。返回 false 表示没找到对应的格子。</summary>
        protected bool EraseHitTile(Vector3 hitPoint)
        {
            if (Tilemap == null) return false;

            if (!TryGetCollectedCell(hitPoint, out var cell))
            {
                Debug.LogWarning($"[{GetType().Name}] {name} 在命中点 {hitPoint} 附近没有找到 tile，忽略这次采集。", this);
                return false;
            }

            // 只删这一格：SetTile 会让 TilemapCollider2D 自动重算碰撞，GameObject 与其它格子都保留
            Tilemap.SetTile(cell, null);
            return true;
        }

        /// <summary>用命中点找要删的那一格；命中点正好压在格子边界上时，退化为找最近的有 tile 的格子。</summary>
        protected bool TryGetCollectedCell(Vector3 hitPoint, out Vector3Int cell)
        {
            var hitCell = Tilemap.WorldToCell(hitPoint);
            if (Tilemap.HasTile(hitCell))
            {
                cell = hitCell;
                return true;
            }

            var radius = Mathf.Max(0, cellSearchRadius);
            var best = hitCell;
            var bestSqr = float.MaxValue;
            var found = false;

            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    var candidate = new Vector3Int(hitCell.x + dx, hitCell.y + dy, hitCell.z);
                    if (!Tilemap.HasTile(candidate)) continue;

                    var sqr = (Tilemap.GetCellCenterWorld(candidate) - hitPoint).sqrMagnitude;
                    if (sqr >= bestSqr) continue;

                    bestSqr = sqr;
                    best = candidate;
                    found = true;
                }
            }

            cell = best;
            return found;
        }

        /// <summary>tilemap 墙没挂 Collider2D 时兜底补一个，否则射线打不到。</summary>
        protected void EnsureCollider()
        {
            if (!ensureCollider || Tilemap == null) return;
            if (GetComponent<Collider2D>() != null) return;

            gameObject.AddComponent<TilemapCollider2D>();
            Debug.LogWarning($"[{GetType().Name}] {name} 上没有 Collider2D，已自动补一个 TilemapCollider2D；建议在场景里手动加上并保存。", this);
        }
    }
}
