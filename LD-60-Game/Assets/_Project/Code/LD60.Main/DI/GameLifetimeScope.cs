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
            builder.RegisterBuildCallback(resolver =>
            {
                ButtonSounds.Inject(gameObject.scene, resolver);
                VolumeInjection.Inject(gameObject.scene, resolver);
            });
        }
    }
}
