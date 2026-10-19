using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LD60.UI
{
    /// <summary>
    /// Надпись на время ожидания первого клика в веб-билде.
    /// </summary>
    public static class ClickToPlayPrompt
    {
        private static GameObject _root;

        public static void Show()
        {
            if (_root != null)
                return;

            _root = new GameObject("ClickToPlay");
            Object.DontDestroyOnLoad(_root);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000;

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var shadeObject = new GameObject("Shade");
            shadeObject.transform.SetParent(_root.transform, false);
            var shade = shadeObject.AddComponent<Image>();
            shade.color = new Color(0f, 0f, 0f, 0.72f);
            shade.raycastTarget = false;
            Stretch(shade.rectTransform);

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(_root.transform, false);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = "Click to play";
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = 72f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            Stretch(label.rectTransform);
        }

        public static void Hide()
        {
            if (_root == null)
                return;

            Object.Destroy(_root);
            _root = null;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
