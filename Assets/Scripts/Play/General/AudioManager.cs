using System;
using System.Collections.Generic;
using Framework;
using UnityEngine;
using UnityEngine.Audio;

namespace Play.General
{
    /// <summary>
    /// 全局音频管理器。
    /// 添加音频：把这个组件挂在场景里的空物体上，在 Inspector 的「音频表」里点 + 加一行，
    /// 然后把 AudioClip 拖进 Clip 格即可（Key 留空会自动使用音频文件名）。
    /// 播放：AudioManager.Instance.Play("音效名");
    /// </summary>
    [AddComponentMenu("NGJ2026/音频管理器 AudioManager")]
    [DisallowMultipleComponent]
    public class AudioManager : MonoSingleton<AudioManager>
    {
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

        [Header("音频表：点 + 加一行，把 AudioClip 拖进 Clip 格")]
        [SerializeField]
        private List<Sound> sounds = new List<Sound>();

        [Header("同时播放的音源数量")]
        [SerializeField, Range(1, 32)]
        private int voiceCount = 8;

        [Header("总音量")]
        [SerializeField, Range(0f, 1f)]
        private float masterVolume = 1f;

        private readonly Dictionary<string, Sound> _lookup =
            new Dictionary<string, Sound>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, AudioSource> _loopSources =
            new Dictionary<string, AudioSource>(StringComparer.OrdinalIgnoreCase);

        private readonly List<AudioSource> _voices = new List<AudioSource>();

        private readonly HashSet<string> _missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private int _nextVoice;

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

        protected override void OnInstanceReady()
        {
            AudioListener.volume = masterVolume;
            Rebuild();
        }

        /// <summary>场景里忘了放 AudioManager 时兜底创建一个，避免调用方空引用。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;

            var go = new GameObject("[AudioManager]（自动创建）");
            go.AddComponent<AudioManager>();

            Debug.LogWarning("[AudioManager] 场景里没有 AudioManager，已自动创建空实例（没有任何音频）。请在场景中创建并配置音频表。", go);
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

        /// <summary>停止某个循环音（如 BGM）。</summary>
        public void Stop(string key)
        {
            if (!string.IsNullOrEmpty(key) && _loopSources.TryGetValue(key, out var source) && source != null)
            {
                source.Stop();
            }
        }

        /// <summary>停止所有循环音和正在播放的音效。</summary>
        public void StopAll()
        {
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

            AudioListener.volume = masterVolume;

            // 运行时改表立刻生效，方便边玩边调
            if (Application.isPlaying && _lookup.Count > 0) Rebuild();
        }
    }
}
