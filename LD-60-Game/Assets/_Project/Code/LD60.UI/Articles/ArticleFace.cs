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
        }

    }
}
