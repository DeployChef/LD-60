using System;
using Cysharp.Threading.Tasks;
using LD60.Core.Bus;

namespace LD60.Gameplay.Story
{
    public sealed class DayOneCapsule : ChapterBehaviour
    {
        public const string Id = "day-one.capsule";

        private readonly IMessageBus _bus;

        public DayOneCapsule(IMessageBus bus)
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
                if (message.CapsuleId == "morning-drop")
                    taken.TrySetResult();
            });

            try
            {
                _bus.Publish(new CapsuleArrived("morning-drop"));
                await taken.Task.AttachExternalCancellation(chapter.Token);
            }
            finally
            {
                wait.Dispose();
            }
        }
    }
}
