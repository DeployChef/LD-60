using Cysharp.Threading.Tasks;

namespace LD60.Gameplay.Story
{
    public sealed class DayTwoClose : ChapterBehaviour
    {
        public const string Id = "day-two.close";

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
