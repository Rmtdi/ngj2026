using Framework;
using UnityEngine;

namespace Play.DestructibleWall
{
    /// <summary>
    /// 松散岩壁：被采集（R）命中时塌掉命中的那一格。
    /// 公共流程（订阅、过滤目标、找格、删格、补碰撞体）都在 CollectableWall 里。
    /// </summary>
    public class LooseWall : CollectableWall
    {
        protected override void OnCollected(Vector3 hitPoint)
        {
            if (!BreakSelf(hitPoint)) return;

            Debug.Log($"[LooseWall] {name} 采集成功，剩余 {Tilemap?.GetUsedTilesCount() ?? 0} 格", this);
        }
    }
}
