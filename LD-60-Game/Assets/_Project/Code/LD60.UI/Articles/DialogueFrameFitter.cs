using TMPro;
using UnityEngine;

namespace LD60.UI
{
    [ExecuteAlways, RequireComponent(typeof(SpriteRenderer))]
    public sealed class DialogueFrameFitter : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;
        [SerializeField] private Vector2 padding = new(0.41f, 0.24f);
        [SerializeField, Min(0f)] private float minimumHeight = 0.85f;

        private SpriteRenderer _frame;
        private Vector2 _lastTextSize;
        private bool _dirty = true;
        private bool _fitting;

        private void OnEnable()
        {
            _frame = GetComponent<SpriteRenderer>();
            if (text == null)
                text = GetComponentInParent<TMP_Text>();
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            Fit();
        }

        private void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        }

        private void OnValidate()
        {
            padding = Vector2.Max(Vector2.zero, padding);
            _dirty = true;
        }

        private void LateUpdate()
        {
            if (text != null && (_dirty || text.rectTransform.rect.size != _lastTextSize))
                Fit();
        }

        private void OnTextChanged(Object changed)
        {
            // TMP raises this after generating its mesh. Resize outside that callback,
            // otherwise the frame moves while the mesh still uses the previous rectangle.
            if (changed == text && !_fitting)
                _dirty = true;
        }

        [ContextMenu("Fit Frame")]
        public void Fit()
        {
            if (_fitting || text == null || transform.parent != text.transform)
                return;
            _fitting = true;
            try
            {
                if (_frame == null)
                    _frame = GetComponent<SpriteRenderer>();
                var rect = text.rectTransform;
                var height = Mathf.Max(0.01f, text.GetPreferredValues(text.text, rect.rect.width, Mathf.Infinity).y);
                if (Mathf.Abs(rect.rect.height - height) > 0.001f)
                {
                    // Preserve the top edge while the content grows downwards.
                    var position = rect.localPosition;
                    position.y += (rect.rect.height - height) * (1f - rect.pivot.y);
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                    rect.localPosition = position;
                }

                var size = rect.rect.size + padding * 2f;
                size.y = Mathf.Max(minimumHeight, size.y);
                var scale = transform.localScale;
                _frame.size = new Vector2(size.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                    size.y / Mathf.Max(0.001f, Mathf.Abs(scale.y)));
                var center = rect.rect.center;
                transform.localPosition = new Vector3(center.x, center.y, transform.localPosition.z);
                text.ForceMeshUpdate(true, true);
                _lastTextSize = rect.rect.size;
                _dirty = false;
            }
            finally
            {
                _fitting = false;
            }
        }
    }
}
