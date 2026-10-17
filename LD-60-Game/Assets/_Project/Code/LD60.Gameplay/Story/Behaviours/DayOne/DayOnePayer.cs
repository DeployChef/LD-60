using Cysharp.Threading.Tasks;

namespace LD60.Gameplay.Story
{
    public sealed class DayOnePayer : ChapterBehaviour
    {
        public const string Id = "day-one.payer";

        private readonly Gifts _gifts;
        private readonly ISequencePlayer _player;

        public DayOnePayer(Gifts gifts, ISequencePlayer player)
        {
            _gifts = gifts;
            _player = player;
        }

        public override bool CanPlay(ChapterRead read)
        {
            return read.WorkSeconds >= 5f;
        }

        public override async UniTask Play(ChapterRunner chapter)
        {
            _player.LookAtRoom();
            await _player.Enter();
            await _player.Say("You kept the city quiet. Take one.");
            var choice = await _player.Choose("The money.", "The lost-dog clipping.");
            if (choice == 0)
            {
                _gifts.Give("money", 4);
                await _player.Say("I'll be back tomorrow.");
            }
            else
            {
                _gifts.Give("lost-dog");
                await _player.Say("Print that one. I'll be back tomorrow.");
            }

            await _player.Exit();
        }
    }
}
