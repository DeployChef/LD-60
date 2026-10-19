using System;
using Cysharp.Threading.Tasks;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace LD60.UI.Audio
{
    /// <summary>
    /// Аудио для меню и вступительной катсцены (Fmod_event_map.md §4.2):
    /// Ивент: event:/menu/start or menu
    /// Параметр: "cutscene or menu" (0 = cut, 1 = menu, 2 = start).
    /// При значении 2 (start) ивент сам глушит себя в FMOD Studio,
    /// после чего код освобождает инстанс.
    /// UI-звуки: event:/menu/UI menu (PlayOneShot).
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class MenuAudio : MonoBehaviour
    {
        private static readonly string[] StartOrMenuPaths = { "event:/menu/start or menu", "event:/start or menu" };
        private static readonly string[] UiMenuPaths = { "event:/menu/UI menu", "event:/UI menu" };
        private static readonly string[] TypePaths = { "event:/type", "event:/SFX/type" };
        private static readonly string[] PaperHandlingPaths = { "event:/oneshot/paper_handing", "event:/paper_handing" };
        private static readonly string[] ConvertPaths = { "event:/oneshot/convert", "event:/convert" };
        private static readonly string[] CutsceneOrMenuParamNames = { "cutscene or menu", "cutscene or menu " };

        private const float CutValue = 0f;
        private const float MenuValue = 1f;
        private const float StartValue = 2f;
        // Marker A: the menu section of the bundled "start or menu" event.
        private const int MenuTimelinePositionMilliseconds = 16000;

        private static MenuAudio _instance;
        private EventInstance _menuMusic;
        private string _resolvedParamName = "cutscene or menu";
        private float _targetState = CutValue;

        public static void EnsureCreated()
        {
            if (_instance != null)
                return;

            var go = new GameObject(nameof(MenuAudio));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<MenuAudio>();
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
            // Клавиша печатной машинки
            if (!PlayFirstAvailable(TypePaths))
            {
                // Фоллбэк на клик меню если нет type ивента
                PlayFirstAvailable(UiMenuPaths);
            }
        }

        public static void PlayPaperHandling()
        {
            // Шелест / взяли бумажку со стола
            PlayFirstAvailable(PaperHandlingPaths);
        }

        public static void PlayPaperDrop()
        {
            // Бросили / положили статью на стол или в газету
            PlayFirstAvailable(UiMenuPaths);
        }

        public static void PlayEnvelopeOpen()
        {
            // Вскрытие конверта / пневмопочта на столе
            PlayFirstAvailable(ConvertPaths);
        }

        private static bool PlayFirstAvailable(string[] paths)
        {
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
            // Ждём пока FMOD инициализирует систему банков
            while (!RuntimeManager.HasBankLoaded("Menu") && !RuntimeManager.HasBankLoaded("Master"))
            {
                await UniTask.Yield();
            }

            EnsureMusicInstance();
            ApplyState();
        }

        private void SetState(float value, string label)
        {
            // Changing the parameter alone leaves the intro's sounds playing
            // until FMOD reaches its transition region, even after Space skips it.
            if (_targetState != value)
                ReleaseMusicInstance(FMOD.Studio.STOP_MODE.IMMEDIATE);
            _targetState = value;
            EnsureMusicInstance();
            ApplyState();
            Debug.Log($"[MenuAudio] cutscene or menu -> {label} ({value})");
        }

        private void TriggerStart()
        {
            _targetState = StartValue;
            if (!_menuMusic.isValid())
                return;

            SetParameter(_menuMusic, _resolvedParamName, StartValue);
            Debug.Log("[MenuAudio] cutscene or menu -> start (2)");

            // Даём звуку перехода отыграть и освобождаем инстанс
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

            if (instance.isValid())
            {
                instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                instance.release();
                if (_menuMusic.handle == instance.handle)
                    _menuMusic = default;
            }
        }

        private void EnsureMusicInstance()
        {
            if (_menuMusic.isValid())
            {
                if (_menuMusic.getPlaybackState(out var state) == FMOD.RESULT.OK && state == PLAYBACK_STATE.STOPPED)
                {
                    StartMusicInstance();
                }
                return;
            }

            foreach (var path in StartOrMenuPaths)
            {
                try
                {
                    _menuMusic = RuntimeManager.CreateInstance(path);
                    if (_menuMusic.isValid())
                    {
                        ResolveParamName(_menuMusic);
                        StartMusicInstance();
                        Debug.Log($"[MenuAudio] инстанс {path} создан и запущен");
                        break;
                    }
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

        private void ReleaseMusicInstance(FMOD.Studio.STOP_MODE mode)
        {
            if (_menuMusic.isValid())
            {
                _menuMusic.stop(mode);
                _menuMusic.release();
                _menuMusic = default;
            }
        }

        private void OnDestroy()
        {
            ReleaseMusicInstance(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        }
    }
}
