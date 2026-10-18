using System.IO;
using LD60.Gameplay.Articles;
using LD60.Gameplay.Context;
using LD60.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LD60.Editor
{
    public static class ArticleSetup
    {
        private const float Cell = 1.05f;
        private const string FormFolder = "Assets/_Project/Prefabs/Articles";
        private const string DataFolder = "Assets/_Project/Data/Articles";
        private const string LayoutFolder = "Assets/_Project/Data/Articles/Layouts";

        [InitializeOnLoadMethod]
        private static void BuildWhenMissing()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                    return;
                var square = AssetDatabase.LoadAssetAtPath<GameObject>(FormFolder + "/ArticleSquare.prefab");
                if (square != null && square.transform.Find("Face1") != null)
                    return;
                Build();
            };
        }

        [MenuItem("LD60/Build Article Props")]
        public static void Build()
        {
            Directory.CreateDirectory(FormFolder);
            Directory.CreateDirectory(DataFolder);
            Directory.CreateDirectory(LayoutFolder);
            var paper = new Color(0.94f, 0.91f, 0.84f, 1f);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Blocking/Unit.png");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Blocking/Blocking-Unlit.mat");
            var font = FindFont();
            var layer = SortingLayerId("DeskItem");

            var dot = BuildForm("ArticleDot", sprite, material, font, layer, paper, new[] { Vector2.zero });
            var line2H = BuildForm("ArticleLine2H", sprite, material, font, layer, paper,
                new[] { new Vector2(-0.5f, 0f), new Vector2(0.5f, 0f) });
            var line2V = BuildForm("ArticleLine2V", sprite, material, font, layer, paper,
                new[] { new Vector2(0f, 0.5f), new Vector2(0f, -0.5f) });
            var line3H = BuildForm("ArticleLine3H", sprite, material, font, layer, paper,
                new[] { new Vector2(-1f, 0f), new Vector2(0f, 0f), new Vector2(1f, 0f) });
            var line3V = BuildForm("ArticleLine3V", sprite, material, font, layer, paper,
                new[] { new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, -1f) });
            var square = BuildForm("ArticleSquare", sprite, material, font, layer, paper,
                new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(-0.5f, 0.5f), new Vector2(0.5f, 0.5f) });
            var note = BuildNote(sprite, material, font, layer);
            var articles = WriteMorningArticles();
            WireGame(dot, line2H, line2V, line3H, line3V, square, note);
            BuildLookup(articles, new[] { square, line3H, line2V, dot, line2H, dot, line3V, line2H, line2V, dot, line3H });
            AssetDatabase.SaveAssets();
            Debug.Log("[Articles] Props, DayOne capsule and ArticleLookup are ready.");
        }

        public static ArticleDefinition[] WriteMorningArticles()
        {
            var slot1 = Layout("Picture1", 1);
            var slot2 = Layout("Picture2", 2);
            var slot3 = Layout("Picture3", 3);

            var morning = MakeArticle("MorningEdition", "morning-edition", ArticleShape.Square, ArticleKind.Report,
                "Morning Edition", "The city woke to a quiet press and a full inbox.", slot2, 0.35f, 0f, 0f, 0f);
            var council = MakeArticle("CouncilVote", "council-vote", ArticleShape.Line3H, ArticleKind.Politics,
                "Council Vote", "The council meets behind closed doors.", slot3, 0f, 0.4f, 0f, 0f, ArticleKind.Ad);
            var bread = MakeArticle("BreadQueue", "bread-queue", ArticleShape.Line2V, ArticleKind.Social,
                "Bread Queue", "The bakery line starts before dawn.", slot2, 0f, 0f, 0.35f, 0f);
            var soap = MakeArticle("SoapCoupon", "soap-coupon", ArticleShape.Dot, ArticleKind.Ad,
                "Soap Coupon", "A clean shirt for a clean conscience.", slot1, 0f, 0f, 0f, 4f, ArticleKind.Politics);
            var city = MakeArticle("CityBrief", "city-brief", ArticleShape.Line2H, ArticleKind.Report,
                "City Brief", "A short note on the morning wires.", slot3, 0.3f, 0f, 0f, 0f);
            var night = MakeArticle("NightWire", "night-wire", ArticleShape.Dot, ArticleKind.Report,
                "Night Wire", "The presses ran after midnight.", slot1, 0.25f, 0f, 0f, 0f);
            var market = MakeArticle("MarketBell", "market-bell", ArticleShape.Line3V, ArticleKind.Social,
                "Market Bell", "The square fills when the bell rings.", slot2, 0f, 0f, 0.25f, 0f);
            var union = MakeArticle("UnionNote", "union-note", ArticleShape.Line2H, ArticleKind.Politics,
                "Union Note", "The shift asks for a louder vote.", slot3, 0f, 0.3f, 0f, 0f, ArticleKind.Ad);
            var tram = MakeArticle("TramDelay", "tram-delay", ArticleShape.Line2V, ArticleKind.Social,
                "Tram Delay", "The first car is late again.", slot1, 0f, 0f, 0.2f, 0f);
            var dog = MakeArticle("LostDog", "lost-dog", ArticleShape.Dot, ArticleKind.Report,
                "Lost Dog", "A brown dog, last seen by the bakery.", slot2, 0.2f, 0f, 0f, 0f);
            var harbor = MakeArticle("HarborTax", "harbor-tax", ArticleShape.Line3H, ArticleKind.Politics,
                "Harbor Tax", "The docks argue over a new levy.", slot3, 0f, 0.2f, 0f, 0f, ArticleKind.Ad);

            var articles = new[] { morning, council, bread, soap, city, night, market, union, tram, dog, harbor };
            var day = AssetDatabase.LoadAssetAtPath<ChapterDefinition>("Assets/_Project/Data/Story/DayOne.asset");
            var chapter = new SerializedObject(day);
            var capsules = chapter.FindProperty("capsules");
            capsules.arraySize = 1;
            var delivery = capsules.GetArrayElementAtIndex(0);
            delivery.FindPropertyRelative("id").stringValue = "morning-drop";
            delivery.FindPropertyRelative("note").stringValue = "Fill the page. Do not place an ad beside politics.";
            var list = delivery.FindPropertyRelative("articles");
            list.arraySize = articles.Length;
            for (var i = 0; i < articles.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = articles[i];
            chapter.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(day);
            AssetDatabase.SaveAssets();
            return articles;
        }

        private static ArticlePiece BuildForm(string name, Sprite sprite, Material material, TMP_FontAsset font, int layer, Color paperColor, Vector2[] cells)
        {
            var root = new GameObject(name);
            foreach (var cell in cells)
                MakeCell(root.transform, cell * Cell);

            var bounds = Bounds(cells);
            var paperObject = new GameObject("Paper");
            paperObject.transform.SetParent(root.transform, false);
            paperObject.transform.localPosition = new Vector3(bounds.center.x, bounds.center.y, 0f);
            paperObject.transform.localScale = new Vector3(bounds.size.x, bounds.size.y, 1f);
            var paper = paperObject.AddComponent<SpriteRenderer>();
            paper.sprite = sprite;
            paper.sharedMaterial = material;
            paper.color = paperColor;
            paper.sortingLayerID = layer;
            paper.sortingOrder = 1;

            var one = BuildFace(root.transform, "Face1", 1, font, layer, sprite, bounds);
            var two = BuildFace(root.transform, "Face2", 2, font, layer, sprite, bounds);
            var three = BuildFace(root.transform, "Face3", 3, font, layer, sprite, bounds);
            two.gameObject.SetActive(false);
            three.gameObject.SetActive(false);

            var piece = root.AddComponent<ArticlePiece>();
            root.AddComponent<DeskDrag>();
            var data = new SerializedObject(piece);
            data.FindProperty("one").objectReferenceValue = one;
            data.FindProperty("two").objectReferenceValue = two;
            data.FindProperty("three").objectReferenceValue = three;
            data.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, FormFolder + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<ArticlePiece>();
        }

        private static ArticleFace BuildFace(Transform parent, string name, int slot, TMP_FontAsset font, int layer, Sprite sprite, Rect bounds)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localPosition = new Vector3(bounds.center.x, bounds.center.y, -0.04f);
            rect.localScale = new Vector3(0.01f, 0.01f, 1f);
            rect.sizeDelta = new Vector2(bounds.size.x * 92f, bounds.size.y * 92f);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingLayerID = layer;
            canvas.sortingOrder = 2;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1
                | AdditionalCanvasShaderChannels.Normal
                | AdditionalCanvasShaderChannels.Tangent;

            TMP_Text title;
            TMP_Text body;
            Image picture = null;
            if (slot == 1)
            {
                title = MakeLabel(root.transform, "Title", font, FontStyles.Bold, 40f, new Vector2(0f, 0.62f), Vector2.one);
                body = MakeLabel(root.transform, "Body", font, FontStyles.Normal, 28f, Vector2.zero, new Vector2(1f, 0.62f));
            }
            else if (slot == 2)
            {
                title = MakeLabel(root.transform, "Title", font, FontStyles.Bold, 36f, new Vector2(0f, 0.72f), Vector2.one);
                picture = MakePicture(root.transform, sprite, new Vector2(0f, 0.34f), new Vector2(1f, 0.72f));
                body = MakeLabel(root.transform, "Body", font, FontStyles.Normal, 26f, Vector2.zero, new Vector2(1f, 0.34f));
            }
            else
            {
                picture = MakePicture(root.transform, sprite, new Vector2(0f, 0.58f), Vector2.one);
                title = MakeLabel(root.transform, "Title", font, FontStyles.Bold, 36f, new Vector2(0f, 0.38f), new Vector2(1f, 0.58f));
                body = MakeLabel(root.transform, "Body", font, FontStyles.Normal, 26f, Vector2.zero, new Vector2(1f, 0.38f));
            }

            var face = root.AddComponent<ArticleFace>();
            var data = new SerializedObject(face);
            data.FindProperty("title").objectReferenceValue = title;
            data.FindProperty("body").objectReferenceValue = body;
            data.FindProperty("picture").objectReferenceValue = picture;
            data.ApplyModifiedPropertiesWithoutUndo();
            return face;
        }

        private static TMP_Text MakeLabel(Transform parent, string name, TMP_FontAsset font, FontStyles style, float max, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = Box(parent, name, anchorMin, anchorMax);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontStyle = style;
            text.alignment = name == "Title" ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = max;
            text.color = new Color(0.16f, 0.13f, 0.1f, 1f);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            text.raycastTarget = false;
            text.text = name;
            return text;
        }

        private static Image MakePicture(Transform parent, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = Box(parent, "Picture", anchorMin, anchorMax);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = new Color(0.45f, 0.42f, 0.38f, 1f);
            return image;
        }

        private static RectTransform Box(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(4f, 4f);
            rect.offsetMax = new Vector2(-4f, -4f);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static Rect Bounds(Vector2[] cells)
        {
            var size = Vector2.one * (Cell * 0.98f);
            var min = cells[0] * Cell - size * 0.5f;
            var max = cells[0] * Cell + size * 0.5f;
            for (var i = 1; i < cells.Length; i++)
            {
                var point = cells[i] * Cell;
                min = Vector2.Min(min, point - size * 0.5f);
                max = Vector2.Max(max, point + size * 0.5f);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
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

        private static void MakeCell(Transform parent, Vector2 position)
        {
            var cell = new GameObject("Cell");
            cell.transform.SetParent(parent, false);
            cell.transform.localPosition = new Vector3(position.x, position.y, 0f);
            cell.transform.localScale = new Vector3(Cell * 0.98f, Cell * 0.98f, 1f);
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

        private static ArticlePictureLayout Layout(string name, int slot)
        {
            var path = LayoutFolder + "/" + name + ".asset";
            var layout = AssetDatabase.LoadAssetAtPath<ArticlePictureLayout>(path);
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<ArticlePictureLayout>();
                AssetDatabase.CreateAsset(layout, path);
            }

            var data = new SerializedObject(layout);
            data.FindProperty("slot").intValue = slot;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(layout);
            return layout;
        }

        private static ArticleDefinition MakeArticle(string name, string id, ArticleShape shape, ArticleKind kind, string title, string body,
            ArticlePictureLayout pictureLayout, float fill, float politics, float social, float money, params ArticleKind[] forbidden)
        {
            var path = DataFolder + "/" + name + ".asset";
            var article = AssetDatabase.LoadAssetAtPath<ArticleDefinition>(path);
            if (article == null)
            {
                article = ScriptableObject.CreateInstance<ArticleDefinition>();
                AssetDatabase.CreateAsset(article, path);
            }

            var data = new SerializedObject(article);
            data.FindProperty("id").stringValue = id;
            data.FindProperty("shape").enumValueIndex = (int)shape;
            data.FindProperty("kind").enumValueIndex = (int)kind;
            data.FindProperty("title").stringValue = title;
            data.FindProperty("body").stringValue = body;
            data.FindProperty("pictureLayout").objectReferenceValue = pictureLayout;
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

        private static void WireGame(ArticlePiece dot, ArticlePiece line2H, ArticlePiece line2V, ArticlePiece line3H, ArticlePiece line3V, ArticlePiece square, NoteSlip note)
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
            data.FindProperty("dot").objectReferenceValue = dot;
            data.FindProperty("line2H").objectReferenceValue = line2H;
            data.FindProperty("line2V").objectReferenceValue = line2V;
            data.FindProperty("line3H").objectReferenceValue = line3H;
            data.FindProperty("line3V").objectReferenceValue = line3V;
            data.FindProperty("square").objectReferenceValue = square;
            data.FindProperty("note").objectReferenceValue = note;
            data.FindProperty("desk").objectReferenceValue = desk;
            data.ApplyModifiedPropertiesWithoutUndo();

            var newspaper = desk.GetComponent<NewspaperDesk>();
            if (newspaper != null)
            {
                var deskData = new SerializedObject(newspaper);
                deskData.FindProperty("dot").objectReferenceValue = dot;
                deskData.FindProperty("line2H").objectReferenceValue = line2H;
                deskData.FindProperty("line2V").objectReferenceValue = line2V;
                deskData.FindProperty("line3H").objectReferenceValue = line3H;
                deskData.FindProperty("line3V").objectReferenceValue = line3V;
                deskData.FindProperty("square").objectReferenceValue = square;
                deskData.ApplyModifiedPropertiesWithoutUndo();
            }

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
