using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace LD60.UI
{
    public sealed class EditionPreview : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer sheet;
        [SerializeField] private Rect captureRect = new(-4.55f, -5.01f, 9.1f, 8.74f);
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private CanvasGroup content;
        [SerializeField] private CanvasGroup blackout;
        [SerializeField, Min(0.05f)] private float blackoutSeconds = 1f;
        [SerializeField, Min(0.05f)] private float revealSeconds = 1f;
        [SerializeField] private UnityEngine.UI.RawImage newspaper;
        [SerializeField] private UnityEngine.UI.AspectRatioFitter aspect;
        [SerializeField] private UnityEngine.UI.Button continueButton;

        private RenderTexture _snapshot;
        private Tween _fade;
        private Action _continue;
        private float _previousTimeScale;
        private bool _closing;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            canvas.enabled = false;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            continueButton.onClick.AddListener(Continue);
        }

        public bool Show(NewspaperDesk board, Action onContinue)
        {
            if (!Application.isPlaying || IsOpen || sheet == null)
                return false;

            _snapshot = Capture(board);
            newspaper.texture = _snapshot;
            aspect.aspectRatio = (float)_snapshot.width / _snapshot.height;
            _continue = onContinue;
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            IsOpen = true;
            _closing = false;
            canvas.enabled = true;
            group.blocksRaycasts = true;
            group.interactable = false;
            group.alpha = 1f;
            content.alpha = 0f;
            blackout.alpha = 0f;
            _fade = DOTween.Sequence()
                .SetUpdate(true)
                .Append(DOTween.To(() => blackout.alpha, value => blackout.alpha = value, 1f, blackoutSeconds).SetEase(Ease.Linear))
                .AppendCallback(() => content.alpha = 1f)
                .Append(DOTween.To(() => blackout.alpha, value => blackout.alpha = value, 0f, revealSeconds).SetEase(Ease.Linear))
                .OnComplete(() => group.interactable = true);
            return true;
        }

        public void Continue()
        {
            if (!IsOpen || _closing || !group.interactable)
                return;

            _closing = true;
            group.interactable = false;
            _fade?.Kill();
            _fade = DOTween.Sequence()
                .SetUpdate(true)
                .Append(DOTween.To(() => blackout.alpha, value => blackout.alpha = value, 1f, blackoutSeconds).SetEase(Ease.Linear))
                .AppendCallback(() =>
                {
                    content.alpha = 0f;
                    var next = _continue;
                    _continue = null;
                    next?.Invoke();
                })
                .Append(DOTween.To(() => blackout.alpha, value => blackout.alpha = value, 0f, revealSeconds).SetEase(Ease.Linear))
                .OnComplete(Close);
        }

        private RenderTexture Capture(NewspaperDesk board)
        {
            // Isolate the printed contents for this synchronous render only.
            // No scene objects or physics layers stay changed after the capture.
            const int captureLayer = 31;
            var layers = new Dictionary<GameObject, int>();
            var renderers = new List<Renderer>(sheet.transform.parent.GetComponentsInChildren<Renderer>());
            foreach (var mark in board.GetComponentsInChildren<StampMark>())
                renderers.AddRange(mark.GetComponentsInChildren<Renderer>());

            var cameraObject = new GameObject("EditionCaptureCamera");
            var previousTarget = RenderTexture.active;
            RenderTexture texture = null;
            try
            {
                foreach (var renderer in renderers)
                {
                    if (!renderer.enabled || renderer.name == "Slot" || renderer.name == "Shadow")
                        continue;
                    var target = renderer.gameObject;
                    if (!layers.ContainsKey(target))
                        layers.Add(target, target.layer);
                    target.layer = captureLayer;
                }

                // Article text and illustrations use world-space uGUI canvases,
                // whose CanvasRenderers are not included in Renderer queries.
                var uiObjects = new List<GameObject>();
                foreach (var uiCanvas in sheet.transform.parent.GetComponentsInChildren<Canvas>())
                    uiObjects.Add(uiCanvas.gameObject);
                foreach (var graphic in sheet.transform.parent.GetComponentsInChildren<UnityEngine.UI.Graphic>())
                    uiObjects.Add(graphic.gameObject);
                foreach (var target in uiObjects)
                {
                    if (!layers.ContainsKey(target))
                        layers.Add(target, target.layer);
                    target.layer = captureLayer;
                }
                Canvas.ForceUpdateCanvases();

                var center = sheet.transform.TransformPoint(captureRect.center);
                var width = sheet.transform.TransformVector(Vector3.right * captureRect.width).magnitude;
                var height = sheet.transform.TransformVector(Vector3.up * captureRect.height).magnitude;
                var ratio = width / Mathf.Max(0.01f, height);
                texture = new RenderTexture(Mathf.Clamp(Mathf.RoundToInt(2048 * ratio), 128, 4096), 2048, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Printed edition",
                    filterMode = FilterMode.Bilinear
                };
                texture.Create();
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.transform.SetPositionAndRotation(center - sheet.transform.forward * 10f, sheet.transform.rotation);
                camera.orthographic = true;
                camera.orthographicSize = height * 0.5f;
                camera.aspect = ratio;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 30f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.cullingMask = 1 << captureLayer;
                camera.targetTexture = texture;
                camera.Render();
                camera.targetTexture = null;
                return texture;
            }
            catch
            {
                if (texture != null)
                {
                    texture.Release();
                    Destroy(texture);
                }
                throw;
            }
            finally
            {
                RenderTexture.active = previousTarget;
                foreach (var entry in layers)
                    if (entry.Key != null)
                        entry.Key.layer = entry.Value;
                Destroy(cameraObject);
            }
        }

        private void Close()
        {
            _fade?.Kill();
            _fade = null;
            if (canvas != null)
                canvas.enabled = false;
            if (group != null)
            {
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
            if (content != null)
                content.alpha = 0f;
            if (blackout != null)
                blackout.alpha = 0f;
            if (IsOpen)
                Time.timeScale = _previousTimeScale;
            IsOpen = false;
            _continue = null;
            if (newspaper != null)
                newspaper.texture = null;
            if (_snapshot != null)
            {
                _snapshot.Release();
                Destroy(_snapshot);
                _snapshot = null;
            }
        }

        private void OnDisable()
        {
            Close();
        }

        private void OnDestroy()
        {
            if (continueButton != null)
                continueButton.onClick.RemoveListener(Continue);
        }
    }
}
