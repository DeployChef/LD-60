using System;
using UnityEngine;

namespace LD60.Gameplay.Story
{
    [Serializable]
    public struct GuestEmotion
    {
        public string key;
        public Sprite sprite;
    }

    [CreateAssetMenu(fileName = "Guest", menuName = "LD60/Guest")]
    public sealed class GuestDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private GuestEmotion[] emotions = Array.Empty<GuestEmotion>();

        public string DisplayName => displayName;

        public Sprite SpriteFor(string emotion)
        {
            if (emotions == null || emotions.Length == 0)
                return null;

            if (string.IsNullOrEmpty(emotion))
                return emotions[0].sprite;

            for (var i = 0; i < emotions.Length; i++)
            {
                if (string.Equals(emotions[i].key, emotion, StringComparison.OrdinalIgnoreCase) && emotions[i].sprite != null)
                    return emotions[i].sprite;
            }

            return null;
        }
    }
}
