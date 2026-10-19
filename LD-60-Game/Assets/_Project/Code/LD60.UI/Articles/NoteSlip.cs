using TMPro;
using UnityEngine;

namespace LD60.UI
{
    public sealed class NoteSlip : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;

        private void Awake()
        {
            RefreshCollider();
        }

        public void Bind(string note)
        {
            if (text != null)
                text.text = note ?? string.Empty;
            RefreshCollider();
        }

        [ContextMenu("Fit Paper Collider")]
        public void RefreshCollider()
        {
            var page = transform.Find("Page");
            if (page == null || !page.TryGetComponent<SpriteRenderer>(out var renderer)
                || renderer.sprite == null || !page.TryGetComponent<BoxCollider>(out var box))
                return;

            var bounds = renderer.localBounds;
            box.center = new Vector3(bounds.center.x, bounds.center.y, box.center.z);
            box.size = new Vector3(bounds.size.x, bounds.size.y, Mathf.Max(0.08f, box.size.z));
        }
    }
}
