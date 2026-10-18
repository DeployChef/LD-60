using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LD60.Core.Bus;
using LD60.Gameplay.Context;
using UnityEngine;

namespace LD60.Gameplay.Story
{
    public enum ChapterResult
    {
        NextDay,
        Ending
    }

    public sealed class ChapterRunner : IDisposable
    {
        private readonly ChapterAction[] _actions;
        private readonly NewspaperRule[] _rules;
        private readonly IMessageBus _bus;
        private readonly StoryContext _context;
        private readonly ChapterActionPlay _playback;
        private readonly Action<ChapterResult> _onFinished;
        private readonly bool[] _fired;
        private readonly Queue<int> _queue = new();
        private readonly ChapterRead _read = new ChapterRead();
        private List<IDisposable> _subscriptions = new();

        private ChapterMode? _mode;
        private ChapterAction _current;
        private CancellationTokenSource _life;
        private CancellationTokenSource _play;
        private UniTaskCompletionSource _complete;
        private bool _ended;
        private bool _skip;
        private float _worked;
        private float _segmentStart;
        private CancellationTokenSource _workWait;

        public ChapterRunner(
            ChapterDefinition chapter,
            IMessageBus bus,
            StoryContext context,
            ChapterActionPlay playback,
            Action<ChapterResult> onFinished)
        {
            _actions = chapter.Actions;
            _rules = chapter.Rules;
            _bus = bus;
            _context = context;
            _playback = playback;
            _onFinished = onFinished;
            _fired = new bool[_actions.Length];
            _life = new CancellationTokenSource();
            _read.Context = context;
        }

        public ChapterMode Mode
        {
            get => _mode ?? ChapterMode.Cutscene;
            private set
            {
                if (_mode == value)
                    return;

                var leavingWork = _mode == ChapterMode.Work;
                _mode = value;
                _bus.Publish(new ChapterModeChanged(value));

                if (leavingWork)
                    PauseWork();

                if (value == ChapterMode.Work)
                    BeginWork();
            }
        }

        public bool IsBusy => _current != null;

        public int WorkSeconds
        {
            get
            {
                var time = _worked;
                if (_workWait != null)
                    time += Mathf.Max(0f, Time.time - _segmentStart);
                return (int)time;
            }
        }

        public void Begin()
        {
            _subscriptions.Add(_bus.Subscribe<FieldStateChanged>(OnField));
            _subscriptions.Add(_bus.Subscribe<PaperStamped>(OnStamped));

            _read.Started = true;
            if (!Consider())
                Mode = ChapterMode.Work;
        }

        public void CompleteCurrent()
        {
            if (_complete != null)
            {
                _complete.TrySetResult();
                return;
            }

            _skip = true;
            _play?.Cancel();
            _playback.Cancel();
        }

        public void Dispose()
        {
            CancelWorkWait();
            _playback.Cancel();
            _life?.Cancel();
            _life?.Dispose();
            _life = null;
            _play?.Dispose();
            _play = null;
            _complete?.TrySetCanceled();

            if (_subscriptions == null)
                return;

            for (var i = 0; i < _subscriptions.Count; i++)
                _subscriptions[i].Dispose();

            _subscriptions = null;
        }

        private void OnField(FieldStateChanged message)
        {
            _read.Fill = message.Fill;
            Consider();
        }

        private void OnStamped(PaperStamped message)
        {
            if (Mode != ChapterMode.Work)
                return;

            if (!ChapterDefinition.Allows(_rules, message.Fill, out var reason))
            {
                Debug.Log($"[Story] Paper rejected. {reason} fill={message.Fill:0.00}");
                return;
            }

            Debug.Log($"[Story] Paper accepted. fill={message.Fill:0.00}");
            _read.Fill = message.Fill;
            _read.Stamped = true;
            if (!Consider())
                Debug.Log("[Story] Rules passed and this chapter has no ending action.");
        }

        private bool Consider()
        {
            var started = false;
            for (var i = 0; i < _actions.Length; i++)
            {
                if (_ended)
                    break;

                var action = _actions[i];
                if (_fired[i] || action == null || !action.Matches(_read))
                    continue;

                _fired[i] = true;
                started = true;
                if (_current != null)
                    _queue.Enqueue(i);
                else
                {
                    Play(i).Forget();
                    if (_ended)
                        break;
                }
            }

            _read.Started = false;
            _read.EnteredWork = false;
            _read.Stamped = false;
            return started;
        }

        private void BeginWork()
        {
            _read.EnteredWork = true;
            _read.WorkSeconds = _worked;
            Consider();
            if (_subscriptions != null && Mode == ChapterMode.Work)
                ScheduleWork();
        }

        private async UniTaskVoid Play(int index)
        {
            var action = _actions[index];
            _current = action;
            _skip = false;
            _play?.Dispose();
            _play = CancellationTokenSource.CreateLinkedTokenSource(_life.Token);
            Mode = ChapterMode.Cutscene;

            Debug.Log($"[Story] {action.name}");
            try
            {
                await _playback.Play(action, this);
            }
            catch (OperationCanceledException)
            {
                if (_ended || _subscriptions == null || !_skip)
                    return;
            }

            if (_ended || _subscriptions == null)
                return;

            _current = null;
            _skip = false;
            if (_queue.Count > 0)
            {
                Play(_queue.Dequeue()).Forget();
                return;
            }

            Mode = ChapterMode.Work;
        }

        public StoryContext Context => _context;

        public CancellationToken Token => _play != null ? _play.Token : CancellationToken.None;

        public UniTask WaitUntilCompleted()
        {
            _complete = new UniTaskCompletionSource();
            return _complete.Task;
        }

        public void End(ChapterResult result)
        {
            if (_ended)
                return;

            _ended = true;
            _current = null;
            _queue.Clear();
            var callback = _onFinished;
            Dispose();
            callback?.Invoke(result);
        }

        private void PauseWork()
        {
            if (_workWait == null)
                return;

            _worked += Mathf.Max(0f, Time.time - _segmentStart);
            _read.WorkSeconds = _worked;
            CancelWorkWait();
        }

        private void CancelWorkWait()
        {
            if (_workWait == null)
                return;

            _workWait.Cancel();
            _workWait.Dispose();
            _workWait = null;
        }

        private void ScheduleWork()
        {
            if (_subscriptions == null || Mode != ChapterMode.Work || _workWait != null)
                return;

            _read.WorkSeconds = _worked;
            Consider();
            if (_subscriptions == null || Mode != ChapterMode.Work || _workWait != null)
                return;

            _segmentStart = Time.time;
            _workWait = new CancellationTokenSource();
            Tick(_workWait.Token).Forget();
        }

        private async UniTaskVoid Tick(CancellationToken token)
        {
            var start = _segmentStart;
            try
            {
                await UniTask.Delay(250, cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_subscriptions == null)
                return;

            _worked += Mathf.Max(0f, Time.time - start);
            _read.WorkSeconds = _worked;
            _workWait.Dispose();
            _workWait = null;
            ScheduleWork();
        }
    }
}
