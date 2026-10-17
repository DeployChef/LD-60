using Cysharp.Threading.Tasks;

namespace LD60.Core
{
    public interface IShellService
    {
        UniTask StartAsync();
        UniTask EnterGameAsync(GameStart start);
        UniTask RestartFromSaveAsync();
        UniTask ReturnToMainMenuAsync();
    }
}
