using System.Threading;
using Cysharp.Threading.Tasks;
using LD60.Gameplay.Story;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LD60.UI
{
    public sealed class DialogueManager : MonoBehaviour, IDialogue
    {
        [SerializeField] private SpriteRenderer panel;
        [SerializeField] private TMP_Text line;
        [SerializeField] private DialogueChoice[] choices = System.Array.Empty<DialogueChoice>();
        [SerializeField] private string continueLabel = "Continue";
        [Header("NPC typing")]
        [SerializeField, Min(1f)] private float charactersPerSecond = 45f;
        [Tooltip("Volume of all NPC voices. Can be adjusted during Play Mode.")]
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.5f;

        private GuestVoice _voice = GuestVoice.None;
        private bool _typing;
        private CancellationTokenSource _replyCancellation;
        private FMOD.Studio.EventInstance _wordVoice;
        private FMOD.Studio.EventDescription _voiceEvent;
        private FMOD.Studio.PARAMETER_ID _characterParameter;
        private FMOD.Studio.PARAMETER_ID _wordParameter;
        private bool _voiceParametersReady;
        private bool _voiceWarningLogged;

        private WorkstationViewController _views;
        private CinemachineBrain _brain;
        private bool _open = true;

        private void OnValidate()
        {
            voiceVolume = Mathf.Clamp01(voiceVolume);
            if (Application.isPlaying && _wordVoice.isValid())
                _wordVoice.setVolume(voiceVolume);
        }

        private void Update()
        {
            var mouse = Mouse.current;
            var hovered = !_typing && mouse != null && Time.timeScale > 0f ? HitChoice(mouse.position.ReadValue()) : -1;
            for (var i = 0; i < choices.Length; i++)
                if (choices[i]?.view != null)
                    choices[i].view.SetHovered(i == hovered);
        }

        private void OnDestroy()
        {
            CancelReply();
            _open = false;
            if (_views != null)
                _views.AllowLookDown(true);
        }

        private void OnDisable() => Hide();

        public void SetSpeaker(GuestDefinition guest, string emotion = null)
        {
            _voice = guest != null ? guest.VoiceFor(emotion) : GuestVoice.None;
        }

        private void Awake()
        {
            _views = FindAnyObjectByType<WorkstationViewController>();
            if (_views != null)
                _brain = _views.GetComponentInChildren<CinemachineBrain>(true);
            Hide();
        }

        public async UniTask Say(string text, CancellationToken token)
        {
            await Choose(new[] { continueLabel }, token, text ?? string.Empty);
        }

        public async UniTask<int> Choose(string[] options, CancellationToken token, string prompt = null)
        {
            CancelReply();
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
            _replyCancellation = cancellation;
            token = cancellation.Token;
            ShowPanel(true);
            if (line != null)
                line.maxVisibleCharacters = int.MaxValue;
            if (line != null && prompt != null)
                line.text = prompt;
            SetActive(line, line != null && !string.IsNullOrEmpty(line.text));
            if (options == null || !System.Array.Exists(options, option => !string.IsNullOrWhiteSpace(option)))
                options = new[] { continueLabel };
            ShowChoices(options);
            panel?.GetComponent<DialoguePanelLayout>()?.RefreshLayout();
            try
            {
                if (line != null && prompt != null)
                    await TypeLine(token);
                return await WaitForChoice(token);
            }
            finally
            {
                if (_replyCancellation == cancellation)
                {
                    _replyCancellation = null;
                    _typing = false;
                    StopWordVoices();
                    if (line != null)
                        line.maxVisibleCharacters = int.MaxValue;
                    HideChoices();
                }
                cancellation.Dispose();
            }
        }

        private async UniTask TypeLine(CancellationToken token)
        {
            // Keep the complete text in TMP so the frame reserves its final size.
            line.ForceMeshUpdate();
            var count = line.textInfo.characterCount;
            var wordStarts = new bool[count];
            for (var i = 0; i < line.textInfo.wordCount; i++)
            {
                var first = line.textInfo.wordInfo[i].firstCharacterIndex;
                if (first >= 0 && first < count)
                    wordStarts[first] = true;
            }
            _typing = count > 0;
            line.maxVisibleCharacters = 0;
            var visible = 0;
            var progress = 0f;
            var clickArmed = Mouse.current == null || !Mouse.current.leftButton.isPressed;
            // Ignore the input frame which opened this reply.
            await UniTask.Yield(token);
            while (visible < count)
            {
                token.ThrowIfCancellationRequested();
                var mouse = Mouse.current;
                if (mouse == null || !mouse.leftButton.isPressed)
                    clickArmed = true;
                if (clickArmed && mouse != null && mouse.leftButton.wasPressedThisFrame && Time.timeScale > 0f)
                    break;
                progress += Time.deltaTime * Mathf.Max(1f, charactersPerSecond);
                var target = Mathf.Min(count, Mathf.FloorToInt(progress));
                var speak = false;
                while (visible < target)
                {
                    speak |= wordStarts[visible];
                    visible++;
                }
                // Coalesce word starts on a slow frame instead of playing a burst of sounds.
                if (speak)
                    PlayWordVoice();
                line.maxVisibleCharacters = visible;
                await UniTask.Yield(token);
            }
            line.maxVisibleCharacters = int.MaxValue;
            _typing = false;
            StopWordVoices();
        }

        private void PlayWordVoice()
        {
            // One voice at a time: even a looping bank event cannot accumulate instances.
            StopWordVoices();
            if (_voice == GuestVoice.None || voiceVolume <= 0f || !FMODUnity.RuntimeManager.IsInitialized)
                return;
            if (!_voiceEvent.isValid())
            {
                _voiceParametersReady = false;
                if (FMODUnity.RuntimeManager.StudioSystem.getEvent("event:/dialog", out _voiceEvent) != FMOD.RESULT.OK)
                    return;
            }
            if (!_voiceParametersReady)
            {
                var foundCharacter = false;
                var foundWord = false;
                _voiceEvent.getParameterDescriptionCount(out var parameterCount);
                for (var i = 0; i < parameterCount; i++)
                {
                    _voiceEvent.getParameterDescriptionByIndex(i, out var parameter);
                    // The bank's parameter names contain trailing spaces.
                    if (((string)parameter.name).Trim() == "персонажи")
                    {
                        _characterParameter = parameter.id;
                        foundCharacter = true;
                    }
                    if (((string)parameter.name).Trim() == "набор слов")
                    {
                        _wordParameter = parameter.id;
                        foundWord = true;
                    }
                }
                _voiceParametersReady = foundCharacter && foundWord;
                if (!_voiceParametersReady)
                {
                    if (!_voiceWarningLogged)
                        Debug.LogWarning("Dialogue voice parameters were not found in event:/dialog.", this);
                    _voiceWarningLogged = true;
                    return;
                }
            }
            if (_voiceEvent.createInstance(out var instance) != FMOD.RESULT.OK)
                return;
            var result = instance.setParameterByID(_characterParameter, (int)_voice, true);
            if (result == FMOD.RESULT.OK)
                result = instance.setParameterByID(_wordParameter, 1f, true);
            instance.setVolume(voiceVolume);
            if (result == FMOD.RESULT.OK)
                result = instance.start();
            if (result == FMOD.RESULT.OK)
                _wordVoice = instance;
            else
                instance.release();
        }

        private void StopWordVoices()
        {
            if (_wordVoice.isValid())
            {
                _wordVoice.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                _wordVoice.release();
            }
            _wordVoice = default;
        }

        private void CancelReply()
        {
            var cancellation = _replyCancellation;
            _replyCancellation = null;
            cancellation?.Cancel();
            _typing = false;
            StopWordVoices();
        }

        private async UniTask<int> WaitForChoice(CancellationToken token)
        {
            await UniTask.Yield(token);
            // A held click from the previous reply must not advance the next one.
            while (!token.IsCancellationRequested && Mouse.current != null && Mouse.current.leftButton.isPressed)
                await UniTask.Yield(token);
            while (!token.IsCancellationRequested)
            {
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame && Time.timeScale > 0f)
                {
                    var index = HitChoice(mouse.position.ReadValue());
                    if (index >= 0)
                        return index;
                }
                await UniTask.Yield(token);
            }
            token.ThrowIfCancellationRequested();
            return 0;
        }

        public void Hide()
        {
            CancelReply();
            if (!_open)
                return;

            ShowPanel(false);
            SetActive(line, false);
            if (line != null)
            {
                line.maxVisibleCharacters = int.MaxValue;
                line.text = string.Empty;
            }
            HideChoices();
        }

        private void ShowPanel(bool show)
        {
            if (panel != null)
                panel.enabled = show;
            if (_views != null)
                _views.AllowLookDown(!show);
        }

        private void ShowChoices(string[] options)
        {
            var count = options != null ? options.Length : 0;
            for (var i = 0; i < choices.Length; i++)
            {
                var choice = choices[i];
                if (choice == null || choice.root == null)
                    continue;

                var visible = i < count && !string.IsNullOrWhiteSpace(options[i]);
                SetActive(choice.root, visible);
                if (choice.label != null)
                {
                    SetActive(choice.label, visible);
                    if (visible)
                        choice.label.text = options[i];
                }
            }
        }

        private void HideChoices()
        {
            if (!_open)
                return;

            for (var i = 0; i < choices.Length; i++)
            {
                var choice = choices[i];
                if (choice == null)
                    continue;

                if (choice.root != null)
                    SetActive(choice.root, false);
                if (choice.label != null)
                    SetActive(choice.label, false);
            }
        }

        private static void SetActive(Component target, bool value)
        {
            if (target != null)
                target.gameObject.SetActive(value);
        }

        private static void SetActive(GameObject target, bool value)
        {
            if (target != null)
                target.SetActive(value);
        }

        private int HitChoice(Vector2 screen)
        {
            var camera = _brain != null ? _brain.OutputCamera : null;
            if (camera == null)
                return -1;

            var ray = camera.ScreenPointToRay(screen);
            var best = -1;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < choices.Length; i++)
            {
                var choice = choices[i];
                if (choice == null || choice.root == null || !choice.root.activeSelf || choice.collider == null)
                    continue;

                RaycastHit hit;
                if (choice.collider.Raycast(ray, out hit, 200f) && hit.distance < bestDistance)
                {
                    best = i;
                    bestDistance = hit.distance;
                }
            }

            return best;
        }
    }
}
