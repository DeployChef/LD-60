using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LD60.Gameplay.Story;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LD60.UI
{
    public sealed class SequencePlayer : MonoBehaviour, ISequencePlayer
    {
        private const float WalkSeconds = 0.75f;

        [SerializeField] private Transform character;
        [SerializeField] private SpriteRenderer dialog;
        [SerializeField] private TMP_Text line;

        private WorkstationViewController _views;
        private CinemachineBrain _brain;
        private CancellationTokenSource _run;
        private readonly List<ChoiceButton> _choices = new List<ChoiceButton>();
        private Vector3 _home;
        private Vector3 _away;
        private bool _placed;

        private void Awake()
        {
            if (character == null)
            {
                var body = GameObject.Find("Character");
                if (body != null)
                    character = body.transform;
            }

            if (dialog == null)
            {
                var panel = GameObject.Find("Dialog");
                if (panel != null)
                    dialog = panel.GetComponent<SpriteRenderer>();
            }

            _views = FindAnyObjectByType<WorkstationViewController>();
            if (_views != null)
                _brain = _views.GetComponentInChildren<CinemachineBrain>(true);
            EnsureLine();
            Park();
        }

        public void Cancel()
        {
            if (_run == null)
                return;

            _run.Cancel();
            _run.Dispose();
            _run = null;
            HideChoices();
        }

        public void LookAtRoom()
        {
            if (_views != null)
                _views.ShowTable(false);
        }

        public UniTask Enter()
        {
            ShowLine(false);
            return Slide(_home, Token);
        }

        public async UniTask Say(string text)
        {
            ShowLine(true, text);
            await UniTask.Yield(Token);
            while (!Token.IsCancellationRequested)
            {
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame && Time.timeScale > 0f)
                    return;
                await UniTask.Yield(Token);
            }

            Token.ThrowIfCancellationRequested();
        }

        public async UniTask<int> Choose(params string[] options)
        {
            if (line != null)
                line.gameObject.SetActive(false);
            if (dialog != null)
                dialog.enabled = true;
            ShowChoices(options);
            await UniTask.Yield(Token);
            try
            {
                while (!Token.IsCancellationRequested)
                {
                    var mouse = Mouse.current;
                    if (mouse != null && mouse.leftButton.wasPressedThisFrame && Time.timeScale > 0f)
                    {
                        var index = HitChoice(mouse.position.ReadValue());
                        if (index >= 0)
                            return index;
                    }

                    await UniTask.Yield(Token);
                }

                Token.ThrowIfCancellationRequested();
                return 0;
            }
            finally
            {
                HideChoices();
            }
        }

        public UniTask Exit()
        {
            ShowLine(false);
            return Slide(_away, Token);
        }

        private CancellationToken Token
        {
            get
            {
                if (_run == null)
                    _run = new CancellationTokenSource();
                return _run.Token;
            }
        }

        private async UniTask Slide(Vector3 target, CancellationToken token)
        {
            if (character == null)
                return;

            var from = character.localPosition;
            var elapsed = 0f;
            while (elapsed < WalkSeconds)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                character.localPosition = Vector3.Lerp(from, target, Mathf.Clamp01(elapsed / WalkSeconds));
                await UniTask.Yield(token);
            }

            character.localPosition = target;
        }

        private void Park()
        {
            if (_placed || character == null)
                return;

            _home = character.localPosition;
            _away = _home + new Vector3(-14f, 0f, 0f);
            character.localPosition = _away;
            ShowLine(false);
            _placed = true;
        }

        private void ShowLine(bool show, string text = null)
        {
            if (dialog != null)
                dialog.enabled = show;
            if (line == null)
                return;

            line.gameObject.SetActive(show);
            if (show)
                line.text = text ?? string.Empty;
            if (show)
                HideChoices();
        }

        private void ShowChoices(string[] options)
        {
            var count = options != null ? options.Length : 0;
            var origin = dialog != null ? dialog.transform.localPosition : Vector3.zero;
            var span = Mathf.Max(0, count - 1) * 1.15f;
            for (var i = 0; i < count; i++)
            {
                var choice = EnsureChoice(i);
                choice.Root.SetActive(true);
                choice.Label.text = options[i];
                choice.Root.transform.localPosition = origin + new Vector3(0f, span * 0.5f - i * 1.15f, -0.12f);
            }

            for (var i = count; i < _choices.Count; i++)
                _choices[i].Root.SetActive(false);
        }

        private void HideChoices()
        {
            for (var i = 0; i < _choices.Count; i++)
                _choices[i].Root.SetActive(false);
        }

        private int HitChoice(Vector2 screen)
        {
            var camera = _brain != null ? _brain.OutputCamera : null;
            if (camera == null)
                return -1;

            var ray = camera.ScreenPointToRay(screen);
            var best = -1;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < _choices.Count; i++)
            {
                var choice = _choices[i];
                if (!choice.Root.activeSelf || choice.Collider == null)
                    continue;

                RaycastHit hit;
                if (choice.Collider.Raycast(ray, out hit, 200f) && hit.distance < bestDistance)
                {
                    best = i;
                    bestDistance = hit.distance;
                }
            }

            return best;
        }

        private ChoiceButton EnsureChoice(int index)
        {
            while (_choices.Count <= index)
            {
                var go = new GameObject("Choice " + _choices.Count);
                var parent = dialog != null ? dialog.transform.parent : transform;
                go.transform.SetParent(parent, false);
                go.transform.localRotation = Quaternion.identity;
                var label = go.AddComponent<TextMeshPro>();
                label.alignment = TextAlignmentOptions.Center;
                label.color = new Color(0.99f, 0.9f, 0.62f, 1f);
                label.enableAutoSizing = true;
                label.fontSizeMin = 0.55f;
                label.fontSizeMax = 1.2f;
                label.rectTransform.sizeDelta = new Vector2(4.4f, 1f);
                if (TMP_Settings.defaultFontAsset != null)
                    label.font = TMP_Settings.defaultFontAsset;
                if (dialog != null)
                {
                    var renderer = label.GetComponent<Renderer>();
                    renderer.sortingLayerID = dialog.sortingLayerID;
                    renderer.sortingOrder = dialog.sortingOrder + 3;
                }

                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(4.4f, 1.15f, 2f);
                _choices.Add(new ChoiceButton { Root = go, Label = label, Collider = box });
            }

            return _choices[index];
        }

        private sealed class ChoiceButton
        {
            public GameObject Root;
            public TMP_Text Label;
            public Collider Collider;
        }

        private void EnsureLine()
        {
            if (line != null || dialog == null)
                return;

            var go = new GameObject("Line");
            go.transform.SetParent(dialog.transform.parent, false);
            go.transform.localPosition = dialog.transform.localPosition + new Vector3(0f, 0f, -0.08f);
            go.transform.localRotation = Quaternion.identity;
            line = go.AddComponent<TextMeshPro>();
            line.alignment = TextAlignmentOptions.Center;
            line.color = new Color(0.96f, 0.94f, 0.9f, 1f);
            line.enableAutoSizing = true;
            line.fontSizeMin = 0.6f;
            line.fontSizeMax = 1.6f;
            line.rectTransform.sizeDelta = new Vector2(4.2f, 2.4f);
            var renderer = line.GetComponent<Renderer>();
            renderer.sortingLayerID = dialog.sortingLayerID;
            renderer.sortingOrder = dialog.sortingOrder + 2;
            if (TMP_Settings.defaultFontAsset != null)
                line.font = TMP_Settings.defaultFontAsset;
            go.SetActive(false);
        }
    }
}
