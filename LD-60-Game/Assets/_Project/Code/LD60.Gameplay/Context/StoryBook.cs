using LD60.Gameplay.Articles;
using UnityEngine;

namespace LD60.Gameplay.Context
{
    public sealed class StoryBook : MonoBehaviour
    {
        [SerializeField] private ChapterDefinition[] chapters = System.Array.Empty<ChapterDefinition>();
        [SerializeField] private ChapterDefinition finale;
        [SerializeField] private DayStartPreset debugStart;
        [SerializeField] private bool debugKeys = true;

        public ChapterDefinition[] Chapters => chapters ?? System.Array.Empty<ChapterDefinition>();
        public ChapterDefinition Finale => finale;
        public DayStartPreset DebugStart => debugStart;
        public bool DebugKeys => debugKeys;

        public bool TryFindCapsule(string id, out CapsuleDelivery delivery)
        {
            var list = Chapters;
            for (var i = 0; i < list.Length; i++)
            {
                if (list[i] != null && list[i].TryFindCapsule(id, out delivery))
                    return true;
            }

            if (finale != null && finale.TryFindCapsule(id, out delivery))
                return true;

            delivery = null;
            return false;
        }

        public ArticleDefinition FindArticle(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            var found = FindArticle(chapters, id);
            if (found != null)
                return found;

            return finale != null ? FindArticle(finale, id) : null;
        }

        private static ArticleDefinition FindArticle(ChapterDefinition[] list, string id)
        {
            if (list == null)
                return null;

            for (var i = 0; i < list.Length; i++)
            {
                var found = FindArticle(list[i], id);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static ArticleDefinition FindArticle(ChapterDefinition chapter, string id)
        {
            if (chapter == null)
                return null;

            var capsules = chapter.Capsules;
            for (var i = 0; i < capsules.Length; i++)
            {
                var delivery = capsules[i];
                if (delivery == null)
                    continue;

                var articles = delivery.Articles;
                for (var j = 0; j < articles.Length; j++)
                {
                    var article = articles[j];
                    if (article != null && string.Equals(article.Id, id, System.StringComparison.Ordinal))
                        return article;
                }
            }

            return null;
        }
    }
}
