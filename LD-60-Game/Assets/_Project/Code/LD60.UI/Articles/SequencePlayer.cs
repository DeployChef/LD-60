using System.Threading;
using Cysharp.Threading.Tasks;
using LD60.Gameplay.Story;
using UnityEngine;

namespace LD60.UI
{   
    public sealed class SequencePlayer : MonoBehaviour, ISequencePlayer
    {
        [SerializeField] private WorkstationViewController views;
        [SerializeField] private GuestView _guestView;
        [SerializeField] private StoryComicsPlayer _storyComics;
        [SerializeField] private StoryComicsData _introComics;
        [SerializeField] private CreditsPlayer _credits;
        
        public void LookAtRoom()
        {
            DeskDrag.PlacementLocked = true;
            DeskDrag.RecallHeld();
            if (views != null)
                views.HoldLookUp(true);
        }

        public void FreeLook()
        {
            DeskDrag.PlacementLocked = false;
            if (views != null)
                views.HoldLookUp(false);
        }

        public void Cancel()
        {
            _guestView.Cancel();
            if (_storyComics != null && _storyComics.IsPlaying)
                _storyComics.CancelSequence();
            if (_credits != null && _credits.IsPlaying)
                _credits.CancelSequence();
            FreeLook();
        }

        public void ShowGuest(Sprite sprite) => _guestView.ShowGuest(sprite);
        public UniTask Enter() => _guestView.Enter();
        public UniTask Exit() => _guestView.Exit();

        private void OnDestroy()
        {
            FreeLook();
        }

        public UniTask PlayIntroComics() => PlayComics(_introComics);

        public UniTask PlayComics(ScriptableObject comics) => PlayComics(comics as StoryComicsData);

        public async UniTask PlayCredits(CancellationToken token = default)
        {
            if (_credits == null)
                _credits = FindAnyObjectByType<CreditsPlayer>(FindObjectsInactive.Include);

            if (_credits == null)
            {
                Debug.LogWarning("[SequencePlayer] CreditsPlayer not found in scene.");
                return;
            }

            if (views != null)
            {
                views.ShowTable(false);
                views.AllowLookDown(false);
            }

            try
            {
                await _credits.PlayAsync(token);
            }
            finally
            {
                if (views != null)
                    views.AllowLookDown(true);
            }
        }

        public async UniTask PlayComics(StoryComicsData comics)
        {
            if (comics == null)
                return;

            if (_storyComics == null)
                _storyComics = FindAnyObjectByType<StoryComicsPlayer>(FindObjectsInactive.Include);

            if (_storyComics == null)
                return;

            if (views != null)
            {
                views.ShowTable(false);
                views.AllowLookDown(false);
            }

            try
            {
                await _storyComics.PlayAsync(comics);
            }
            finally
            {
                if (views != null)
                    views.AllowLookDown(true);
            }
        }
    }
}
