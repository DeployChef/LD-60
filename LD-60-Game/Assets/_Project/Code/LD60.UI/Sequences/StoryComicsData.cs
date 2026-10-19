using System;
using UnityEngine;

namespace LD60.UI
{
    [Serializable]
    public struct StoryComicsBlock
    {
        public Sprite image;
        [TextArea(2, 5)]
        public string subtitle;
    }

    [CreateAssetMenu(fileName = "StoryComicsData", menuName = "LD60/Story/Story Comics")]
    public sealed class StoryComicsData : ScriptableObject
    {
        public const int DefaultBlockCount = 6;

        [SerializeField] private Sprite background;
        [SerializeField] private StoryComicsBlock[] blocks = new StoryComicsBlock[DefaultBlockCount];

        public Sprite Background => background;
        public StoryComicsBlock[] Blocks => blocks;

        private void OnValidate()
        {
            if (blocks == null)
            {
                blocks = new StoryComicsBlock[DefaultBlockCount];
            }
            else if (blocks.Length != DefaultBlockCount)
            {
                Array.Resize(ref blocks, DefaultBlockCount);
            }
        }
    }
}
