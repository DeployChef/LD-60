using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FMOD.Studio;
using FMODUnity;
using LD60.Core.Audio;
using UnityEngine;

namespace LD60.Main.Audio
{
    public sealed class AudioManager : MonoBehaviour, IAudioManager
    {
        private const string MasterKey = "ld60.audio.master";
        private const string SfxKey = "ld60.audio.sfx";
        private const string MusicKey = "ld60.audio.music";

        [SerializeField] private AudioCatalog catalog;
        [SerializeField] private List<AudioSource> sources = new();

        private readonly Dictionary<string, float> _lastPlayTimes = new();
        private readonly Dictionary<AudioSource, AudioChannel> _channels = new();

        private float _master = 1f;
        private float _sfx = 1f;
        private float _music = 1f;
        private Bus _masterBus;
        private Bus _musicBus;
        private Bus _sfxBus;
        private Bus _dialogBus;
        private Bus _uiBus;
        private bool _fmodReady;

        public float MasterVolume => _master;
        public float SfxVolume => _sfx;
        public float MusicVolume => _music;

        private void Awake()
        {
            _master = PlayerPrefs.GetFloat(MasterKey, 1f);
            _sfx = PlayerPrefs.GetFloat(SfxKey, 1f);
            _music = PlayerPrefs.GetFloat(MusicKey, 1f);
            AudioListener.volume = _master;
        }

        private void Start()
        {
            InitializeFmodVolumes().Forget();
        }

        private async UniTask InitializeFmodVolumes()
        {
            // Root audio starts before the banks, especially in WebGL.
            var cancelled = await UniTask.WaitUntil(
                    () => RuntimeManager.IsInitialized && RuntimeManager.HasBankLoaded("Master"),
                    cancellationToken: this.GetCancellationTokenOnDestroy())
                .SuppressCancellationThrow();
            if (cancelled)
                return;

            _masterBus = RuntimeManager.GetBus("bus:/");
            _musicBus = RuntimeManager.GetBus("bus:/all_music");
            _sfxBus = RuntimeManager.GetBus("bus:/sfx_gr");
            _dialogBus = RuntimeManager.GetBus("bus:/dialog_gr");
            _uiBus = RuntimeManager.GetBus("bus:/ui");
            _fmodReady = true;
            ApplyFmodVolumes();
        }

        public void SetMasterVolume(float volume)
        {
            _master = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(MasterKey, _master);
            AudioListener.volume = _master;
            ApplyFmodVolumes();
        }

        public void SetSfxVolume(float volume)
        {
            _sfx = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(SfxKey, _sfx);
            ApplyPlaying();
            ApplyFmodVolumes();
        }

        public void SetMusicVolume(float volume)
        {
            _music = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(MusicKey, _music);
            ApplyPlaying();
            ApplyFmodVolumes();
        }

        private void ApplyFmodVolumes()
        {
            if (!_fmodReady)
                return;

            _masterBus.setVolume(_master);
            _musicBus.setVolume(_music);
            _sfxBus.setVolume(_sfx);
            _dialogBus.setVolume(_sfx);
            _uiBus.setVolume(_sfx);
        }

        public void Play(string soundId)
        {
            // WebGL: единственный аудио-движок — FMOD (§6.11 инструкции). Второй
            // движок (Unity AudioSource) поверх FMOD даёт микротреск музыки при
            // кликах и hover-спаме. UI-звуки в браузере появятся через event:/UI,
            // когда Валентин добавит туда параметр-кликер (схема SfxAudio).
            if (Application.platform == RuntimePlatform.WebGLPlayer)
                return;

            if (catalog == null || !catalog.TryGet(soundId, out var sound))
                return;

            if (sound.Clips == null || sound.Clips.Length == 0 || sources.Count == 0)
                return;

            var now = Time.unscaledTime;
            if (sound.Cooldown > 0f
                && _lastPlayTimes.TryGetValue(soundId, out var lastTime)
                && now - lastTime < sound.Cooldown)
                return;

            var source = FindSource(sound);
            if (source == null)
                return;

            source.clip = sound.Clips[Random.Range(0, sound.Clips.Length)];
            source.loop = sound.Loop;
            source.pitch = sound.BasePitch;
            source.volume = VolumeFor(sound.Channel);
            source.Play();
            _channels[source] = sound.Channel;
            _lastPlayTimes[soundId] = now;
        }

        private float VolumeFor(AudioChannel channel)
        {
            return channel == AudioChannel.Music ? _music : _sfx;
        }

        private void ApplyPlaying()
        {
            foreach (var pair in _channels)
            {
                if (pair.Key != null)
                    pair.Key.volume = VolumeFor(pair.Value);
            }
        }

        private AudioSource FindSource(SoundDefinition sound)
        {
            AudioSource busy = null;

            for (var i = 0; i < sources.Count; i++)
            {
                var candidate = sources[i];
                if (candidate == null)
                    continue;

                if (!candidate.isPlaying)
                    return candidate;

                busy = candidate;
            }

            return sound.AllowOverlap ? busy : null;
        }
    }
}
