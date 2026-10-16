namespace LD60.Core.Audio
{
    public interface IAudioManager
    {
        void Play(string soundId);

        float MasterVolume { get; }
        float SfxVolume { get; }
        float MusicVolume { get; }

        void SetMasterVolume(float volume);
        void SetSfxVolume(float volume);
        void SetMusicVolume(float volume);
    }
}
