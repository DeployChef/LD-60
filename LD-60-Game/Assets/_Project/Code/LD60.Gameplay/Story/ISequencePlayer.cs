using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace LD60.Gameplay.Story
{
    public interface IDialogue
    {
        UniTask Say(string text, CancellationToken token);
        UniTask<int> Choose(string[] options, CancellationToken token, string prompt = null);
        void Hide();
    }

    public interface ISequencePlayer
    {
        void Cancel();
        void LookAtRoom();
        void ShowGuest(UnityEngine.Sprite sprite);
        UniTask Enter();
        UniTask Exit();
        UniTask PlayIntroComics();
        UniTask PlayComics(UnityEngine.ScriptableObject comics);
    }
}
