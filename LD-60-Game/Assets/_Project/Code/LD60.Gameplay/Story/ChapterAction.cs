using System;
using LD60.Gameplay.Context;
using UnityEngine;

namespace LD60.Gameplay.Story
{
    public enum ChapterWhen
    {
        ChapterStart,
        EnterWork,
        AfterWorkSeconds,
        FieldFill,
        PaperStamped,
        Context
    }

    public abstract class ChapterAction : ScriptableObject
    {
        [SerializeField] private ChapterWhen when;
        [Tooltip("AfterWorkSeconds or FieldFill.")]
        [SerializeField] private float at = 1f;
        [Tooltip("Empty always passes. money is the balance.")]
        [SerializeField] private string parameter;
        [SerializeField] private float minimum;

        protected ChapterWhen When => when;

        public virtual bool Matches(ChapterRead read)
        {
            if (read == null || !SignalMatches(read))
                return false;

            return read.Context == null || read.Context.Meets(parameter, minimum);
        }

        private bool SignalMatches(ChapterRead read)
        {
            switch (when)
            {
                case ChapterWhen.ChapterStart:
                    return read.Started;
                case ChapterWhen.EnterWork:
                    return read.EnteredWork;
                case ChapterWhen.AfterWorkSeconds:
                    return read.WorkSeconds + 0.0001f >= at;
                case ChapterWhen.FieldFill:
                    return read.Fill + 0.0001f >= at;
                case ChapterWhen.PaperStamped:
                    return read.Stamped;
                case ChapterWhen.Context:
                    return true;
                default:
                    return false;
            }
        }
    }
}
