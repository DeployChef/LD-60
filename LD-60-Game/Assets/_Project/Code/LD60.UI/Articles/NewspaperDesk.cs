using System;
using System.Collections.Generic;
using System.Globalization;
using LD60.Core.Bus;
using LD60.Gameplay.Articles;
using LD60.Gameplay.Context;
using LD60.Gameplay.Story;
using TMPro;
using UnityEngine;
using VContainer;

namespace LD60.UI
{
    public sealed class NewspaperDesk : MonoBehaviour
    {
        [SerializeField] private PageGrid page;
        [SerializeField] private ArticleTray discard;
        [SerializeField] private ArticleTray save;
        [SerializeField] private TMP_Text stats;
        [SerializeField] private ArticlePiece square;
        [SerializeField] private ArticlePiece zed;
        [SerializeField] private ArticlePiece gamma;

        private readonly List<Vector2> _cells = new();
        private Desk _desk;
        private StoryBook _story;
        private StoryContext _context;
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
            _day?.Dispose();
            _gift?.Dispose();
        }

        public void PrepareLift(ArticlePiece piece)
        {
            if (piece == null)
                return;

            page?.Release(piece);
            discard?.Release(piece);
            save?.Release(piece);
            piece.transform.localRotation = Quaternion.identity;
            piece.transform.localScale = Vector3.one;
            Publish();
        }

        public bool Settle(ArticlePiece piece)
        {
            if (piece == null || page == null)
                return true;

            var point = CenterOf(piece);
            if (save != null && save.Contains(point))
            {
                if (!save.TryAccept(piece))
                    return false;

                Publish();
                return true;
            }

            if (discard != null && discard.Contains(point))
            {
                discard.TryAccept(piece);
                Publish();
                return true;
            }

            piece.transform.localRotation = Quaternion.identity;
            piece.transform.localScale = Vector3.one;
            if (page.Covers(piece))
            {
                if (!page.TryPlace(piece))
                    return false;

                Publish();
                return true;
            }

            piece.transform.localScale = Vector3.one;
            Publish();
            return true;
        }

        private void OnDay(DayStarted message)
        {
            var kept = save != null ? new List<ArticlePiece>(save.Pieces) : new List<ArticlePiece>();
            if (page != null)
                page.Clear();
            if (discard != null)
                discard.Clear();

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

            RestoreKept();
            Publish();
        }

        private void OnGift(ArticleGiven message)
        {
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
            piece.transform.localScale = Vector3.one;
            piece.transform.localPosition = new Vector3(-4.6f, 0.2f, -0.12f);
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
                save.TryAccept(piece);
            }
        }

        private ArticlePiece Form(ArticleDefinition article)
        {
            if (article == null)
                return null;

            switch (article.Shape)
            {
                case ArticleShape.Z: return zed;
                case ArticleShape.Gamma: return gamma;
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
                    + "\nPolitics  " + politics.ToString("0.00", CultureInfo.InvariantCulture)
                    + "\nSocial  " + social.ToString("0.00", CultureInfo.InvariantCulture);
            }

            if (save != null)
            {
                save.RefreshLabel();
                if (_desk != null)
                    _desk.SetKeptArticles(Ids(save.Pieces));
            }

            _desk?.SetScores(fill, politics, social);
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

        private Vector2 CenterOf(ArticlePiece piece)
        {
            piece.CopyCells(_cells);
            if (_cells.Count == 0)
                return piece.transform.localPosition;

            var sum = Vector2.zero;
            for (var i = 0; i < _cells.Count; i++)
            {
                var world = piece.transform.TransformPoint(_cells[i]);
                var local = transform.InverseTransformPoint(world);
                sum += new Vector2(local.x, local.y);
            }

            return sum / _cells.Count;
        }
    }
}
