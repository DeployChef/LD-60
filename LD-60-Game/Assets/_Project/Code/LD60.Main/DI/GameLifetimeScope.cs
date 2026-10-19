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
            builder.RegisterComponentInHierarchy<DeskMoneyView>();
            builder.RegisterComponentInHierarchy<SequencePlayer>().As<ISequencePlayer>();
            builder.RegisterComponentInHierarchy<DialogueManager>().As<IDialogue>();
            builder.Register<ChapterActionPlay>(Lifetime.Singleton);
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
