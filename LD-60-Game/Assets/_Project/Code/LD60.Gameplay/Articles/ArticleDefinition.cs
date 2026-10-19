using System;
using UnityEngine;

namespace LD60.Gameplay.Articles
{
    public enum ArticleShape
    {
        Dot,
        Line2H,
        Line2V,
        Line3H,
        Line3V,
        Square,
        Banner5x2
    }

    public enum ArticleKind
    {
        Report,
        Politics,
        Social,
        Ad
    }

    [CreateAssetMenu(fileName = "Article", menuName = "LD60/Article")]
    public sealed class ArticleDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private ArticleShape shape;
        [SerializeField] private ArticleKind kind;
        [SerializeField] private string title;
        [SerializeField, TextArea(3, 8)] private string body;
        [SerializeField] private Sprite picture;
        [SerializeField] private ArticlePictureLayout pictureLayout;
        [SerializeField] private float fill;
        [SerializeField] private float politics;
        [SerializeField] private float social;
        [SerializeField] private float money;
        [SerializeField] private ArticleKind[] forbiddenNeighbors = Array.Empty<ArticleKind>();
        [SerializeField] private string[] forbiddenArticles = Array.Empty<string>();

        public string Id => string.IsNullOrEmpty(id) ? name : id;
        public ArticleShape Shape => shape;
        public ArticleKind Kind => kind;
        public string Title => title;
        public string Body => body;
        public Sprite Picture => picture;
        public ArticlePictureLayout PictureLayout => pictureLayout;
        public float Fill => fill;
        public float Politics => politics;
        public float Social => social;
        public float Money => money;
        public ArticleKind[] ForbiddenNeighbors => forbiddenNeighbors ?? Array.Empty<ArticleKind>();
        public string[] ForbiddenArticles => forbiddenArticles ?? Array.Empty<string>();

#if UNITY_EDITOR
        public void EditorConfigure(
            string articleId,
            ArticleShape articleShape,
            ArticleKind articleKind,
            string headline,
            string copy,
            Sprite sprite,
            ArticlePictureLayout layout,
            float fillValue,
            float politicsValue,
            float socialValue,
            float moneyValue,
            ArticleKind[] forbidden,
            string[] forbiddenArticlesList = null)
        {
            id = articleId;
            shape = articleShape;
            kind = articleKind;
            title = headline;
            body = copy;
            picture = sprite;
            pictureLayout = layout;
            fill = fillValue;
            politics = politicsValue;
            social = socialValue;
            money = moneyValue;
            forbiddenNeighbors = forbidden ?? Array.Empty<ArticleKind>();
            forbiddenArticles = forbiddenArticlesList ?? Array.Empty<string>();
        }
#endif
    }
}
