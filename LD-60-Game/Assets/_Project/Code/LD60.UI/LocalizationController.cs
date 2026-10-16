using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace LD60.UI
{
    public sealed class LocalizationController : MonoBehaviour
    {
        static readonly Color Lit = new(0.93f, 0.62f, 0.22f, 1f);
        static readonly Color LitHighlight = new(1f, 0.74f, 0.36f, 1f);
        static readonly Color LitPressed = new(0.78f, 0.48f, 0.12f, 1f);

        [SerializeField] private Button russianButton;
        [SerializeField] private Button englishButton;

        private void Awake()
        {
            russianButton.onClick.AddListener(OnRussianClicked);
            englishButton.onClick.AddListener(OnEnglishClicked);
        }

        private void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += RefreshLanguage;
        }

        private void Start()
        {
            RefreshLanguage(LocalizationSettings.SelectedLocale);
        }

        private void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= RefreshLanguage;
        }

        private void OnDestroy()
        {
            russianButton.onClick.RemoveListener(OnRussianClicked);
            englishButton.onClick.RemoveListener(OnEnglishClicked);
        }

        private void OnRussianClicked()
        {
            SelectLocale("ru");
        }

        private void OnEnglishClicked()
        {
            SelectLocale("en");
        }

        private static void SelectLocale(string code)
        {
            var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
            if (locale != null)
                LocalizationSettings.SelectedLocale = locale;
        }

        private void RefreshLanguage(Locale locale)
        {
            var code = locale != null ? locale.Identifier.Code : string.Empty;
            Paint(russianButton, code.StartsWith("ru"));
            Paint(englishButton, code.StartsWith("en"));
        }

        private static void Paint(Button button, bool selected)
        {
            var colors = ColorBlock.defaultColorBlock;
            if (selected)
            {
                colors.normalColor = Lit;
                colors.highlightedColor = LitHighlight;
                colors.pressedColor = LitPressed;
                colors.selectedColor = Lit;
            }

            button.colors = colors;
        }
    }
}
