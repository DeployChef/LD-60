using Cysharp.Threading.Tasks;

namespace LD60.Gameplay.Story
{
    public sealed class DayTwoReturn : ChapterBehaviour
    {
        public const string Id = "day-two.return";

        private readonly Gifts _gifts;
        private readonly ISequencePlayer _player;

        public DayTwoReturn(Gifts gifts, ISequencePlayer player)
        {
            _gifts = gifts;
            _player = player;
        }

        public override bool CanPlay(ChapterRead read)
        {
            return read.WorkSeconds >= 8f;
        }

        public override async UniTask Play(ChapterRunner chapter)
        {
            _player.LookAtRoom();
            await _player.Enter();
            await _player.Say("I came back. The city still owes you.");
            _gifts.Give("money", 2);
            await _player.Exit();
        }
    }
}
