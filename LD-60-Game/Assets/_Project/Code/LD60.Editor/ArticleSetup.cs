using System.IO;
using LD60.Gameplay.Articles;
using LD60.Gameplay.Context;
using LD60.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LD60.Editor
{
    public static class ArticleSetup
    {
        private const float Cell = 1.05f;
        private const string FormFolder = "Assets/_Project/Prefabs/Articles";
        private const string DataFolder = "Assets/_Project/Data/Articles";

        [MenuItem("LD60/Build Article Props")]
        public static void Build()
        {
            Directory.CreateDirectory(FormFolder);
            Directory.CreateDirectory(DataFolder);
            var paper = new Color(0.94f, 0.91f, 0.84f, 1f);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Blocking/Unit.png");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Blocking/Blocking-Unlit.mat");
            var font = FindFont();
            var layer = SortingLayerId("DeskItem");

            var square = BuildForm("ArticleSquare", sprite, material, font, layer, paper, ArticleShape.Square,
                new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(-0.5f, 0.5f), new Vector2(0.5f, 0.5f) },
                new Vector2(0f, 0.28f), new Vector2(0.82f, 0.82f),
                new Vector2(0f, 0.82f), new Vector2(1.9f, 0.34f), 4.5f,
                new Vector2(0f, -0.42f), new Vector2(1.85f, 0.72f), 3.2f);
            var zed = BuildForm("ArticleZ", sprite, material, font, layer, paper, ArticleShape.Z,
                new[] { new Vector2(-1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -0.5f), new Vector2(1f, -0.5f) },
                new Vector2(-0.85f, 0.15f), new Vector2(0.72f, 0.72f),
                new Vector2(0.35f, 0.5f), new Vector2(1.15f, 0.32f), 3.6f,
                new Vector2(0.45f, -0.42f), new Vector2(1.2f, 0.48f), 2.6f);
            var gamma = BuildForm("ArticleGamma", sprite, material, font, layer, paper, ArticleShape.Gamma,
                new[] { new Vector2(-0.5f, -1f), new Vector2(-0.5f, 0f), new Vector2(-0.5f, 1f), new Vector2(0.5f, -1f) },
                new Vector2(-0.15f, 0.35f), new Vector2(0.72f, 0.72f),
                new Vector2(-0.15f, 0.95f), new Vector2(1.5f, 0.32f), 3.6f,
                new Vector2(0.05f, -0.55f), new Vector2(1.7f, 0.7f), 2.8f);
            var note = BuildNote(sprite, material, font, layer);

            var morning = MakeArticle("MorningEdition", ArticleShape.Square, ArticleKind.Report,
                "Morning Edition", "The city woke to a quiet press and a full inbox.", 0.35f, 0f, 0f, 0f);
            var council = MakeArticle("CouncilVote", ArticleShape.Z, ArticleKind.Politics,
                "Council Vote", "The council meets behind closed doors.", 0f, 0.4f, 0f, 0f, ArticleKind.Ad);
            var bread = MakeArticle("BreadQueue", ArticleShape.Gamma, ArticleKind.Social,
                "Bread Queue", "The bakery line starts before dawn.", 0f, 0f, 0.35f, 0f);
            var soap = MakeArticle("SoapCoupon", ArticleShape.Square, ArticleKind.Ad,
                "Soap Coupon", "A clean shirt for a clean conscience.", 0f, 0f, 0f, 4f, ArticleKind.Politics);

            var day = AssetDatabase.LoadAssetAtPath<ChapterDefinition>("Assets/_Project/Data/Story/DayOne.asset");
            var chapter = new SerializedObject(day);
            var capsules = chapter.FindProperty("capsules");
            capsules.arraySize = 1;
            var delivery = capsules.GetArrayElementAtIndex(0);
            delivery.FindPropertyRelative("id").stringValue = "morning-drop";
            delivery.FindPropertyRelative("note").stringValue = "Fill the page. Do not place an ad beside politics.";
            var list = delivery.FindPropertyRelative("articles");
            list.arraySize = 3;
            list.GetArrayElementAtIndex(0).objectReferenceValue = morning;
            list.GetArrayElementAtIndex(1).objectReferenceValue = council;
            list.GetArrayElementAtIndex(2).objectReferenceValue = bread;
            chapter.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(day);

            WireGame(square, zed, gamma, note);
            BuildLookup(new[] { morning, council, bread, soap }, new[] { square, zed, gamma, square });
            AssetDatabase.SaveAssets();
            Debug.Log("[Articles] Props, DayOne capsule and ArticleLookup are ready.");
        }

        private static ArticlePiece BuildForm(string name, Sprite sprite, Material material, TMP_FontAsset font, int layer, Color paper,
            ArticleShape shape, Vector2[] cells, Vector2 pictureAt, Vector2 pictureSize,
            Vector2 titleAt, Vector2 titleSize, float titleMax, Vector2 bodyAt, Vector2 bodySize, float bodyMax)
        {
            var root = new GameObject(name);
            foreach (var cell in cells)
                MakeCell(root.transform, cell * Cell, sprite, material, paper, layer);

            var pictureObject = new GameObject("Picture");
            pictureObject.transform.SetParent(root.transform, false);
            pictureObject.transform.localPosition = new Vector3(pictureAt.x, pictureAt.y, -0.02f);
            pictureObject.transform.localScale = new Vector3(pictureSize.x, pictureSize.y, 1f);
            var picture = pictureObject.AddComponent<SpriteRenderer>();
            picture.sprite = sprite;
            picture.sharedMaterial = material;
            picture.color = new Color(0.45f, 0.42f, 0.38f, 1f);
            picture.sortingLayerID = layer;
            picture.sortingOrder = 2;

            var title = MakeText(root.transform, "Title", titleAt, titleSize, titleMax, font, layer, FontStyles.Bold);
            var body = MakeText(root.transform, "Body", bodyAt, bodySize, bodyMax, font, layer, FontStyles.Normal);
            var piece = root.AddComponent<ArticlePiece>();
            root.AddComponent<DeskDrag>();
            var data = new SerializedObject(piece);
            data.FindProperty("picture").objectReferenceValue = picture;
            data.FindProperty("title").objectReferenceValue = title;
            data.FindProperty("body").objectReferenceValue = body;
            data.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, FormFolder + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<ArticlePiece>();
        }

        private static NoteSlip BuildNote(Sprite sprite, Material material, TMP_FontAsset font, int layer)
        {
            var root = new GameObject("NoteSlip");
            var page = new GameObject("Page");
            page.transform.SetParent(root.transform, false);
            page.transform.localScale = new Vector3(2.5f, 1.45f, 1f);
            var renderer = page.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.color = new Color(0.96f, 0.9f, 0.62f, 1f);
            renderer.sortingLayerID = layer;
            var box = page.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 1f, 0.08f);
            var text = MakeText(root.transform, "Text", Vector2.zero, new Vector2(2.25f, 1.15f), 3f, font, layer, FontStyles.Italic);
            var slip = root.AddComponent<NoteSlip>();
            root.AddComponent<DeskDrag>();
            var data = new SerializedObject(slip);
            data.FindProperty("text").objectReferenceValue = text;
            data.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, FormFolder + "/NoteSlip.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<NoteSlip>();
        }

        private static void MakeCell(Transform parent, Vector2 position, Sprite sprite, Material material, Color color, int layer)
        {
            var cell = new GameObject("Cell");
            cell.transform.SetParent(parent, false);
            cell.transform.localPosition = new Vector3(position.x, position.y, 0f);
            cell.transform.localScale = new Vector3(Cell * 0.98f, Cell * 0.98f, 1f);
            var renderer = cell.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.color = color;
            renderer.sortingLayerID = layer;
            var box = cell.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 1f, 0.08f);
        }

        private static TMP_Text MakeText(Transform parent, string name, Vector2 position, Vector2 size, float max, TMP_FontAsset font, int layer, FontStyles style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(position.x, position.y, -0.03f);
            var text = go.AddComponent<TextMeshPro>();
            text.font = font;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.8f;
            text.fontSizeMax = max;
            text.color = new Color(0.16f, 0.13f, 0.1f, 1f);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            text.rectTransform.sizeDelta = size;
            var renderer = text.renderer;
            renderer.sortingLayerID = layer;
            renderer.sortingOrder = 4;
            return text;
        }

        private static ArticleDefinition MakeArticle(string name, ArticleShape shape, ArticleKind kind, string title, string body,
            float fill, float politics, float social, float money, params ArticleKind[] forbidden)
        {
            var path = DataFolder + "/" + name + ".asset";
            var article = AssetDatabase.LoadAssetAtPath<ArticleDefinition>(path);
            if (article == null)
            {
                article = ScriptableObject.CreateInstance<ArticleDefinition>();
                AssetDatabase.CreateAsset(article, path);
            }

            var data = new SerializedObject(article);
            data.FindProperty("shape").enumValueIndex = (int)shape;
            data.FindProperty("kind").enumValueIndex = (int)kind;
            data.FindProperty("title").stringValue = title;
            data.FindProperty("body").stringValue = body;
            data.FindProperty("fill").floatValue = fill;
            data.FindProperty("politics").floatValue = politics;
            data.FindProperty("social").floatValue = social;
            data.FindProperty("money").floatValue = money;
            var neighbors = data.FindProperty("forbiddenNeighbors");
            neighbors.arraySize = forbidden.Length;
            for (var i = 0; i < forbidden.Length; i++)
                neighbors.GetArrayElementAtIndex(i).enumValueIndex = (int)forbidden[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(article);
            return article;
        }

        private static void WireGame(ArticlePiece square, ArticlePiece zed, ArticlePiece gamma, NoteSlip note)
        {
            var scene = EditorSceneManager.GetSceneByName("Game");
            if (!scene.isLoaded)
                scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity", OpenSceneMode.Additive);
            EditorSceneManager.SetActiveScene(scene);

            var desk = GameObject.Find("Desk").transform;
            foreach (var childName in new[] { "PaperBack", "PaperA", "PaperB", "PaperC", "PaperD" })
            {
                var child = desk.Find(childName);
                if (child != null)
                    Object.DestroyImmediate(child.gameObject);
            }

            var slotObject = GameObject.Find("MessageBox");
            if (slotObject.GetComponent<BoxCollider>() == null)
            {
                var box = slotObject.AddComponent<BoxCollider>();
                box.size = new Vector3(1f, 1f, 0.2f);
            }

            var slot = slotObject.GetComponent<CapsuleSlot>();
            if (slot == null)
                slot = slotObject.AddComponent<CapsuleSlot>();
            var data = new SerializedObject(slot);
            data.FindProperty("square").objectReferenceValue = square;
            data.FindProperty("zed").objectReferenceValue = zed;
            data.FindProperty("gamma").objectReferenceValue = gamma;
            data.FindProperty("note").objectReferenceValue = note;
            data.FindProperty("desk").objectReferenceValue = desk;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void BuildLookup(ArticleDefinition[] articles, ArticlePiece[] forms)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var cameraObject = new GameObject("Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.15f, 0.14f, 1f);
            camera.fieldOfView = 28f;
            camera.transform.position = new Vector3(6.4f, 0.2f, -16f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 40f;

            for (var i = 0; i < articles.Length; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(forms[i].gameObject);
                var piece = instance.GetComponent<ArticlePiece>();
                piece.Bind(articles[i]);
                piece.transform.position = new Vector3(i * 4.6f, 0f, 0f);
            }

            EditorSceneManager.SaveScene(scene, "Assets/_Project/Scenes/ArticleLookup.unity");
            EditorSceneManager.CloseScene(scene, true);
            var game = EditorSceneManager.GetSceneByName("Game");
            if (game.isLoaded)
                EditorSceneManager.SetActiveScene(game);
        }

        private static TMP_FontAsset FindFont()
        {
            var guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            if (guids.Length == 0)
                return null;
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static int SortingLayerId(string name)
        {
            var layers = SortingLayer.layers;
            for (var i = 0; i < layers.Length; i++)
            {
                if (layers[i].name == name)
                    return layers[i].id;
            }

            return 0;
        }
    }
}
