using UnityEngine;

namespace LD60.UI
{
    public sealed class ArticleDebugView : MonoBehaviour
    {
        [SerializeField] private bool showValues;

        public static bool ShowValues
        {
            get
            {
                var views = FindObjectsByType<ArticleDebugView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (var i = 0; i < views.Length; i++)
                {
                    if (views[i].showValues)
                        return true;
                }

                return false;
            }
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnDisable()
        {
            Refresh();
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled)
                return;
            Refresh();
        }

        public static void Refresh()
        {
            var pieces = FindObjectsByType<ArticlePiece>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < pieces.Length; i++)
                pieces[i].Refresh();
        }
    }
}
