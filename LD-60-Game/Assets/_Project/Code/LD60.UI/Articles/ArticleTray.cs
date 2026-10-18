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

        public bool Contains(Vector2 deskPoint)
        {
            var center = (Vector2)transform.localPosition;
            var half = new Vector2(Mathf.Abs(transform.localScale.x), Mathf.Abs(transform.localScale.y)) * 0.5f;
            return Mathf.Abs(deskPoint.x - center.x) <= half.x && Mathf.Abs(deskPoint.y - center.y) <= half.y;
        }

        public void Release(ArticlePiece piece)
        {
            if (piece == null)
                return;

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
            var width = Mathf.Abs(transform.localScale.x);
            var height = Mathf.Abs(transform.localScale.y);
            var x = transform.localPosition.x + Random.Range(-width * 0.34f, width * 0.34f);
            var y = transform.localPosition.y + Random.Range(-height * 0.32f, height * 0.32f);
            var depth = -0.08f;
            piece.transform.SetParent(transform.parent, false);
            piece.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-24f, 24f));
            piece.transform.localScale = Vector3.one * slotScale;
            piece.transform.localPosition = new Vector3(x, y, depth);
        }
    }
}
