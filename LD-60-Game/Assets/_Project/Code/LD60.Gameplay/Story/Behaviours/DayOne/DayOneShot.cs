using Cysharp.Threading.Tasks;

namespace LD60.Gameplay.Story
{
    public sealed class DayOneShot : ChapterBehaviour
    {
        public const string Id = "day-one.shot";

        public override bool CanPlay(ChapterRead read)
        {
            return read.Stamped && read.Context != null && read.Context.Meets("heat", 1f);
        }

        public override UniTask Play(ChapterRunner chapter)
        {
            chapter.End(ChapterResult.Ending);
            return UniTask.CompletedTask;
        }
    }
}
