using Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Play.DestructibleWall
{
    /// <summary>
    /// 可采集墙体的抽象模板类。
    ///
    /// 负责所有"被 R 采集"的公共流程，子类只关心"采集之后发生什么"：
    ///   1. 订阅 / 退订 WallCollectedEvent（EventBus 是静态的，必须成对）
    ///   2. 过滤掉"打中的不是我"的事件，避免整张图的墙一起响应
    ///   3. 提供 <see cref="BreakSelf"/>：破坏整面墙
    ///
    /// 注意：破坏方式是「整个 GameObject 取消激活」，不是删掉一格 tile。
    /// 删单格会在墙上留下一个四周都是碰撞体的小洞，角色贴着洞口时很容易卡住，
    /// 整面墙关掉后碰撞体一起消失，不会出现"破坏完反而动不了"。
    /// </summary>
    public abstract class CollectableWall : MonoBehaviour
    {
        [Header("命中检测")]
        [Tooltip("tilemap 墙上没有 Collider2D 时自动补一个，否则 R 的射线打不到这面墙")]
        [SerializeField]
        private bool ensureCollider = true;

        protected virtual void Awake()
        {
            EnsureCollider();
        }

        protected virtual void OnEnable()
        {
            EventBus.Subscribe<WallCollectedEvent>(OnWallCollected);
        }

        protected virtual void OnDisable()
        {
            // 一定要退订：EventBus 是静态的，对象销毁/隐藏后还留着旧委托会报空引用
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
        /// 破坏整面墙：整个 GameObject 取消激活，碰撞体、渲染一起消失。
        /// 这样不会在墙上留下能把角色卡住的小洞。
        /// </summary>
        protected void BreakSelf()
        {
            gameObject.SetActive(false);
        }

        /// <summary>tilemap 墙漏挂 Collider2D 时兜底补一个，否则射线打不到。</summary>
        protected void EnsureCollider()
        {
            if (!ensureCollider) return;
            if (GetComponent<Tilemap>() == null) return;
            if (GetComponent<Collider2D>() != null) return;

            gameObject.AddComponent<TilemapCollider2D>();
            Debug.LogWarning($"[{GetType().Name}] {name} 上没有 Collider2D，已自动补一个 TilemapCollider2D；建议在场景里手动加上并保存。", this);
        }
    }
}
