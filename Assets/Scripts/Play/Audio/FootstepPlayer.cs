using System.Collections.Generic;
using Play.General;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Play
{
    /// <summary>
    /// 走路脚步声：移动时按固定间隔播放一次脚步音。
    /// 音效由"脚下的地面 GameObject"的标签决定（没打标签就用对象名），在 ResolveGroundKey 的
    /// switch 里扩展即可；每步带一点随机音高，避免机械感。
    /// </summary>
    public class FootstepPlayer : MonoBehaviour
    {
        [Header("节奏")]
        [Tooltip("每走一步的间隔（秒）。走速越快可以调小")]
        [SerializeField, Min(0.05f)]
        private float stepInterval = 0.35f;

        [SerializeField, Range(0f, 1f)]
        private float volume = 0.8f;

        [Tooltip("每步音高的随机浮动，0 = 完全一样")]
        [SerializeField, Range(0f, 0.5f)]
        private float pitchJitter = 0.08f;

        [Tooltip("判定脚下地面时，从角色中心往下的偏移（脚底位置）")]
        [SerializeField]
        private Vector3 feetOffset = new Vector3(0f, -0.3f, 0f);

        [Header("移动判定")]
        [Tooltip("实际位移速度低于这个值就当作没在走（贴着墙按方向键时不会响）")]
        [SerializeField, Min(0f)]
        private float minMoveSpeed = 0.1f;

        [Header("地面标签 → 音效Key")]
        [SerializeField]
        private string rockKey = AudioManager.Keys.GroundRock;

        [Tooltip("识别不到地面（或没在 switch 里配）时用的音效")]
        [SerializeField]
        private string defaultKey = AudioManager.Keys.GroundRock;

        private PlayHandler _handler;
        private float _stepTimer;
        private Vector3 _lastPosition;

        private readonly List<Tilemap> _grounds = new List<Tilemap>();
        private float _refreshTimer;

        private void Awake()
        {
            _handler = GetComponent<PlayHandler>();
            _lastPosition = transform.position;
        }

        private void Update()
        {
            // 用实际位移判断有没有真的在动：贴着墙按方向键时位移是 0，就不出声
            var delta = transform.position - _lastPosition;
            _lastPosition = transform.position;

            var actualSpeed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
            var inputMoving = _handler != null && _handler.pressed.Count > 0;
            var moving = inputMoving && actualSpeed > minMoveSpeed;

            if (!moving)
            {
                // 停下来的第一步要立刻响
                _stepTimer = 0f;
                return;
            }

            _stepTimer -= Time.deltaTime;
            if (_stepTimer > 0f) return;

            _stepTimer = stepInterval;
            PlayStep();
        }

        private void PlayStep()
        {
            var key = ResolveGroundKey();

            var pitch = 1f + Random.Range(-pitchJitter, pitchJitter);

            AudioManager.Instance?.Play(key, volume, pitch);
        }

        /// <summary>根据脚下地面对象的标签（没标签则用对象名）决定播哪个音效。</summary>
        private string ResolveGroundKey()
        {
            RefreshGrounds();

            var feet = transform.position + feetOffset;

            foreach (var tilemap in _grounds)
            {
                if (tilemap == null) continue;

                var cell = tilemap.WorldToCell(feet);
                if (!tilemap.HasTile(cell)) continue;

                var id = tilemap.gameObject.tag;
                if (id == "Untagged") id = tilemap.gameObject.name;

                switch (id)
                {
                    case "RockGround":
                        return rockKey;

                    // 以后加新地面，在这里补一行就行，例如：
                    // case "SandGround": return sandKey;

                    default:
                        return defaultKey;
                }
            }

            return defaultKey;
        }

        /// <summary>关卡是叠加加载的，所以地面 Tilemap 每隔一会儿重新找一次。</summary>
        private void RefreshGrounds()
        {
            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer > 0f && _grounds.Count > 0) return;

            _refreshTimer = 0.5f;
            _grounds.Clear();

            foreach (var tilemap in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
            {
                var name = tilemap.gameObject.name;
                if (name.Contains("Ground")) _grounds.Add(tilemap);
            }
        }
    }
}
