using System;
using Framework;
using TMPro;
using UnityEngine;

namespace Play.DestructibleWall
{
    public class DestructibleWall : MonoBehaviour
    {
        public void Awake()
        {
            EventBus.Subscribe<WallCollectedEvent>(WallCollected);     
        }

        private void WallCollected(WallCollectedEvent obj)
        {
            Debug.Log("接收");
            switch (gameObject.tag)
            {
                case "LooseWall" : Close(); break;
            }
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }
    }
}