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
    }
}
