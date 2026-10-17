using LD60.Core;
using LD60.Core.Bus;
using LD60.Gameplay.Context;
using UnityEngine;

namespace LD60.Gameplay.Story
{
    public sealed class StoryDriver : System.IDisposable
    {
        private readonly IMessageBus _bus;
        private readonly StoryContext _context;
        private readonly Desk _desk;
        private readonly ChapterDefinition[] _chapters;
        private readonly DayStartPreset _debugStart;
        private readonly IGameSession _session;
        private ChapterRunner _runner;
        private int _index = -1;
        private bool _advance;

        public StoryDriver(
            IMessageBus bus,
            StoryContext context,
            Desk desk,
            ChapterDefinition[] chapters,
            DayStartPreset debugStart,
            IGameSession session)
        {
            _bus = bus;
            _context = context;
            _desk = desk;
            _chapters = chapters ?? System.Array.Empty<ChapterDefinition>();
            _debugStart = debugStart;
            _session = session;
        }

        public int ActiveIndex => _index;

        public void Begin()
        {
            if (_session.Launch == GameStart.Continue && _session.TryGetCheckpoint(out var index, out var saved))
            {
                _context.Load(saved);
                Debug.Log($"[Story] Continue chapter {index}. {_context.Describe()}");
                BeginChapter(index);
                return;
            }

            if (_debugStart != null)
            {
                _context.Load(_debugStart.Context);
                Debug.Log($"[Story] Debug start at chapter {_debugStart.ChapterIndex}. {_context.Describe()}");
                BeginChapter(_debugStart.ChapterIndex);
                return;
            }

            _context.Load(new StoryContextData());
            BeginChapter(0);
        }

        public void Tick()
        {
            if (_advance)
            {
                _advance = false;
                Next();
                return;
            }

            _runner?.Tick(Time.deltaTime);
        }

        public void Next()
        {
            BeginChapter(_index + 1);
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
            _runner?.CompleteCurrent();
        }

        public string Describe()
        {
            var chapter = _index >= 0 && _index < _chapters.Length && _chapters[_index] != null
                ? _chapters[_index].name
                : "none";
            var mode = _runner != null ? _runner.Mode.ToString() : "none";
            var work = _runner != null ? _runner.WorkTime : 0f;
            var busy = _runner != null && _runner.IsBusy;
            var day = _index >= 0 ? _index + 1 : 0;
            return $"day {day} ({chapter}), mode {mode}, work {work:0.0}s, busy {busy}, fill {_desk.Fill:0.00}, {_context.Describe()}";
        }

        public void Dispose()
        {
            _runner?.Dispose();
            _runner = null;
        }

        private void BeginChapter(int index)
        {
            _runner?.Dispose();
            _runner = null;

            if (_chapters.Length == 0)
            {
                Debug.LogError("[Story] No chapters assigned on StoryBootstrap.");
                return;
            }

            if (index < 0 || index >= _chapters.Length)
            {
                Debug.Log(index > 0 ? "[Story] No more chapters." : "[Story] Chapter index is out of range.");
                return;
            }

            var definition = _chapters[index];
            if (definition == null)
            {
                Debug.LogError($"[Story] Chapter {index} is not assigned.");
                return;
            }

            _index = index;
            _desk.ResetDay();
            _session.WriteCheckpoint(index, _context.Capture());
            Debug.Log($"[Story] Chapter {index} '{definition.name}' started. {_context.Describe()}");
            _runner = new ChapterRunner(definition, _bus, OnDayFinished);
            _runner.Begin();
        }

        private void OnDayFinished()
        {
            _context.SetKeptArticles(_desk.KeptArticleIds);
            Debug.Log($"[Story] Day closed. {_context.Describe()}");
            _advance = true;
        }
    }
}
