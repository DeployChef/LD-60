using System;
using System.Collections.Generic;

namespace LD60.Core.Bus
{
    public sealed class MessageBus : IMessageBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new();

        public void Publish<T>(T message) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
                return;

            var snapshot = list.ToArray();
            for (var i = 0; i < snapshot.Length; i++)
                ((Action<T>)snapshot[i]).Invoke(message);
        }

        public IDisposable Subscribe<T>(Action<T> handler) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
            {
                list = new List<Delegate>();
                _handlers[typeof(T)] = list;
            }

            list.Add(handler);
            return new Subscription(() => list.Remove(handler));
        }

        private sealed class Subscription : IDisposable
        {
            private readonly Action _onDispose;

            public Subscription(Action onDispose)
            {
                _onDispose = onDispose;
            }

            public void Dispose()
            {
                _onDispose();
            }
        }
    }
}
