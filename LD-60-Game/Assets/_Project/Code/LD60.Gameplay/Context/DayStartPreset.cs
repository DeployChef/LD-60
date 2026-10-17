using UnityEngine;

namespace LD60.Gameplay.Context
{
    [CreateAssetMenu(fileName = "DayStart", menuName = "LD60/Day Start")]
    public sealed class DayStartPreset : ScriptableObject
    {
        [Tooltip("Zero-based chapter index. 0 is the first day.")]
        [SerializeField] private int chapterIndex;

        [SerializeField] private StoryContextData context = new();

        public int ChapterIndex => chapterIndex;
        public StoryContextData Context => context != null ? context : new StoryContextData();

        public void OverwriteContext(StoryContextData source)
        {
            context = source != null ? source.Clone() : new StoryContextData();
        }
    }
}
