using LD60.UI;
using UnityEditor;
using UnityEngine;

namespace LD60.Editor
{
    public static class ArticleStyleSetup
    {
        private const string StylesPath = "Assets/_Project/Data/Articles/ArticleStyles.asset";
        private const string PrefabsPath = "Assets/_Project/Prefabs/Articles";

        [MenuItem("LD60/Repair Article Styles")]
        public static void RepairMissing()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data/Articles"))
                return;
            var styles = AssetDatabase.LoadAssetAtPath<ArticleStyles>(StylesPath);
            if (styles == null)
            {
                styles = ScriptableObject.CreateInstance<ArticleStyles>();
                AssetDatabase.CreateAsset(styles, StylesPath);
            }

            var data = new SerializedObject(styles);
            var kinds = new[] { "report", "politics", "social", "ad" };
            var colors = new[] { "Green", "Blue", "Red", "Black" };
            var fields = new[] { "dot", "line2H", "line2V", "line3H", "line3V", "square", "banner5x2" };
            var sizes = new[] { "1x1_100x102", "2x1_200x100", "1x2_100x200", "3x1_300x100", "1x3_100x300", "2x2_200x200", "5x2_500x202" };
            for (var kind = 0; kind < kinds.Length; kind++)
            {
                for (var shape = 0; shape < fields.Length; shape++)
                {
                    var property = data.FindProperty(kinds[kind]).FindPropertyRelative(fields[shape]);
                    if (property.objectReferenceValue != null)
                        continue;
                    var color = shape == 6 ? "Black" : colors[kind];
                    var path = $"Assets/_Project/Art/Articles/{color}/{color}_{sizes[shape]}.png";
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        if (asset is Sprite sprite)
                        {
                            property.objectReferenceValue = sprite;
                            break;
                        }
                    }
                }
            }
            if (data.ApplyModifiedPropertiesWithoutUndo())
                AssetDatabase.SaveAssetIfDirty(styles);

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsPath }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var piece = asset.GetComponent<ArticlePiece>();
                if (piece == null || new SerializedObject(piece).FindProperty("styles").objectReferenceValue != null)
                    continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var config = new SerializedObject(root.GetComponent<ArticlePiece>());
                    config.FindProperty("styles").objectReferenceValue = styles;
                    config.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }
    }
}
