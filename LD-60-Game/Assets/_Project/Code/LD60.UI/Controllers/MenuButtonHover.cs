using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LD60.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class MenuButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Color hoverTextColor = new Color32(255, 255, 200, 255);
        [SerializeField, Min(0f)] private float duration = 0.12f;

        private Button _button;
        private TMP_Text[] _labels;
        private Color[] _normalColors;
        private Vector3 _normalRotation;
        private Sequence _animation;
        private bool _hovered;
        private bool _active;
        private Image _background;
        private Sprite _normalSprite;
        private SpriteState _normalSpriteState;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_button != null)
                return;

            _button = GetComponent<Button>();
            _background = _button.targetGraphic as Image;
            if (_background != null)
                _normalSprite = _background.sprite;
            _normalSpriteState = _button.spriteState;
            _normalRotation = transform.localEulerAngles;
            _normalRotation.z = Mathf.DeltaAngle(0f, _normalRotation.z);
            _labels = GetComponentsInChildren<TMP_Text>(true);
            _normalColors = new Color[_labels.Length];
            for (var i = 0; i < _labels.Length; i++)
                _normalColors[i] = _labels[i].color;
        }

        public void SetActiveState(bool active)
        {
            Initialize();
            _active = active;
            var state = _normalSpriteState;
            if (active)
                state.selectedSprite = state.highlightedSprite;
            if (_background != null)
                _background.sprite = active && state.highlightedSprite != null
                    ? state.highlightedSprite : _normalSprite;
            _button.spriteState = state;
            Animate();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isActiveAndEnabled && _button.IsInteractable())
                SetHovered(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (isActiveAndEnabled)
                SetHovered(false);
        }

        private void LateUpdate()
        {
            if (_hovered && !_button.IsInteractable())
                SetHovered(false);
        }

        private void SetHovered(bool hovered)
        {
            _hovered = hovered;
            Animate();
        }

        private void Animate()
        {
            var highlighted = (_hovered || _active) && _button.IsInteractable();
            _animation?.Kill();
            var rotation = _normalRotation;
            if (highlighted)
                rotation.z = -rotation.z;

            _animation = DOTween.Sequence().SetUpdate(true);
            _animation.Join(transform.DOLocalRotate(rotation, duration).SetEase(Ease.OutQuad));
            for (var i = 0; i < _labels.Length; i++)
            {
                var label = _labels[i];
                var color = highlighted ? hoverTextColor : _normalColors[i];
                _animation.Join(DOTween.To(() => label.color, value => label.color = value,
                    color, duration).SetEase(Ease.OutQuad));
            }
        }

        private void OnDisable()
        {
            _animation?.Kill();
            _animation = null;
            _hovered = false;
            _active = false;
            if (_labels == null)
                return;

            if (_background != null)
                _background.sprite = _normalSprite;
            _button.spriteState = _normalSpriteState;
            transform.localEulerAngles = _normalRotation;
            for (var i = 0; i < _labels.Length; i++)
                if (_labels[i] != null)
                    _labels[i].color = _normalColors[i];
        }
    }
}
