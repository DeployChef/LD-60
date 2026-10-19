using System;
using DG.Tweening;
using LD60.UI.Audio;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LD60.Main.Audio
{
    [DisallowMultipleComponent]
    public sealed class GramophoneController : MonoBehaviour
    {
        [Serializable]
        public sealed class TrackButton
        {
            [Range(1, 3)] public int trackNumber = 1;
            public SpriteRenderer renderer;
            public Collider collider;
            public Sprite activeSprite;
            public Sprite inactiveSprite;

            [NonSerialized] public Vector3 initialLocalScale = Vector3.one;
            [NonSerialized] public Vector3 initialLocalPos = Vector3.zero;
            [NonSerialized] public Tween scaleTween;
            [NonSerialized] public Tween posTween;
            [NonSerialized] public Tween colorTween;
            [NonSerialized] public bool isHovered;
        }

        [Header("Power Switch")]
        [SerializeField] private SpriteRenderer powerRenderer;
        [SerializeField] private Collider powerCollider;
        [SerializeField] private Sprite powerOnSprite;
        [SerializeField] private Sprite powerOffSprite;

        [Header("Track Radio Buttons (1, 2, 3)")]
        [SerializeField] private TrackButton[] trackButtons = Array.Empty<TrackButton>();

        [Header("Hover Visuals")]
        [SerializeField] private float hoverScaleMultiplier = 1.15f;
        [SerializeField] private float hoverZOffset = -0.05f;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color hoverColor = new Color(1f, 1f, 0.72f, 1f);
        [SerializeField] private float animDuration = 0.12f;

        private Camera _renderCamera;
        private Vector3 _powerInitialScale = Vector3.one;
        private Vector3 _powerInitialPos = Vector3.zero;
        private Tween _powerScaleTween;
        private Tween _powerPosTween;
        private Tween _powerColorTween;
        private bool _powerHovered;

        private bool _lastVisualOn;
        private int _lastVisualTrack = -1;

        private void Awake()
        {
            if (powerRenderer != null)
            {
                _powerInitialScale = powerRenderer.transform.localScale;
                _powerInitialPos = powerRenderer.transform.localPosition;
            }

            if (trackButtons != null)
            {
                for (var i = 0; i < trackButtons.Length; i++)
                {
                    var btn = trackButtons[i];
                    if (btn?.renderer != null)
                    {
                        btn.initialLocalScale = btn.renderer.transform.localScale;
                        btn.initialLocalPos = btn.renderer.transform.localPosition;
                    }
                }
            }
        }

        private void Start()
        {
            FindRenderCamera();
            SyncVisuals(force: true);
        }

        private void OnEnable()
        {
            FindRenderCamera();
            SyncVisuals(force: true);
        }

        private void OnDisable()
        {
            ResetHover();
        }

        private void OnDestroy()
        {
            KillAllTweens();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f)
                return;

            SyncVisuals(force: false);
            HandlePointerInteraction();
        }

        private Camera GetRenderCamera()
        {
            if (_renderCamera != null && _renderCamera.isActiveAndEnabled)
                return _renderCamera;

            FindRenderCamera();
            return _renderCamera;
        }

        private void FindRenderCamera()
        {
            var workstationCam = GameObject.Find("WorkstationCamera");
            if (workstationCam != null)
            {
                var cam = workstationCam.GetComponent<Camera>();
                if (cam != null && cam.isActiveAndEnabled)
                {
                    _renderCamera = cam;
                    return;
                }
            }

            var all = Camera.allCameras;
            Camera highestDepthCam = null;
            for (var i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c != null && c.isActiveAndEnabled)
                {
                    if (highestDepthCam == null || c.depth > highestDepthCam.depth)
                        highestDepthCam = c;
                }
            }

            if (highestDepthCam != null)
            {
                _renderCamera = highestDepthCam;
                return;
            }

            _renderCamera = Camera.main;
        }

        private void HandlePointerInteraction()
        {
            var camera = GetRenderCamera();
            if (camera == null)
                return;

            var (hasMouse, mousePos, clicked) = ReadMouseState();
            if (!hasMouse)
            {
                ResetHover();
                return;
            }

            // 1. Точная проверка попадания мыши в 2D-плоскость спрайта
            var isOverPower = IsPointerOverRenderer(camera, mousePos, powerRenderer, powerCollider);

            var overTrackIndex = -1;
            if (trackButtons != null)
            {
                for (var i = 0; i < trackButtons.Length; i++)
                {
                    var btn = trackButtons[i];
                    if (btn?.renderer == null)
                        continue;

                    if (IsPointerOverRenderer(camera, mousePos, btn.renderer, btn.collider))
                    {
                        overTrackIndex = i;
                        break;
                    }
                }
            }

            // 2. Обработка клика
            if (clicked)
            {
                if (isOverPower)
                {
                    OnPowerClicked();
                }
                else if (overTrackIndex >= 0)
                {
                    OnTrackClicked(trackButtons[overTrackIndex].trackNumber);
                }
            }

            // 3. Обновление ховер-эффекта
            SetPowerHovered(isOverPower);
            SetTrackHovered(overTrackIndex);
        }

        private bool IsPointerOverRenderer(Camera camera, Vector2 screenPos, SpriteRenderer sr, Collider col)
        {
            if (sr == null || sr.sprite == null || !sr.gameObject.activeInHierarchy)
                return false;

            // Пересечение луча камеры с плоскостью спрайта
            var spriteTransform = sr.transform;
            var plane = new Plane(-spriteTransform.forward, spriteTransform.position);
            var ray = camera.ScreenPointToRay(screenPos);

            if (plane.Raycast(ray, out var distance))
            {
                var hitPoint = ray.GetPoint(distance);
                var localPoint = spriteTransform.InverseTransformPoint(hitPoint);

                var sprite = sr.sprite;
                var ppu = sprite.pixelsPerUnit;
                var rect = sprite.rect;
                var pivot = sprite.pivot;

                var minX = -pivot.x / ppu;
                var maxX = (rect.width - pivot.x) / ppu;
                var minY = -pivot.y / ppu;
                var maxY = (rect.height - pivot.y) / ppu;

                const float padding = 0.08f;
                var padX = (maxX - minX) * padding;
                var padY = (maxY - minY) * padding;

                if (localPoint.x >= minX - padX && localPoint.x <= maxX + padX &&
                    localPoint.y >= minY - padY && localPoint.y <= maxY + padY)
                {
                    return true;
                }
            }

            // Запасной фоллбэк: проверка через коллайдер
            if (col != null && col.Raycast(ray, out _, 100f))
                return true;

            return false;
        }

        private (bool hasMouse, Vector2 mousePos, bool clicked) ReadMouseState()
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var pos = mouse.position.ReadValue();
                var clicked = mouse.leftButton.wasPressedThisFrame;
                return (true, pos, clicked);
            }

            if (Input.mousePresent)
            {
                return (true, Input.mousePosition, Input.GetMouseButtonDown(0));
            }

            return (false, Vector2.zero, false);
        }

        public void OnPowerClicked()
        {
            var nextOn = !LoudspeakerAudio.IsOn;
            Debug.Log($"[Gramophone] Power clicked -> {(nextOn ? "ON" : "OFF")}");
            SoundService.PlayButtonClick();

            if (powerRenderer != null)
            {
                powerRenderer.transform.DOKill(true);
                powerRenderer.transform.DOPunchScale(_powerInitialScale * -0.2f, 0.15f, 8, 1).SetUpdate(true);
            }

            LoudspeakerAudio.SetOn(nextOn);
            SyncVisuals(force: true);
        }

        public void OnTrackClicked(int trackNumber)
        {
            Debug.Log($"[Gramophone] Track clicked -> Track {trackNumber}");
            SoundService.PlayButtonClick();

            var index = trackNumber - 1;
            if (trackButtons != null && index >= 0 && index < trackButtons.Length)
            {
                var btn = trackButtons[index];
                if (btn?.renderer != null)
                {
                    btn.renderer.transform.DOKill(true);
                    btn.renderer.transform.DOPunchScale(btn.initialLocalScale * -0.2f, 0.15f, 8, 1).SetUpdate(true);
                }
            }

            LoudspeakerAudio.SetTrack(trackNumber);
            SyncVisuals(force: true);
        }

        private void SyncVisuals(bool force)
        {
            var isOn = LoudspeakerAudio.IsOn;
            var track = LoudspeakerAudio.Track;

            if (force || _lastVisualOn != isOn)
            {
                _lastVisualOn = isOn;
                if (powerRenderer != null)
                    powerRenderer.sprite = isOn ? powerOnSprite : powerOffSprite;
            }

            if (force || _lastVisualTrack != track)
            {
                _lastVisualTrack = track;
                if (trackButtons != null)
                {
                    for (var i = 0; i < trackButtons.Length; i++)
                    {
                        var btn = trackButtons[i];
                        if (btn?.renderer == null)
                            continue;

                        var isSelected = btn.trackNumber == track;
                        btn.renderer.sprite = isSelected ? btn.activeSprite : btn.inactiveSprite;
                    }
                }
            }
        }

        private void SetPowerHovered(bool hovered)
        {
            if (_powerHovered == hovered)
                return;

            _powerHovered = hovered;

            if (powerRenderer != null)
            {
                _powerScaleTween?.Kill();
                _powerPosTween?.Kill();
                _powerColorTween?.Kill();

                var targetScale = hovered ? _powerInitialScale * hoverScaleMultiplier : _powerInitialScale;
                var targetPos = hovered ? _powerInitialPos + new Vector3(0f, 0f, hoverZOffset) : _powerInitialPos;
                var targetColor = hovered ? hoverColor : normalColor;

                _powerScaleTween = powerRenderer.transform.DOScale(targetScale, animDuration)
                    .SetEase(hovered ? Ease.OutBack : Ease.OutQuad)
                    .SetUpdate(true);

                _powerPosTween = powerRenderer.transform.DOLocalMove(targetPos, animDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);

                _powerColorTween = DOTween.To(() => powerRenderer.color, c => powerRenderer.color = c, targetColor, animDuration)
                    .SetUpdate(true);
            }
        }

        private void SetTrackHovered(int hoveredIndex)
        {
            if (trackButtons == null)
                return;

            for (var i = 0; i < trackButtons.Length; i++)
            {
                var btn = trackButtons[i];
                if (btn?.renderer == null)
                    continue;

                var isHovered = i == hoveredIndex;
                if (btn.isHovered == isHovered)
                    continue;

                btn.isHovered = isHovered;

                btn.scaleTween?.Kill();
                btn.posTween?.Kill();
                btn.colorTween?.Kill();

                var targetScale = isHovered ? btn.initialLocalScale * hoverScaleMultiplier : btn.initialLocalScale;
                var targetPos = isHovered ? btn.initialLocalPos + new Vector3(0f, 0f, hoverZOffset) : btn.initialLocalPos;
                var targetColor = isHovered ? hoverColor : normalColor;

                btn.scaleTween = btn.renderer.transform.DOScale(targetScale, animDuration)
                    .SetEase(isHovered ? Ease.OutBack : Ease.OutQuad)
                    .SetUpdate(true);

                btn.posTween = btn.renderer.transform.DOLocalMove(targetPos, animDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);

                btn.colorTween = DOTween.To(() => btn.renderer.color, c => btn.renderer.color = c, targetColor, animDuration)
                    .SetUpdate(true);
            }
        }

        private void ResetHover()
        {
            SetPowerHovered(false);
            SetTrackHovered(-1);
        }

        private void KillAllTweens()
        {
            _powerScaleTween?.Kill();
            _powerPosTween?.Kill();
            _powerColorTween?.Kill();

            if (trackButtons != null)
            {
                for (var i = 0; i < trackButtons.Length; i++)
                {
                    var btn = trackButtons[i];
                    if (btn == null)
                        continue;

                    btn.scaleTween?.Kill();
                    btn.posTween?.Kill();
                    btn.colorTween?.Kill();
                }
            }
        }
    }
}
