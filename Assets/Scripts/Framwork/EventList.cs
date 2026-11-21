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
}