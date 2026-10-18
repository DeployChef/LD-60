using System;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace LD60.Main.Audio
{
    /// <summary>
    /// Headless-прелоудер FMOD для WebGL (Fmod_Webgl_instruction.md, раздел 5).
    /// Своего UI нет: стартовое окно игры делает команда разработчиков и
    /// «подсоединяется» к этому сервису — читает Stage / Progress01 /
    /// SamplesLoaded / SamplesTotal / IsReady и ждёт PrepareAsync.
    /// WebGL однопоточная среда: банки качаются по сети асинхронно, звук стартует
    /// только после первого клика игрока (политика автоплея браузеров).
    /// Порядок готовности: банки (HasBankLoaded) → сэмплы событий
    /// (getSampleLoadingState == LOADED) → только потом можно создавать инстансы.
    /// Загрузку выполняет сам плагин (Load Banks: All), из кода только поллинг —
    /// ручной LoadBank дублирует скачивание.
    /// Будущий FMOD-менеджер буферизует Play до FmodWebglPreloader.IsReady / BecameReady.
    /// </summary>
    [DefaultExecutionOrder(1000)] // обновляться после RuntimeManager.Update (System::update)
    public sealed class FmodWebglPreloader : MonoBehaviour
    {
        private const int StallLogFrames = 300; // ~5 секунд при 60 fps

        public enum PreloadStage
        {
            Idle,
            Init,
            Banks,
            Samples,
            WaitForInput,
            Ready,
        }

        public static bool IsReady { get; private set; }
        public static event Action BecameReady;

        /// <summary>Текущая стадия загрузки — для стартового окна игры.</summary>
        public static PreloadStage Stage { get; private set; } = PreloadStage.Idle;

        /// <summary>Общий прогресс 0..1 — для прогресс-бара стартового окна.</summary>
        public static float Progress01 { get; private set; }

        public static int SamplesLoaded { get; private set; }
        public static int SamplesTotal { get; private set; }

        private static FmodWebglPreloader _instance;
        private static UniTask _pending;

        public static UniTask PrepareAsync(CancellationToken cancellationToken = default)
        {
            if (IsReady)
                return UniTask.CompletedTask;

            if (_instance == null)
            {
                var go = new GameObject(nameof(FmodWebglPreloader));
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<FmodWebglPreloader>();
            }

            if (_pending.Status != UniTaskStatus.Pending)
                _pending = _instance.RunAsync(cancellationToken);

            return _pending;
        }

        private async UniTask RunAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            // Доступ к StudioSystem инициализирует FMOD и запускает загрузку банков.
            Stage = PreloadStage.Init;
            Progress01 = 0.05f;
            _ = RuntimeManager.StudioSystem;
#if !UNITY_EDITOR
            SilenceFmodNativeSpam();
#endif
            await UniTask.NextFrame(cancellationToken);

            // 1. Банки. Имя банка без расширения: bankId = "Master", "Master.strings".
            Stage = PreloadStage.Banks;
            var stall = 0;
            while (!(RuntimeManager.HasBankLoaded("Master")
                     && RuntimeManager.HasBankLoaded("Master.strings")))
            {
                Progress01 = 0.2f;
                if (++stall % StallLogFrames == 0)
                    Debug.Log($"[FmodPreloader] ждём банки ({stopwatch.ElapsedMilliseconds} ms): {DescribeBanks()}");
                await UniTask.Yield(cancellationToken);
            }

            Debug.Log($"[FmodPreloader] банки загружены: {DescribeBanks()} (+{stopwatch.ElapsedMilliseconds} ms)");

            // 2. Сэмплы: инстанс, созданный раньше сэмплов, даёт «API отвечает, а звука нет».
            Stage = PreloadStage.Samples;
            stall = 0;
            while (true)
            {
                var (done, total) = CountSampleLoading();
                SamplesLoaded = done;
                SamplesTotal = total;
                Progress01 = 0.35f + 0.55f * (total == 0 ? 1f : done / (float)total);
                if (done >= total)
                    break;

                if (++stall % StallLogFrames == 0)
                    Debug.LogWarning($"[FmodPreloader] сэмплы {done}/{total} за {stopwatch.ElapsedMilliseconds} ms");
                await UniTask.Yield(cancellationToken);
            }

            // 3. Первый клик — только в браузере: без взаимодействия звука не будет.
            //    Любой ввод снимает гейт, в том числе клик по кнопке стартового окна.
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                Stage = PreloadStage.WaitForInput;
                Progress01 = 0.95f;
                while (!AnyUserInputThisFrame())
                    await UniTask.Yield(cancellationToken);
            }

            Stage = PreloadStage.Ready;
            Progress01 = 1f;
            IsReady = true;
            Debug.Log($"[FmodPreloader] FMOD готов за {stopwatch.ElapsedMilliseconds} ms");
            BecameReady?.Invoke();
        }

        private static bool AnyUserInputThisFrame()
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                return true;

            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
                return true;

            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.anyKey.wasPressedThisFrame;
        }

        private static (int done, int total) CountSampleLoading()
        {
            var done = 0;
            var total = 0;

            if (RuntimeManager.StudioSystem.getBankList(out var banks) != FMOD.RESULT.OK)
                return (0, 0);

            foreach (var bank in banks)
            {
                if (!bank.isValid())
                    continue;

                if (bank.getEventList(out var events) != FMOD.RESULT.OK)
                    continue;

                foreach (var description in events)
                {
                    total++;
                    if (description.getSampleLoadingState(out var state) != FMOD.RESULT.OK)
                        continue;

                    if (state == LOADING_STATE.LOADED)
                        done++;
                    else if (state == LOADING_STATE.ERROR)
                        Debug.LogError("[FmodPreloader] ошибка загрузки сэмплов события — оно будет молчать");
                }
            }

            return (done, total);
        }

        private static string DescribeBanks()
        {
            if (RuntimeManager.StudioSystem.getBankList(out var banks) != FMOD.RESULT.OK)
                return "неизвестно";

            var text = new System.Text.StringBuilder();
            foreach (var bank in banks)
            {
                if (bank.getPath(out var path) == FMOD.RESULT.OK)
                    text.Append(path).Append(' ');
            }

            return text.Length == 0 ? "пусто" : text.ToString().Trim();
        }

#if !UNITY_EDITOR
        private static FMOD.DEBUG_CALLBACK _fmodDebugSwallow;

        /// <summary>
        /// В релизном плеере плагин НЕ инициализирует FMOD-логирование
        /// (RuntimeManager делает это только под UNITY_EDITOR || DEVELOPMENT_BUILD),
        /// и FMOD печатает ошибки OS-слоя нативно в консоль браузера — спам
        /// "FMOD_OS_CriticalSection_Enter/Leave: cannot enter NULL critical section".
        /// Ставим пустой колбэк с уровнем NONE: путь вывода по умолчанию
        /// отключается, консоль чистая. Ошибки загрузки банков это не глушит —
        /// плагин кидает их как Unity-исключения (BankLoadException).
        /// </summary>
        private static void SilenceFmodNativeSpam()
        {
            if (_fmodDebugSwallow != null)
                return;

            _fmodDebugSwallow = SwallowDebugCallback;
            FMOD.Debug.Initialize(FMOD.DEBUG_FLAGS.NONE, FMOD.DEBUG_MODE.CALLBACK, _fmodDebugSwallow, null);
        }

        [AOT.MonoPInvokeCallback(typeof(FMOD.DEBUG_CALLBACK))]
        private static FMOD.RESULT SwallowDebugCallback(FMOD.DEBUG_FLAGS flags, IntPtr file, int line, IntPtr func, IntPtr message)
        {
            return FMOD.RESULT.OK;
        }
#endif
    }
}
