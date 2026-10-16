using Cysharp.Threading.Tasks;
using LD60.Core;
using UnityEngine;

namespace LD60.Main
{
    public sealed class ShellService : IShellService
    {
        private readonly SceneLoader _scenes = new();
        private bool _busy;

        public async UniTask StartAsync()
        {
            await _scenes.LoadAdditive(GameScenes.MainMenu);
        }

        public async UniTask EnterGameAsync()
        {
            if (_busy)
                return;

            _busy = true;
            try
            {
                await TransitionAsync(GameScenes.MainMenu, GameScenes.Game);
            }
            finally
            {
                _busy = false;
            }
        }

        public async UniTask ReturnToMainMenuAsync()
        {
            if (_busy)
                return;

            _busy = true;
            try
            {
                await TransitionAsync(GameScenes.Game, GameScenes.MainMenu);
            }
            finally
            {
                _busy = false;
            }
        }

        private async UniTask TransitionAsync(string unload, string load)
        {
            await _scenes.LoadAdditive(GameScenes.Loading);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            try
            {
                await _scenes.Unload(unload);
                await _scenes.LoadAdditive(load);
            }
            finally
            {
                await _scenes.Unload(GameScenes.Loading);
            }
        }
    }
}
