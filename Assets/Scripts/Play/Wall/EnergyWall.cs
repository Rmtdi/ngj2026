using Framework;
using Play.General;
using UnityEngine;

namespace Play.DestructibleWall
{
    /// <summary>
    /// 能源物质：被采集（R）命中即通关，切到下一关。
    /// 注意：只调 SceneLoader.LoadScene 一次——它内部会自动卸掉当前场景，
    /// 不要先 UnloadScene 再 LoadScene（那样第二次调用会因为 IsLoading 被忽略，导致黑屏没场景）。
    /// </summary>
    public class EnergyWall : CollectableWall
    {
        [Header("能源")]
        [Tooltip("采集后是否让能源消失（整个物体取消激活）")]
        [SerializeField]
        private bool disappearAfterCollected = true;

        [Header("通关后")]
        [Tooltip("采集到能源后加载的场景名（必须在 Build Settings 里）")]
        [SerializeField]
        private string nextScene = "The Second Level";

        [Tooltip("进入下一关时玩家的出生位置")]
        [SerializeField]
        private Vector3 nextSpawnPosition = Vector3.zero;

        protected override void OnCollected(Vector3 hitPoint)
        {
            AudioManager.Instance.Play("Win");
            EventBus.Publish(new EnergyCollectedEvent(hitPoint));

            if (disappearAfterCollected) BreakSelf();

            Debug.Log($"[EnergyWall] {name} 被采集，加载下一关：{nextScene}", this);

            var loader = SceneLoader.Instance;
            if (loader == null)
            {
                Debug.LogError("[EnergyWall] 找不到 SceneLoader，无法切关。", this);
                return;
            }
            loader.LoadScene(nextScene, nextSpawnPosition);
        }
    }
}
