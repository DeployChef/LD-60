using System;
using LD60.Gameplay.Context;
using UnityEngine;

namespace LD60.Gameplay.Story
{
    public enum VisitStepKind
    {
        Enter,
        Say,
        Choose,
        Give,
        Exit,
        Remember
    }

    [Serializable]
    public sealed class VisitStep
    {
        public VisitStepKind kind;
        [Tooltip("Emotion key on the guest. Empty uses the guest's first sprite.")]
        public string emotion;
        [TextArea(2, 4)] public string text;
        public string giftId;
        public int amount = 1;
        public string optionA;
        public string optionB;
        [Tooltip("Step index after this one. -1 continues to the next step.")]
        public int next = -1;
        [Tooltip("Step index after option A. -1 continues to the next step.")]
        public int nextA = -1;
        [Tooltip("Step index after option B. -1 continues to the next step.")]
        public int nextB = -1;
        [Tooltip("Empty plays this line always. Otherwise it plays only when the parameter is greater than the number, and a miss falls through to the next line.")]
        public DialogueRule rule;
        [Tooltip("When this line plays, store this context parameter. Empty stores nothing.")]
        public string remember;
        public float value;
    }

    [Serializable]
    public struct DialogueRule
    {
        [Tooltip("Context parameter. Empty leaves this rule off.")]
        public string parameter;
        [Tooltip("Pass when the parameter is greater than this value.")]
        public float greaterThan;

        public bool Passes(StoryContext context)
        {
            if (string.IsNullOrEmpty(parameter))
                return true;

            if (context == null)
                return false;

            return context.GetParameter(parameter) > greaterThan;
        }
    }

    [CreateAssetMenu(fileName = "Visit", menuName = "LD60/Chapter Action/Visit")]
    public sealed class VisitAction : ChapterAction
    {
        [Tooltip("With When set to Context, this calls the dialogue once the parameter is greater than the number. With another When, both have to pass.")]
        [SerializeField] private DialogueRule rule = new DialogueRule();
        [SerializeField] private GuestDefinition guest;
        [SerializeField] private VisitStep[] steps = Array.Empty<VisitStep>();

        public GuestDefinition Guest => guest;
        public VisitStep[] Steps => steps ?? Array.Empty<VisitStep>();

        public override bool Matches(ChapterRead read)
        {
            if (!string.IsNullOrEmpty(rule.parameter) && !rule.Passes(read != null ? read.Context : null))
                return false;

            if (string.IsNullOrEmpty(rule.parameter) && When == ChapterWhen.Context)
                return false;

            return base.Matches(read);
        }
    }
}
