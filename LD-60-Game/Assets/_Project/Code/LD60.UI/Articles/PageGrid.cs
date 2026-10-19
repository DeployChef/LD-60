using System.Collections.Generic;
using LD60.Gameplay.Articles;
using LD60.Gameplay.Context;
using UnityEngine;

namespace LD60.UI
{
    public sealed class PageGrid : MonoBehaviour
    {
        private readonly List<ArticlePiece> _placed = new();
        private readonly List<Transform> _hits = new();
        private readonly List<Vector2> _locals = new();
        private readonly List<Slot> _slots = new();
        private readonly Dictionary<ArticlePiece, List<Transform>> _footprints = new();
        private readonly List<SpriteRenderer> _painted = new();
        private readonly List<Color> _paint = new();
        private NeighborRule[] _day = System.Array.Empty<NeighborRule>();
        private float _pitch = 1f;

        public IReadOnlyList<ArticlePiece> Placed => _placed;

        private void OnEnable()
        {
            ReadSlots();
        }

        public void Clear()
        {
            ClearPreview();
            _footprints.Clear();
            _placed.Clear();
        }

        public void SetDayRules(NeighborRule[] rules)
        {
            _day = rules ?? System.Array.Empty<NeighborRule>();
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
            if (_slots.Count == 0)
                ReadSlots();

            Collect(piece, _hits);
            var inside = 0;
            for (var i = 0; i < _hits.Count; i++)
            {
                if (_hits[i] != null)
                    inside++;
            }

            return _hits.Count > 0 && inside * 2 >= _hits.Count;
        }

        public PlaceIssue Inspect(ArticlePiece piece)
        {
            if (_slots.Count == 0)
                ReadSlots();

            Collect(piece, _hits);
            return Judge(piece, _hits);
        }

        public bool Touches()
        {
            for (var i = 0; i < _hits.Count; i++)
            {
                if (_hits[i] != null)
                    return true;
            }

            return false;
        }

        public bool ContainsPoint(Vector3 world)
        {
            if (_slots.Count == 0)
                ReadSlots();

            return Nearest(world, _pitch * 0.65f) != null;
        }

        public void Paint(PlaceIssue issue)
        {
            ClearPreview();
            var color = issue == PlaceIssue.None
                ? new Color(0.42f, 0.78f, 0.36f, 1f)
                : new Color(0.86f, 0.28f, 0.22f, 1f);
            for (var i = 0; i < _hits.Count; i++)
            {
                if (_hits[i] == null)
                    continue;

                var renderer = _hits[i].GetComponent<SpriteRenderer>();
                if (renderer == null || _painted.Contains(renderer))
                    continue;

                _painted.Add(renderer);
                _paint.Add(renderer.color);
                renderer.color = color;
            }
        }

        public void ClearPreview()
        {
            for (var i = 0; i < _painted.Count; i++)
            {
                if (_painted[i] != null)
                    _painted[i].color = _paint[i];
            }

            _painted.Clear();
            _paint.Clear();
        }

        public bool TryPlace(ArticlePiece piece)
        {
            if (_slots.Count == 0)
                ReadSlots();

            Collect(piece, _hits);
            if (Judge(piece, _hits) != PlaceIssue.None)
                return false;

            Snap(piece, _hits[0], _locals[0]);

            if (!_footprints.TryGetValue(piece, out var stored))
            {
                stored = new List<Transform>();
                _footprints.Add(piece, stored);
                _placed.Add(piece);
            }

            stored.Clear();
            stored.AddRange(_hits);
            return true;
        }

        private void ReadSlots()
        {
            _slots.Clear();
            var found = new List<Transform>();
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.name == "Slot")
                    found.Add(child);
            }

            if (found.Count == 0)
                return;

            _pitch = Pitch(found);
            var tolerance = _pitch * 0.45f;
            var columns = Bands(found, true, tolerance);
            var rows = Bands(found, false, tolerance);
            for (var i = 0; i < found.Count; i++)
            {
                var slot = found[i];
                var column = BandOf(columns, slot);
                var row = rows.Count - 1 - BandOf(rows, slot);
                _slots.Add(new Slot(slot, new Vector2Int(column, row)));
            }
        }

        private void Collect(ArticlePiece piece, List<Transform> into)
        {
            into.Clear();
            _locals.Clear();
            piece.CopyCells(_locals);
            var limit = _pitch * 0.55f;
            for (var i = 0; i < _locals.Count; i++)
            {
                var world = piece.transform.TransformPoint(_locals[i]);
                into.Add(Nearest(world, limit));
            }
        }

        private Transform Nearest(Vector3 world, float limit)
        {
            Transform best = null;
            var bestDistance = limit;
            for (var i = 0; i < _slots.Count; i++)
            {
                var distance = PlaneDistance(world, _slots[i].Transform.position);
                if (distance > bestDistance)
                    continue;
                bestDistance = distance;
                best = _slots[i].Transform;
            }

            return best;
        }

        private void Snap(ArticlePiece piece, Transform slot, Vector2 cell)
        {
            var desk = transform.parent != null ? transform.parent : transform;
            if (piece.transform.parent != desk)
                piece.transform.SetParent(desk, true);

            piece.transform.rotation = desk.rotation;
            // Pieces share the grid's parent, so they must also share its scale.
            // The paper may itself be scaled independently of the desk.
            piece.transform.localScale = transform.localScale;
            var delta = slot.position - piece.transform.TransformPoint(cell);
            var normal = desk.forward;
            delta -= normal * Vector3.Dot(delta, normal);
            piece.transform.position += delta;
            var local = piece.transform.localPosition;
            local.z = -0.08f;
            piece.transform.localPosition = local;
        }

        private bool Occupied(Transform slot, ArticlePiece ignore)
        {
            foreach (var pair in _footprints)
            {
                if (pair.Key == ignore)
                    continue;
                if (pair.Value.Contains(slot))
                    return true;
            }

            return false;
        }

        private bool NeighborsAllow(ArticlePiece piece, List<Transform> slots)
        {
            var article = piece.Article;
            if (article == null)
                return true;

            for (var i = 0; i < slots.Count; i++)
            {
                if (Blocked(article, Neighbor(slots[i], Vector2Int.right), slots))
                    return false;
                if (Blocked(article, Neighbor(slots[i], Vector2Int.left), slots))
                    return false;
                if (Blocked(article, Neighbor(slots[i], Vector2Int.up), slots))
                    return false;
                if (Blocked(article, Neighbor(slots[i], Vector2Int.down), slots))
                    return false;
            }

            return true;
        }

        private bool Blocked(ArticleDefinition article, Transform slot, List<Transform> own)
        {
            if (slot == null || own.Contains(slot))
                return false;

            var other = PieceAt(slot);
            if (other == null || other.Article == null)
                return false;

            if (DayForbids(article.Kind, other.Article.Kind))
                return true;

            return Forbids(article, other.Article.Kind) || Forbids(other.Article, article.Kind);
        }

        private PlaceIssue Judge(ArticlePiece piece, List<Transform> slots)
        {
            if (slots.Count == 0)
                return PlaceIssue.Blocked;

            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null)
                    return PlaceIssue.Blocked;
                if (Occupied(slots[i], piece))
                    return PlaceIssue.Taken;
                for (var j = 0; j < i; j++)
                {
                    if (slots[j] == slots[i])
                        return PlaceIssue.Blocked;
                }
            }

            if (!NeighborsAllow(piece, slots))
                return PlaceIssue.Beside;

            return PlaceIssue.None;
        }

        private bool DayForbids(ArticleKind left, ArticleKind right)
        {
            for (var i = 0; i < _day.Length; i++)
            {
                var rule = _day[i];
                if (rule == null)
                    continue;
                if ((rule.kind == left && rule.other == right) || (rule.kind == right && rule.other == left))
                    return true;
            }

            return false;
        }

        private Transform Neighbor(Transform slot, Vector2Int step)
        {
            var index = IndexOf(slot) + step;
            for (var i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Index == index)
                    return _slots[i].Transform;
            }

            return null;
        }

        private Vector2Int IndexOf(Transform slot)
        {
            for (var i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Transform == slot)
                    return _slots[i].Index;
            }

            return Vector2Int.zero;
        }

        private ArticlePiece PieceAt(Transform slot)
        {
            foreach (var pair in _footprints)
            {
                if (pair.Value.Contains(slot))
                    return pair.Key;
            }

            return null;
        }

        private float PlaneDistance(Vector3 world, Vector3 other)
        {
            var desk = transform.parent != null ? transform.parent : transform;
            var delta = world - other;
            var normal = desk.forward;
            delta -= normal * Vector3.Dot(delta, normal);
            // Slot pitch is measured in grid-local units.
            return transform.InverseTransformVector(delta).magnitude;
        }

        private static float Pitch(List<Transform> slots)
        {
            var gaps = new List<float>();
            for (var i = 0; i < slots.Count; i++)
            {
                var best = float.MaxValue;
                var origin = (Vector2)slots[i].localPosition;
                for (var j = 0; j < slots.Count; j++)
                {
                    if (i == j)
                        continue;
                    var distance = Vector2.Distance(origin, slots[j].localPosition);
                    if (distance > 0.05f && distance < best)
                        best = distance;
                }

                if (best < float.MaxValue)
                    gaps.Add(best);
            }

            gaps.Sort();
            return gaps.Count == 0 ? 1f : gaps[gaps.Count / 2];
        }

        private static List<List<Transform>> Bands(List<Transform> slots, bool horizontal, float tolerance)
        {
            var ordered = new List<Transform>(slots);
            ordered.Sort((a, b) =>
            {
                var left = a.localPosition;
                var right = b.localPosition;
                return horizontal ? left.x.CompareTo(right.x) : right.y.CompareTo(left.y);
            });

            var bands = new List<List<Transform>>();
            for (var i = 0; i < ordered.Count; i++)
            {
                var key = horizontal ? ordered[i].localPosition.x : ordered[i].localPosition.y;
                if (bands.Count == 0 || Mathf.Abs(key - BandKey(bands[bands.Count - 1], horizontal)) > tolerance)
                    bands.Add(new List<Transform>());
                bands[bands.Count - 1].Add(ordered[i]);
            }

            return bands;
        }

        private static int BandOf(List<List<Transform>> bands, Transform slot)
        {
            for (var i = 0; i < bands.Count; i++)
            {
                if (bands[i].Contains(slot))
                    return i;
            }

            return 0;
        }

        private static float BandKey(List<Transform> band, bool horizontal)
        {
            return horizontal ? band[0].localPosition.x : band[0].localPosition.y;
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

        private readonly struct Slot
        {
            public readonly Transform Transform;
            public readonly Vector2Int Index;

            public Slot(Transform transform, Vector2Int index)
            {
                Transform = transform;
                Index = index;
            }
        }
    }

    public enum PlaceIssue
    {
        None,
        Blocked,
        Taken,
        Beside
    }
}
