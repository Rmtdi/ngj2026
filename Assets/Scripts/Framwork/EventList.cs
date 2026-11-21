using UnityEngine;
using UnityEngine.UIElements;
using Tools;

namespace Framework
{
    //泛型约束，不让其他类型的结构体进来
    public interface IEvent
    {
        
    }

    public struct WallCollectedEvent : IEvent
    {
        public GameObject RobotGO;
    }

    /// <summary>
    /// 采集（R）命中了某个物体。
    /// 关键点：事件里必须带上「被击中的是哪一个」，否则所有同名墙都会一起响应。
    /// </summary>
    public struct WallCollectedEvent : IEvent
    {
        /// <summary>被射线击中的那个物体（hit.collider.gameObject）。</summary>
        public GameObject Target;

        /// <summary>命中点，已往角色方向回收一点，保证落在被击中的那个 tile / 物体内部。</summary>
        public Vector3 HitPoint;

        public WallCollectedEvent(GameObject target, Vector3 hitPoint)
        {
            Target = target;
            HitPoint = hitPoint;
        }
    }
}