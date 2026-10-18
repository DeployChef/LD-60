using Cysharp.Threading.Tasks;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LD60.Main.Audio
{
    /// <summary>
    /// SFX (event:/SFX) — вечный контейнер-ивент (Fmod_Webgl_instruction.md §1):
    /// стартуется один раз и живёт в запущенном состоянии, таймлайн пустой.
    /// Параметр "newspaper": typing=0, type=1, none=2 (начальное — none).
    /// На значении type(1) лежит вложенный ивент (звук печатания), на typing(0) — другой.
    /// «Клик параметром» (§1 + §5.6): по вызову newspaper → type, вложенный ивент
    /// ДОИГРЫВАЕТ (ждём поллингом его инстансов), затем newspaper → none.
    /// Тест (временное, убрать после отладки): клавиша E.
    /// </summary>
    [DefaultExecutionOrder(1000)] // после RuntimeManager.Update (System::update)
    public sealed class SfxAudio : MonoBehaviour
    {
        private const string SfxPath = "event:/SFX";
        private const string NewspaperParameter = "newspaper";

        private const float TypingValue = 0f; // label "typing"
        private const float TypeValue = 1f; // label "type"
        private const float NoneValue = 2f; // label "none"

        // Вложенный ивент «звук type»: в иерархии Studio вложен в SFX — путь может
        // быть event:/type или event:/SFX/type, пробуем оба.
        private static readonly string[] TypeEventPaths = { "event:/type", "event:/SFX/type" };

        private const float SpawnTimeoutSeconds = 1f; // ждём появления вложенного ивента
        private const float FinishTimeoutSeconds = 30f; // страховка от залипания параметра

        private static SfxAudio _instance;

        private EventInstance _sfx;
        private bool _clickRunning;

        public static void EnsureCreated()
        {
            if (_instance != null)
                return;

            var go = new GameObject(nameof(SfxAudio));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SfxAudio>();
        }

        /// <summary>
        /// «Клик» параметром newspaper: none → type → вложенный ивент доигрывает → none.
        /// Повторный вызов, пока предыдущий звук играет, игнорируется.
        /// </summary>
        public static void PlayType()
        {
            EnsureCreated();
            _instance.ClickTypeAsync().Forget();
        }

        private void Start()
        {
            InitAsync().Forget();
        }

        private async UniTaskVoid InitAsync()
        {
            await FmodWebglPreloader.PrepareAsync();

            _sfx = RuntimeManager.CreateInstance(SfxPath);
            _sfx.start();

            Debug.Log("[Sfx] контейнер SFX запущен: newspaper=none (тест: E — звук type)");
        }

        // ВРЕМЕННЫЙ ТЕСТ: клавиша E запускает «клик» параметром. Убрать после отладки.
        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
                PlayType();
        }

        private async UniTaskVoid ClickTypeAsync()
        {
            if (!_sfx.isValid() || _clickRunning)
                return;

            _clickRunning = true;
            try
            {
                SetNewspaper(TypeValue, "type");

                // Ждём появления инстанса вложенного ивента (спавнится на следующем FMOD-апдейте).
                var deadline = Time.unscaledTime + SpawnTimeoutSeconds;
                while (!AnyTypeInstanceActive())
                {
                    if (Time.unscaledTime >= deadline)
                    {
                        Debug.LogWarning("[Sfx] вложенный ивент type не появился за 1 с — банки устарели? (Ctrl+B → клик в Unity)");
                        SetNewspaper(NoneValue, "none (принудительно, звук не появился)");
                        return;
                    }

                    await UniTask.Yield();
                }

                // Ждём, пока вложенный ивент ДОИГРАЕТ (инстансы закончились).
                deadline = Time.unscaledTime + FinishTimeoutSeconds;
                while (AnyTypeInstanceActive())
                {
                    if (Time.unscaledTime >= deadline)
                    {
                        Debug.LogWarning("[Sfx] вложенный ивент type играет дольше 30 с — принудительный возврат newspaper=none");
                        break;
                    }

                    await UniTask.Yield();
                }

                SetNewspaper(NoneValue, "none");
            }
            finally
            {
                _clickRunning = false;
            }
        }

        private void SetNewspaper(float value, string label)
        {
            if (!_sfx.isValid())
                return;

            _sfx.setParameterByName(NewspaperParameter, value, ignoreseekspeed: true);
            if (_sfx.getParameterByName(NewspaperParameter, out var readback) == FMOD.RESULT.OK)
                Debug.Log($"[Sfx] newspaper = {value} ({label}), readback {readback}");
            else
                Debug.LogWarning("[Sfx] readback newspaper не удался — параметра нет в банке (пересобери банки)");
        }

        /// <summary>Есть ли живой (играющий) инстанс вложенного ивента type.</summary>
        private static bool AnyTypeInstanceActive()
        {
            foreach (var path in TypeEventPaths)
            {
                if (RuntimeManager.StudioSystem.getEvent(path, out var description) != FMOD.RESULT.OK
                    || !description.isValid())
                    continue;

                if (description.getInstanceList(out var instances) != FMOD.RESULT.OK)
                    continue;

                foreach (var instance in instances)
                {
                    if (instance.getPlaybackState(out var state) != FMOD.RESULT.OK)
                        continue;

                    if (state == PLAYBACK_STATE.STARTING
                        || state == PLAYBACK_STATE.PLAYING
                        || state == PLAYBACK_STATE.SUSTAINING)
                        return true;
                }
            }

            return false;
        }

        private void OnDestroy()
        {
            if (_sfx.isValid())
            {
                _sfx.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                _sfx.release();
            }
        }
    }
}
