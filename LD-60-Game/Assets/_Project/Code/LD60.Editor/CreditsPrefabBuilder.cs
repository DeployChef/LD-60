using System.IO;
using System.Linq;
using LD60.Gameplay.Context;
using LD60.Gameplay.Story;
using LD60.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LD60.Editor
{
    public static class CreditsPrefabBuilder
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Credits.prefab";
        private const string FrameImagePath = "Assets/_Project/Art/Intro/vecteezy_thai-pattern-frame-and-gray-vintage-border-for-making-cards_44651929.png";
        private const string StoneImagePath = "Assets/_Project/Art/Intro/photo-stone-texture-pattern.jpg";
        private const string FontAssetPath = "Assets/_Project/Art/Fonts/OldStandardTT-Italic SDF.asset";
        private const string InputAssetPath = "Assets/_Project/Settings/Game.inputactions";
        private const string CreditsStoryAssetPath = "Assets/_Project/Data/Story/Credits.asset";
        private const string CreditsActionAssetPath = "Assets/_Project/Data/Story/Actions/Credits/step_1_credits.asset";

        [MenuItem("LD60/Build Credits Prefab")]
        public static GameObject BuildPrefab()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);

            var frameSprite = AssetDatabase.LoadAllAssetsAtPath(FrameImagePath)
                .OfType<Sprite>()
                .FirstOrDefault();

            var stoneSprite = AssetDatabase.LoadAllAssetsAtPath(StoneImagePath)
                .OfType<Sprite>()
                .FirstOrDefault();

            // Root Canvas
            var root = new GameObject("Credits", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(CreditsPlayer));
            root.layer = LayerMask.NameToLayer("UI");

            var rootRect = root.GetComponent<RectTransform>();
            rootRect.localScale = Vector3.one;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.pivot = new Vector2(0.5f, 0.5f);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var rootCanvasGroup = root.GetComponent<CanvasGroup>();
            rootCanvasGroup.alpha = 0f;
            rootCanvasGroup.blocksRaycasts = false;
            rootCanvasGroup.interactable = false;

            var creditsPlayer = root.GetComponent<CreditsPlayer>();

            // 1. Solid Underlay & Background Image
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.layer = LayerMask.NameToLayer("UI");
            bgGo.transform.SetParent(root.transform, false);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImage = bgGo.GetComponent<Image>();
            bgImage.sprite = stoneSprite;
            bgImage.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            bgImage.raycastTarget = true;

            // 2. Vintage Frame Image
            var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            frameGo.layer = LayerMask.NameToLayer("UI");
            frameGo.transform.SetParent(root.transform, false);
            var frameRect = frameGo.GetComponent<RectTransform>();
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;
            var frameImage = frameGo.GetComponent<Image>();
            frameImage.sprite = frameSprite;
            frameImage.color = Color.white;
            frameImage.raycastTarget = false;

            // 3. Game Over Group & Text
            var gameOverGo = new GameObject("GameOver", typeof(RectTransform), typeof(CanvasGroup), typeof(TextMeshProUGUI));
            gameOverGo.layer = LayerMask.NameToLayer("UI");
            gameOverGo.transform.SetParent(root.transform, false);
            var gameOverRect = gameOverGo.GetComponent<RectTransform>();
            gameOverRect.anchorMin = new Vector2(0.5f, 0.5f);
            gameOverRect.anchorMax = new Vector2(0.5f, 0.5f);
            gameOverRect.pivot = new Vector2(0.5f, 0.5f);
            gameOverRect.sizeDelta = new Vector2(900f, 220f);
            gameOverRect.anchoredPosition = Vector2.zero;
            var gameOverGroup = gameOverGo.GetComponent<CanvasGroup>();
            gameOverGroup.alpha = 0f;
            var gameOverText = gameOverGo.GetComponent<TextMeshProUGUI>();
            gameOverText.font = font;
            if (font != null && font.material != null)
                gameOverText.fontSharedMaterial = font.material;
            gameOverText.text = "GAME OVER";
            gameOverText.fontSize = 76f;
            gameOverText.fontStyle = FontStyles.Bold | FontStyles.Italic;
            gameOverText.alignment = TextAlignmentOptions.Center;
            gameOverText.color = new Color(0.95f, 0.92f, 0.85f, 1f);
            gameOverText.raycastTarget = false;

            // 4. Crawl Viewport (inside ornate frame, with RectMask2D and CanvasGroup)
            var viewportGo = new GameObject("CrawlViewport", typeof(RectTransform), typeof(RectMask2D), typeof(CanvasGroup));
            viewportGo.layer = LayerMask.NameToLayer("UI");
            viewportGo.transform.SetParent(root.transform, false);
            var viewportRect = viewportGo.GetComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0.12f, 0.10f);
            viewportRect.anchorMax = new Vector2(0.88f, 0.90f);
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            var crawlGroup = viewportGo.GetComponent<CanvasGroup>();
            crawlGroup.alpha = 0f;
            viewportGo.SetActive(false);

            // 5. Crawl Content
            var contentGo = new GameObject("CrawlContent", typeof(RectTransform), typeof(ContentSizeFitter), typeof(TextMeshProUGUI));
            contentGo.layer = LayerMask.NameToLayer("UI");
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0.5f);
            contentRect.anchorMax = new Vector2(1f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.offsetMin = new Vector2(40f, 0f);
            contentRect.offsetMax = new Vector2(-40f, 0f);
            contentRect.anchoredPosition = new Vector2(0f, -5000f);

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var crawlText = contentGo.GetComponent<TextMeshProUGUI>();
            crawlText.font = font;
            if (font != null && font.material != null)
                crawlText.fontSharedMaterial = font.material;
            crawlText.fontSize = 32f;
            crawlText.lineSpacing = 16f;
            crawlText.paragraphSpacing = 32f;
            crawlText.alignment = TextAlignmentOptions.Center;
            crawlText.color = new Color(0.92f, 0.90f, 0.85f, 1f);
            crawlText.textWrappingMode = TextWrappingModes.Normal;
            crawlText.raycastTarget = false;

            // 6. Thank You Group & Text
            var thanksGo = new GameObject("Thanks", typeof(RectTransform), typeof(CanvasGroup), typeof(TextMeshProUGUI));
            thanksGo.layer = LayerMask.NameToLayer("UI");
            thanksGo.transform.SetParent(root.transform, false);
            var thanksRect = thanksGo.GetComponent<RectTransform>();
            thanksRect.anchorMin = new Vector2(0.5f, 0.5f);
            thanksRect.anchorMax = new Vector2(0.5f, 0.5f);
            thanksRect.pivot = new Vector2(0.5f, 0.5f);
            thanksRect.sizeDelta = new Vector2(900f, 200f);
            thanksRect.anchoredPosition = Vector2.zero;
            var thanksGroup = thanksGo.GetComponent<CanvasGroup>();
            thanksGroup.alpha = 0f;
            var thanksText = thanksGo.GetComponent<TextMeshProUGUI>();
            thanksText.font = font;
            if (font != null && font.material != null)
                thanksText.fontSharedMaterial = font.material;
            thanksText.text = "СПАСИБО ЗА ИГРУ";
            thanksText.fontSize = 52f;
            thanksText.fontStyle = FontStyles.Italic;
            thanksText.alignment = TextAlignmentOptions.Center;
            thanksText.color = new Color(0.95f, 0.92f, 0.85f, 1f);
            thanksText.raycastTarget = false;

            // 7. Full Screen Black Fade Overlay
            var blackGo = new GameObject("BlackFade", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            blackGo.layer = LayerMask.NameToLayer("UI");
            blackGo.transform.SetParent(root.transform, false);
            var blackRect = blackGo.GetComponent<RectTransform>();
            blackRect.anchorMin = Vector2.zero;
            blackRect.anchorMax = Vector2.one;
            blackRect.offsetMin = Vector2.zero;
            blackRect.offsetMax = Vector2.zero;
            var blackGroup = blackGo.GetComponent<CanvasGroup>();
            blackGroup.alpha = 0f;
            blackGroup.blocksRaycasts = false;
            var blackImage = blackGo.GetComponent<Image>();
            blackImage.color = Color.black;
            blackImage.raycastTarget = false;

            // 8. Skip Hint
            var hintGo = new GameObject("SkipHint", typeof(RectTransform), typeof(TextMeshProUGUI));
            hintGo.layer = LayerMask.NameToLayer("UI");
            hintGo.transform.SetParent(root.transform, false);
            var hintRect = hintGo.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, 0f);
            hintRect.anchorMax = new Vector2(0.5f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector2(0f, 36f);
            hintRect.sizeDelta = new Vector2(700f, 40f);
            var hintText = hintGo.GetComponent<TextMeshProUGUI>();
            hintText.font = font;
            if (font != null && font.material != null)
                hintText.fontSharedMaterial = font.material;
            hintText.text = "[Удерживайте ПРОБЕЛ для ускорения / ESC для пропуска]";
            hintText.fontSize = 20f;
            hintText.alignment = TextAlignmentOptions.Center;
            hintText.color = new Color(1f, 1f, 1f, 0.45f);
            hintText.raycastTarget = false;
            hintGo.SetActive(false);

            // Wire all fields into CreditsPlayer serialized object
            var so = new SerializedObject(creditsPlayer);
            so.FindProperty("rootCanvasGroup").objectReferenceValue = rootCanvasGroup;
            so.FindProperty("backgroundImage").objectReferenceValue = bgImage;
            so.FindProperty("frameImage").objectReferenceValue = frameImage;
            so.FindProperty("gameOverGroup").objectReferenceValue = gameOverGroup;
            so.FindProperty("gameOverText").objectReferenceValue = gameOverText;
            so.FindProperty("crawlViewport").objectReferenceValue = viewportRect;
            so.FindProperty("crawlGroup").objectReferenceValue = crawlGroup;
            so.FindProperty("crawlContent").objectReferenceValue = contentRect;
            so.FindProperty("crawlText").objectReferenceValue = crawlText;
            so.FindProperty("thanksGroup").objectReferenceValue = thanksGroup;
            so.FindProperty("thanksText").objectReferenceValue = thanksText;
            so.FindProperty("blackFadeGroup").objectReferenceValue = blackGroup;
            so.FindProperty("skipHint").objectReferenceValue = hintGo;
            so.FindProperty("input").objectReferenceValue = input;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Ensure root is active so localScale remains (1, 1, 1)
            // CanvasGroup.alpha = 0 and blocksRaycasts = false keeps it invisible and uninteractive on scene start
            root.SetActive(true);
            rootRect.localScale = Vector3.one;

            // Save Prefab
            var dir = Path.GetDirectoryName(PrefabPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            // Guarantee prefab root scale is (1, 1, 1)
            using (var scope = new PrefabUtility.EditPrefabContentsScope(PrefabPath))
            {
                var contentRoot = scope.prefabContentsRoot;
                contentRoot.SetActive(true);
                var scopeRect = contentRoot.GetComponent<RectTransform>();
                if (scopeRect != null)
                {
                    scopeRect.localScale = Vector3.one;
                    scopeRect.anchorMin = Vector2.zero;
                    scopeRect.anchorMax = Vector2.one;
                    scopeRect.offsetMin = Vector2.zero;
                    scopeRect.offsetMax = Vector2.zero;
                    scopeRect.pivot = new Vector2(0.5f, 0.5f);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CreditsPrefabBuilder] Successfully created Credits prefab at '{PrefabPath}'.");
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        [MenuItem("LD60/Wire Credits into Game Scene and Story")]
        public static void WireCredits()
        {
            // 1. Ensure fresh Prefab exists
            var prefab = BuildPrefab();

            // 2. Create CreditsAction asset
            var actionDir = Path.GetDirectoryName(CreditsActionAssetPath);
            if (!Directory.Exists(actionDir))
                Directory.CreateDirectory(actionDir);

            var actionAsset = AssetDatabase.LoadAssetAtPath<CreditsAction>(CreditsActionAssetPath);
            if (actionAsset == null)
            {
                actionAsset = ScriptableObject.CreateInstance<CreditsAction>();
                AssetDatabase.CreateAsset(actionAsset, CreditsActionAssetPath);
            }

            // 3. Wire into Credits.asset (ChapterDefinition)
            var chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(CreditsStoryAssetPath);
            if (chapter != null)
            {
                var chSo = new SerializedObject(chapter);
                var actionsProp = chSo.FindProperty("actions");
                actionsProp.arraySize = 1;
                actionsProp.GetArrayElementAtIndex(0).objectReferenceValue = actionAsset;
                chSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(chapter);
            }

            // 4. Open Game.unity and ensure Credits instance is present
            const string gameScenePath = "Assets/_Project/Scenes/Game.unity";
            var activeScene = EditorSceneManager.GetActiveScene();
            var shouldReopen = activeScene.path != gameScenePath;

            var gameScene = EditorSceneManager.OpenScene(gameScenePath, OpenSceneMode.Single);

            // Clean up any old Credits GameObjects to avoid stale broken serialized overrides
            var oldCreditsList = Object.FindObjectsByType<CreditsPlayer>(FindObjectsInactive.Include);
            foreach (var old in oldCreditsList)
            {
                if (old != null && old.gameObject != null)
                    Object.DestroyImmediate(old.gameObject);
            }

            // Instantiate fresh prefab
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, gameScene);
            instance.name = "Credits";
            instance.SetActive(true);

            var rect = instance.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.localScale = Vector3.one;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            }

            var creditsPlayer = instance.GetComponent<CreditsPlayer>();
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            EditorUtility.SetDirty(instance);

            // Wire into SequencePlayer
            var seqPlayer = Object.FindAnyObjectByType<SequencePlayer>(FindObjectsInactive.Include);
            if (seqPlayer != null)
            {
                var seqSo = new SerializedObject(seqPlayer);
                var creditsProp = seqSo.FindProperty("_credits");
                if (creditsProp != null)
                    creditsProp.objectReferenceValue = creditsPlayer;
                seqSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(seqPlayer);
            }

            // Wire into StoryBook finale
            var storyBook = Object.FindAnyObjectByType<StoryBook>(FindObjectsInactive.Include);
            if (storyBook != null && chapter != null)
            {
                var sbSo = new SerializedObject(storyBook);
                var finaleProp = sbSo.FindProperty("finale");
                if (finaleProp != null)
                    finaleProp.objectReferenceValue = chapter;
                sbSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(storyBook);
            }

            EditorSceneManager.MarkSceneDirty(gameScene);
            EditorSceneManager.SaveScene(gameScene);

            if (shouldReopen && !string.IsNullOrEmpty(activeScene.path))
            {
                EditorSceneManager.OpenScene(activeScene.path, OpenSceneMode.Single);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[CreditsPrefabBuilder] Successfully wired fresh Credits prefab into Game.unity, SequencePlayer, StoryBook and Credits chapter.");
        }
    }
}
