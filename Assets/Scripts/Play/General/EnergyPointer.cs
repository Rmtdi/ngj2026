using System;
using Play.DestructibleWall;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Play.General
{
    /// <summary>
    /// 能源指示箭头：始终指向场景里的 EnergyWall（能源物质）。
    ///
    /// 用法：挂在玩家下的一个子物体上，子物体带 SpriteRenderer 显示箭头图。
    /// 贴图默认朝下（-Y），所以最终角度 = 吸附角 + 90°。
    /// </summary>
    public class EnergyPointer : MonoBehaviour
    {
        [Header("目标")]
        [Tooltip("能源物质的 Tag")]
        [SerializeField]
        private string targetTag = "EnergyWall";

        [Tooltip("找不到目标时，多久重试一次（秒）")]
        [SerializeField]
        private float retryInterval = 0.2f;

        [Header("显示")]
        [Tooltip("距离小于这个值就不再旋转（避免原地抖动）")]
        [SerializeField]
        private float deadZone = 0.2f;

        [Tooltip("方向计算的原点：玩家中心。留空则自动用父物体（箭头挂在玩家下面）")]
        [SerializeField]
        private Transform origin;

        private Transform _target;
        private Vector3 _targetPoint;
        private SpriteRenderer _arrow;
        private float _retryTimer;

        [SerializeField]
        public Vector3 offset;

        /// <summary>方向计算的起点：玩家中心，不是箭头自己的位置。</summary>
        private Vector3 OriginPosition => origin != null ? origin.position + offset : transform.position + offset;

        private void Awake()
        {
            _arrow = GetComponent<SpriteRenderer>();

            // 箭头是玩家的子物体，默认拿父物体当"玩家中心"
            if (origin == null) origin = transform.parent;
        }

        private void Update()
        {
            if (!HasTarget()) ResolveTarget();

            if (!HasTarget())
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);

            // 从「玩家中心」指向目标，而不是从箭头自己的位置
            var dir = (Vector2)(_targetPoint - OriginPosition);
            if (dir.sqrMagnitude < deadZone * deadZone) return;

            // 八向吸附：0=右, 90=上, ±180=左, -90=下
            var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            var snapped = Mathf.Round(angle / 45f) * 45f;

            // 贴图默认朝下，补 90°：下=0°、右=90°、上=180°、左=270°
            transform.localRotation = Quaternion.Euler(0f, 0f, snapped + 90f);
        }

        private bool HasTarget()
        {
            return _target != null && _target.gameObject.activeInHierarchy;
        }

        private void ResolveTarget()
        {
            _retryTimer -= Time.unscaledDeltaTime;
            if (_retryTimer > 0f) return;

            _retryTimer = Mathf.Max(0.05f, retryInterval);

            GameObject found = null;

            if (!string.IsNullOrEmpty(targetTag))
            {
                try
                {
                    found = GameObject.FindWithTag(targetTag);
                }
                catch (UnityException)
                {
                    // Tag 没在 TagManager 里定义，忽略，走下面的备用方案
                }
            }

            if (found == null)
            {
                var wall = FindFirstObjectByType<EnergyWall>(FindObjectsInactive.Exclude);
                if (wall != null) found = wall.gameObject;
            }

            _target = found != null ? found.transform : null;
            _targetPoint = found != null ? ComputeTargetPoint(found) : Vector3.zero;
        }

        /// <summary>
        /// 能源物质是画在 Tilemap 上的，GameObject 的 transform 通常在 (0,0)，
        /// 真正的位置是那些 tile 的世界坐标。这里取离玩家最近的一格的中心。
        /// </summary>
        private Vector3 ComputeTargetPoint(GameObject go)
        {
            var tilemap = go.GetComponent<Tilemap>();
            if (tilemap == null) return go.transform.position;

            var best = go.transform.position;
            var bestSqr = float.MaxValue;
            var from = OriginPosition;

            foreach (var cell in tilemap.cellBounds.allPositionsWithin)
            {
                if (!tilemap.HasTile(cell)) continue;

                var world = tilemap.GetCellCenterWorld(cell);
                var sqr = (world - from).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = world;
            }

            return best;
        }

        private void SetVisible(bool visible)
        {
            if (_arrow != null) _arrow.enabled = visible;
        }
    }
}
