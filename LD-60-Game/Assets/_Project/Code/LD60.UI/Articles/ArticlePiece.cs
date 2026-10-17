using System.Collections.Generic;
using LD60.Gameplay.Articles;
using TMPro;
using UnityEngine;

namespace LD60.UI
{
    [ExecuteAlways]
    public sealed class ArticlePiece : MonoBehaviour
    {
        [SerializeField] private ArticleDefinition article;
        [SerializeField] private SpriteRenderer picture;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text body;

        public ArticleDefinition Article => article;

        private void OnEnable()
        {
            Refresh();
        }

        private void OnValidate()
        {
            Refresh();
        }

        public void Bind(ArticleDefinition value)
        {
            article = value;
            Refresh();
        }

        public void Refresh()
        {
            var debug = ArticleDebugView.ShowValues && article != null;
            if (title != null)
                title.text = debug
                    ? article.Id + "\n" + article.Kind + "  " + article.Shape
                    : article != null ? article.Title : string.Empty;
            if (body != null)
                body.text = debug ? Values() : article != null ? article.Body : string.Empty;

            var showPicture = !debug && article != null && article.Picture != null;
            if (picture != null)
            {
                picture.gameObject.SetActive(showPicture);
                if (showPicture)
                {
                    picture.sprite = article.Picture;
                    picture.color = Color.white;
                }
            }

            Layout(showPicture);
        }

        private string Values()
        {
            return "fill " + article.Fill.ToString("0.##")
                + "\npolitics " + article.Politics.ToString("0.##")
                + "\nsocial " + article.Social.ToString("0.##")
                + "\nmoney " + article.Money.ToString("0.##");
        }

        private void Layout(bool pictureOn)
        {
            var cells = Cells();
            if (cells.Count == 0 || title == null || body == null)
                return;

            var textCells = cells;
            if (pictureOn && picture != null)
            {
                FitSprite(picture, cells[0]);
                textCells = new List<CellBox>(cells);
                textCells.RemoveAt(0);
            }

            if (textCells.Count == 0)
                textCells = cells;

            var top = Rows(textCells);
            FitText(title, BoundsOf(top[0]));
            if (top.Count == 1)
            {
                var band = BoundsOf(top[0]);
                FitText(title, new Rect(band.x, band.y + band.height * 0.62f, band.width, band.height * 0.38f));
                FitText(body, new Rect(band.x, band.y, band.width, band.height * 0.62f));
                return;
            }

            FitText(body, BoundsOf(BodyCells(top)));
        }

        private List<List<CellBox>> Rows(List<CellBox> cells)
        {
            var rows = new List<List<CellBox>>();
            for (var i = 0; i < cells.Count; i++)
            {
                if (rows.Count == 0 || cells[i].Center.y < rows[rows.Count - 1][0].Center.y - cells[i].Size.y * 0.45f)
                    rows.Add(new List<CellBox>());
                rows[rows.Count - 1].Add(cells[i]);
            }

            return rows;
        }

        private static List<CellBox> BodyCells(List<List<CellBox>> rows)
        {
            var rest = new List<CellBox>();
            for (var i = 1; i < rows.Count; i++)
                rest.AddRange(rows[i]);

            var sameSpan = true;
            var first = Span(rows[1]);
            for (var i = 2; i < rows.Count; i++)
            {
                var span = Span(rows[i]);
                if (Mathf.Abs(span.x - first.x) > 0.2f || Mathf.Abs(span.y - first.y) > 0.2f)
                    sameSpan = false;
            }

            if (sameSpan)
                return rest;

            var bestX = rest[0].Center.x;
            var best = 1;
            for (var i = 0; i < rest.Count; i++)
            {
                var count = 0;
                for (var j = 0; j < rest.Count; j++)
                {
                    if (Mathf.Abs(rest[j].Center.x - rest[i].Center.x) < rest[i].Size.x * 0.45f)
                        count++;
                }

                if (count > best)
                {
                    best = count;
                    bestX = rest[i].Center.x;
                }
            }

            var column = new List<CellBox>();
            for (var i = 0; i < rest.Count; i++)
            {
                if (Mathf.Abs(rest[i].Center.x - bestX) < rest[i].Size.x * 0.45f)
                    column.Add(rest[i]);
            }

            return column;
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

        private static Vector2 Span(List<CellBox> row)
        {
            var bounds = BoundsOf(row);
            return new Vector2(bounds.xMin, bounds.xMax);
        }

        private List<CellBox> Cells()
        {
            var cells = new List<CellBox>();
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.name != "Cell")
                    continue;
                var scale = child.localScale;
                cells.Add(new CellBox(child.localPosition, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y))));
            }

            cells.Sort((a, b) => b.Center.y.CompareTo(a.Center.y));
            return cells;
        }

        private static Rect BoundsOf(List<CellBox> cells)
        {
            var first = cells[0];
            var min = first.Center - first.Size * 0.5f;
            var max = first.Center + first.Size * 0.5f;
            for (var i = 1; i < cells.Count; i++)
            {
                var cellMin = cells[i].Center - cells[i].Size * 0.5f;
                var cellMax = cells[i].Center + cells[i].Size * 0.5f;
                min = Vector2.Min(min, cellMin);
                max = Vector2.Max(max, cellMax);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void FitSprite(SpriteRenderer renderer, CellBox cell)
        {
            renderer.transform.localPosition = new Vector3(cell.Center.x, cell.Center.y, -0.02f);
            renderer.transform.localScale = new Vector3(cell.Size.x * 0.78f, cell.Size.y * 0.78f, 1f);
        }

        private static void FitText(TMP_Text text, Rect area)
        {
            text.gameObject.SetActive(true);
            var rect = text.rectTransform;
            rect.localPosition = new Vector3(area.center.x, area.center.y, -0.03f);
            rect.sizeDelta = new Vector2(area.width * 0.92f, area.height * 0.9f);
        }

        private readonly struct CellBox
        {
            public readonly Vector2 Center;
            public readonly Vector2 Size;

            public CellBox(Vector3 center, Vector2 size)
            {
                Center = center;
                Size = size;
            }
        }
    }
}
