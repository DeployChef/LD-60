using LD60.Gameplay.Context;

namespace LD60.Gameplay.Story
{
    public readonly struct ChapterModeChanged
    {
        public readonly ChapterMode Mode;

        public ChapterModeChanged(ChapterMode mode)
        {
            Mode = mode;
        }
    }

    public readonly struct FieldStateChanged
    {
        public readonly float Fill;
        public readonly float Politics;
        public readonly float Social;

        public FieldStateChanged(float fill, float politics, float social)
        {
            Fill = fill;
            Politics = politics;
            Social = social;
        }
    }

    public readonly struct DayStarted
    {
        public readonly string Title;
        public readonly ChapterDefinition Chapter;

        public DayStarted(ChapterDefinition chapter)
        {
            Chapter = chapter;
            Title = chapter != null ? chapter.Title : string.Empty;
        }
    }

    public readonly struct PaperStamped
    {
        public readonly float Fill;

        public PaperStamped(float fill)
        {
            Fill = fill;
        }
    }

    public readonly struct DaySummary
    {
        public readonly float Fill;
        public readonly float Politics;
        public readonly float Social;

        public DaySummary(float fill, float politics, float social)
        {
            Fill = fill;
            Politics = politics;
            Social = social;
        }
    }

    public readonly struct ArticleGiven
    {
        public readonly string ArticleId;

        public ArticleGiven(string articleId)
        {
            ArticleId = articleId;
        }
    }

    public readonly struct CapsuleTaken
    {
        public readonly string CapsuleId;

        public CapsuleTaken(string capsuleId)
        {
            CapsuleId = capsuleId;
        }
    }

    public readonly struct CapsuleArrived
    {
        public readonly string CapsuleId;

        public CapsuleArrived(string capsuleId)
        {
            CapsuleId = capsuleId;
        }
    }

}
