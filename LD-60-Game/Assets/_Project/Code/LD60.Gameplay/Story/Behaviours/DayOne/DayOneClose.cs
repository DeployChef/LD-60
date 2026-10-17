using Cysharp.Threading.Tasks;

namespace LD60.Gameplay.Story
{
    public sealed class DayOneClose : ChapterBehaviour
    {
        public const string Id = "day-one.close";

        public override bool CanPlay(ChapterRead read)
        {
            return read.Stamped;
        }

        public override async UniTask Play(ChapterRunner chapter)
        {
            await chapter.WaitUntilCompleted();
            chapter.End(ChapterResult.NextDay);
        }
    }
}
