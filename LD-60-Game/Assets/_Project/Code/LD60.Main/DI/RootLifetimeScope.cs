using LD60.Core;
using LD60.Core.Audio;
using LD60.Core.Bus;
using LD60.Gameplay.Context;
using LD60.Main.Audio;
using VContainer;
using VContainer.Unity;

namespace LD60.Main.DI
{
    public sealed class RootLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<ShellService>(Lifetime.Singleton).As<IShellService>().As<IGameSession>();
            builder.Register<MessageBus>(Lifetime.Singleton).As<IMessageBus>();
            builder.RegisterComponentInHierarchy<AudioManager>().As<IAudioManager>();
            builder.Register<AudioService>(Lifetime.Singleton);
            builder.RegisterBuildCallback(resolver => resolver.Resolve<AudioService>());
        }
    }
}
