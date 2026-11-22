using System;
using System.Collections;
using System.Collections.Generic;
using Framework;
using UnityEngine;
using UnityEngine.Audio;

namespace Play.General
{
    /// <summary>
    /// 全局音频管理器（对应《寻声》Demo 策划案 3.2 / 3.3）。
    ///
    /// 添加音频：把这个组件挂在场景里的空物体上，在 Inspector 的「音频表」里点 + 加一行，
    /// 把 AudioClip 拖进 Clip 格即可（Key 留空会自动使用音频文件名）。
    ///
    /// 普通播放：AudioManager.Instance.Play("音效名");
    /// 按距离分级：AudioManager.Instance.PlayDetect(Keys.WallRock, hitPoint);
    /// 脚步循环：AudioManager.Instance.PlayFootstep(Keys.GroundRock);
    /// 标记点：  AudioManager.Instance.PlayMarker(标记位置);
    /// </summary>
    [AddComponentMenu("NGJ2026/音频管理器 AudioManager")]
    [DisallowMultipleComponent]
    public class AudioManager : MonoSingleton<AudioManager>
    {
        /// <summary>
        /// 策划案约定的音效 Key。Inspector 音频表里照这些名字填，代码里用常量，避免手写字符串写错。
        /// </summary>
        public static class Keys
        {
            // 3.2 二、探测时不同材质返回的声音
            public const string WallRock = "Wall_Rock";           // 普通岩壁 → 坚硬岩石
            public const string WallLoose = "Wall_Loose";         // 松散岩壁 → 松散岩石
            public const string WallLava = "Wall_Lava";           // 岩浆壁 → 熔岩
            public const string WallWater = "Wall_Water";         // 水 → 水
            public const string WallElectric = "Wall_Electric";   // 电石 → 电
            public const string WallFluorite = "Wall_Fluorite";   // 萤石 → 坚硬岩石
            public const string WallEnergy = "Wall_Energy";       // 能源物质 → 能源物质

            // 3.2 三、行走时不同地面发出的声音
            public const string GroundRock = "Ground_Rock";       // 普通岩石
            public const string GroundWetRock = "Ground_WetRock"; // 积水岩石
            public const string GroundSand = "Ground_Sand";       // 沙地
            public const string GroundCliff = "Ground_Cliff";     // 悬崖边 → 系统警告

            // 3.3 四、标记点
            public const string Mark = "Mark";

            // 操作音
            public const string Detect = "Detect";
            public const string Collect = "Collect";
        }

        [Serializable]
        public class Sound
        {
            [Tooltip("调用名。留空会自动使用 AudioClip 的文件名")]
            public string key;

            [Tooltip("把音频文件拖到这里")]
            public AudioClip clip;

            [Range(0f, 1f)]
            [Tooltip("音量")]
            public float volume = 1f;

            [Range(0.1f, 3f)]
            [Tooltip("音调，1 = 原速")]
            public float pitch = 1f;

            [Tooltip("勾选：循环播放（BGM 等）；不勾：播一次的音效")]
            public bool loop;

            [Range(0f, 1f)]
            [Tooltip("0 = 2D 全局声；1 = 3D 随距离衰减")]
            public float spatialBlend;

            [Tooltip("可选：输出到 AudioMixer 的某个分组")]
            public AudioMixerGroup mixerGroup;

            public string ResolvedKey =>
                string.IsNullOrWhiteSpace(key)
                    ? (clip != null ? clip.name : null)
                    : key.Trim();
        }

        /// <summary>探测距离分档：落到哪个区间就用哪个音量。</summary>
        [Serializable]
        public class DistanceBand
        {
            [Tooltip("距离上限（身位）")]
            public float maxDistance = 1f;

            [Range(0f, 1f)]
            [Tooltip("该距离内的音量倍率")]
            public float volume = 1f;
        }

        [Header("音频表：点 + 加一行，把 AudioClip 拖进 Clip 格")]
        [SerializeField]
        private List<Sound> sounds = new List<Sound>();

        [Header("同时播放的音源数量")]
        [SerializeField, Range(1, 32)]
        private int voiceCount = 8;

        [Header("总音量")]
        [SerializeField, Range(0f, 1f)]
        private float masterVolume = 1f;

        [Header("探测距离分级（策划案 3.2 一）")]
        [SerializeField]
        private List<DistanceBand> distanceBands = new List<DistanceBand>
        {
            new DistanceBand { maxDistance = 1f, volume = 1f },      // 0-1 身位：强
            new DistanceBand { maxDistance = 2f, volume = 0.75f },   // 1-2 身位：次强
            new DistanceBand { maxDistance = 4f, volume = 0.5f },    // 2-4 身位：中
            new DistanceBand { maxDistance = 5f, volume = 0.25f },   // 4-5 身位：次弱（>5 身位没有声音）
        };

        [SerializeField, Tooltip("1 个身位等于多少世界单位。1 格 = 1 单位时保持 1 即可")]
        private float unitsPerBody = 1f;

        private readonly Dictionary<string, Sound> _lookup =
            new Dictionary<string, Sound>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, AudioSource> _loopSources =
            new Dictionary<string, AudioSource>(StringComparer.OrdinalIgnoreCase);

        private readonly List<AudioSource> _voices = new List<AudioSource>();

        private readonly HashSet<string> _missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private int _nextVoice;

        private AudioSource _footstepSource;

        private Coroutine _markerRoutine;
        private Vector3 _markerPosition;

        private Transform _listener;

        /// <summary>总音量，直接作用在 AudioListener 上。</summary>
        public float MasterVolume
        {
            get => AudioListener.volume;
            set
            {
                masterVolume = Mathf.Clamp01(value);
                AudioListener.volume = masterVolume;
            }
        }

        /// <summary>暂停 / 恢复所有声音。</summary>
        public bool Paused
        {
            get => AudioListener.pause;
            set => AudioListener.pause = value;
        }

        /// <summary>场上是否已经有一个标记点在发声（策划案限制最多 1 个）。</summary>
        public bool MarkerActive => _markerRoutine != null;

        protected override void OnInstanceReady()
        {
            AudioListener.volume = masterVolume;
            Rebuild();
        }

        /// <summary>重新读取 Inspector 里的音频表（运行时改了列表可以手动调）。</summary>
        public void Rebuild()
        {
            _lookup.Clear();
            _missing.Clear();

            foreach (var sound in sounds)
            {
                if (sound == null || sound.clip == null) continue;

                var key = sound.ResolvedKey;
                if (string.IsNullOrEmpty(key)) continue;

                if (_lookup.ContainsKey(key))
                {
                    Debug.LogWarning($"[AudioManager] 音频名重复：{key}，后一条被忽略。", this);
                    continue;
                }

                _lookup.Add(key, sound);
            }

            EnsureVoices();
        }

        /// <summary>播一次音效，或开始一个循环音。volumeScale / pitchScale 是临时倍率。</summary>
        public void Play(string key, float volumeScale = 1f, float pitchScale = 1f)
        {
            if (!TryGet(key, out var sound)) return;

            if (sound.loop)
            {
                PlayLoop(key, sound, volumeScale);
                return;
            }

            var source = NextVoice();

            source.outputAudioMixerGroup = sound.mixerGroup;
            source.spatialBlend = sound.spatialBlend;
            source.pitch = sound.pitch * pitchScale;

            // PlayOneShot 允许同一个音源上叠着播，不必等上一声放完
            source.PlayOneShot(sound.clip, Mathf.Max(0f, sound.volume * volumeScale));
        }

        // ------------------------------------------------------------------
        // 3.2 一、距离对声音的影响
        // ------------------------------------------------------------------

        /// <summary>把距离（身位）换算成音量倍率；超过最后一档返回 0（没有声音）。</summary>
        public float GetDistanceVolume(float distanceInBodies)
        {
            if (distanceBands == null) return 0f;

            foreach (var band in distanceBands)
            {
                if (band == null) continue;
                if (distanceInBodies <= band.maxDistance) return band.volume;
            }

            return 0f;
        }

        /// <summary>玩家（AudioListener）到目标点的距离，单位：身位。只算 XY，忽略相机高度。</summary>
        public float DistanceToListener(Vector3 worldPosition)
        {
            var listener = Listener;
            var from = listener != null ? (Vector2)listener.position : (Vector2)transform.position;
            var to = (Vector2)worldPosition;

            return Vector2.Distance(from, to) / Mathf.Max(0.0001f, unitsPerBody);
        }

        /// <summary>按距离分级播放；距离太远（没有声音）时返回 false，不占音源。</summary>
        public bool PlayByDistance(string key, float distanceInBodies, float pitchScale = 1f)
        {
            var volume = GetDistanceVolume(distanceInBodies);
            if (volume <= 0f) return false;

            Play(key, volume, pitchScale);
            return true;
        }

        /// <summary>按目标点与玩家的实际距离分级播放。</summary>
        public bool PlayByDistanceAt(string key, Vector3 worldPosition, float pitchScale = 1f)
        {
            return PlayByDistance(key, DistanceToListener(worldPosition), pitchScale);
        }

        /// <summary>
        /// 3.3 二、探测：声波碰到第一个物体后，按“命中点到玩家的距离”播放该材质的声音。
        /// </summary>
        public bool PlayDetect(string wallKey, Vector3 hitPosition, float pitchScale = 1f)
        {
            return PlayByDistanceAt(wallKey, hitPosition, pitchScale);
        }

        // ------------------------------------------------------------------
        // 3.2 三、不同地面行走时自动发出的声音
        // ------------------------------------------------------------------

        /// <summary>
        /// 切换脚下的地面并循环播放对应脚步音（策划案 3.2 三）。
        /// 换地面会自动切到新的音效；同一地面重复调用不会重头播。
        /// </summary>
        public void PlayFootstep(string groundKey, float volumeScale = 1f)
        {
            if (!TryGet(groundKey, out var sound))
            {
                StopFootstep();
                return;
            }

            if (_footstepSource == null)
            {
                var go = new GameObject("Footstep");
                go.transform.SetParent(transform, false);

                _footstepSource = go.AddComponent<AudioSource>();
                _footstepSource.playOnAwake = false;
                _footstepSource.loop = true;
                _footstepSource.spatialBlend = 0f;
            }

            if (_footstepSource.isPlaying && _footstepSource.clip == sound.clip) return;

            _footstepSource.outputAudioMixerGroup = sound.mixerGroup;
            _footstepSource.clip = sound.clip;
            _footstepSource.loop = true;
            _footstepSource.volume = Mathf.Clamp01(sound.volume * volumeScale);
            _footstepSource.pitch = sound.pitch;
            _footstepSource.Play();
        }

        /// <summary>停下脚步音（角色停住时调用）。</summary>
        public void StopFootstep()
        {
            if (_footstepSource != null) _footstepSource.Stop();
        }

        // ------------------------------------------------------------------
        // 3.3 四、标记点：每秒响一次，强弱随玩家与标记点的距离变化
        // ------------------------------------------------------------------

        /// <summary>
        /// 放下标记点并每 interval 秒响一次（策划案：每秒一次，强弱规则同 3.2 一）。
        /// 场上同时只保留一个标记点，重复调用会替换旧的。
        /// </summary>
        public void PlayMarker(Vector3 worldPosition, string key = Keys.Mark, float interval = 1f)
        {
            StopMarker();

            _markerPosition = worldPosition;
            _markerRoutine = StartCoroutine(MarkerLoop(key, Mathf.Max(0.05f, interval)));
        }

        /// <summary>关闭标记点声音（策划案里按 T）。</summary>
        public void StopMarker()
        {
            if (_markerRoutine == null) return;

            StopCoroutine(_markerRoutine);
            _markerRoutine = null;
        }

        private IEnumerator MarkerLoop(string key, float interval)
        {
            var wait = new WaitForSeconds(interval);

            while (true)
            {
                // 玩家会走动，所以每次都用当前位置重新算强弱
                PlayByDistanceAt(key, _markerPosition);
                yield return wait;
            }
        }

        // ------------------------------------------------------------------
        // 停止 / 查询
        // ------------------------------------------------------------------

        /// <summary>停止某个循环音（如 BGM）。</summary>
        public void Stop(string key)
        {
            if (!string.IsNullOrEmpty(key) && _loopSources.TryGetValue(key, out var source) && source != null)
            {
                source.Stop();
            }
        }

        /// <summary>停止所有循环音、脚步音、标记点、正在播放的音效。</summary>
        public void StopAll()
        {
            StopMarker();
            StopFootstep();

            foreach (var source in _loopSources.Values)
            {
                if (source != null) source.Stop();
            }

            foreach (var source in _voices)
            {
                source.Stop();
            }
        }

        /// <summary>某个循环音是否正在播放。</summary>
        public bool IsPlaying(string key)
        {
            return !string.IsNullOrEmpty(key)
                   && _loopSources.TryGetValue(key, out var source)
                   && source != null
                   && source.isPlaying;
        }

        private void PlayLoop(string key, Sound sound, float volumeScale)
        {
            if (_loopSources.TryGetValue(key, out var source) && source != null && source.isPlaying)
            {
                return; // 同一首已经在放，不打断
            }

            if (source == null)
            {
                var go = new GameObject($"Loop_{key}");
                go.transform.SetParent(transform, false);
                source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                _loopSources[key] = source;
            }

            source.clip = sound.clip;
            source.loop = true;
            source.volume = Mathf.Max(0f, sound.volume * volumeScale);
            source.pitch = sound.pitch;
            source.spatialBlend = sound.spatialBlend;
            source.outputAudioMixerGroup = sound.mixerGroup;
            source.Play();
        }

        private Transform Listener
        {
            get
            {
                if (_listener != null) return _listener;

                var listener = FindFirstObjectByType<AudioListener>();
                if (listener != null) _listener = listener.transform;

                return _listener;
            }
        }

        private AudioSource NextVoice()
        {
            foreach (var source in _voices)
            {
                if (!source.isPlaying) return source;
            }

            var voice = _voices[_nextVoice % _voices.Count];
            _nextVoice = (_nextVoice + 1) % _voices.Count;
            return voice;
        }

        private bool TryGet(string key, out Sound sound)
        {
            if (!string.IsNullOrEmpty(key) && _lookup.TryGetValue(key, out sound)) return true;

            sound = null;
            if (_missing.Add(key ?? string.Empty))
            {
                Debug.LogWarning($"[AudioManager] 找不到音频：{key}。检查 Inspector 音频表里的 Key。", this);
            }

            return false;
        }

        private void EnsureVoices()
        {
            for (var i = _voices.Count; i < voiceCount; i++)
            {
                var go = new GameObject($"Voice{i}");
                go.transform.SetParent(transform, false);

                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;

                _voices.Add(source);
            }
        }

        private void OnValidate()
        {
            if (sounds == null) return;

            // 拖入音频后自动补上调用名，省得手动打字
            foreach (var sound in sounds)
            {
                if (sound == null) continue;
                if (string.IsNullOrWhiteSpace(sound.key) && sound.clip != null)
                {
                    sound.key = sound.clip.name;
                }
            }

            // 距离分档必须按距离从小到大排，否则 GetDistanceVolume 会错
            if (distanceBands != null)
            {
                foreach (var band in distanceBands)
                {
                    if (band != null) band.maxDistance = Mathf.Max(0f, band.maxDistance);
                }

                distanceBands.Sort((a, b) => (a?.maxDistance ?? 0f).CompareTo(b?.maxDistance ?? 0f));
            }

            AudioListener.volume = masterVolume;

            // 运行时改表立刻生效，方便边玩边调
            if (Application.isPlaying && _lookup.Count > 0) Rebuild();
        }
    }
}
