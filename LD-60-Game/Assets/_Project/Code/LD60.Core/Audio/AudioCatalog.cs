using System.Collections.Generic;
using UnityEngine;

namespace LD60.Core.Audio
{
    [CreateAssetMenu(fileName = "AudioCatalog", menuName = "LD-60/Audio Catalog")]
    public sealed class AudioCatalog : ScriptableObject
    {
        public static class Ids
        {
            public const string UiMove = "UiMove";
            public const string UiClick = "UiClick";
        }

        [SerializeField] private List<SoundDefinition> sounds = new();

        public bool TryGet(string id, out SoundDefinition definition)
        {
            for (var i = 0; i < sounds.Count; i++)
            {
                var sound = sounds[i];
                if (sound != null && sound.Id == id)
                {
                    definition = sound;
                    return true;
                }
            }

            definition = null;
            return false;
        }
    }
}
