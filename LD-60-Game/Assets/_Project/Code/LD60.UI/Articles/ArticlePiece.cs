using System.Collections.Generic;
using LD60.Gameplay.Articles;
using UnityEngine;

namespace LD60.UI
{
    [ExecuteAlways]
    public sealed class ArticlePiece : MonoBehaviour
    {
        [SerializeField] private ArticleDefinition article;
        [SerializeField] private ArticleFace one;
        [SerializeField] private ArticleFace two;
        [SerializeField] private ArticleFace three;

        public ArticleDefinition Article => article;

        private void OnEnable()
        {
            Refresh();
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled || !InScene())
                return;
            Refresh();
        }

        public void Bind(ArticleDefinition value)
        {
            article = value;
            Refresh();
        }

        public void Refresh()
        {
            if (!InScene())
                return;

            var debug = ArticleDebugView.ShowValues && article != null;
            var slot = !debug && article != null && article.PictureLayout != null ? article.PictureLayout.Slot : 1;
            var headline = debug
                ? article.Id + "\n" + article.Kind + "  " + article.Shape
                : article != null ? article.Title : string.Empty;
            var copy = debug ? Values() : article != null ? article.Body : string.Empty;
            var sprite = !debug && article != null ? article.Picture : null;

            Show(one, slot == 1, headline, copy, null);
            Show(two, slot == 2, headline, copy, sprite);
            Show(three, slot == 3, headline, copy, sprite);
        }

        public void CopyCells(List<Vector2> centers)
        {
            centers.Clear();
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.name != "Cell")
                    continue;
                centers.Add(child.localPosition);
            }
        }

        public static int ApplyDrawOrder(GameObject root, int order)
        {
            if (root == null)
                return order;

            var layer = 0;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var drawn = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.GetComponent<TMPro.TMP_SubMesh>() != null)
                    continue;
                if (renderer.GetComponent<TMPro.TextMeshPro>() != null)
                    continue;

                renderer.sortingOrder = order;
                layer = renderer.sortingLayerID;
                drawn = true;
            }

            if (drawn)
                order++;

            for (var i = 0; i < renderers.Length; i++)
            {
                var text = renderers[i] != null ? renderers[i].GetComponent<TMPro.TextMeshPro>() : null;
                if (text == null)
                    continue;

                text.sortingOrder = order;
                order++;
            }

            var canvases = root.GetComponentsInChildren<Canvas>(true);
            var stacked = false;
            for (var i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] == null)
                    continue;

                canvases[i].overrideSorting = true;
                if (layer != 0)
                    canvases[i].sortingLayerID = layer;
                canvases[i].sortingOrder = order;
                stacked = true;
            }

            if (stacked)
                order++;

            return order;
        }

        private static void Show(ArticleFace face, bool on, string headline, string copy, Sprite sprite)
        {
            if (face == null)
                return;

            face.gameObject.SetActive(on);
            if (on)
                face.Apply(headline, copy, sprite);
        }

        private bool InScene()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(this))
                return false;
#endif
            return gameObject.scene.IsValid();
        }

        private string Values()
        {
            return "fill " + article.Fill.ToString("0.##")
                + "\npolitics " + article.Politics.ToString("0.##")
                + "\nsocial " + article.Social.ToString("0.##")
                + "\nmoney " + article.Money.ToString("0.##");
        }
    }
}
