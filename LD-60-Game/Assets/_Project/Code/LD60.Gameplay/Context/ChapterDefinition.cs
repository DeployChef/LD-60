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
        Summary,
        Ending
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
        [Tooltip("Empty runs the event unconditionally. Otherwise the context parameter must reach the minimum. money is the money balance.")]
        public string conditionParameter;
        public float conditionMinimum;
        [Tooltip("Context parameter written when the event starts. Empty skips the write.")]
        public string setParameter;
        public float setValue;
    }

    [Serializable]
    public sealed class NewspaperRule
    {
        public string parameterId = "fill";
        public float minimum = 1f;
    }

    [Serializable]
    public sealed class CapsuleDelivery
    {
        public string id;
        public LD60.Gameplay.Articles.ArticleDefinition[] articles = Array.Empty<LD60.Gameplay.Articles.ArticleDefinition>();
        [TextArea(2, 6)] public string note;

        public LD60.Gameplay.Articles.ArticleDefinition[] Articles => articles ?? Array.Empty<LD60.Gameplay.Articles.ArticleDefinition>();
        public string Note => note;
    }

    [CreateAssetMenu(fileName = "Chapter", menuName = "LD60/Chapter")]
    public sealed class ChapterDefinition : ScriptableObject
    {
        [SerializeField] private ChapterEvent[] events = Array.Empty<ChapterEvent>();
        [SerializeField] private NewspaperRule[] rules = Array.Empty<NewspaperRule>();
        [SerializeField] private CapsuleDelivery[] capsules = Array.Empty<CapsuleDelivery>();

        public ChapterEvent[] Events => events ?? Array.Empty<ChapterEvent>();
        public NewspaperRule[] Rules => rules ?? Array.Empty<NewspaperRule>();
        public CapsuleDelivery[] Capsules => capsules ?? Array.Empty<CapsuleDelivery>();

        public bool TryFindCapsule(string id, out CapsuleDelivery delivery)
        {
            var list = Capsules;
            for (var i = 0; i < list.Length; i++)
            {
                if (list[i] != null && string.Equals(list[i].id, id, StringComparison.Ordinal))
                {
                    delivery = list[i];
                    return true;
                }
            }

            delivery = null;
            return false;
        }
    }
}
