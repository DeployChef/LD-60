using UnityEngine;
using VContainer;

namespace LD60.Gameplay.Story
{
    public sealed class ChapterBehaviours
    {
        private readonly IObjectResolver _resolver;
        private readonly ISequencePlayer _player;

        public ChapterBehaviours(IObjectResolver resolver, ISequencePlayer player)
        {
            _resolver = resolver;
            _player = player;
        }

        public void Cancel()
        {
            _player?.Cancel();
        }

        public ChapterBehaviour Create(string id)
        {
            switch (id)
            {
                case DayOneCapsule.Id: return _resolver.Resolve<DayOneCapsule>();
                case DayOnePayer.Id: return _resolver.Resolve<DayOnePayer>();
                case DayOneFullPage.Id: return _resolver.Resolve<DayOneFullPage>();
                case DayOneShot.Id: return _resolver.Resolve<DayOneShot>();
                case DayOneClose.Id: return _resolver.Resolve<DayOneClose>();
                case DayTwoCapsule.Id: return _resolver.Resolve<DayTwoCapsule>();
                case DayTwoReturn.Id: return _resolver.Resolve<DayTwoReturn>();
                case DayTwoClose.Id: return _resolver.Resolve<DayTwoClose>();
                case CreditsClose.Id: return _resolver.Resolve<CreditsClose>();
                default:
                    Debug.LogWarning($"[Story] No behaviour for '{id}'.");
                    return null;
            }
        }
    }
}
