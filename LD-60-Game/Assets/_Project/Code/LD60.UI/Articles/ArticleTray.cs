using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LD60.UI
{
    public sealed class ArticleTray : MonoBehaviour
    {
        [SerializeField] private string title = "Save";
        [SerializeField] private int capacity;
        [SerializeField] private float slotScale = 1.15f;
        [SerializeField] private TMP_Text label;

        public float SlotScale => slotScale;

        private readonly List<ArticlePiece> _pieces = new();

        public IReadOnlyList<ArticlePiece> Pieces => _pieces;

        private void Awake()
        {
            var sprite = GetComponent<SpriteRenderer>();
            if (sprite == null || sprite.sprite == null)
                return;

            var box = GetComponent<BoxCollider>();
            if (box == null)
                box = gameObject.AddComponent<BoxCollider>();

            var scale = transform.lossyScale;
            var size = sprite.sprite.bounds.size;
            size.z = 0.4f / Mathf.Max(0.01f, Mathf.Abs(scale.z));
            box.center = sprite.sprite.bounds.center;
            box.size = size;
        }

        public void Clear()
        {
            _pieces.Clear();
            RefreshLabel();
        }

        public int CountId(string id)
        {
            var count = 0;
            for (var i = 0; i < _pieces.Count; i++)
            {
                var article = _pieces[i] != null ? _pieces[i].Article : null;
                if (article != null && string.Equals(article.Id, id, System.StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        public bool Holds(ArticlePiece piece)
        {
            return piece != null && _pieces.Contains(piece);
        }

        public bool Contains(Vector3 world)
        {
            if (!TryArea(out var min, out var max))
                return false;

            var local = transform.InverseTransformPoint(world);
            return local.x >= min.x && local.x <= max.x && local.y >= min.y && local.y <= max.y;
        }

        public void Release(ArticlePiece piece)
        {
            if (piece == null)
                return;

            if (piece.transform.parent == transform && transform.parent != null)
                piece.transform.SetParent(transform.parent, true);

            _pieces.Remove(piece);
            RefreshLabel();
        }

        public bool TryAccept(ArticlePiece piece)
        {
            if (piece == null)
                return false;

            var already = _pieces.Contains(piece);
            if (!already && capacity > 0 && _pieces.Count >= capacity)
                return false;

            if (!already)
                _pieces.Add(piece);

            Toss(piece);
            RefreshLabel();
            return true;
        }

        public void RefreshLabel()
        {
            if (label == null)
                return;

            label.text = capacity > 0 ? title + "  " + _pieces.Count + "/" + capacity : title;
        }

        private void Toss(ArticlePiece piece)
        {
            piece.transform.SetParent(transform, false);
            piece.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-14f, 14f));
            var parentScale = transform.lossyScale;
            piece.transform.localScale = new Vector3(
                slotScale / Mathf.Max(0.01f, Mathf.Abs(parentScale.x)),
                slotScale / Mathf.Max(0.01f, Mathf.Abs(parentScale.y)),
                slotScale / Mathf.Max(0.01f, Mathf.Abs(parentScale.z)));

            if (!TryArea(out var areaMin, out var areaMax))
            {
                piece.transform.localPosition = new Vector3(0f, 0f, -0.05f);
                return;
            }

            var half = PieceHalf(piece);
            var min = areaMin + half;
            var max = areaMax - half;
            if (min.x > max.x)
            {
                var mid = (areaMin.x + areaMax.x) * 0.5f;
                min.x = mid;
                max.x = mid;
            }

            if (min.y > max.y)
            {
                var mid = (areaMin.y + areaMax.y) * 0.5f;
                min.y = mid;
                max.y = mid;
            }

            piece.transform.localPosition = new Vector3(Random.Range(min.x, max.x), Random.Range(min.y, max.y), -0.05f);
        }

        private Vector2 PieceHalf(ArticlePiece piece)
        {
            var paper = piece.transform.Find("Paper");
            var renderer = paper != null ? paper.GetComponent<SpriteRenderer>() : null;
            if (renderer == null || renderer.sprite == null)
                return new Vector2(0.5f, 0.5f);

            var extents = renderer.sprite.bounds.extents;
            var scale = paper.lossyScale;
            var tray = transform.lossyScale;
            var halfX = extents.x * Mathf.Abs(scale.x) / Mathf.Max(0.01f, Mathf.Abs(tray.x));
            var halfY = extents.y * Mathf.Abs(scale.y) / Mathf.Max(0.01f, Mathf.Abs(tray.y));
            var angle = piece.transform.localEulerAngles.z * Mathf.Deg2Rad;
            var cosine = Mathf.Abs(Mathf.Cos(angle));
            var sine = Mathf.Abs(Mathf.Sin(angle));
            return new Vector2(halfX * cosine + halfY * sine, halfX * sine + halfY * cosine);
        }

        private bool TryArea(out Vector2 min, out Vector2 max)
        {
            var sprite = GetComponent<SpriteRenderer>();
            if (sprite == null || sprite.sprite == null)
            {
                min = Vector2.zero;
                max = Vector2.zero;
                return false;
            }

            var bounds = sprite.sprite.bounds;
            min = bounds.min;
            max = bounds.max;
            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!TryArea(out var min, out var max))
                return;

            var z = -0.02f;
            var a = transform.TransformPoint(new Vector3(min.x, min.y, z));
            var b = transform.TransformPoint(new Vector3(max.x, min.y, z));
            var c = transform.TransformPoint(new Vector3(max.x, max.y, z));
            var d = transform.TransformPoint(new Vector3(min.x, max.y, z));
            Gizmos.color = new Color(0.35f, 0.9f, 0.4f, 0.95f);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }
#endif
    }
}
