using Cysharp.Threading.Tasks;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LD60.Main.Audio
{
    /// <summary>
    /// Громофон — ПОЛНОСТЬЮ ОТДЕЛЬНЫЙ ивент event:/громофон (решение Валентина,
    /// не вложен в Music). Один вечный глобальный инстанс, создаётся после
    /// готовности FMOD. Управление — только параметрами:
    ///   "on or off" — on=0, off=1 (labels);
    ///   "track"     — track1=0, track2=1, track3=2 (Min 0 / Max 2).
    /// Внутри ивента всё сделано в FMOD Studio: смена трека — ритуал
    /// «stop track» (звук остановки пластинки) → «start» → на 6.26 с маркеры
    /// по track==0/1/2 ведут на выбранную пластинку. Выключение (off) —
    /// громофон сам глушится своим CommandSound.
    /// Нюанс: после самостопа инстанс STOPPED — параметры его не оживят,
    /// поэтому при включении (и опросом) код делает start() заново и
    /// пере-применяет параметры (start сбрасывает их в начальные значения).
    /// Music (event:/Music) — отдельная вечная музыка, к громофону отношения
    /// больше не имеет.
    /// Тест (временное, убрать после отладки): клавиши (верхний ряд и numpad)
    /// 0=off, 9=on, 1/2/3=выбор трека.
    /// </summary>
    [DefaultExecutionOrder(1000)] // после RuntimeManager.Update (System::update)
    public sealed class LoudspeakerAudio : MonoBehaviour
    {
        private const string MusicPath = "event:/Music";
        private const string LoudspeakerPath = "event:/громофон";
        private const string OnOffParameter = "on or off";
        private const string TrackParameter = "track";

        private const float OnValue = 0f; // label "on"
        private const float OffValue = 1f; // label "off"

        private const float ParamRefreshInterval = 0.1f;

        private static LoudspeakerAudio _instance;

        private EventInstance _loudspeaker;
        private EventInstance _music;

        private bool _on;
        private int _track = 1;
        private float _nextParamRefresh;

        public static bool IsOn => _instance != null && _instance._on;
        public static int Track => _instance != null ? _instance._track : 1;

        public static void EnsureCreated()
        {
            if (_instance != null)
                return;

            var go = new GameObject(nameof(LoudspeakerAudio));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<LoudspeakerAudio>();
        }

        /// <summary>Включить/выключить громофон (параметр "on or off").</summary>
        public static void SetOn(bool on)
        {
            EnsureCreated();
            _instance._on = on;
            _instance.ApplyParameters(verbose: true);
            _instance.EnsurePlaying();
        }

        /// <summary>Выбрать трек 1..3 (параметр "track").</summary>
        public static void SetTrack(int trackNumber)
        {
            EnsureCreated();
            _instance._track = Mathf.Clamp(trackNumber, 1, 3);
            _instance.ApplyParameters(verbose: true);
        }

        private void Start()
        {
            InitAsync().Forget();
        }

        private async UniTaskVoid InitAsync()
        {
            // Инстансы создаём только после готовности банков и сэмплов (WebGL).
            await FmodWebglPreloader.PrepareAsync();

            _loudspeaker = RuntimeManager.CreateInstance(LoudspeakerPath);
            _music = RuntimeManager.CreateInstance(MusicPath);

            // Громофон стартует выключенным: включение — start() + параметры
            // (чтобы не озвучивать ритуал выключения на буте).
            ApplyParameters(verbose: true);
            _music.start();

            Debug.Log("[Loudspeaker] громофон (отдельный ивент) и Music готовы: off, track1 (тест: 9=on, 0=off, 1/2/3=трек)");
        }

        // ВРЕМЕННЫЙ ТЕСТ: переключение параметров клавишами (верхний ряд и numpad).
        // 0 = off, 9 = on, 1/2/3 = track1/track2/track3. Убрать после отладки.
        private void Update()
        {
            HandleTestKeys();

            if (Time.unscaledTime < _nextParamRefresh)
                return;

            _nextParamRefresh = Time.unscaledTime + ParamRefreshInterval;
            if (_on)
                EnsurePlaying();
        }

        private void HandleTestKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.digit0Key.wasPressedThisFrame || keyboard.numpad0Key.wasPressedThisFrame)
                SetOn(false);
            else if (keyboard.digit9Key.wasPressedThisFrame || keyboard.numpad9Key.wasPressedThisFrame)
                SetOn(true);
            else if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                SetTrack(1);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                SetTrack(2);
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                SetTrack(3);
        }

        /// <summary>
        /// Пока громофон включён, инстанс должен играть: после самостопа
        /// (off-ритуал или конец логики) оживляем start()'ом и сразу
        /// пере-применяем параметры — start сбрасывает их в начальные.
        /// </summary>
        private void EnsurePlaying()
        {
            if (!_loudspeaker.isValid())
                return;

            if (_loudspeaker.getPlaybackState(out var state) != FMOD.RESULT.OK)
                return;

            if (state == PLAYBACK_STATE.STOPPED)
            {
                _loudspeaker.start();
                ApplyParameters(verbose: false);
                Debug.Log($"[Loudspeaker] инстанс перезапущен (был STOPPED), track{_track}, on={_on}");
            }
        }

        private void ApplyParameters(bool verbose)
        {
            if (!_loudspeaker.isValid())
                return;

            var onOffValue = _on ? OnValue : OffValue;
            var trackValue = _track - 1;

            _loudspeaker.getParameterByName(OnOffParameter, out var oldOnOff);
            _loudspeaker.getParameterByName(TrackParameter, out var oldTrack);

            _loudspeaker.setParameterByName(OnOffParameter, onOffValue, ignoreseekspeed: true);
            _loudspeaker.setParameterByName(TrackParameter, trackValue, ignoreseekspeed: true);

            if (verbose)
            {
                _loudspeaker.getPlaybackState(out var state);
                Debug.Log($"[Loudspeaker] громофон: {state}, on-or-off {oldOnOff}->{onOffValue}, track {oldTrack}->{trackValue}");
            }
        }

        private void OnDestroy()
        {
            if (_loudspeaker.isValid())
            {
                _loudspeaker.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                _loudspeaker.release();
            }

            if (_music.isValid())
            {
                _music.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                _music.release();
            }
        }
    }
}
