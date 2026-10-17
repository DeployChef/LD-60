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
    }
}
