using System;
using LD60.Gameplay.Articles;
using UnityEngine;

namespace LD60.Gameplay.Context
{
    public enum ChapterMode
    {
        Cutscene,
        Work
    }

    [Serializable]
    public sealed class NewspaperRule
    {
        public string parameterId = "fill";
        public float minimum = 1f;
    }

    [Serializable]
    public sealed class NeighborRule
    {
        public ArticleKind kind;
        public ArticleKind other;
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
        [SerializeField] private string title;
        [SerializeField] private LD60.Gameplay.Story.ChapterAction[] actions = Array.Empty<LD60.Gameplay.Story.ChapterAction>();
        [SerializeField] private NewspaperRule[] rules = Array.Empty<NewspaperRule>();
        [SerializeField] private NeighborRule[] neighbors = Array.Empty<NeighborRule>();
        [SerializeField] private CapsuleDelivery[] capsules = Array.Empty<CapsuleDelivery>();

        public string Title => string.IsNullOrEmpty(title) ? name : title;
        public LD60.Gameplay.Story.ChapterAction[] Actions => actions ?? Array.Empty<LD60.Gameplay.Story.ChapterAction>();
        public NewspaperRule[] Rules => rules ?? Array.Empty<NewspaperRule>();
        public NeighborRule[] Neighbors => neighbors ?? Array.Empty<NeighborRule>();
        public CapsuleDelivery[] Capsules => capsules ?? Array.Empty<CapsuleDelivery>();

        public bool Allows(float fill, out string reason)
        {
            return Allows(Rules, fill, out reason);
        }

        public static bool Allows(NewspaperRule[] rules, float fill, out string reason)
        {
            reason = null;
            if (rules == null)
                return true;

            for (var i = 0; i < rules.Length; i++)
            {
                var rule = rules[i];
                if (rule == null)
                    continue;

                var id = rule.parameterId ?? string.Empty;
                if (id.Equals("fill", StringComparison.OrdinalIgnoreCase))
                {
                    if (fill + 0.0001f < rule.minimum)
                    {
                        reason = "The page is not full enough.";
                        return false;
                    }

                    continue;
                }

                reason = "This edition cannot be stamped.";
                return false;
            }

            return true;
        }

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
