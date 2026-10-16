using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace LD60.Main
{
    public sealed class SceneLoader
    {
        public bool IsLoaded(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded;
        }

        public async UniTask LoadAdditive(string sceneName, CancellationToken cancellationToken = default)
        {
            if (IsLoaded(sceneName))
                return;

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            await op.ToUniTask(cancellationToken: cancellationToken);
        }

        public async UniTask Unload(string sceneName, CancellationToken cancellationToken = default)
        {
            if (!IsLoaded(sceneName))
                return;

            await SceneManager.UnloadSceneAsync(sceneName).ToUniTask(cancellationToken: cancellationToken);
        }
    }
}
