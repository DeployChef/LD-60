using System.Collections.Generic;
using LD60.Gameplay.Articles;
using UnityEngine;

namespace LD60.UI
{
    public sealed class PageGrid : MonoBehaviour
    {
        [SerializeField] private int columns = 5;
        [SerializeField] private int rows = 5;
        [SerializeField] private float pitch = 1.05f;
        [SerializeField] private Vector2 origin = new(-2.6f, -2.175f);

        private readonly List<ArticlePiece> _placed = new();
        private readonly List<Vector2Int> _cells = new();
        private readonly List<Vector2> _locals = new();
        private readonly Dictionary<ArticlePiece, List<Vector2Int>> _footprints = new();

        public IReadOnlyList<ArticlePiece> Placed => _placed;

        public Vector2 CellCenter(int column, int row)
        {
            return origin + new Vector2(column * pitch, row * pitch);
        }

        public void Clear()
        {
            _footprints.Clear();
            _placed.Clear();
        }

        public void Release(ArticlePiece piece)
        {
            if (piece == null || !_footprints.ContainsKey(piece))
                return;

            _footprints.Remove(piece);
            _placed.Remove(piece);
        }

        public bool Covers(ArticlePiece piece)
        {
            Collect(piece, _cells);
            var inside = 0;
            for (var i = 0; i < _cells.Count; i++)
            {
                if (Inside(_cells[i]))
                    inside++;
            }

            return _cells.Count > 0 && inside * 2 >= _cells.Count;
        }

        public bool TryPlace(ArticlePiece piece)
        {
            Collect(piece, _cells);
            if (_cells.Count == 0)
                return false;

            for (var i = 0; i < _cells.Count; i++)
            {
                if (!Inside(_cells[i]) || Occupied(_cells[i], piece))
                    return false;
                for (var j = 0; j < i; j++)
                {
                    if (_cells[j] == _cells[i])
                        return false;
                }
            }

            if (!NeighborsAllow(piece, _cells))
                return false;

            var local = CellCenter(_cells[0].x, _cells[0].y) - _locals[0];
            piece.transform.localPosition = new Vector3(local.x, local.y, -0.08f);
            piece.transform.localRotation = Quaternion.identity;
            piece.transform.localScale = Vector3.one;

            if (!_footprints.TryGetValue(piece, out var stored))
            {
                stored = new List<Vector2Int>();
                _footprints.Add(piece, stored);
                _placed.Add(piece);
            }

            stored.Clear();
            stored.AddRange(_cells);
            return true;
        }

        private void Collect(ArticlePiece piece, List<Vector2Int> into)
        {
            into.Clear();
            _locals.Clear();
            piece.CopyCells(_locals);
            var desk = transform.parent != null ? transform.parent : transform;
            for (var i = 0; i < _locals.Count; i++)
            {
                var world = piece.transform.TransformPoint(_locals[i]);
                var local = desk.InverseTransformPoint(world);
                var grid = (new Vector2(local.x, local.y) - origin) / pitch;
                into.Add(new Vector2Int(Mathf.RoundToInt(grid.x), Mathf.RoundToInt(grid.y)));
            }
        }

        private bool Inside(Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < columns && cell.y < rows;
        }

        private bool Occupied(Vector2Int cell, ArticlePiece ignore)
        {
            foreach (var pair in _footprints)
            {
                if (pair.Key == ignore)
                    continue;
                if (pair.Value.Contains(cell))
                    return true;
            }

            return false;
        }

        private bool NeighborsAllow(ArticlePiece piece, List<Vector2Int> cells)
        {
            var article = piece.Article;
            if (article == null)
                return true;

            for (var i = 0; i < cells.Count; i++)
            {
                if (Blocked(article, cells[i] + Vector2Int.right, cells))
                    return false;
                if (Blocked(article, cells[i] + Vector2Int.left, cells))
                    return false;
                if (Blocked(article, cells[i] + Vector2Int.up, cells))
                    return false;
                if (Blocked(article, cells[i] + Vector2Int.down, cells))
                    return false;
            }

            return true;
        }

        private bool Blocked(ArticleDefinition article, Vector2Int cell, List<Vector2Int> own)
        {
            if (own.Contains(cell))
                return false;

            var other = PieceAt(cell);

            if (other == null || other.Article == null)
                return false;

            return Forbids(article, other.Article.Kind) || Forbids(other.Article, article.Kind);
        }

        private static bool Forbids(ArticleDefinition article, ArticleKind kind)
        {
            var banned = article.ForbiddenNeighbors;
            for (var i = 0; i < banned.Length; i++)
            {
                if (banned[i] == kind)
                    return true;
            }

            return false;
        }

        private ArticlePiece PieceAt(Vector2Int cell)
        {
            foreach (var pair in _footprints)
            {
                if (pair.Value.Contains(cell))
                    return pair.Key;
            }

            return null;
        }
    }
}
