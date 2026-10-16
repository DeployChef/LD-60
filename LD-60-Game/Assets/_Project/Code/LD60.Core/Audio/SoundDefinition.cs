using UnityEngine;

namespace LD60.Core.Audio
{
    [System.Serializable]
    public sealed class SoundDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private AudioChannel channel = AudioChannel.Sfx;
        [SerializeField] private AudioClip[] clips;
        [SerializeField] private bool allowOverlap = true;
        [SerializeField] private float cooldown;
        [SerializeField] private bool loop;
        [SerializeField] private float basePitch = 1f;

        public string Id => id;
        public AudioChannel Channel => channel;
        public AudioClip[] Clips => clips;
        public bool AllowOverlap => allowOverlap;
        public float Cooldown => cooldown;
        public bool Loop => loop;
        public float BasePitch => basePitch;
    }
}
