using System.Collections.Generic;
using DG.Tweening;
using LD60.Gameplay.Articles;
using UnityEngine;
using UnityEngine.UI;

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

        private Tween _motion;

        private void Awake()
        {
            if (!Application.isPlaying)
                return;

            EnsureShadow();
        }

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

        public void SetGhost(bool ghost)
        {
            var alpha = ghost ? 0.4f : 1f;
            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var color = renderers[i].color;
                var shadow = renderers[i].gameObject.name == "Shadow";
                color.a = shadow ? (ghost ? 0.16f : 0.42f) : alpha;
                renderers[i].color = color;
            }

            Fade(one, alpha);
            Fade(two, alpha);
            Fade(three, alpha);
        }

        public void StopMotion()
        {
            if (_motion == null)
                return;

            _motion.Kill();
            _motion = null;
        }

        public void PlayLift()
        {
            if (!isActiveAndEnabled)
                return;

            StopMotion();
            var spin = transform.DOLocalRotateQuaternion(Quaternion.identity, 0.2f).SetEase(Ease.OutQuad);
            var shadow = transform.Find("Shadow");
            if (shadow == null)
            {
                _motion = spin.SetTarget(this);
                return;
            }

            _motion = DOTween.Sequence()
                .SetTarget(this)
                .Join(spin)
                .Join(shadow.DOLocalMove(ShadowOut(), 0.2f).SetEase(Ease.OutQuad));
        }

        public void PlayDrop()
        {
            if (!isActiveAndEnabled)
                return;

            StopMotion();
            var endPos = transform.localPosition;
            var endRot = transform.localRotation;
            var startPos = endPos;
            startPos.z -= 0.26f;
            transform.localPosition = startPos;
            transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
            var drop = DOTween.Sequence()
                .SetTarget(this)
                .Join(transform.DOLocalMove(endPos, 0.32f).SetEase(Ease.OutQuad))
                .Join(transform.DOLocalRotateQuaternion(endRot, 0.32f).SetEase(Ease.OutQuad));
            var shadow = transform.Find("Shadow");
            if (shadow != null)
                drop.Join(shadow.DOLocalMove(ShadowUnder(), 0.32f).SetEase(Ease.OutQuad));
            _motion = drop;
        }

        public void PlayDropOnto(Vector3 worldPos, Quaternion worldRot, float worldScale, TweenCallback done)
        {
            if (!isActiveAndEnabled)
            {
                if (done != null)
                    done();
                return;
            }

            StopMotion();
            var parent = transform.parent;
            var endPos = parent != null ? parent.InverseTransformPoint(worldPos) : worldPos;
            var endRot = parent != null ? Quaternion.Inverse(parent.rotation) * worldRot : worldRot;
            var startPos = transform.localPosition;
            startPos.z -= 0.26f;
            transform.localPosition = startPos;
            transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
            var drop = DOTween.Sequence()
                .SetTarget(this)
                .Join(transform.DOLocalMove(endPos, 0.32f).SetEase(Ease.OutQuad))
                .Join(transform.DOLocalRotateQuaternion(endRot, 0.32f).SetEase(Ease.OutQuad))
                .Join(transform.DOScale(Vector3.one * worldScale, 0.32f).SetEase(Ease.OutQuad));
            var shadow = transform.Find("Shadow");
            if (shadow != null)
                drop.Join(shadow.DOLocalMove(ShadowUnder(), 0.32f).SetEase(Ease.OutQuad));
            if (done != null)
                drop.OnComplete(done);
            _motion = drop;
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

                renderer.sortingOrder = renderer.gameObject.name == "Shadow" ? order : order + 1;
                layer = renderer.sortingLayerID;
                drawn = true;
            }

            if (drawn)
                order += 2;

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

        private void EnsureShadow()
        {
            if (transform.Find("Shadow") != null)
                return;

            var paper = transform.Find("Paper");
            var source = paper != null ? paper.GetComponent<SpriteRenderer>() : null;
            if (source == null || source.sprite == null)
                return;

            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(transform, false);
            shadow.transform.localRotation = Quaternion.identity;
            shadow.transform.localPosition = paper.localPosition + new Vector3(0f, 0f, 0.03f);
            shadow.transform.localScale = paper.localScale;
            var renderer = shadow.AddComponent<SpriteRenderer>();
            renderer.sprite = source.sprite;
            renderer.sharedMaterial = source.sharedMaterial;
            renderer.color = new Color(0.05f, 0.04f, 0.03f, 0.42f);
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder - 1;
        }

        private Vector3 ShadowUnder()
        {
            var paper = transform.Find("Paper");
            var origin = paper != null ? paper.localPosition : Vector3.zero;
            origin.z += 0.03f;
            return origin;
        }

        private Vector3 ShadowOut()
        {
            return ShadowUnder() + new Vector3(0.07f, -0.08f, 0f);
        }

        private void OnDisable()
        {
            StopMotion();
        }

        private static void Fade(ArticleFace face, float alpha)
        {
            if (face == null)
                return;

            var group = face.GetComponent<CanvasGroup>();
            if (group == null)
                group = face.gameObject.AddComponent<CanvasGroup>();
            group.alpha = alpha;
            group.blocksRaycasts = false;
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
