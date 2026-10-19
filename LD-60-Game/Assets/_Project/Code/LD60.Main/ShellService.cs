using Cysharp.Threading.Tasks;
using LD60.Core;
using LD60.Gameplay.Context;
using LD60.Main.Audio;
using UnityEngine;

namespace LD60.Main
{
    public sealed class ShellService : IShellService, IGameSession
    {
        private readonly SceneLoader _scenes = new();
        private readonly DayCheckpointStore _store = new();
        private readonly IGameAnalytics _analytics;
        private bool _busy;
        private bool _showIntro = true;
        private bool _hasCheckpoint;
        private int _chapterIndex;
        private StoryContextData _checkpoint = new();

        public ShellService(IGameAnalytics analytics = null)
        {
            _analytics = analytics;
            _hasCheckpoint = _store.TryLoad(out _chapterIndex, out _checkpoint);
        }

        public GameStart Launch { get; private set; } = GameStart.New;
        public bool ShowIntro => _showIntro;

        public bool HasResumableDay => _hasCheckpoint && _chapterIndex > 0;

        public bool TryGetCheckpoint(out int chapterIndex, out StoryContextData context)
        {
            chapterIndex = _chapterIndex;
            context = _checkpoint;
            return _hasCheckpoint;
        }

        public void WriteCheckpoint(int chapterIndex, StoryContextData context)
        {
            _hasCheckpoint = true;
            _chapterIndex = chapterIndex;
            _checkpoint = context != null ? context.Clone() : new StoryContextData();
            _store.Save(_chapterIndex, _checkpoint);
        }

        public async UniTask StartAsync()
        {
            await _scenes.LoadAdditive(GameScenes.MainMenu);
        }

        public async UniTask EnterGameAsync(GameStart start)
        {
            if (_busy)
                return;

            _busy = true;
            Launch = start;
            if (start == GameStart.New)
                ClearCheckpoint();

            _analytics?.TrackGameStart();

            try
            {
                await TransitionAsync(GameScenes.MainMenu, GameScenes.Game);
                LoudspeakerAudio.SetOn(true);
            }
            finally
            {
                _busy = false;
            }
        }

        public async UniTask RestartFromSaveAsync()
        {
            if (_busy)
                return;

            _busy = true;
            Time.timeScale = 1f;
            Launch = GameStart.Continue;
            try
            {
                await TransitionAsync(GameScenes.Game, GameScenes.Game);
                LoudspeakerAudio.SetOn(true);
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
            _showIntro = false;
            try
            {
                LoudspeakerAudio.SetOn(false);
                await TransitionAsync(GameScenes.Game, GameScenes.MainMenu);
            }
            finally
            {
                _busy = false;
            }
        }

        public async UniTask FinishStoryAsync()
        {
            if (_busy)
                return;

            _busy = true;
            _showIntro = true;
            Time.timeScale = 1f;
            ClearCheckpoint();
            try
            {
                await TransitionAsync(GameScenes.Game, GameScenes.MainMenu);
            }
            finally
            {
                _busy = false;
            }
        }

        public void ClearCheckpoint()
        {
            _hasCheckpoint = false;
            _chapterIndex = 0;
            _checkpoint = new StoryContextData();
            _store.Clear();
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
