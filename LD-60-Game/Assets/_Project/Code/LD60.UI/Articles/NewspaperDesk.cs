using System;
using System.Collections.Generic;
using System.Globalization;
using DG.Tweening;
using LD60.Core.Bus;
using LD60.Gameplay.Articles;
using LD60.Gameplay.Context;
using LD60.Gameplay.Story;
using LD60.UI.Audio;
using TMPro;
using UnityEngine;
using VContainer;

namespace LD60.UI
{
    public sealed class NewspaperDesk : MonoBehaviour
    {
        [SerializeField] private SpriteHintOutline firstStampHint;
        private bool _editionSubmitted;
        [SerializeField] private PageGrid page;
        [SerializeField] private ArticleTray save;
        [SerializeField] private float deskScale = 0.75f;
        [SerializeField] private Vector2 incomingCenter = new(-4.8f, 0.3f);
        [SerializeField] private float incomingWidth = 1.2f;
        [SerializeField] private float incomingHeight = 4.8f;
        [SerializeField] private float horizontalSpread = 0.12f;
        [SerializeField] private Vector2 notePosition = new(5.7f, 3.35f);
        [SerializeField] private TMP_Text stats;
        [SerializeField] private TextMeshPro noticePrefab;
        [SerializeField] private EditionPreview editionPreview;
        [SerializeField] private ArticlePiece dot;
        [SerializeField] private ArticlePiece line2H;
        [SerializeField] private ArticlePiece line2V;
        [SerializeField] private ArticlePiece line3H;
        [SerializeField] private ArticlePiece line3V;
        [SerializeField] private ArticlePiece square;
        [SerializeField] private ArticlePiece banner5x2;
        [SerializeField] private GameObject foodSandwich;
        [SerializeField] private GameObject foodPie;

        private readonly List<Vector2> _cells = new();
        private int _draw = 48;
        private Desk _desk;
        private StoryBook _story;
        private StoryContext _context;
        private ChapterDefinition _chapter;
        private TextMeshPro _notice;
        private SpriteRenderer _noticeFrame;
        private Tween _noticeFade;
        private IDisposable _day;
        private IDisposable _gift;

        [Inject]
        public void Construct(IMessageBus bus, Desk desk, StoryBook story, StoryContext context)
        {
            _desk = desk;
            _story = story;
            _context = context;
            _day?.Dispose();
            _gift?.Dispose();
            _day = bus.Subscribe<DayStarted>(OnDay);
            _gift = bus.Subscribe<ArticleGiven>(OnGift);
        }

        private void OnDestroy()
        {
            _noticeFade?.Kill();
            _day?.Dispose();
            _gift?.Dispose();
        }

        private void OnDisable()
        {
            HideNotice();
        }

        public void Hover(ArticlePiece piece)
        {
            if (piece == null || page == null)
                return;

            var issue = page.Inspect(piece);
            if (!page.Touches())
            {
                page.ClearPreview();
                piece.SetGhost(false);
                return;
            }

            page.Paint(issue);
            piece.SetGhost(true);
        }

        public void EndHover(ArticlePiece piece)
        {
            page?.ClearPreview();
            if (piece != null)
                piece.SetGhost(false);
        }

        public void Notify(string message, Vector3 world)
        {
            // Notices are runtime objects; editor previews must not become scene content.
            if (!Application.isPlaying)
                return;
            EnsureNotice();
            if (_notice == null)
                return;

            _notice.transform.position = world - transform.forward * 0.25f + transform.up * 0.9f;
            _notice.transform.rotation = transform.rotation;
            _notice.text = message ?? string.Empty;
            _noticeFade?.Kill();
            _notice.gameObject.SetActive(true);
            var preferred = _notice.GetPreferredValues(_notice.text, 6.3f, Mathf.Infinity);
            // Leave room for the serif glyphs extending beyond TMP's advance widths.
            _notice.rectTransform.sizeDelta = new Vector2(Mathf.Clamp(preferred.x + 0.12f, 0.8f, 6.5f), Mathf.Max(0.35f, preferred.y + 0.08f));
            if (_noticeFrame != null)
                _noticeFrame.size = _notice.rectTransform.sizeDelta + new Vector2(0.56f, 0.36f);
            _notice.ForceMeshUpdate();
            SetNoticeAlpha(1f);
            _noticeFade = DOTween.To(() => _notice.alpha, SetNoticeAlpha, 0f, 0.3f)
                .SetDelay(0.7f)
                .SetUpdate(true)
                .OnComplete(HideNotice)
                .SetTarget(this);
        }

        public void PlaceNote(NoteSlip slip)
        {
            if (slip == null)
                return;

            slip.transform.SetParent(transform, false);
            slip.transform.localRotation = Quaternion.identity;
            slip.transform.localScale = Vector3.one;
            slip.transform.localPosition = new Vector3(notePosition.x, notePosition.y, -0.12f);
            Raise(slip.gameObject);
        }

        public bool OverPage(Vector3 world)
        {
            return page != null && page.ContainsPoint(world);
        }

        public bool EditionReady(out string reason)
        {
            if (_chapter == null)
            {
                reason = "There is nothing to stamp.";
                return false;
            }

            var placedIds = page != null ? Ids(page.Placed) : null;
            return _chapter.Allows(_desk != null ? _desk.Fill : 0f, placedIds, out reason);
        }

        public void LeaveMark(Vector3 world, Sprite sprite, Material material, Vector3 size, int layer,
            bool preserveSpriteColor = false)
        {
            if (sprite == null)
                return;

            var mark = new GameObject("StampMark");
            mark.transform.SetParent(transform, false);
            var local = transform.InverseTransformPoint(world);
            local.z = -0.09f;
            mark.transform.localPosition = local;
            mark.transform.localRotation = Quaternion.identity;
            mark.transform.localScale = size;
            var renderer = mark.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.color = preserveSpriteColor ? Color.white : new Color(0.78f, 0.1f, 0.08f, 0.42f);
            renderer.sortingLayerID = layer;
            renderer.sortingOrder = _draw;
            _draw++;
            mark.AddComponent<StampMark>();
            PressMark(mark.transform);
        }

        public void CommitStamp()
        {
            if (editionPreview != null && editionPreview.IsOpen)
                return;
            _editionSubmitted = true;
            if (firstStampHint != null)
                firstStampHint.SetHighlighted(false);
            HideNotice();
            if (editionPreview != null && editionPreview.Show(this, () => _desk?.Stamp()))
                return;
            _desk?.Stamp();
        }

        public void PrepareLift(ArticlePiece piece)
        {
            if (piece == null)
                return;

            page?.Release(piece);
            save?.Release(piece);
            piece.StopMotion();
            if (piece.transform.parent != transform)
                piece.transform.SetParent(transform, true);
            piece.transform.localScale = Vector3.one;
            piece.PlayLift();
            Publish();
        }

        public bool Settle(ArticlePiece piece, out string reason)
        {
            reason = null;
            if (piece == null || page == null)
                return true;

            EndHover(piece);
            piece.StopMotion();
            piece.transform.localRotation = Quaternion.identity;
            piece.transform.localScale = Vector3.one;
            var point = CenterOf(piece);
            if (save != null && save.Contains(point))
            {
                var from = piece.transform.position;
                if (!save.TryAccept(piece))
                {
                    reason = "Save is full.";
                    return false;
                }

                var restPos = piece.transform.localPosition;
                var restRot = piece.transform.localRotation;
                var restScale = piece.transform.localScale;
                var worldPos = piece.transform.position;
                var worldRot = piece.transform.rotation;
                var tray = save.transform;
                piece.transform.SetParent(transform, false);
                piece.transform.localScale = Vector3.one;
                piece.transform.localRotation = Quaternion.identity;
                piece.transform.position = from;
                Raise(piece.gameObject);
                piece.PlayDropOnto(worldPos, worldRot, save.SlotScale, () =>
                {
                    if (piece == null)
                        return;

                    piece.transform.SetParent(tray, false);
                    piece.transform.localPosition = restPos;
                    piece.transform.localRotation = restRot;
                    piece.transform.localScale = restScale;
                });
                SoundService.PlayPaperDrop();
                Publish();
                return true;
            }

            if (page.Covers(piece))
            {
                var issue = page.Inspect(piece);
                if (issue != PlaceIssue.None || !page.TryPlace(piece))
                {
                    reason = Line(issue);
                    return false;
                }

                Raise(piece.gameObject);
                piece.PlayDrop();
                SoundService.PlayPaperDrop();
                Publish();
                return true;
            }

            PlaceLoose(piece);
            Publish();
            return true;
        }

        public void PlaceLoose(ArticlePiece piece)
        {
            if (piece == null)
                return;

            if (piece.transform.parent != transform)
                piece.transform.SetParent(transform, true);

            piece.transform.localScale = Vector3.one * deskScale;
            var local = piece.transform.localPosition;
            local.z = -0.08f;
            piece.transform.localPosition = local;
            piece.transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-12f, 12f));
            Raise(piece.gameObject);
            piece.PlayDrop();
            SoundService.PlayPaperDrop();
        }

        public void Deliver(ArticlePiece piece, int index, int count)
        {
            if (piece == null)
                return;

            if (piece.transform.parent != transform)
                piece.transform.SetParent(transform, false);

            piece.transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-14f, 14f));
            piece.transform.localScale = Vector3.one * deskScale;
            piece.transform.localPosition = IncomingSpot(index, count);
            Raise(piece.gameObject);
            piece.PlayDrop();
        }

        public void Raise(GameObject item)
        {
            if (item == null)
                return;

            var order = _draw;
            _draw = ArticlePiece.ApplyDrawOrder(item, order) + 1;
            var drag = item.GetComponent<DeskDrag>();
            if (drag != null)
                drag.Hold(order);
        }

        private void OnDay(DayStarted message)
        {
            _editionSubmitted = false;
            HideNotice();
            var kept = save != null ? new List<ArticlePiece>(save.Pieces) : new List<ArticlePiece>();
            _chapter = message.Chapter;
            if (page != null)
            {
                page.SetDayRules(_chapter != null ? _chapter.Neighbors : null);
                page.Clear();
            }

            var marks = GetComponentsInChildren<StampMark>(true);
            for (var i = 0; i < marks.Length; i++)
            {
                DOTween.Kill(marks[i].transform);
                Destroy(marks[i].gameObject);
            }

            var pieces = GetComponentsInChildren<ArticlePiece>(true);
            for (var i = 0; i < pieces.Length; i++)
            {
                if (kept.Contains(pieces[i]))
                    continue;

                Destroy(pieces[i].gameObject);
            }

            var notes = GetComponentsInChildren<NoteSlip>(true);
            for (var i = 0; i < notes.Length; i++)
                Destroy(notes[i].gameObject);

            if (foodSandwich != null)
                foodSandwich.SetActive(false);
            if (foodPie != null)
                foodPie.SetActive(false);

            RestoreKept();
            Publish();
        }

        private void OnGift(ArticleGiven message)
        {
            if (string.Equals(message.ArticleId, "food-sandwich", StringComparison.OrdinalIgnoreCase))
            {
                if (foodSandwich != null)
                    foodSandwich.SetActive(true);
                return;
            }

            if (string.Equals(message.ArticleId, "food-pie", StringComparison.OrdinalIgnoreCase))
            {
                if (foodPie != null)
                    foodPie.SetActive(true);
                return;
            }

            var article = _story != null ? _story.FindArticle(message.ArticleId) : null;
            var source = Form(article);
            if (article == null || source == null)
            {
                Debug.LogWarning($"[Story] No article '{message.ArticleId}' to give.");
                return;
            }

            var piece = Instantiate(source, transform);
            piece.Bind(article);
            piece.transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-8f, 8f));
            piece.transform.localPosition = new Vector3(-4.6f, 0.2f, -0.12f);
            PlaceLoose(piece);
        }

        private void RestoreKept()
        {
            if (save == null || _context == null || _story == null)
                return;

            var ids = _context.KeptArticleIds;
            for (var i = 0; i < ids.Count; i++)
            {
                if (save.CountId(ids[i]) > 0)
                    continue;

                var article = _story.FindArticle(ids[i]);
                var source = Form(article);
                if (article == null || source == null)
                    continue;

                var piece = Instantiate(source, transform);
                piece.Bind(article);
                if (save.TryAccept(piece))
                    Raise(piece.gameObject);
            }
        }

        private ArticlePiece Form(ArticleDefinition article)
        {
            if (article == null)
                return null;

            switch (article.Shape)
            {
                case ArticleShape.Dot: return dot;
                case ArticleShape.Line2H: return line2H;
                case ArticleShape.Line2V: return line2V;
                case ArticleShape.Line3H: return line3H;
                case ArticleShape.Line3V: return line3V;
                case ArticleShape.Banner5x2: return banner5x2 != null ? banner5x2 : square;
                default: return square;
            }
        }

        private void Publish()
        {
            var fill = 0f;
            var politics = 0f;
            var social = 0f;
            if (page != null)
            {
                var placed = page.Placed;
                for (var i = 0; i < placed.Count; i++)
                {
                    var article = placed[i] != null ? placed[i].Article : null;
                    if (article == null)
                        continue;

                    fill += article.Fill;
                    politics += article.Politics;
                    social += article.Social;
                }
            }

            if (stats != null)
            {
                stats.text = "Fill  " + fill.ToString("0.00", CultureInfo.InvariantCulture)
                    + "    |    Politics  " + politics.ToString("0.00", CultureInfo.InvariantCulture)
                    + "    |    Social  " + social.ToString("0.00", CultureInfo.InvariantCulture);
            }

            if (save != null)
            {
                save.RefreshLabel();
                if (_desk != null)
                    _desk.SetKeptArticles(Ids(save.Pieces));
            }

            if (_context != null)
            {
                _context.SetParameter("politics", politics);
                _context.SetParameter("social", social);
            }

            if (page != null && _desk != null)
                _desk.SetPlacedArticles(Ids(page.Placed));

            _desk?.SetScores(fill, politics, social);
            if (firstStampHint != null)
                firstStampHint.SetHighlighted(!_editionSubmitted && page != null && page.Placed.Count > 0 && EditionReady(out _));
        }

        private static List<string> Ids(IReadOnlyList<ArticlePiece> pieces)
        {
            var ids = new List<string>();
            for (var i = 0; i < pieces.Count; i++)
            {
                var article = pieces[i] != null ? pieces[i].Article : null;
                if (article != null)
                    ids.Add(article.Id);
            }

            return ids;
        }

        private static string Line(PlaceIssue issue)
        {
            switch (issue)
            {
                case PlaceIssue.Taken: return "That space is taken.";
                case PlaceIssue.Beside: return "These cannot sit side by side.";
                case PlaceIssue.Exclusive: return "These stories cannot be published together.";
                default: return "It does not fit there.";
            }
        }

        private void EnsureNotice()
        {
            if (_notice != null)
                return;

            if (noticePrefab != null)
            {
                _notice = Instantiate(noticePrefab, transform, false);
                _notice.name = "Notice";
                _notice.gameObject.hideFlags = HideFlags.DontSave;
                _noticeFrame = _notice.GetComponentInChildren<SpriteRenderer>();
                var sampleRenderer = page != null ? page.GetComponentInChildren<SpriteRenderer>() : null;
                if (sampleRenderer != null)
                    _notice.sortingLayerID = sampleRenderer.sortingLayerID;
                _notice.sortingOrder = 4200;
                if (_noticeFrame != null)
                {
                    _noticeFrame.sortingLayerID = _notice.sortingLayerID;
                    _noticeFrame.sortingOrder = 4199;
                }
                SetNoticeAlpha(0f);
                _notice.gameObject.SetActive(false);
                return;
            }

            var font = stats != null ? stats.font : TMP_Settings.defaultFontAsset;
            var go = new GameObject("Notice");
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(transform, false);
            _notice = go.AddComponent<TextMeshPro>();
            _notice.font = font;
            _notice.fontStyle = FontStyles.Bold;
            _notice.alignment = TextAlignmentOptions.Center;
            _notice.enableAutoSizing = false;
            _notice.fontSize = 2.4f;
            _notice.fontSizeMin = 0.8f;
            _notice.fontSizeMax = 2.4f;
            _notice.color = new Color(0.62f, 0.1f, 0.08f, 1f);
            _notice.rectTransform.sizeDelta = new Vector2(8f, 1.1f);
            _notice.textWrappingMode = TextWrappingModes.Normal;
            _notice.overflowMode = TextOverflowModes.Truncate;
            _notice.raycastTarget = false;
            _notice.alpha = 0f;
            var sample = page != null ? page.GetComponentInChildren<SpriteRenderer>() : null;
            if (sample != null)
                _notice.sortingLayerID = sample.sortingLayerID;
            _notice.sortingOrder = 4200;
            go.SetActive(false);
        }

        private void HideNotice()
        {
            _noticeFade?.Kill();
            _noticeFade = null;
            SetNoticeAlpha(0f);
            if (_notice != null)
                _notice.gameObject.SetActive(false);
        }

        private void SetNoticeAlpha(float alpha)
        {
            if (_notice != null)
                _notice.alpha = alpha;
            if (_noticeFrame != null)
            {
                var color = _noticeFrame.color;
                color.a = alpha;
                _noticeFrame.color = color;
            }
        }

        private Vector3 IncomingSpot(int index, int count)
        {
            var columns = count < 3 ? Mathf.Max(1, count) : 3;
            var rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));
            var column = index % columns;
            var row = index / columns;
            var x = incomingCenter.x + incomingWidth * ((column + 0.5f) / columns - 0.5f);
            var y = incomingCenter.y + incomingHeight * (0.5f - (row + 0.5f) / rows);
            x += UnityEngine.Random.Range(-horizontalSpread, horizontalSpread);
            y += UnityEngine.Random.Range(-0.22f, 0.22f);
            return new Vector3(x, y, -0.08f);
        }

        private Vector2 SurfaceHalf()
        {
            var surface = transform.Find("Surface");
            var renderer = surface != null ? surface.GetComponent<SpriteRenderer>() : null;
            if (renderer == null || renderer.sprite == null)
                return new Vector2(9.6f, 5.4f);

            var extents = renderer.sprite.bounds.extents;
            var scale = surface.lossyScale;
            var desk = transform.lossyScale;
            return new Vector2(
                extents.x * Mathf.Abs(scale.x) / Mathf.Max(0.01f, Mathf.Abs(desk.x)),
                extents.y * Mathf.Abs(scale.y) / Mathf.Max(0.01f, Mathf.Abs(desk.y)));
        }

        private static Vector2 NoteHalf(NoteSlip slip)
        {
            var renderer = slip.GetComponentInChildren<SpriteRenderer>();
            if (renderer == null || renderer.sprite == null)
                return new Vector2(1.25f, 0.72f);

            var extents = renderer.sprite.bounds.extents;
            var scale = renderer.transform.localScale;
            return new Vector2(extents.x * Mathf.Abs(scale.x), extents.y * Mathf.Abs(scale.y));
        }

        private static void PressMark(Transform mark)
        {
            if (mark == null)
                return;

            var size = mark.localScale;
            mark.localScale = size * 1.4f;
            DOTween.Sequence()
                .SetTarget(mark)
                .Append(mark.DOScale(size * 0.78f, 0.12f).SetEase(Ease.InQuad))
                .Append(mark.DOScale(size, 0.1f).SetEase(Ease.OutQuad));
        }

        private Vector3 CenterOf(ArticlePiece piece)
        {
            piece.CopyCells(_cells);
            if (_cells.Count == 0)
                return piece.transform.position;

            var sum = Vector3.zero;
            for (var i = 0; i < _cells.Count; i++)
                sum += piece.transform.TransformPoint(_cells[i]);

            return sum / _cells.Count;
        }
    }
}
