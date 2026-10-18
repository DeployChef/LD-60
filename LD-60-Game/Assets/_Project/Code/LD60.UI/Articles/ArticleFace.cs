using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LD60.UI
{
    public sealed class ArticleFace : MonoBehaviour
    {
        public enum PicturePosition { BelowTitle, AboveTitle, LeftColumn }

        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text body;
        [SerializeField] private Image picture;
        [Header("Layout (Canvas units)")]
        [SerializeField] private Vector2 padding = new(7f, 7f);
        [SerializeField, Min(0f)] private float spacing = 4f;
        [SerializeField] private PicturePosition picturePosition;
        [SerializeField, Range(0.15f, 0.5f)] private float pictureFraction = 0.35f;

        private bool _layingOut;

        public void Apply(string headline, string copy, Sprite sprite)
        {
            if (title != null)
                title.text = headline ?? string.Empty;
            if (body != null)
                body.text = copy ?? string.Empty;
            if (picture != null)
            {
                picture.sprite = sprite;
                picture.gameObject.SetActive(sprite != null);
            }
            RefreshLayout();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled)
                RefreshLayout();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Defer RectTransform changes until Unity has finished deserializing.
            UnityEditor.EditorApplication.delayCall += RefreshInEditor;
        }

        private void RefreshInEditor()
        {
            if (this != null && !UnityEditor.EditorUtility.IsPersistent(this))
                RefreshLayout();
        }
#endif

        [ContextMenu("Refresh Layout")]
        public void RefreshLayout()
        {
            if (_layingOut || title == null || body == null || transform is not RectTransform root)
                return;

            _layingOut = true;
            try
            {
                var width = Mathf.Max(1f, root.rect.width - padding.x * 2f);
                var height = Mathf.Max(1f, root.rect.height - padding.y * 2f);
                var left = padding.x;
                var top = padding.y;
                var hasPicture = picture != null && picture.sprite != null && picture.gameObject.activeSelf;

                if (hasPicture && picturePosition == PicturePosition.LeftColumn)
                {
                    var pictureWidth = width * pictureFraction;
                    Place(picture.rectTransform, left, top, pictureWidth, height);
                    left += pictureWidth + spacing;
                    width = Mathf.Max(1f, width - pictureWidth - spacing);
                }

                // Fit the heading first; reserve space for its actual line count, not a fixed percentage.
                title.fontSize = title.enableAutoSizing ? title.fontSizeMax : title.fontSize;
                Place(title.rectTransform, left, top, width, height * 0.4f);
                title.ForceMeshUpdate(true);
                var titleHeight = Mathf.Min(PreferredHeight(title, width, title.fontSize), height * 0.4f);
                titleHeight = Mathf.Max(1f, titleHeight);
                var pictureHeight = hasPicture && picturePosition != PicturePosition.LeftColumn
                    ? Mathf.Min(width * 0.7f, Mathf.Max(0f, height - titleHeight - spacing * 2f) * pictureFraction)
                    : 0f;
                if (pictureHeight > 0f)
                {
                    // Optional art must leave enough room for the copy at its configured minimum size.
                    var copyHeight = PreferredHeight(body, width, body.enableAutoSizing ? body.fontSizeMin : body.fontSize);
                    pictureHeight = Mathf.Min(pictureHeight, Mathf.Max(0f, height - titleHeight - spacing * 2f - copyHeight));
                }
                if (hasPicture && picturePosition != PicturePosition.LeftColumn && pictureHeight <= 0f)
                    Place(picture.rectTransform, left, top, width, 0f);

                if (pictureHeight > 0f && picturePosition == PicturePosition.AboveTitle)
                {
                    Place(picture.rectTransform, left, top, width, pictureHeight);
                    top += pictureHeight + spacing;
                }

                Place(title.rectTransform, left, top, width, titleHeight);
                top += titleHeight + spacing;

                if (pictureHeight > 0f && picturePosition == PicturePosition.BelowTitle)
                {
                    Place(picture.rectTransform, left, top, width, pictureHeight);
                    top += pictureHeight + spacing;
                }

                Place(body.rectTransform, left, top, width, Mathf.Max(1f, root.rect.height - padding.y - top));
            }
            finally
            {
                _layingOut = false;
            }
        }

        private static float PreferredHeight(TMP_Text text, float width, float size)
        {
            var autoSize = text.enableAutoSizing;
            var previousSize = text.fontSize;
            text.enableAutoSizing = false;
            text.fontSize = size;
            var height = Mathf.Ceil(text.GetPreferredValues(text.text, width, Mathf.Infinity).y);
            text.fontSize = previousSize;
            text.enableAutoSizing = autoSize;
            return height;
        }

        private static void Place(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
