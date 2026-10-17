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

        public FieldStateChanged(float fill)
        {
            Fill = fill;
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

    public readonly struct DialogueFinished
    {
        public readonly string DialogueId;

        public DialogueFinished(string dialogueId)
        {
            DialogueId = dialogueId;
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
}
