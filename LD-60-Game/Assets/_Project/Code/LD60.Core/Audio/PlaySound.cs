namespace LD60.Core.Audio
{
    public readonly struct PlaySound
    {
        public PlaySound(string soundId)
        {
            SoundId = soundId;
        }

        public string SoundId { get; }
    }
}
