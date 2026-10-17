using System.Threading;
using Cysharp.Threading.Tasks;

namespace LD60.Gameplay.Story
{
    public interface ISequencePlayer
    {
        void Cancel();
        UniTask Enter();
        UniTask Say(string line);
        UniTask<int> Choose(params string[] options);
        UniTask Exit();
        void LookAtRoom();
    }
}
