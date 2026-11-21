using Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Play.Wall
{
    /// <summary>
    /// 松散岩壁：被采集（R）命中时，只删掉被命中的那一格 tile。
    ///
    /// 三条硬规则（别再改回去）：
    /// 1. 绝不对整个 GameObject 做 SetActive(false)。
    ///    松散岩壁是画在 tilemap 上的一片格子，一个 GameObject 身上往往有好几格，
    ///    关掉 GameObject 会把整面墙一起关掉，和“只塌一格”的玩法直接冲突。
    /// 2. 必须挂 Collider2D，否则 PlayHandler 的 Physics2D.Raycast 根本打不到它，
    ///    按 R 就不会有任何反应。这里在 Awake 里兜底补一个 TilemapCollider2D。
    /// 3. 只处理“打中的正好是我”的事件；同一张图上别的松散岩壁、别的墙面不受影响。
    /// </summary>
    [RequireComponent(typeof(Tilemap))]
    public class LooseWall : MonoBehaviour
    {
        [Header("命中检测")]
        [Tooltip("身上没有 Collider2D 时自动补一个 TilemapCollider2D，否则 R 的射线打不到这面墙")]
        [SerializeField]
        private bool ensureCollider = true;

        [Header("删格容错")]
        [Tooltip("命中点所在格没有 tile 时，向周围再搜几格（0 = 不搜）。一般 1 就够")]
        [SerializeField]
        private int cellSearchRadius = 1;

        private Tilemap _tilemap;

        private void Awake()
        {
            _tilemap = GetComponent<Tilemap>();
            EnsureCollider();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<WallCollectedEvent>(OnCollected);
        }

        private void OnDisable()
        {
            // 一定要退订：EventBus 是静态的，对象销毁后还留着旧的委托会报空引用
            EventBus.Unsubscribe<WallCollectedEvent>(OnCollected);
        }

        private void OnCollected(WallCollectedEvent evt)
        {
            // 核心：只有“打中的正好是我”才处理，别的 loosewall 会在这里直接 return
            if (evt.Target != gameObject) return;

            if (!TryGetCollectedCell(evt.HitPoint, out var cell))
            {
                // 命中了我的 collider，但命中点附近找不到属于我的格子：什么都不做，尤其是不能关 GameObject
                Debug.LogWarning($"[LooseWall] {name} 在命中点 {evt.HitPoint} 附近没有找到 tile，忽略这次采集。", this);
                return;
            }

            // 只删这一格 tile：SetTile 会让 TilemapCollider2D 自动重算碰撞，GameObject 与其它格子都保留
            _tilemap.SetTile(cell, null);

            Debug.Log($"[LooseWall] {name} 采集成功，删掉格子 {cell}（剩余 {_tilemap.GetUsedTilesCount()} 格）", this);
        }

        /// <summary>用命中点找要删的那一格；命中点正好压在格子边界上时，退化为找最近的有 tile 的格子。</summary>
        private bool TryGetCollectedCell(Vector3 hitPoint, out Vector3Int cell)
        {
            var hitCell = _tilemap.WorldToCell(hitPoint);
            if (_tilemap.HasTile(hitCell))
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
                    if (!_tilemap.HasTile(candidate)) continue;

                    var sqr = (_tilemap.GetCellCenterWorld(candidate) - hitPoint).sqrMagnitude;
                    if (sqr >= bestSqr) continue;

                    bestSqr = sqr;
                    best = candidate;
                    found = true;
                }
            }

            cell = best;
            return found;
        }

        private void EnsureCollider()
        {
            if (!ensureCollider || _tilemap == null) return;
            if (GetComponent<Collider2D>() != null) return;

            gameObject.AddComponent<TilemapCollider2D>();
            Debug.LogWarning($"[LooseWall] {name} 上没有 Collider2D，已自动补一个 TilemapCollider2D；建议在场景里手动加上并保存。", this);
        }
    }
}
