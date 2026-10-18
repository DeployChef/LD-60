using Cysharp.Threading.Tasks;
using LD60.Gameplay.Context;

namespace LD60.Gameplay.Story
{
    public sealed class ChapterRead
    {
        public float WorkSeconds;
        public float Fill;
        public bool Started;
        public bool EnteredWork;
        public bool Stamped;
        public StoryContext Context;
    }
}
