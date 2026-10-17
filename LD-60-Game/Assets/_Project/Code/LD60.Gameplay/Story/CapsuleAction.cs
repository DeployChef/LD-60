using UnityEngine;

namespace LD60.Gameplay.Story
{
    [CreateAssetMenu(fileName = "Capsule", menuName = "LD60/Chapter Action/Capsule")]
    public sealed class CapsuleAction : ChapterAction
    {
        [SerializeField] private string capsuleId;

        public string CapsuleId => capsuleId;
    }
}
