using LD60.Gameplay.Context;
using LD60.Gameplay.Story;
using LD60.UI;
using VContainer;
using VContainer.Unity;

namespace LD60.Main.DI
{
    public sealed class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<PauseMenuController>();
            builder.RegisterComponentInHierarchy<StoryBook>();
            builder.RegisterComponentInHierarchy<StoryDebugController>();
            builder.Register<StoryContext>(Lifetime.Singleton);
            builder.Register<Desk>(Lifetime.Singleton);
            builder.RegisterEntryPoint<StoryDriver>().AsSelf();
            builder.RegisterBuildCallback(resolver =>
            {
                ButtonSounds.Inject(gameObject.scene, resolver);
                VolumeInjection.Inject(gameObject.scene, resolver);
            });
        }
    }
}
