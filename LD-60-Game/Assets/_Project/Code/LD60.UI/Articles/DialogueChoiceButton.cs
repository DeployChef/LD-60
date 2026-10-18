using DG.Tweening;
using TMPro;
using UnityEngine;

namespace LD60.UI
{
    [DisallowMultipleComponent]
    public sealed class DialogueChoiceButton : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer background;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite activeSprite;
        [SerializeField] private Color normalColor = new(0.196f, 0.196f, 0.196f, 1f);
        [SerializeField] private Color hoverColor = new Color32(255, 255, 200, 255);
        [SerializeField] private float tilt = 1f;
        [SerializeField, Min(0f)] private float duration = 0.12f;

        private bool _hovered;
        private Tween _rotation;
        private Tween _color;

        public void SetHovered(bool hovered)
        {
            if (_hovered == hovered)
                return;
            _hovered = hovered;
            _rotation?.Kill();
            _color?.Kill();
            if (background != null)
                background.sprite = hovered ? activeSprite : normalSprite;
            _rotation = transform.DOLocalRotate(new Vector3(0f, 0f, hovered ? -tilt : tilt), duration)
                .SetEase(Ease.OutQuad).SetUpdate(true);
            if (label != null)
                _color = DOTween.To(() => label.color, value => label.color = value,
                    hovered ? hoverColor : normalColor, duration).SetUpdate(true);
        }

        private void OnEnable()
        {
            ResetStyle();
        }

        private void OnDisable()
        {
            ResetStyle();
        }

        private void ResetStyle()
        {
            _rotation?.Kill();
            _color?.Kill();
            _hovered = false;
            transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
            if (background != null)
                background.sprite = normalSprite;
            if (label != null)
                label.color = normalColor;
        }
    }
}
