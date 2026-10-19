using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LD60.Core;
using LD60.UI.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

namespace LD60.UI
{
    public sealed class CreditsPlayer : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private CanvasGroup rootCanvasGroup;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image frameImage;
        [SerializeField] private CanvasGroup gameOverGroup;
        [SerializeField] private TMP_Text gameOverText;
        [SerializeField] private RectTransform crawlViewport;
        [SerializeField] private CanvasGroup crawlGroup;
        [SerializeField] private RectTransform crawlContent;
        [SerializeField] private TMP_Text crawlText;
        [SerializeField] private CanvasGroup thanksGroup;
        [SerializeField] private TMP_Text thanksText;
        [SerializeField] private CanvasGroup blackFadeGroup;
        [SerializeField] private GameObject skipHint;
        [SerializeField] private InputActionAsset input;

        [Header("Content")]
        [SerializeField] private string gameOverString = "GAME OVER";
        [TextArea(10, 30)]
        [SerializeField] private string creditsContent =
@"<b>THE LAST WORD</b>

A game for Ludum Dare 60

<size=120%><b>CHEF GAMES</b></size>
Game Design & Programming

<size=120%><b>DARIYA</b></size>
Art & Visual Design

<size=120%><b>AUDIO</b></size>
Music & Sound Design

<size=120%><b>NARRATIVE & SCRIPT</b></size>
The Last Word Team

<size=120%><b>SPECIAL THANKS</b></size>
Ludum Dare Community
All Our Playtesters
And You, The Player!";

        [SerializeField] private string thanksString = "СПАСИБО ЗА ИГРУ";

        [Header("Timings (seconds)")]
        [SerializeField] private float frameFadeInDuration = 1.2f;
        [SerializeField] private float gameOverFadeInDuration = 0.8f;
        [SerializeField] private float gameOverStayDuration = 2.0f;
        [SerializeField] private float gameOverFadeOutDuration = 0.6f;
        [SerializeField] private float crawlSpeed = 65f;
        [SerializeField] private float crawlFastForwardMultiplier = 4f;
        [SerializeField] private float thanksFadeInDuration = 0.8f;
        [SerializeField] private float thanksStayDuration = 2.5f;
        [SerializeField] private float thanksFadeOutDuration = 0.6f;
        [SerializeField] private float blackFadeDuration = 1.2f;
        [SerializeField] private float postBlackDelay = 0.5f;

        [Header("Flow")]
        [SerializeField] private bool returnToMainMenuOnFinish = false;
        [SerializeField] private bool playOnStart = false;

        private IShellService _shell;
        private InputAction _skipAction;
        private bool _playing;
        private bool _hasFinished;
        private bool _skipRequested;
        private float _sequenceStartTime;
        private CancellationTokenSource _playCts;

        public event Action Finished;

        public bool IsPlaying => _playing;
        public bool HasFinished => _hasFinished;
        public string CreditsContent
        {
            get => creditsContent;
            set
            {
                creditsContent = value;
                if (crawlText != null)
                    crawlText.text = value;
            }
        }

        [Inject]
        public void Construct(IShellService shell)
        {
            _shell = shell;
        }

        private void Awake()
        {
            if (input != null)
                _skipAction = input.FindAction("Game/Pause", false);

            ResetVisuals();
        }

        private void OnEnable()
        {
            if (_skipAction != null)
            {
                _skipAction.performed += OnSkipPerformed;
                _skipAction.Enable();
            }
        }

        private void OnDisable()
        {
            if (_skipAction != null)
            {
                _skipAction.performed -= OnSkipPerformed;
                _skipAction.Disable();
            }
        }

        private void Start()
        {
            if (playOnStart && !_playing)
                Play();
        }

        private void OnDestroy()
        {
            CancelSequence();
        }

        private void OnValidate()
        {
            if (gameOverText != null && !string.IsNullOrEmpty(gameOverString))
                gameOverText.text = gameOverString;

            if (crawlText != null && !string.IsNullOrEmpty(creditsContent))
                crawlText.text = creditsContent;

            if (thanksText != null && !string.IsNullOrEmpty(thanksString))
                thanksText.text = thanksString;
        }

        private void OnSkipPerformed(InputAction.CallbackContext _)
        {
            if (!_playing || Time.unscaledTime - _sequenceStartTime < 0.6f)
                return;

            _skipRequested = true;
        }

        private bool ConsumeSkipRequest()
        {
            if (_skipAction != null && _skipAction.triggered && Time.unscaledTime - _sequenceStartTime >= 0.6f)
                _skipRequested = true;

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && Time.unscaledTime - _sequenceStartTime >= 0.6f)
                _skipRequested = true;

            if (!_skipRequested)
                return false;

            _skipRequested = false;
            return true;
        }

        public void Play()
        {
            PlayAsync().Forget();
        }

        public void Skip()
        {
            _skipRequested = true;
        }

        public void CancelSequence()
        {
            if (_playCts != null)
            {
                _playCts.Cancel();
                _playCts.Dispose();
                _playCts = null;
            }
            _playing = false;
        }

        public async UniTask PlayAsync(CancellationToken externalToken = default)
        {
            if (_playing)
                return;

            Debug.Log("[CreditsPlayer] Starting credits sequence...");
            _playing = true;
            _hasFinished = false;
            _skipRequested = false;
            _sequenceStartTime = Time.unscaledTime;

            if (_playCts != null)
            {
                _playCts.Cancel();
                _playCts.Dispose();
            }

            _playCts = externalToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(externalToken, this.GetCancellationTokenOnDestroy())
                : CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            var token = _playCts.Token;

            gameObject.SetActive(true);
            ResetVisuals();

            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.blocksRaycasts = true;
                rootCanvasGroup.interactable = true;
            }

            // Prepare audio
            try
            {
                SoundService.PlayFinal();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CreditsPlayer] Audio switch warning: {e.Message}");
            }

            // Wait one frame to avoid consuming clicks that ended the previous screen
            await UniTask.Yield(PlayerLoopTiming.Update, token);
            _skipRequested = false;
            _sequenceStartTime = Time.unscaledTime;

            if (skipHint != null)
                skipHint.SetActive(true);

            try
            {
                // Phase 1: Fade in Frame & Background ("затемняем в эту рамку")
                Debug.Log("[CreditsPlayer] Phase 1: Fade in Frame");
                await FadeCanvasGroupAsync(rootCanvasGroup, 0f, 1f, frameFadeInDuration, token);

                // Phase 2: Game Over appears ("там пишется гейм овер с тем шрифтом, который в интро")
                if (gameOverGroup != null)
                {
                    Debug.Log("[CreditsPlayer] Phase 2: Game Over");
                    if (gameOverText != null)
                        gameOverText.text = gameOverString;

                    await FadeCanvasGroupAsync(gameOverGroup, 0f, 1f, gameOverFadeInDuration, token);
                    await DelayOrSkipAsync(gameOverStayDuration, token);
                    await FadeCanvasGroupAsync(gameOverGroup, 1f, 0f, gameOverFadeOutDuration, token);
                }

                // Phase 3: Crawling credits ("начинают титры ползти снизу вверх")
                Debug.Log("[CreditsPlayer] Phase 3: Crawl");
                await PlayCrawlAsync(token);

                // Phase 4: Thank you message ("пишем спасибо за игру")
                if (thanksGroup != null)
                {
                    Debug.Log("[CreditsPlayer] Phase 4: Thanks");
                    if (thanksText != null)
                        thanksText.text = thanksString;

                    await FadeCanvasGroupAsync(thanksGroup, 0f, 1f, thanksFadeInDuration, token);
                    await DelayOrSkipAsync(thanksStayDuration, token);
                    await FadeCanvasGroupAsync(thanksGroup, 1f, 0f, thanksFadeOutDuration, token);
                }

                // Phase 5: Fade completely to black ("экран темнеет полностью")
                if (blackFadeGroup != null)
                {
                    Debug.Log("[CreditsPlayer] Phase 5: Fade to black");
                    await FadeCanvasGroupAsync(blackFadeGroup, 0f, 1f, blackFadeDuration, token);
                    if (postBlackDelay > 0f)
                        await UniTask.Delay((int)(postBlackDelay * 1000f), ignoreTimeScale: true, cancellationToken: token);
                }

                // Phase 6: Standalone Return to main menu (if requested)
                if (returnToMainMenuOnFinish)
                {
                    await ReturnToMainMenuAsync();
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[CreditsPlayer] Sequence cancelled.");
            }
            finally
            {
                _playing = false;
                _hasFinished = true;
                if (rootCanvasGroup != null)
                {
                    rootCanvasGroup.blocksRaycasts = false;
                    rootCanvasGroup.interactable = false;
                }
                Finished?.Invoke();
                Debug.Log("[CreditsPlayer] Credits sequence complete.");
            }
        }

        private async UniTask PlayCrawlAsync(CancellationToken token)
        {
            if (crawlViewport == null || crawlContent == null || crawlText == null)
                return;

            if (!string.IsNullOrEmpty(creditsContent))
                crawlText.text = creditsContent;

            // Make sure viewport is invisible while setting initial offscreen position
            if (crawlGroup != null)
                crawlGroup.alpha = 0f;
            crawlViewport.gameObject.SetActive(true);

            // Force layout rebuild so content height is accurate
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(crawlContent);

            var viewportHeight = crawlViewport.rect.height;
            var contentHeight = crawlContent.rect.height;

            // Position crawlContent so its top is just below the bottom of the viewport
            var startY = -viewportHeight * 0.5f - contentHeight * 0.5f;
            var endY = viewportHeight * 0.5f + contentHeight * 0.5f + 40f;

            var currentY = startY;
            crawlContent.anchoredPosition = new Vector2(0f, currentY);

            // Now make the crawl visible at its initial below-screen position
            if (crawlGroup != null)
                crawlGroup.alpha = 1f;

            while (currentY < endY)
            {
                if (token.IsCancellationRequested)
                    return;

                if (ConsumeSkipRequest())
                    break;

                var speed = crawlSpeed;
                if (IsFastForwardHeld())
                    speed *= crawlFastForwardMultiplier;

                var dt = Time.unscaledDeltaTime;
                currentY += speed * dt;
                crawlContent.anchoredPosition = new Vector2(0f, currentY);

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            if (crawlGroup != null)
                crawlGroup.alpha = 0f;
            crawlViewport.gameObject.SetActive(false);
        }

        private bool IsFastForwardHeld()
        {
            if (_skipAction != null && _skipAction.IsPressed())
                return true;

            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.isPressed || kb.downArrowKey.isPressed || kb.enterKey.isPressed))
                return true;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
                return true;

            return false;
        }

        private async UniTask DelayOrSkipAsync(float duration, CancellationToken token)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                if (token.IsCancellationRequested)
                    return;

                if (ConsumeSkipRequest())
                    return;

                var dt = Time.unscaledDeltaTime;
                if (IsFastForwardHeld())
                    dt *= crawlFastForwardMultiplier;

                elapsed += dt;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        private async UniTask FadeCanvasGroupAsync(CanvasGroup group, float from, float to, float duration, CancellationToken token)
        {
            if (group == null)
                return;

            group.alpha = from;
            if (duration <= 0.001f)
            {
                group.alpha = to;
                return;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                if (token.IsCancellationRequested)
                    return;

                if (ConsumeSkipRequest())
                {
                    group.alpha = to;
                    return;
                }

                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                group.alpha = Mathf.Lerp(from, to, t);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            group.alpha = to;
        }

        private void ResetVisuals()
        {
            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = 0f;
                rootCanvasGroup.blocksRaycasts = false;
                rootCanvasGroup.interactable = false;
            }

            if (gameOverGroup != null)
                gameOverGroup.alpha = 0f;

            if (crawlGroup != null)
                crawlGroup.alpha = 0f;

            if (crawlViewport != null)
                crawlViewport.gameObject.SetActive(false);

            if (crawlContent != null)
                crawlContent.anchoredPosition = new Vector2(0f, -5000f);

            if (thanksGroup != null)
                thanksGroup.alpha = 0f;

            if (blackFadeGroup != null)
                blackFadeGroup.alpha = 0f;

            if (skipHint != null)
                skipHint.SetActive(false);
        }

        private async UniTask ReturnToMainMenuAsync()
        {
            if (_shell != null)
            {
                await _shell.FinishStoryAsync();
                return;
            }

            var scope = FindAnyObjectByType<VContainer.Unity.LifetimeScope>();
            if (scope != null && scope.Container != null)
            {
                try
                {
                    var shell = scope.Container.Resolve<IShellService>();
                    if (shell != null)
                    {
                        await shell.FinishStoryAsync();
                        return;
                    }
                }
                catch
                {
                    // Ignore container resolution error
                }
            }

            Debug.LogWarning("[CreditsPlayer] IShellService not found, falling back to SceneManager.LoadScene(MainMenu).");
            await UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(GameScenes.MainMenu);
        }
    }
}
