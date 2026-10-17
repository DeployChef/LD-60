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
            builder.RegisterComponentInHierarchy<CapsuleSlot>();
            builder.RegisterComponentInHierarchy<NewspaperDesk>();
            builder.RegisterComponentInHierarchy<SequencePlayer>().As<ISequencePlayer>();
            builder.Register<DayOneCapsule>(Lifetime.Transient);
            builder.Register<DayOnePayer>(Lifetime.Transient);
            builder.Register<DayOneFullPage>(Lifetime.Transient);
            builder.Register<DayOneShot>(Lifetime.Transient);
            builder.Register<DayOneClose>(Lifetime.Transient);
            builder.Register<DayTwoCapsule>(Lifetime.Transient);
            builder.Register<DayTwoReturn>(Lifetime.Transient);
            builder.Register<DayTwoClose>(Lifetime.Transient);
            builder.Register<CreditsClose>(Lifetime.Transient);
            builder.Register<ChapterBehaviours>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<ChapterHeadline>();
            builder.Register<StoryContext>(Lifetime.Singleton);
            builder.Register<Gifts>(Lifetime.Singleton);
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
