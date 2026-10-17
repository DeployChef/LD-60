using Cysharp.Threading.Tasks;
using LD60.Core;
using LD60.Main.Audio;
using LD60.Main.DI;
using UnityEngine;
using VContainer;

namespace LD60.Main
{
    public sealed class Startup : MonoBehaviour
    {
        [SerializeField] private RootLifetimeScope rootScope;

        private static bool _started;

        private void Awake()
        {
            if (_started)
                return;

            _started = true;
            Application.quitting += OnApplicationQuitting;
            BootAsync().Forget();
        }

        private async UniTaskVoid BootAsync()
        {
            rootScope.Build();
            // WebGL: до меню ждём банки FMOD, сэмплы и первый клик игрока (прелоудер).
            await FmodWebglPreloader.PrepareAsync(destroyCancellationToken);
            // Громофон: один глобальный инстанс (дочерний ивент Music) + вечный Music.
            LoudspeakerAudio.EnsureCreated();
            var shell = rootScope.Container.Resolve<IShellService>();
            await shell.StartAsync();
        }

        private static void OnApplicationQuitting()
        {
            _started = false;
            Application.quitting -= OnApplicationQuitting;
        }
    }
}
