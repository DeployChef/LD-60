using System;

namespace LD60.Core.Bus
{
    public interface IMessageBus
    {
        void Publish<T>(T message) where T : struct;
        IDisposable Subscribe<T>(Action<T> handler) where T : struct;
    }
}
