using LD60.UI;
using VContainer;
using VContainer.Unity;

namespace LD60.Main.DI
{
    public sealed class MainMenuLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<MainMenuController>();
            builder.RegisterBuildCallback(resolver =>
            {
                ButtonSounds.Inject(gameObject.scene, resolver);
                VolumeInjection.Inject(gameObject.scene, resolver);
            });
        }
    }
}
