using Cysharp.Threading.Tasks;

namespace LD60.Core
{
    public interface IShellService
    {
        bool ShowIntro { get; }

        UniTask StartAsync();
        UniTask EnterGameAsync(GameStart start);
        UniTask RestartFromSaveAsync();
        UniTask ReturnToMainMenuAsync();
        UniTask FinishStoryAsync();
    }
}
