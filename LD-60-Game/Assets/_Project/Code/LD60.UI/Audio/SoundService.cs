using Cysharp.Threading.Tasks;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace LD60.UI.Audio
{
    /// <summary>
    /// Общие вызовы FMOD: меню, разовые SFX, печатная машинка комиксов,
    /// штамп, финал и снапшот паузы.
    /// Громофон и контейнер event:/SFX живут отдельно.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class SoundService : MonoBehaviour
    {
        private static readonly string[] StartOrMenuPaths = { "event:/menu/start or menu", "event:/start or menu" };
        private static readonly string[] UiMenuPaths = { "event:/menu/UI menu", "event:/UI menu" };
        private static readonly string[] StampPaths = { "event:/SFX/type", "event:/type" };
        private const string SfxPath = "event:/SFX";
        private const string NewspaperParameter = "newspaper";
        // Как раньше на субтитрах: щелчок 2 → 1. В собранном банке это и есть удар штампа.
        private const float StampOffValue = 2f;
        private const float StampOnValue = 1f;
        private static readonly string[] PaperHandlingPaths = { "event:/oneshot/paper_handing", "event:/paper_handing" };
        private static readonly string[] ConvertPaths = { "event:/oneshot/convert", "event:/convert" };
        private static readonly string[] CutsceneOrMenuParamNames = { "cutscene or menu", "cutscene or menu " };

        private const string TypewriterPath = "event:/comics";
        private const string FinalPath = "event:/Final";
        private const string WriteFinalParameter = "write_final";
        private const float WriteFinalNone = 4f;
        private const string PauseSnapshotPath = "snapshot:/pause";

        private const float CutValue = 0f;
        private const float MenuValue = 1f;
        private const float StartValue = 2f;
        // Marker A: the menu section of the bundled "start or menu" event.
        private const int MenuTimelinePositionMilliseconds = 16000;

        private static SoundService _instance;
        private EventInstance _menuMusic;
        private EventInstance _sfx;
        private EventInstance _typewriter;
        private EventInstance _final;
        private EventInstance _pauseSnapshot;
        private string _resolvedParamName = "cutscene or menu";
        private float _targetState = CutValue;
        private bool _typewriterMissing;

        public static void EnsureCreated()
        {
            if (_instance != null)
                return;

            var go = new GameObject(nameof(SoundService));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SoundService>();
        }

        public static void PlayCutscene()
        {
            EnsureCreated();
            _instance.SetState(CutValue, "cutscene");
        }

        public static void PlayMenu()
        {
            EnsureCreated();
            _instance.SetState(MenuValue, "menu");
        }

        public static void PlayStart()
        {
            EnsureCreated();
            _instance.TriggerStart();
        }

        public static void PlayButtonHover()
        {
            PlayFirstAvailable(UiMenuPaths);
        }

        public static void PlayButtonClick()
        {
            if (!PlayFirstAvailable(StampPaths))
                PlayFirstAvailable(UiMenuPaths);
        }

        public static void PlayPaperHandling()
        {
            PlayFirstAvailable(PaperHandlingPaths);
        }

        public static void PlayPaperDrop()
        {
            PlayFirstAvailable(UiMenuPaths);
        }

        public static void PlayEnvelopeOpen()
        {
            PlayFirstAvailable(ConvertPaths);
        }

        /// <summary>
        /// Тот же щелчок, что раньше играл на субтитрах комикса: newspaper 2 → 1.
        /// </summary>
        public static void PlayStamp()
        {
            EnsureCreated();
            _instance.TriggerStamp();
        }

        /// <summary>Луп печатной машинки на время печати субтитров.</summary>
        public static void StartTypewriter(float volume)
        {
            EnsureCreated();
            _instance.BeginTypewriter(volume);
        }

        public static void SetTypewriterVolume(float volume)
        {
            if (_instance == null || !_instance._typewriter.isValid())
                return;

            _instance._typewriter.setVolume(Mathf.Clamp01(volume));
        }

        public static void StopTypewriter()
        {
            if (_instance == null)
                return;

            _instance.EndTypewriter();
        }

        /// <summary>
        /// Титры. write_final = none: отдельного «как дописали финал» в игре пока нет.
        /// Ивент сам ставит глобальный final и зовёт меню.
        /// </summary>
        public static void PlayFinal()
        {
            EnsureCreated();
            _instance.StartFinal();
        }

        /// <summary>
        /// snapshot:/pause глушит музыку и включает эквалайзер.
        /// В обёртке 2.03 нет EventDescription.activate: снапшот — это ивент,
        /// start включает его, stop выключает. В группе он вытесняет snapshot:/start.
        /// </summary>
        public static void SetPaused(bool paused)
        {
            if (!paused)
            {
                if (_instance != null)
                    _instance.ApplyPause(false);
                return;
            }

            EnsureCreated();
            _instance.ApplyPause(paused);
        }

        private static bool PlayFirstAvailable(string[] paths)
        {
            if (!RuntimeManager.IsInitialized)
                return false;

            for (var i = 0; i < paths.Length; i++)
            {
                try
                {
                    if (RuntimeManager.StudioSystem.getEvent(paths[i], out var description) != FMOD.RESULT.OK
                        || !description.isValid())
                        continue;
                    RuntimeManager.PlayOneShot(paths[i]);
                    return true;
                }
                catch (EventNotFoundException)
                {
                }
            }

            return false;
        }

        private void Start()
        {
            InitAsync().Forget();
        }

        private async UniTaskVoid InitAsync()
        {
            while (!RuntimeManager.IsInitialized
                   || (!RuntimeManager.HasBankLoaded("Menu") && !RuntimeManager.HasBankLoaded("Master")))
            {
                await UniTask.Yield();
            }

            EnsureMusicInstance();
            ApplyState();
        }

        private void SetState(float value, string label)
        {
            StopFinal();
            // Changing the parameter alone leaves the intro's sounds playing
            // until FMOD reaches its transition region, even after Space skips it.
            if (_targetState != value)
                ReleaseMusicInstance(FMOD.Studio.STOP_MODE.IMMEDIATE);
            _targetState = value;
            EnsureMusicInstance();
            ApplyState();
            Debug.Log($"[Sound] cutscene or menu -> {label} ({value})");
        }

        private void TriggerStart()
        {
            StopFinal();
            _targetState = StartValue;
            if (!_menuMusic.isValid())
                return;

            SetParameter(_menuMusic, _resolvedParamName, StartValue);
            Debug.Log("[Sound] cutscene or menu -> start (2)");
            ReleaseAfterStopAsync(_menuMusic).Forget();
        }

        private async UniTaskVoid ReleaseAfterStopAsync(EventInstance instance)
        {
            var deadline = Time.unscaledTime + 5f;
            while (Time.unscaledTime < deadline && instance.isValid())
            {
                if (instance.getPlaybackState(out var state) == FMOD.RESULT.OK && state == PLAYBACK_STATE.STOPPED)
                    break;

                await UniTask.Yield();
            }

            if (!instance.isValid())
                return;

            instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            instance.release();
            if (_menuMusic.handle == instance.handle)
                _menuMusic = default;
        }

        private void EnsureMusicInstance()
        {
            if (_menuMusic.isValid())
            {
                if (_menuMusic.getPlaybackState(out var state) == FMOD.RESULT.OK && state == PLAYBACK_STATE.STOPPED)
                    StartMusicInstance();
                return;
            }

            foreach (var path in StartOrMenuPaths)
            {
                try
                {
                    _menuMusic = RuntimeManager.CreateInstance(path);
                    if (!_menuMusic.isValid())
                        continue;

                    ResolveParamName(_menuMusic);
                    StartMusicInstance();
                    Debug.Log($"[Sound] инстанс {path} создан и запущен");
                    break;
                }
                catch (EventNotFoundException)
                {
                }
            }
        }

        private void ResolveParamName(EventInstance instance)
        {
            foreach (var name in CutsceneOrMenuParamNames)
            {
                if (instance.getParameterByName(name, out _) == FMOD.RESULT.OK)
                {
                    _resolvedParamName = name;
                    return;
                }
            }
        }

        private void StartMusicInstance()
        {
            ApplyState();
            if (_targetState == MenuValue)
                _menuMusic.setTimelinePosition(MenuTimelinePositionMilliseconds);
            _menuMusic.start();
        }

        private void ApplyState()
        {
            if (!_menuMusic.isValid())
                return;

            SetParameter(_menuMusic, _resolvedParamName, _targetState);
        }

        private static void SetParameter(EventInstance instance, string paramName, float value)
        {
            instance.setParameterByName(paramName, value, ignoreseekspeed: true);
        }

        private void TriggerStamp()
        {
            try
            {
                if (!_sfx.isValid())
                {
                    _sfx = RuntimeManager.CreateInstance(SfxPath);
                    if (_sfx.isValid())
                    {
                        _sfx.setVolume(1f);
                        _sfx.start();
                    }
                }

                if (_sfx.isValid())
                {
                    _sfx.setVolume(1f);
                    _sfx.setParameterByName(NewspaperParameter, StampOffValue, ignoreseekspeed: true);
                    _sfx.setParameterByName(NewspaperParameter, StampOnValue, ignoreseekspeed: true);
                    return;
                }
            }
            catch (EventNotFoundException)
            {
            }

            for (var i = 0; i < StampPaths.Length; i++)
            {
                try
                {
                    if (RuntimeManager.StudioSystem.getEvent(StampPaths[i], out var description) != FMOD.RESULT.OK
                        || !description.isValid())
                        continue;

                    var instance = RuntimeManager.CreateInstance(StampPaths[i]);
                    instance.setVolume(1f);
                    instance.start();
                    instance.release();
                    return;
                }
                catch (EventNotFoundException)
                {
                }
            }
        }

        private void BeginTypewriter(float volume)
        {
            if (_typewriter.isValid())
            {
                _typewriter.setVolume(Mathf.Clamp01(volume));
                return;
            }

            if (_typewriterMissing || !RuntimeManager.IsInitialized)
                return;

            try
            {
                _typewriter = RuntimeManager.CreateInstance(TypewriterPath);
            }
            catch (EventNotFoundException)
            {
                _typewriterMissing = true;
                Debug.LogWarning("[Sound] event:/comics не найден. Пересобери банки FMOD (Ctrl+B) и кликни в Unity.");
                return;
            }

            if (!_typewriter.isValid())
                return;

            _typewriter.setVolume(Mathf.Clamp01(volume));
            _typewriter.start();
        }

        private void EndTypewriter()
        {
            ReleaseInstance(ref _typewriter, FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        }

        private void StartFinal()
        {
            EndTypewriter();
            ReleaseMusicInstance(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            if (_final.isValid())
                return;

            if (!RuntimeManager.IsInitialized)
                return;

            try
            {
                _final = RuntimeManager.CreateInstance(FinalPath);
            }
            catch (EventNotFoundException)
            {
                Debug.LogWarning("[Sound] event:/Final не найден");
                return;
            }

            if (!_final.isValid())
                return;

            _final.setParameterByName(WriteFinalParameter, WriteFinalNone, ignoreseekspeed: true);
            _final.start();
            Debug.Log("[Sound] Final started (write_final=none)");
        }

        private void StopFinal()
        {
            ReleaseInstance(ref _final, FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        }

        private void ApplyPause(bool paused)
        {
            if (!paused)
            {
                if (_pauseSnapshot.isValid())
                    Debug.Log("[Sound] snapshot:/pause off");
                ReleaseInstance(ref _pauseSnapshot, FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                return;
            }

            if (_pauseSnapshot.isValid() || !RuntimeManager.IsInitialized)
                return;

            var system = RuntimeManager.StudioSystem;
            if (!system.isValid())
                return;

            if (system.getEvent(PauseSnapshotPath, out var description) != FMOD.RESULT.OK || !description.isValid())
            {
                Debug.LogWarning("[Sound] snapshot:/pause не найден в банках");
                return;
            }

            if (description.createInstance(out _pauseSnapshot) != FMOD.RESULT.OK || !_pauseSnapshot.isValid())
                return;

            _pauseSnapshot.start();
            Debug.Log("[Sound] snapshot:/pause on");
        }

        private void ReleaseMusicInstance(FMOD.Studio.STOP_MODE mode)
        {
            ReleaseInstance(ref _menuMusic, mode);
        }

        private static void ReleaseInstance(ref EventInstance instance, FMOD.Studio.STOP_MODE mode)
        {
            if (!instance.isValid())
                return;

            instance.stop(mode);
            instance.release();
            instance = default;
        }

        private void OnDestroy()
        {
            ReleaseMusicInstance(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            ReleaseInstance(ref _sfx, FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            EndTypewriter();
            StopFinal();
            ReleaseInstance(ref _pauseSnapshot, FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            if (_instance == this)
                _instance = null;
        }
    }
}
