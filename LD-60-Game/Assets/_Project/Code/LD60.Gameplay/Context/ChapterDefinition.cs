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
        [SerializeField] private string[] requireOneOfArticles = Array.Empty<string>();

        public string Title => string.IsNullOrEmpty(title) ? name : title;
        public LD60.Gameplay.Story.ChapterAction[] Actions => actions ?? Array.Empty<LD60.Gameplay.Story.ChapterAction>();
        public NewspaperRule[] Rules => rules ?? Array.Empty<NewspaperRule>();
        public NeighborRule[] Neighbors => neighbors ?? Array.Empty<NeighborRule>();
        public CapsuleDelivery[] Capsules => capsules ?? Array.Empty<CapsuleDelivery>();
        public string[] RequireOneOfArticles => requireOneOfArticles ?? Array.Empty<string>();

        public bool Allows(float fill, out string reason)
        {
            return Allows(Rules, fill, null, RequireOneOfArticles, out reason);
        }

        public bool Allows(float fill, System.Collections.Generic.IReadOnlyList<string> placedArticles, out string reason)
        {
            return Allows(Rules, fill, placedArticles, RequireOneOfArticles, out reason);
        }

        public static bool Allows(NewspaperRule[] rules, float fill, out string reason)
        {
            return Allows(rules, fill, null, null, out reason);
        }

        public static bool Allows(NewspaperRule[] rules, float fill, System.Collections.Generic.IReadOnlyList<string> placedArticles, string[] requiredOneOf, out string reason)
        {
            reason = null;
            if (requiredOneOf != null && requiredOneOf.Length > 0)
            {
                var found = false;
                if (placedArticles != null)
                {
                    for (var i = 0; i < placedArticles.Count; i++)
                    {
                        var id = placedArticles[i];
                        for (var j = 0; j < requiredOneOf.Length; j++)
                        {
                            if (string.Equals(id, requiredOneOf[j], StringComparison.OrdinalIgnoreCase))
                            {
                                found = true;
                                break;
                            }
                        }

                        if (found)
                            break;
                    }
                }

                if (!found)
                {
                    reason = "The final edition must include one of the lead manifestos.";
                    return false;
                }
            }

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
