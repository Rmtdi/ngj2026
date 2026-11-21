using Framework;
using UnityEngine;

namespace Play.DestructibleWall
{
    /// <summary>
    /// 能源物质：被采集（R）命中即通关。
    /// 这里只负责"广播 EnergyCollectedEvent"，具体播胜利动画 / 切场景交给上层监听。
    /// </summary>
    public class EnergyWall : CollectableWall
    {
        [Header("能源")]
        [Tooltip("采集后是否让能源消失（tilemap 墙删格 / 普通物体取消激活）")]
        [SerializeField]
        private bool disappearAfterCollected = true;

        protected override void OnCollected(Vector3 hitPoint)
        {
            EventBus.Publish(new EnergyCollectedEvent(hitPoint));

            if (disappearAfterCollected) BreakSelf(hitPoint);

            Debug.Log($"[EnergyWall] {name} 被采集，已广播 EnergyCollectedEvent", this);
        }
    }
}
