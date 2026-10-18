using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LD60.UI
{
    public sealed class ArticleFace : MonoBehaviour
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text body;
        [SerializeField] private Image picture;

        public void Apply(string headline, string copy, Sprite sprite)
        {
            if (title != null)
                title.text = headline ?? string.Empty;
            if (body != null)
                body.text = copy ?? string.Empty;
            if (picture != null && sprite != null)
                picture.sprite = sprite;
        }
    }
}
