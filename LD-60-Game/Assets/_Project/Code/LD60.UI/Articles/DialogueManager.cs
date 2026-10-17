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

        private CinemachineBrain _brain;
        private bool _open = true;

        private void OnDestroy()
        {
            _open = false;
        }

        private void Awake()
        {
            var views = FindAnyObjectByType<WorkstationViewController>();
            if (views != null)
                _brain = views.GetComponentInChildren<CinemachineBrain>(true);
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

        public async UniTask<int> Choose(string[] options, CancellationToken token)
        {
            ShowPanel(true);
            SetActive(line, false);
            ShowChoices(options);
            await UniTask.Yield(token);
            try
            {
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
            HideChoices();
        }

        private void ShowPanel(bool show)
        {
            if (panel != null)
                panel.enabled = show;
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
