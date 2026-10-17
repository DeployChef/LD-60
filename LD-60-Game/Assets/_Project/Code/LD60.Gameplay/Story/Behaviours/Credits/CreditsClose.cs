using Cysharp.Threading.Tasks;

namespace LD60.Gameplay.Story
{
    public sealed class CreditsClose : ChapterBehaviour
    {
        public const string Id = "credits.close";

        public override bool CanPlay(ChapterRead read)
        {
            return read.Started;
        }

        public override async UniTask Play(ChapterRunner chapter)
        {
            await chapter.WaitUntilCompleted();
            chapter.End(ChapterResult.NextDay);
        }
    }
}
