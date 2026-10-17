using Cysharp.Threading.Tasks;

namespace LD60.Gameplay.Story
{
    public sealed class DayOneFullPage : ChapterBehaviour
    {
        public const string Id = "day-one.full-page";

        private readonly ISequencePlayer _player;

        public DayOneFullPage(ISequencePlayer player)
        {
            _player = player;
        }

        public override bool CanPlay(ChapterRead read)
        {
            return read.Fill >= 0.8f;
        }

        public override async UniTask Play(ChapterRunner chapter)
        {
            _player.LookAtRoom();
            await _player.Enter();
            await _player.Say("The page is full enough. Send it.");
            await _player.Exit();
        }
    }
}
