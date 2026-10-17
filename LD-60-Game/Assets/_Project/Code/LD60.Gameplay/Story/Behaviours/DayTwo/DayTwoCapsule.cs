using System;
using Cysharp.Threading.Tasks;
using LD60.Core.Bus;

namespace LD60.Gameplay.Story
{
    public sealed class DayTwoCapsule : ChapterBehaviour
    {
        public const string Id = "day-two.capsule";

        private readonly IMessageBus _bus;

        public DayTwoCapsule(IMessageBus bus)
        {
            _bus = bus;
        }

        public override bool CanPlay(ChapterRead read)
        {
            return read.EnteredWork;
        }

        public override async UniTask Play(ChapterRunner chapter)
        {
            var taken = new UniTaskCompletionSource();
            var wait = _bus.Subscribe<CapsuleTaken>(message =>
            {
                if (message.CapsuleId == "day-two-drop")
                    taken.TrySetResult();
            });

            try
            {
                _bus.Publish(new CapsuleArrived("day-two-drop"));
                await taken.Task.AttachExternalCancellation(chapter.Token);
            }
            finally
            {
                wait.Dispose();
            }
        }
    }
}
