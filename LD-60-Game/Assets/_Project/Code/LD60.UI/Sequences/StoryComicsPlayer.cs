using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LD60.UI.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LD60.UI
{
    public sealed class StoryComicsPlayer : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private StoryComicsData data;
        [SerializeField] private bool playOnStart;
        [SerializeField] private InputActionAsset input;

        [Header("UI References")]
        [SerializeField] private CanvasGroup compositionGroup;
        [SerializeField] private Image darkOverlay;
        [SerializeField] private RectTransform storyBackRect;
        [SerializeField] private Image storyBackImage;
        [SerializeField] private Image[] blockImages = new Image[StoryComicsData.DefaultBlockCount];
        [SerializeField] private TMP_Text subtitleText;

        [Header("Timings")]
        [SerializeField] private float screenDarkenDuration = 0.5f;
        [SerializeField, Range(0f, 1f)] private float darkOverlayTargetAlpha = 0.82f;
        [SerializeField] private float backFlyInDuration = 0.65f;
        [SerializeField] private Vector2 backFlyInOffset = new Vector2(0f, -1080f);
        [SerializeField] private float blockFadeDuration = 0.6f;
        [SerializeField] private float charsPerSecond = 65f;
        [SerializeField] private float postBlockDelay = 1.5f;
        [SerializeField] private float compositionFadeOutDuration = 0.8f;
        [SerializeField] private bool deactivateOnFinish = true;

        [Header("Audio")]
        [Tooltip("Typing sound volume in this comic only. Master and SFX sliders still apply.")]
        [SerializeField, Range(0f, 1f)] private float typingVolume = 0.35f;

        private InputAction _skipAction;
        private bool _skipPressed;
        private bool _playing;
        private Vector2 _storyBackRestingPosition;
        private bool _restingPositionCaptured;
        private CancellationTokenSource _playCts;
        private bool _typingSoundActive;

        public event Action Finished;

        public bool IsPlaying => _playing;
        public StoryComicsData Data => data;

        private void Awake()
        {
            CaptureRestingPosition();
            if (input != null)
                _skipAction = input.FindAction("Menu/Skip", false);
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

            StopTypingAudio();
        }

        private void Start()
        {
            if (playOnStart && !_playing && data != null)
                Play(data);
        }

        private void OnDestroy()
        {
            CancelSequence();
        }

        public void Play()
        {
            Play(data);
        }

        public void Play(StoryComicsData comicsData)
        {
            PlayAsync(comicsData).Forget();
        }

        public async UniTask PlayAsync(StoryComicsData comicsData = null, CancellationToken externalToken = default)
        {
            if (comicsData != null)
                data = comicsData;

            if (data == null)
            {
                Debug.LogWarning("[StoryComicsPlayer] No StoryComicsData assigned.");
                return;
            }

            CancelSequence();
            CaptureRestingPosition();

            gameObject.SetActive(true);
            _playing = true;
            _skipPressed = false;

            _playCts = externalToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(externalToken, this.GetCancellationTokenOnDestroy())
                : CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            var token = _playCts.Token;

            try
            {
                PrepareInitialState(data);

                // Wait one frame so initial press that opened the cutscene does not skip anything
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                _skipPressed = false;

                // 1. Darken the screen
                await AnimateScreenDarkenAsync(token);

                // 2. Fly in the background sheet (DariyaArt/story_back)
                await AnimateBackFlyInAsync(token);

                // 3. Sequentially reveal each of the 6 blocks with typewriter subtitles & sound
                var blocks = data.Blocks;
                var count = blocks != null ? Mathf.Min(blocks.Length, blockImages.Length) : 0;
                for (var i = 0; i < count; i++)
                {
                    await PlayBlockAsync(i, blocks[i], token);
                }

                // 4. Fade out the entire composition and finish
                await AnimateCompositionFadeOutAsync(token);
            }
            catch (OperationCanceledException)
            {
                StopTypingAudio();
                return;
            }
            finally
            {
                StopTypingAudio();
            }

            Finish();
        }

        public void CancelSequence()
        {
            StopTypingAudio();
            if (_playCts != null)
            {
                _playCts.Cancel();
                _playCts.Dispose();
                _playCts = null;
            }
            _playing = false;
        }

        [ContextMenu("Apply Data To Preview")]
        public void ApplyDataToPreview()
        {
            if (data == null)
                return;

            if (storyBackImage != null && data.Background != null)
                storyBackImage.sprite = data.Background;

            var blocks = data.Blocks;
            if (blocks == null || blockImages == null)
                return;

            var count = Mathf.Min(blocks.Length, blockImages.Length);
            for (var i = 0; i < count; i++)
            {
                if (blockImages[i] == null)
                    continue;

                blockImages[i].sprite = blocks[i].image;
                SetImageAlpha(blockImages[i], 1f);
            }

            if (subtitleText != null && count > 0)
            {
                subtitleText.text = blocks[0].subtitle ?? string.Empty;
                subtitleText.maxVisibleCharacters = int.MaxValue;
            }
        }

        private void CaptureRestingPosition()
        {
            if (_restingPositionCaptured || storyBackRect == null)
                return;

            _storyBackRestingPosition = storyBackRect.anchoredPosition;
            _restingPositionCaptured = true;
        }

        private void PrepareInitialState(StoryComicsData comicsData)
        {
            if (compositionGroup != null)
            {
                compositionGroup.alpha = 1f;
                compositionGroup.blocksRaycasts = true;
                compositionGroup.interactable = true;
            }

            if (darkOverlay != null)
                SetImageAlpha(darkOverlay, 0f);

            if (storyBackImage != null)
            {
                if (comicsData.Background != null)
                    storyBackImage.sprite = comicsData.Background;
                SetImageAlpha(storyBackImage, 0f);
            }

            if (storyBackRect != null)
                storyBackRect.anchoredPosition = _storyBackRestingPosition + backFlyInOffset;

            var blocks = comicsData.Blocks;
            if (blockImages != null)
            {
                for (var i = 0; i < blockImages.Length; i++)
                {
                    var img = blockImages[i];
                    if (img == null)
                        continue;

                    if (blocks != null && i < blocks.Length && blocks[i].image != null)
                        img.sprite = blocks[i].image;

                    SetImageAlpha(img, 0f);
                }
            }

            if (subtitleText != null)
            {
                subtitleText.text = string.Empty;
                subtitleText.maxVisibleCharacters = 0;
                SetTextAlpha(subtitleText, 1f);
            }
        }

        private async UniTask AnimateScreenDarkenAsync(CancellationToken token)
        {
            if (darkOverlay == null || screenDarkenDuration <= 0f)
            {
                if (darkOverlay != null)
                    SetImageAlpha(darkOverlay, darkOverlayTargetAlpha);
                return;
            }

            var elapsed = 0f;
            while (elapsed < screenDarkenDuration)
            {
                if (ConsumeSkipRequest())
                    break;

                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / screenDarkenDuration);
                SetImageAlpha(darkOverlay, SmoothStep01(t) * darkOverlayTargetAlpha);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            SetImageAlpha(darkOverlay, darkOverlayTargetAlpha);
        }

        private async UniTask AnimateBackFlyInAsync(CancellationToken token)
        {
            SoundService.PlayPaperHandling();

            var startPos = _storyBackRestingPosition + backFlyInOffset;
            var targetPos = _storyBackRestingPosition;

            if (backFlyInDuration <= 0f)
            {
                if (storyBackRect != null)
                    storyBackRect.anchoredPosition = targetPos;
                if (storyBackImage != null)
                    SetImageAlpha(storyBackImage, 1f);
                return;
            }

            var elapsed = 0f;
            while (elapsed < backFlyInDuration)
            {
                if (ConsumeSkipRequest())
                    break;

                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / backFlyInDuration);
                var eased = EaseOutCubic(t);

                if (storyBackRect != null)
                    storyBackRect.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, eased);

                if (storyBackImage != null)
                    SetImageAlpha(storyBackImage, Mathf.Clamp01(t * 1.5f));

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            if (storyBackRect != null)
                storyBackRect.anchoredPosition = targetPos;
            if (storyBackImage != null)
                SetImageAlpha(storyBackImage, 1f);
        }

        private async UniTask PlayBlockAsync(int index, StoryComicsBlock block, CancellationToken token)
        {
            var blockImage = index >= 0 && index < blockImages.Length ? blockImages[index] : null;
            if (blockImage != null)
            {
                if (block.image != null)
                    blockImage.sprite = block.image;
                SetImageAlpha(blockImage, 0f);
            }

            var line = block.subtitle ?? string.Empty;
            var totalChars = 0;

            if (subtitleText != null)
            {
                subtitleText.text = line;
                subtitleText.maxVisibleCharacters = 0;
                subtitleText.ForceMeshUpdate();
                totalChars = subtitleText.textInfo.characterCount;
                if (totalChars <= 0 && line.Length > 0)
                    totalChars = line.Length;
            }

            var fadeDuration = Mathf.Max(0.01f, blockFadeDuration);
            var speed = Mathf.Max(1f, charsPerSecond);
            var elapsed = 0f;
            var fadeDone = blockImage == null;
            var typingDone = totalChars <= 0;

            if (!typingDone)
                StartTypingAudio();

            while (!fadeDone || !typingDone)
            {
                if (ConsumeSkipRequest())
                {
                    // Skip current block animation and immediately advance to the next block
                    if (blockImage != null)
                        SetImageAlpha(blockImage, 1f);
                    if (subtitleText != null)
                        subtitleText.maxVisibleCharacters = totalChars;
                    StopTypingAudio();
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    return;
                }

                elapsed += Time.unscaledDeltaTime;

                if (!fadeDone)
                {
                    var fadeT = Mathf.Clamp01(elapsed / fadeDuration);
                    SetImageAlpha(blockImage, SmoothStep01(fadeT));
                    if (fadeT >= 1f)
                        fadeDone = true;
                }

                if (!typingDone)
                {
                    var visible = Mathf.Clamp(Mathf.FloorToInt(elapsed * speed), 0, totalChars);
                    if (subtitleText != null)
                        subtitleText.maxVisibleCharacters = visible;

                    if (visible >= totalChars)
                    {
                        typingDone = true;
                        StopTypingAudio();
                    }
                    else
                    {
                        MaintainTypingAudio();
                    }
                }

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            StopTypingAudio();
            if (blockImage != null)
                SetImageAlpha(blockImage, 1f);
            if (subtitleText != null)
                subtitleText.maxVisibleCharacters = totalChars;

            // Wait a short pause before advancing to the next block (Space skips this pause too)
            var pauseElapsed = 0f;
            while (pauseElapsed < postBlockDelay)
            {
                if (ConsumeSkipRequest())
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    return;
                }

                pauseElapsed += Time.unscaledDeltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        private async UniTask AnimateCompositionFadeOutAsync(CancellationToken token)
        {
            if (compositionGroup == null || compositionFadeOutDuration <= 0f)
            {
                if (compositionGroup != null)
                    compositionGroup.alpha = 0f;
                return;
            }

            var elapsed = 0f;
            while (elapsed < compositionFadeOutDuration)
            {
                if (ConsumeSkipRequest())
                    break;

                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / compositionFadeOutDuration);
                compositionGroup.alpha = 1f - SmoothStep01(t);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            compositionGroup.alpha = 0f;
        }

        private void Finish()
        {
            _playing = false;
            StopTypingAudio();

            if (compositionGroup != null)
            {
                compositionGroup.blocksRaycasts = false;
                compositionGroup.interactable = false;
            }

            if (deactivateOnFinish)
                gameObject.SetActive(false);

            Finished?.Invoke();
        }

        private void OnSkipPerformed(InputAction.CallbackContext context)
        {
            if (_playing)
                _skipPressed = true;
        }

        private bool ConsumeSkipRequest()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                _skipPressed = true;

            if (!_skipPressed)
                return false;

            _skipPressed = false;
            return true;
        }

        private void StartTypingAudio()
        {
            _typingSoundActive = true;
            SoundService.StartTypewriter(typingVolume);
        }

        private void MaintainTypingAudio()
        {
            if (_typingSoundActive)
                SoundService.SetTypewriterVolume(typingVolume);
        }

        private void StopTypingAudio()
        {
            if (!_typingSoundActive)
                return;

            _typingSoundActive = false;
            SoundService.StopTypewriter();
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            if (image == null)
                return;

            var color = image.color;
            color.a = alpha;
            image.color = color;
        }

        private static void SetTextAlpha(TMP_Text text, float alpha)
        {
            if (text == null)
                return;

            var color = text.color;
            color.a = alpha;
            text.color = color;
        }

        private static float SmoothStep01(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private static float EaseOutCubic(float t)
        {
            var inv = 1f - t;
            return 1f - inv * inv * inv;
        }
    }
}
