using TMPro;
using UnityEngine;

namespace LD60.UI
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
    public sealed class DialogueChoiceLayout : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Vector2 padding = new(0.35f, 0.13f);
        [SerializeField] private Vector2 minimumSize = new(1f, 1.02f);

        private SpriteRenderer _background;
        private BoxCollider _hitArea;
        private Vector2 _lastSize;
        private bool _dirty = true;
        private bool _fitting;
        private string _lastText;

        private void OnEnable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            RefreshLayout();
        }

        private void OnDisable() => TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);

        private void OnTextChanged(Object changed)
        {
            if (changed == label && !_fitting)
                _dirty = true;
        }

        private void OnValidate()
        {
            padding = Vector2.Max(Vector2.zero, padding);
            minimumSize = Vector2.Max(Vector2.one * 0.01f, minimumSize);
            _dirty = true;
        }

        private void LateUpdate()
        {
            if (_background == null || _dirty || _background.size != _lastSize ||
                (label != null && label.text != _lastText))
                RefreshLayout();
        }

        [ContextMenu("Refresh Layout")]
        public void RefreshLayout()
        {
            if (_background == null)
                _background = GetComponent<SpriteRenderer>();
            if (_hitArea == null)
                _hitArea = GetComponent<BoxCollider>();
            if (label == null)
                label = GetComponentInChildren<TMP_Text>(true);
            if (label == null || _fitting)
                return;

            _fitting = true;
            try
            {
                // Measure at the intended font size, independently of the previous answer's rect.
                var autoSize = label.enableAutoSizing;
                var fontSize = autoSize ? label.fontSizeMax : label.fontSize;
                label.enableAutoSizing = false;
                label.fontSize = fontSize;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                var preferred = label.GetPreferredValues(label.text, Mathf.Infinity, Mathf.Infinity);
                label.enableAutoSizing = autoSize;
                var scale = label.transform.localScale;
                var size = Vector2.Max(minimumSize, new Vector2(
                    preferred.x * Mathf.Abs(scale.x), preferred.y * Mathf.Abs(scale.y)) + padding * 2f);
                _background.size = size;
                var inner = Vector2.Max(Vector2.one * 0.01f, size - padding * 2f);
                var rect = label.rectTransform;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(inner.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                    inner.y / Mathf.Max(0.001f, Mathf.Abs(scale.y)));
                rect.localPosition = new Vector3(0f, 0f, rect.localPosition.z);
                if (_hitArea != null)
                {
                    _hitArea.size = new Vector3(size.x, size.y, _hitArea.size.z);
                    _hitArea.center = new Vector3(0f, 0f, _hitArea.center.z);
                }
                _lastSize = size;
                _lastText = label.text;
                label.ForceMeshUpdate(true, true);
                _dirty = false;
            }
            finally
            {
                _fitting = false;
            }
        }
    }
}
