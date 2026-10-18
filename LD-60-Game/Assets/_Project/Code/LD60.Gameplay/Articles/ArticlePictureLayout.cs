using UnityEngine;

namespace LD60.Gameplay.Articles
{
    [CreateAssetMenu(fileName = "PictureLayout", menuName = "LD60/Picture Layout")]
    public sealed class ArticlePictureLayout : ScriptableObject
    {
        [SerializeField] private int slot = 1;

        public int Slot => slot < 1 || slot > 3 ? 1 : slot;
    }
}
