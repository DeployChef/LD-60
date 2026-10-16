using System;
using LD60.Core.Bus;

namespace LD60.Core.Audio
{
    public sealed class AudioService : IDisposable
    {
        private readonly IDisposable _subscription;

        public AudioService(IMessageBus bus, IAudioManager audio)
        {
            _subscription = bus.Subscribe<PlaySound>(message =>
            {
                if (!string.IsNullOrEmpty(message.SoundId))
                    audio.Play(message.SoundId);
            });
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }
    }
}
