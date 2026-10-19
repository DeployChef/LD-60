using System;
using Cysharp.Threading.Tasks;
using LD60.Core;
using LD60.Core.Bus;
using LD60.Gameplay.Context;
using UnityEngine;
using VContainer.Unity;

namespace LD60.Gameplay.Story
{
    public sealed class StoryDriver : IStartable, IDisposable
    {
        private readonly IMessageBus _bus;
        private readonly StoryContext _context;
        private readonly Desk _desk;
        private readonly IGameSession _session;
        private readonly IShellService _shell;
        private readonly ChapterActionPlay _playback;
        private readonly ISequencePlayer _player;
        private readonly ChapterDefinition[] _chapters;
        private readonly ChapterDefinition _finale;
        private readonly DayStartPreset _debugStart;
        private ChapterRunner _runner;
        private int _index = -1;
        private bool _inFinale;
        private bool _disposed;

        public StoryDriver(
            IMessageBus bus,
            StoryContext context,
            Desk desk,
            IGameSession session,
            IShellService shell,
            ChapterActionPlay playback,
            ISequencePlayer player,
            StoryBook story)
        {
            _bus = bus;
            _context = context;
            _desk = desk;
            _session = session;
            _shell = shell;
            _playback = playback;
            _player = player;
            _chapters = story.Chapters;
            _finale = story.Finale;
            _debugStart = story.DebugStart;
            DebugKeys = story.DebugKeys;
        }

        public bool DebugKeys { get; }
        public int ActiveIndex => _index;

        public void Start()
        {
            if (_session.Launch == GameStart.Continue && _session.TryGetCheckpoint(out var index, out var saved))
            {
                _context.Load(saved);
                Debug.Log($"[Story] Continue chapter {index}. {_context.Describe()}");
                StartChapterAsync(index, playIntro: false).Forget();
                return;
            }

            if (_debugStart != null)
            {
                _context.Load(_debugStart.Context);
                Debug.Log($"[Story] Debug start at chapter {_debugStart.ChapterIndex}. {_context.Describe()}");
                StartChapterAsync(_debugStart.ChapterIndex, playIntro: _debugStart.ChapterIndex == 0).Forget();
                return;
            }

            _context.Load(new StoryContextData());
            StartChapterAsync(0, playIntro: true).Forget();
        }

        private async UniTaskVoid StartChapterAsync(int index, bool playIntro)
        {
            if (playIntro && _player != null)
            {
                await _player.PlayIntroComics();
                if (_disposed)
                    return;
            }

            Play(index);
        }

        public void AddFill(float amount)
        {
            _desk.SetFill(_desk.Fill + amount);
        }

        public void Stamp()
        {
            _desk.Stamp();
        }

        public void CompleteCurrent()
        {
            if (_runner != null)
            {
                _runner.CompleteCurrent();
                return;
            }

            _player?.Cancel();
        }

        public void LogStatus()
        {
            Debug.Log($"[Story] {Describe()}");
        }

        public string Describe()
        {
            var chapter = _inFinale
                ? _finale != null ? _finale.name : "finale"
                : _index >= 0 && _index < _chapters.Length && _chapters[_index] != null ? _chapters[_index].name : "none";
            var mode = _runner != null ? _runner.Mode.ToString() : "none";
            var work = _runner != null ? _runner.WorkSeconds : 0;
            var busy = _runner != null && _runner.IsBusy;
            var day = _index >= 0 ? _index + 1 : 0;
            var where = _inFinale ? $"finale after day {day} ({chapter})" : $"day {day} ({chapter})";
            return $"{where}, mode {mode}, work {work}s, busy {busy}, fill {_desk.Fill:0.00}, {_context.Describe()}";
        }

        public void Dispose()
        {
            _disposed = true;
            _runner?.Dispose();
            _runner = null;
        }

        private void Play(int index)
        {
            BeginChapter(index, false);
        }

        private void BeginChapter(int index, bool finale)
        {
            _runner?.Dispose();
            _runner = null;

            ChapterDefinition chapter;
            if (finale)
            {
                chapter = _finale;
            }
            else
            {
                if (_chapters.Length == 0)
                {
                    Debug.LogError("[Story] No chapters assigned on StoryBook.");
                    return;
                }

                if (index >= _chapters.Length)
                {
                    Debug.Log("[Story] No more days. Finale.");
                    BeginChapter(_index, true);
                    return;
                }

                if (index < 0)
                {
                    Debug.Log("[Story] Chapter index is out of range.");
                    return;
                }

                chapter = _chapters[index];
                _index = index;
            }

            if (chapter == null)
            {
                Debug.LogError(finale
                    ? "[Story] Ending fired, but no finale chapter is assigned."
                    : $"[Story] Chapter {index} is not assigned.");
                if (finale)
                    Finish();
                return;
            }

            _inFinale = finale;
            _desk.ResetDay();
            _bus.Publish(new DayStarted(chapter));
            if (finale)
                _session.ClearCheckpoint();
            else
                _session.WriteCheckpoint(_index, _context.Capture());

            Debug.Log(finale
                ? $"[Story] Finale '{chapter.name}'."
                : $"[Story] Chapter {index} '{chapter.name}' started. {_context.Describe()}");
            _runner = new ChapterRunner(chapter, _bus, _context, _playback, OnChapterFinished);
            _runner.Begin();
        }

        private void OnChapterFinished(ChapterResult result)
        {
            if (_inFinale)
            {
                Finish();
                return;
            }

            if (result == ChapterResult.Ending)
            {
                Debug.Log("[Story] Ending. The next day will not start.");
                BeginChapter(_index, true);
                return;
            }

            _context.SetKeptArticles(_desk.KeptArticleIds);
            Debug.Log($"[Story] Day closed. {_context.Describe()}");
            BeginChapter(_index + 1, false);
        }

        private async void Finish()
        {
            Debug.Log("[Story] Finale finished.");
            var alive = _player is UnityEngine.Object u ? u != null : _player != null;
            if (alive)
                await _player.PlayCredits();

            _shell.FinishStoryAsync().Forget();
        }
    }
}
