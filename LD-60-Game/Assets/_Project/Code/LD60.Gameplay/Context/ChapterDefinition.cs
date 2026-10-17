using System;
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
        [SerializeField] private string[] behaviourIds = Array.Empty<string>();
        [SerializeField] private NewspaperRule[] rules = Array.Empty<NewspaperRule>();
        [SerializeField] private CapsuleDelivery[] capsules = Array.Empty<CapsuleDelivery>();

        public string Title => string.IsNullOrEmpty(title) ? name : title;
        public string[] BehaviourIds => behaviourIds ?? Array.Empty<string>();
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
