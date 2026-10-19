using TMPro;
using UnityEngine;

namespace LD60.UI
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
    public sealed class DialoguePanelLayout : MonoBehaviour
    {
        [SerializeField] private TMP_Text line;
        [SerializeField, Min(0f)] private float topPadding = 0.3f;
        [SerializeField, Min(0f)] private float bottomPadding = 0.3f;
        [SerializeField, Min(0f)] private float textAnswerSpacing = 0.25f;
        [SerializeField, Min(0f)] private float answerSpacing = 0.15f;
        [SerializeField, Min(0f)] private float minimumHeight = 2.4f;
        [SerializeField] private SpriteRenderer counter;
        [SerializeField, Min(0f)] private float counterClearance = 0.25f;

        private SpriteRenderer _panel;
        private DialogueChoiceLayout[] _choices;
        private Vector3 _top;
        private Vector3 _lastPosition;
        private bool _initialized;
        private bool _dirty = true;
        private bool _fitting;
        private string _lastText;
        private int _visibleChoices;
        private float _lastCounterTop;

        private void OnEnable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            RefreshLayout();
        }

        private void OnDisable() => TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        private void OnValidate() => _dirty = true;

        private void OnTextChanged(Object changed)
        {
            if (!_fitting && changed is TMP_Text text && text.transform.IsChildOf(transform))
                _dirty = true;
        }

        private int VisibleChoices()
        {
            var mask = 0;
            if (_choices != null)
                for (var i = 0; i < _choices.Length; i++)
                    if (_choices[i] != null && _choices[i].gameObject.activeSelf)
                        mask |= 1 << i;
            return mask;
        }

        private void LateUpdate()
        {
            if (_dirty || (line != null && line.text != _lastText) ||
                VisibleChoices() != _visibleChoices || transform.position != _lastPosition ||
                (counter != null && !Mathf.Approximately(counter.bounds.max.y, _lastCounterTop)))
                RefreshLayout();
        }

        [ContextMenu("Refresh Layout")]
        public void RefreshLayout()
        {
            if (_fitting)
                return;
            if (_panel == null)
                _panel = GetComponent<SpriteRenderer>();
            if (line == null)
                line = transform.Find("Line")?.GetComponent<TMP_Text>();
            if (line == null)
                return;
            if (_choices == null)
                _choices = GetComponentsInChildren<DialogueChoiceLayout>(true);
            if (!_initialized)
            {
                _top = transform.TransformPoint(new Vector3(0f, _panel.size.y * 0.5f));
                _lastPosition = transform.position;
                _initialized = true;
            }
            // Respect manual repositioning of the whole window in the Inspector.
            _top += transform.position - _lastPosition;
            _fitting = true;
            try
            {
                var scale = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y));
                line.overflowMode = TextOverflowModes.Overflow;
                line.verticalAlignment = VerticalAlignmentOptions.Top;
                line.GetComponentInChildren<DialogueFrameFitter>(true)?.Fit();
                var frame = line.GetComponentInChildren<SpriteRenderer>(true);
                if (frame == null)
                    return;
                var frameHeight = frame.bounds.size.y / scale;
                var height = topPadding + frameHeight + bottomPadding;
                var count = 0;
                foreach (var choice in _choices)
                {
                    if (choice == null || !choice.gameObject.activeSelf)
                        continue;
                    choice.RefreshLayout();
                    height += ChoiceHeight(choice, scale);
                    count++;
                }
                if (count > 0)
                    height += textAnswerSpacing + answerSpacing * (count - 1);
                height = Mathf.Max(minimumHeight, height);
                _panel.size = new Vector2(_panel.size.x, height);
                // Clamp this layout pass without moving the preferred anchor: short
                // messages can return to their original position after a long one.
                var layoutTop = _top;
                if (counter != null)
                {
                    var bottom = (layoutTop - transform.up * (height * scale)).y;
                    layoutTop += Vector3.up * Mathf.Max(0f, counter.bounds.max.y + counterClearance - bottom);
                }
                transform.position = layoutTop - transform.up * (height * scale * 0.5f);

                var cursor = topPadding + frameHeight * 0.5f;
                var frameCenter = frame.bounds.center;
                var target = layoutTop - transform.up * (cursor * scale);
                line.transform.position += transform.up * Vector3.Dot(target - frameCenter, transform.up);
                cursor = topPadding + frameHeight + textAnswerSpacing;
                foreach (var choice in _choices)
                {
                    if (choice == null || !choice.gameObject.activeSelf)
                        continue;
                    var choiceHeight = ChoiceHeight(choice, scale);
                    target = layoutTop - transform.up * ((cursor + choiceHeight * 0.5f) * scale);
                    choice.transform.position += transform.up * Vector3.Dot(target - choice.transform.position, transform.up);
                    cursor += choiceHeight + answerSpacing;
                }
                _lastPosition = transform.position;
                _lastText = line.text;
                _visibleChoices = VisibleChoices();
                _lastCounterTop = counter != null ? counter.bounds.max.y : 0f;
                _dirty = false;
            }
            finally
            {
                _fitting = false;
            }
        }

        private static float ChoiceHeight(DialogueChoiceLayout choice, float panelScale)
        {
            // World bounds grow while the hover tween tilts a button. Layout uses
            // the sprite's own height so that animation cannot resize the window.
            return choice.GetComponent<SpriteRenderer>().size.y *
                Mathf.Abs(choice.transform.lossyScale.y) / panelScale;
        }
    }
}
