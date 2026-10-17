using Cysharp.Threading.Tasks;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LD60.Main.Audio
{
    /// <summary>
    /// Громофон — event:/громофон, дочерний ивент Music (event:/Music).
    /// Один вечный глобальный инстанс громофона + один вечный инстанс Music:
    /// запускаем Music, он подхватывает громофон, если тот включён.
    /// Параметры громофона (Fmod_Webgl_instruction.md §1 — контейнер + параметры):
    ///   "on or off" — on=0, off=1 (labels); ставится и на громофон, и на Music
    ///                (у Music свой прокси этого параметра — гейт подхвата);
    ///   "track"     — track1=0, track2=1, track3=2 (Min 0 / Max 2); только громофон.
    /// Громофон стартует выключенным; включение/выбор трека — параметрами.
    /// Тест (временное, убрать после отладки): клавиши 0=off, 9=on, 1/2/3=выбор трека.
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

        private static LoudspeakerAudio _instance;

        private EventInstance _loudspeaker;
        private EventInstance _music;

        private bool _on;
        private int _track = 1;

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
            _instance.ApplyOn();
        }

        /// <summary>Выбрать трек 1..3 (параметр "track").</summary>
        public static void SetTrack(int trackNumber)
        {
            EnsureCreated();
            _instance._track = Mathf.Clamp(trackNumber, 1, 3);
            _instance.ApplyTrack();
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

            ApplyOn();
            ApplyTrack();

            _loudspeaker.start();
            _music.start();

            Debug.Log("[Loudspeaker] громофон и Music запущены: громофон off, track1 (тест: 9=on, 0=off, 1/2/3=трек)");
        }

        // ВРЕМЕННЫЙ ТЕСТ: переключение параметров клавишами (верхний ряд и numpad).
        // 0 = off, 9 = on, 1/2/3 = track1/track2/track3. Убрать после отладки.
        private void Update()
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

        private void ApplyOn()
        {
            var value = _on ? OnValue : OffValue;
            var label = _on ? "on" : "off";
            SetParameterLogged(_loudspeaker, OnOffParameter, value, label);
            SetParameterLogged(_music, OnOffParameter, value, label);
        }

        private void ApplyTrack()
        {
            SetParameterLogged(_loudspeaker, TrackParameter, _track - 1, "track" + _track);
        }

        private static void SetParameterLogged(EventInstance instance, string parameterName, float value, string label)
        {
            if (!instance.isValid())
                return;

            instance.setParameterByName(parameterName, value, ignoreseekspeed: true);
            if (instance.getParameterByName(parameterName, out var readback) == FMOD.RESULT.OK)
                Debug.Log($"[Loudspeaker] {parameterName} = {value} ({label}), readback {readback}");
            else
                Debug.LogWarning($"[Loudspeaker] {parameterName}: readback failed — параметр не найден в банке");
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
