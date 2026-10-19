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
            [NonSerialized] public Tween scaleTween;
            [NonSerialized] public bool isHovered;
        }

        [Header("Power Switch")]
        [SerializeField] private SpriteRenderer powerRenderer;
        [SerializeField] private Collider powerCollider;
        [SerializeField] private Sprite powerOnSprite;
        [SerializeField] private Sprite powerOffSprite;

        [Header("Track Radio Buttons (1, 2, 3)")]
        [SerializeField] private TrackButton[] trackButtons = Array.Empty<TrackButton>();

        [Header("Hover Animation")]
        [SerializeField] private float hoverScaleMultiplier = 1.08f;
        [SerializeField] private float animDuration = 0.12f;

        private Camera _cachedCamera;
        private Vector3 _powerInitialScale = Vector3.one;
        private Tween _powerScaleTween;
        private bool _powerHovered;

        private bool _lastVisualOn;
        private int _lastVisualTrack = -1;

        private void Awake()
        {
            if (powerRenderer != null)
                _powerInitialScale = powerRenderer.transform.localScale;

            if (trackButtons != null)
            {
                for (var i = 0; i < trackButtons.Length; i++)
                {
                    if (trackButtons[i]?.renderer != null)
                        trackButtons[i].initialLocalScale = trackButtons[i].renderer.transform.localScale;
                }
            }
        }

        private void Start()
        {
            SyncVisuals(force: true);
        }

        private void OnEnable()
        {
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

            // Синхронизация состояния (на случай переключения из других источников или хоткеев)
            SyncVisuals(force: false);

            HandleInput();
        }

        private Camera GetActiveCamera()
        {
            if (_cachedCamera == null || !_cachedCamera.gameObject.activeInHierarchy)
                _cachedCamera = Camera.main;

            return _cachedCamera;
        }

        private void HandleInput()
        {
            var camera = GetActiveCamera();
            if (camera == null)
                return;

            var (hasMouse, mousePos, clicked) = ReadPointerInput();
            if (!hasMouse)
            {
                ResetHover();
                return;
            }

            var ray = camera.ScreenPointToRay(mousePos);

            // Проверяем тумблер питания
            var hitPower = false;
            if (powerCollider != null && powerCollider.Raycast(ray, out _, 200f))
            {
                hitPower = true;
                if (clicked)
                    OnPowerClicked();
            }
            SetPowerHovered(hitPower);

            // Проверяем радиокнопки треков
            var hitTrackIndex = -1;
            if (trackButtons != null)
            {
                for (var i = 0; i < trackButtons.Length; i++)
                {
                    var btn = trackButtons[i];
                    if (btn == null || btn.collider == null)
                        continue;

                    if (btn.collider.Raycast(ray, out _, 200f))
                    {
                        hitTrackIndex = i;
                        if (clicked)
                            OnTrackClicked(btn.trackNumber);
                        break;
                    }
                }
            }

            SetTrackHovered(hitTrackIndex);
        }

        private (bool hasMouse, Vector2 mousePos, bool clicked) ReadPointerInput()
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
            MenuAudio.PlayButtonClick();
            var nextOn = !LoudspeakerAudio.IsOn;
            LoudspeakerAudio.SetOn(nextOn);
            SyncVisuals(force: true);
        }

        public void OnTrackClicked(int trackNumber)
        {
            MenuAudio.PlayButtonClick();
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
            if (hovered)
                MenuAudio.PlayButtonHover();

            if (powerRenderer != null)
            {
                _powerScaleTween?.Kill();
                var target = hovered ? _powerInitialScale * hoverScaleMultiplier : _powerInitialScale;
                _powerScaleTween = powerRenderer.transform.DOScale(target, animDuration)
                    .SetEase(Ease.OutQuad)
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
                if (isHovered)
                    MenuAudio.PlayButtonHover();

                btn.scaleTween?.Kill();
                var target = isHovered ? btn.initialLocalScale * hoverScaleMultiplier : btn.initialLocalScale;
                btn.scaleTween = btn.renderer.transform.DOScale(target, animDuration)
                    .SetEase(Ease.OutQuad)
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
            if (trackButtons != null)
            {
                for (var i = 0; i < trackButtons.Length; i++)
                    trackButtons[i]?.scaleTween?.Kill();
            }
        }
    }
}
