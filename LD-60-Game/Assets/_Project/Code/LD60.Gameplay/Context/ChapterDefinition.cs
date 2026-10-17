using System;
using UnityEngine;

namespace LD60.Gameplay.Context
{
    public enum ChapterMode
    {
        Cutscene,
        Dialogue,
        Work
    }

    public enum StoryTriggerKind
    {
        OnChapterStart,
        OnEnterWork,
        AfterWorkSeconds,
        OnFieldFillAtLeast,
        OnDialogueFinished,
        OnCapsuleTaken,
        OnPaperStamped
    }

    public enum StoryActionKind
    {
        Sequence,
        Dialogue,
        Capsule,
        Summary
    }

    [Serializable]
    public sealed class ChapterEvent
    {
        public StoryTriggerKind trigger;
        [Tooltip("AfterWorkSeconds")]
        public float seconds;
        [Tooltip("OnFieldFillAtLeast, 0 to 1")]
        public float fill = 1f;
        [Tooltip("Dialogue or capsule id this trigger waits for")]
        public string id;
        public StoryActionKind action;
        public string actionId;
    }

    [Serializable]
    public sealed class NewspaperRule
    {
        public string parameterId = "fill";
        public float minimum = 1f;
    }

    [CreateAssetMenu(fileName = "Chapter", menuName = "LD60/Chapter")]
    public sealed class ChapterDefinition : ScriptableObject
    {
        [SerializeField] private ChapterEvent[] events = Array.Empty<ChapterEvent>();
        [SerializeField] private NewspaperRule[] rules = Array.Empty<NewspaperRule>();

        public ChapterEvent[] Events => events ?? Array.Empty<ChapterEvent>();
        public NewspaperRule[] Rules => rules ?? Array.Empty<NewspaperRule>();
    }
}
