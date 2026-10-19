using System;
using LD60.Core.Bus;
using LD60.Gameplay.Articles;
using LD60.Gameplay.Context;
using LD60.Gameplay.Story;
using LD60.UI.Audio;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace LD60.UI
{
    public sealed class CapsuleSlot : MonoBehaviour
    {
        [SerializeField] private ArticlePiece dot;
        [SerializeField] private ArticlePiece line2H;
        [SerializeField] private ArticlePiece line2V;
        [SerializeField] private ArticlePiece line3H;
        [SerializeField] private ArticlePiece line3V;
        [SerializeField] private ArticlePiece square;
        [SerializeField] private ArticlePiece banner5x2;
        [SerializeField] private NoteSlip note;
        [SerializeField] private Transform desk;
        [SerializeField] private Transform pile;
        [SerializeField] private float spacing = 2.7f;
        [SerializeField] private Sprite closedSprite;
        [SerializeField] private Sprite openedSprite;

        private IMessageBus _bus;
        private StoryBook _story;
        private NewspaperDesk _board;
        private WorkstationViewController _views;
        private CinemachineBrain _brain;
        private SpriteRenderer _renderer;
        private Color _awakeColor;
        private IDisposable _arrived;
        private string _pending;
        private bool _opened;
        private Vector3 _letterPosition;

        [Inject]
        public void Construct(IMessageBus bus, StoryBook story, NewspaperDesk board)
        {
            _bus = bus;
            _story = story;
            _board = board;
            _arrived?.Dispose();
            _arrived = _bus.Subscribe<CapsuleArrived>(OnArrived);
        }

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _awakeColor = _renderer != null ? _renderer.color : Color.white;
            _letterPosition = transform.localPosition;
            _views = FindAnyObjectByType<WorkstationViewController>();
            if (_views != null)
                _brain = _views.GetComponentInChildren<CinemachineBrain>(true);
            if (_brain == null)
                _brain = FindAnyObjectByType<CinemachineBrain>();
            if (pile == null && desk != null)
                pile = desk.Find("Pile");
            SetWaiting(!string.IsNullOrEmpty(_pending));
        }

        private void OnDestroy()
        {
            _arrived?.Dispose();
        }

        private void Update()
        {
            if (string.IsNullOrEmpty(_pending))
                return;

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || Time.timeScale == 0f)
                return;

            var camera = _brain != null ? _brain.OutputCamera : null;
            if (camera != null && Hits(camera, mouse.position.ReadValue()))
            {
                if (_views != null)
                    _views.ShowTable(true);
                Open();
            }
        }

        private void OnArrived(CapsuleArrived message)
        {
            if (_story == null || !_story.TryFindCapsule(message.CapsuleId, out _))
            {
                _bus.Publish(new CapsuleTaken(message.CapsuleId));
                return;
            }

            _pending = message.CapsuleId;
            _opened = false;
            SetWaiting(true);
        }

        private void Open()
        {
            if (string.IsNullOrEmpty(_pending))
                return;
            var id = _pending;
            _pending = null;
            _opened = true;
            SetWaiting(false);
            MenuAudio.PlayEnvelopeOpen();
            if (_story != null && _story.TryFindCapsule(id, out var delivery))
                LayOut(delivery);
            _bus.Publish(new CapsuleTaken(id));
        }

        private void LayOut(CapsuleDelivery delivery)
        {
            var articles = delivery.Articles;
            var spawned = new System.Collections.Generic.List<ArticlePiece>();
            for (var i = 0; i < articles.Length; i++)
            {
                var source = Form(articles[i]);
                if (source == null || articles[i] == null)
                    continue;

                var piece = Instantiate(source, desk);
                piece.Bind(articles[i]);
                spawned.Add(piece);
            }

            for (var i = 0; i < spawned.Count; i++)
            {
                if (_board != null)
                    _board.Deliver(spawned[i], i, spawned.Count);
                else
                    spawned[i].transform.localPosition = new Vector3(
                        -7.2f + (i % 3) * spacing * 0.7f,
                        2.2f - (i / 3) * spacing * 0.85f,
                        -0.08f);
            }

            if (note != null && !string.IsNullOrEmpty(delivery.Note))
            {
                var slip = Instantiate(note, desk);
                slip.Bind(delivery.Note);
                if (_board != null)
                    _board.PlaceNote(slip);
                else
                    slip.transform.localPosition = new Vector3(-7.8f, 4.1f, -0.12f);
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

        private bool Hits(Camera camera, Vector2 screen)
        {
            var ray = camera.ScreenPointToRay(screen);
            var collider = GetComponent<Collider>();
            return collider != null && collider.Raycast(ray, out _, 200f);
        }

        private void SetWaiting(bool waiting)
        {
            if (_renderer != null)
            {
                var sprite = _opened && !waiting ? openedSprite : closedSprite;
                if (sprite != null)
                {
                    _renderer.sprite = sprite;
                    _renderer.color = _awakeColor;
                    // Both pictures share the envelope body. Only the flap grows upwards.
                    var bottomOffset = closedSprite != null
                        ? (closedSprite.bounds.min.y - sprite.bounds.min.y) * transform.localScale.y
                        : 0f;
                    transform.localPosition = _letterPosition + Vector3.up * bottomOffset;
                    var box = GetComponent<BoxCollider>();
                    if (box != null)
                    {
                        var bounds = sprite.bounds;
                        box.size = new Vector3(bounds.size.x, bounds.size.y, Mathf.Max(0.2f, box.size.z));
                        box.center = new Vector3(bounds.center.x, bounds.center.y, box.center.z);
                    }
                }
                else
                    _renderer.color = waiting ? _awakeColor : _awakeColor * 0.45f;
            }
            var collider = GetComponent<Collider>();
            if (collider != null)
                collider.enabled = waiting;
        }
    }
}
