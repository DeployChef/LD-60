using System;
using LD60.Gameplay.Articles;
using UnityEngine;

namespace LD60.UI
{
    [CreateAssetMenu(fileName = "ArticleStyles", menuName = "LD60/Article Styles")]
    public sealed class ArticleStyles : ScriptableObject
    {
        [Serializable]
        private sealed class Backgrounds
        {
            public Sprite dot;
            public Sprite line2H;
            public Sprite line2V;
            public Sprite line3H;
            public Sprite line3V;
            public Sprite square;
            public Sprite banner5x2;

            public Sprite For(ArticleShape shape)
            {
                return shape switch
                {
                    ArticleShape.Dot => dot,
                    ArticleShape.Line2H => line2H,
                    ArticleShape.Line2V => line2V,
                    ArticleShape.Line3H => line3H,
                    ArticleShape.Line3V => line3V,
                    ArticleShape.Square => square,
                    ArticleShape.Banner5x2 => banner5x2,
                    _ => null
                };
            }
        }

        [Tooltip("News / reports: suggested green border. Empty slots keep the prefab background.")]
        [SerializeField] private Backgrounds report = new();
        [Tooltip("Politics: blue border.")]
        [SerializeField] private Backgrounds politics = new();
        [Tooltip("Social topics: red border.")]
        [SerializeField] private Backgrounds social = new();
        [Tooltip("Advertisements: black border.")]
        [SerializeField] private Backgrounds ad = new();

        public Sprite GetBackground(ArticleKind kind, ArticleShape shape)
        {
            var backgrounds = kind switch
            {
                ArticleKind.Report => report,
                ArticleKind.Politics => politics,
                ArticleKind.Social => social,
                ArticleKind.Ad => ad,
                _ => null
            };
            return backgrounds?.For(shape);
        }
    }
}
