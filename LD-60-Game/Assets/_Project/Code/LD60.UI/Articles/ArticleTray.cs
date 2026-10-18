using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LD60.UI
{
    public sealed class ArticleTray : MonoBehaviour
    {
        [SerializeField] private string title = "Save";
        [SerializeField] private int capacity;
        [SerializeField] private float slotScale = 0.4f;
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
            var sprite = GetComponent<SpriteRenderer>();
            if (sprite == null || sprite.sprite == null)
                return false;

            var local = transform.InverseTransformPoint(world);
            var center = sprite.sprite.bounds.center;
            var extents = sprite.sprite.bounds.extents;
            return Mathf.Abs(local.x - center.x) <= extents.x && Mathf.Abs(local.y - center.y) <= extents.y;
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
            var sprite = GetComponent<SpriteRenderer>();
            var extent = sprite != null && sprite.sprite != null
                ? sprite.sprite.bounds.extents
                : new Vector3(0.5f, 0.5f, 0f);
            piece.transform.SetParent(transform, false);
            piece.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-24f, 24f));
            var parentScale = transform.lossyScale;
            piece.transform.localScale = new Vector3(
                slotScale / Mathf.Max(0.01f, Mathf.Abs(parentScale.x)),
                slotScale / Mathf.Max(0.01f, Mathf.Abs(parentScale.y)),
                slotScale / Mathf.Max(0.01f, Mathf.Abs(parentScale.z)));
            piece.transform.localPosition = new Vector3(
                Random.Range(-extent.x * 0.55f, extent.x * 0.55f),
                Random.Range(-extent.y * 0.5f, extent.y * 0.5f),
                -0.05f);
        }
    }
}
