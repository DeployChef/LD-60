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

        private WorkstationViewController _views;
        private CinemachineBrain _brain;
        private bool _open = true;

        private void Update()
        {
            var mouse = Mouse.current;
            var hovered = mouse != null && Time.timeScale > 0f ? HitChoice(mouse.position.ReadValue()) : -1;
            for (var i = 0; i < choices.Length; i++)
                if (choices[i]?.view != null)
                    choices[i].view.SetHovered(i == hovered);
        }

        private void OnDestroy()
        {
            _open = false;
            if (_views != null)
                _views.AllowLookDown(true);
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
            ShowPanel(true);
            SetActive(line, true);
            if (line != null)
                line.text = text ?? string.Empty;

            HideChoices();
            await UniTask.Yield(token);
            while (!token.IsCancellationRequested)
            {
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame && Time.timeScale > 0f)
                    return;
                await UniTask.Yield(token);
            }

            token.ThrowIfCancellationRequested();
        }

        public async UniTask<int> Choose(string[] options, CancellationToken token, string prompt = null)
        {
            ShowPanel(true);
            if (line != null && prompt != null)
                line.text = prompt;
            SetActive(line, line != null && !string.IsNullOrEmpty(line.text));
            ShowChoices(options);
            try
            {
                await UniTask.Yield(token);
                // The click that chose the previous reply must not also choose this one.
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
            finally
            {
                HideChoices();
            }
        }

        public void Hide()
        {
            if (!_open)
                return;

            ShowPanel(false);
            SetActive(line, false);
            if (line != null)
                line.text = string.Empty;
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

                var visible = i < count;
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
