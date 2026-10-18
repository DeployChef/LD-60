using LD60.Core;

namespace LD60.Gameplay.Context
{
    public interface IGameSession
    {
        GameStart Launch { get; }

        bool HasResumableDay { get; }

        bool TryGetCheckpoint(out int chapterIndex, out StoryContextData context);

        void WriteCheckpoint(int chapterIndex, StoryContextData context);

        void ClearCheckpoint();
    }
}
