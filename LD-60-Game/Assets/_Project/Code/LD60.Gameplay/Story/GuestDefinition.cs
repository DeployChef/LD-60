using System;
using UnityEngine;

namespace LD60.Gameplay.Story
{
    public enum GuestVoice
    {
        None = -1,
        Babka = 0,
        Official = 1,
        Chekist = 2,
        Deputy = 3,
        AnonymousMasked = 4,
        AnonymousUnmasked = 5,
        Fishman = 6
    }

    [Serializable]
    public struct GuestEmotion
    {
        public string key;
        public Sprite sprite;
        public bool overrideVoice;
        public GuestVoice voice;
    }

    [CreateAssetMenu(fileName = "Guest", menuName = "LD60/Guest")]
    public sealed class GuestDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private GuestVoice voice = GuestVoice.None;
        [SerializeField] private GuestEmotion[] emotions = Array.Empty<GuestEmotion>();

        public string DisplayName => displayName;

        public GuestVoice VoiceFor(string emotion)
        {
            if (emotions != null)
                foreach (var entry in emotions)
                    if (entry.overrideVoice && string.Equals(entry.key, emotion, StringComparison.OrdinalIgnoreCase))
                        return entry.voice;
            return voice;
        }

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
