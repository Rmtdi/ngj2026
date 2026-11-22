using Play.General;
using UnityEngine;

namespace Play.DestructibleWall
{
    /// <summary>
    /// 松散岩壁：被采集（R）命中时，整面墙被破坏。
    /// 公共流程（订阅、过滤目标、补碰撞体）都在 CollectableWall 里。
    /// </summary>
    public class LooseWall : CollectableWall
    {
        protected override void OnCollected(Vector3 hitPoint)
        {
            Debug.Log($"[LooseWall] {name} 采集成功，整面墙被破坏", this);
            
            AudioManager.Instance.Play("Break_Wall");
            BreakSelf();
        }
    }
}
