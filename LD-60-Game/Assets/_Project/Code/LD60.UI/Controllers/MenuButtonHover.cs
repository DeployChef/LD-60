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

        private void Awake()
        {
            _button = GetComponent<Button>();
            _normalRotation = transform.localEulerAngles;
            _normalRotation.z = Mathf.DeltaAngle(0f, _normalRotation.z);
            _labels = GetComponentsInChildren<TMP_Text>(true);
            _normalColors = new Color[_labels.Length];
            for (var i = 0; i < _labels.Length; i++)
                _normalColors[i] = _labels[i].color;
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
            _animation?.Kill();
            var rotation = _normalRotation;
            if (hovered)
                rotation.z = -rotation.z;

            _animation = DOTween.Sequence().SetUpdate(true);
            _animation.Join(transform.DOLocalRotate(rotation, duration).SetEase(Ease.OutQuad));
            for (var i = 0; i < _labels.Length; i++)
            {
                var label = _labels[i];
                var color = hovered ? hoverTextColor : _normalColors[i];
                _animation.Join(DOTween.To(() => label.color, value => label.color = value,
                    color, duration).SetEase(Ease.OutQuad));
            }
        }

        private void OnDisable()
        {
            _animation?.Kill();
            _animation = null;
            _hovered = false;
            if (_labels == null)
                return;

            transform.localEulerAngles = _normalRotation;
            for (var i = 0; i < _labels.Length; i++)
                if (_labels[i] != null)
                    _labels[i].color = _normalColors[i];
        }
    }
}
