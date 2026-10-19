using UnityEngine;

namespace LD60.Gameplay.Story
{
    [CreateAssetMenu(fileName = "Ending", menuName = "LD60/Chapter Action/Ending")]
    public sealed class EndingAction : ChapterAction
    {
        [SerializeField] private ScriptableObject comics;

        public ScriptableObject Comics => comics;
    }
}
