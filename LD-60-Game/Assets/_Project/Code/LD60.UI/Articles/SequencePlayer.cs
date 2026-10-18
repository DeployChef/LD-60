using System.Threading;
using Cysharp.Threading.Tasks;
using LD60.Gameplay.Story;
using UnityEngine;

namespace LD60.UI
{
    public sealed class SequencePlayer : MonoBehaviour, ISequencePlayer
    {
        private const float WalkSeconds = 0.75f;

        [SerializeField] private Transform character;
        [SerializeField] private SpriteRenderer body;

        private WorkstationViewController _views;
        private CancellationTokenSource _run;
        private Vector3 _home;
        private Vector3 _away;
        private bool _placed;

        private void Awake()
        {
            if (character == null)
            {
                var guest = GameObject.Find("Character");
                if (guest != null)
                    character = guest.transform;
            }

            if (body == null && character != null)
                body = character.GetComponent<SpriteRenderer>();

            _views = FindAnyObjectByType<WorkstationViewController>();
            Park();
        }

        public void Cancel()
        {
            if (_run == null)
                return;

            _run.Cancel();
            _run.Dispose();
            _run = null;
        }

        public void LookAtRoom()
        {
            if (_views != null)
                _views.ShowTable(false);
        }

        public void ShowGuest(Sprite sprite)
        {
            if (body != null && sprite != null)
                body.sprite = sprite;
        }

        public UniTask Enter()
        {
            return Slide(_home, Token);
        }

        public UniTask Exit()
        {
            return Slide(_away, Token);
        }

        private CancellationToken Token
        {
            get
            {
                if (_run == null)
                    _run = new CancellationTokenSource();
                return _run.Token;
            }
        }

        private async UniTask Slide(Vector3 target, CancellationToken token)
        {
            if (character == null)
                return;

            var from = character.localPosition;
            var elapsed = 0f;
            while (elapsed < WalkSeconds)
            {
                token.ThrowIfCancellationRequested();
                if (character == null)
                    return;

                elapsed += Time.deltaTime;
                character.localPosition = Vector3.Lerp(from, target, Mathf.Clamp01(elapsed / WalkSeconds));
                await UniTask.Yield(token);
            }

            if (character != null)
                character.localPosition = target;
        }

        private void Park()
        {
            if (_placed || character == null)
                return;

            _home = character.localPosition;
            _away = _home + new Vector3(-14f, 0f, 0f);
            character.localPosition = _away;
            _placed = true;
        }
    }
}
