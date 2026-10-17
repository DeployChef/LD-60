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
        private readonly ChapterEvent[] _events;
        private readonly NewspaperRule[] _rules;
        private readonly IMessageBus _bus;
        private readonly StoryContext _context;
        private readonly Action<ChapterResult> _onFinished;
        private readonly bool[] _fired;
        private readonly Queue<int> _queue = new();
        private List<IDisposable> _subscriptions = new();

        private ChapterMode? _mode;
        private ChapterEvent _current;
        private CancellationTokenSource _workWait;
        private float _worked;
        private float _segmentStart;

        public ChapterRunner(ChapterDefinition chapter, IMessageBus bus, StoryContext context, Action<ChapterResult> onFinished)
        {
            _events = chapter.Events;
            _rules = chapter.Rules;
            _bus = bus;
            _context = context;
            _onFinished = onFinished;
            _fired = new bool[_events.Length];
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
            _subscriptions.Add(_bus.Subscribe<DialogueFinished>(OnDialogueFinished));
            _subscriptions.Add(_bus.Subscribe<CapsuleTaken>(OnCapsuleTaken));

            if (!RaiseMatching(StoryTriggerKind.OnChapterStart, null))
                Mode = ChapterMode.Work;
        }

        public void CompleteCurrent()
        {
            if (_current != null)
                FinishCurrent();
        }

        public void Dispose()
        {
            CancelWorkWait();
            if (_subscriptions == null)
                return;

            for (var i = 0; i < _subscriptions.Count; i++)
                _subscriptions[i].Dispose();

            _subscriptions = null;
        }

        private void OnField(FieldStateChanged message)
        {
            RaiseMatching(StoryTriggerKind.OnFieldFillAtLeast, null, message.Fill);
        }

        private void OnStamped(PaperStamped message)
        {
            if (Mode != ChapterMode.Work)
                return;

            if (!RulesMet(message.Fill))
            {
                Debug.Log($"[Story] Paper rejected. fill={message.Fill:0.00}");
                return;
            }

            Debug.Log($"[Story] Paper accepted. fill={message.Fill:0.00}");
            if (!RaiseMatching(StoryTriggerKind.OnPaperStamped, null))
                Debug.Log("[Story] Rules passed and this chapter has no ending event.");
        }

        private void OnDialogueFinished(DialogueFinished message)
        {
            RaiseMatching(StoryTriggerKind.OnDialogueFinished, message.DialogueId);
        }

        private void OnCapsuleTaken(CapsuleTaken message)
        {
            RaiseMatching(StoryTriggerKind.OnCapsuleTaken, message.CapsuleId);
        }

        private bool RulesMet(float fill)
        {
            for (var i = 0; i < _rules.Length; i++)
            {
                var rule = _rules[i];
                if (rule == null)
                    continue;

                var id = rule.parameterId ?? string.Empty;
                if (id.Equals("fill", StringComparison.OrdinalIgnoreCase))
                {
                    if (fill + 0.0001f < rule.minimum)
                        return false;
                    continue;
                }

                Debug.LogWarning($"[Story] Unknown newspaper rule '{id}'.");
                return false;
            }

            return true;
        }

        private bool RaiseMatching(StoryTriggerKind kind, string id, float value = float.NaN)
        {
            var started = false;
            for (var i = 0; i < _events.Length; i++)
            {
                if (_fired[i] || _events[i] == null || _events[i].trigger != kind || !CanFire(_events[i]))
                    continue;

                if (!TriggerMatches(_events[i], id, value))
                    continue;

                _fired[i] = true;
                started = true;
                if (_current != null)
                    _queue.Enqueue(i);
                else
                    Play(i);
            }

            return started;
        }

        private bool CanFire(ChapterEvent storyEvent)
        {
            return _context.Meets(storyEvent.conditionParameter, storyEvent.conditionMinimum);
        }

        private static bool TriggerMatches(ChapterEvent storyEvent, string id, float value)
        {
            switch (storyEvent.trigger)
            {
                case StoryTriggerKind.AfterWorkSeconds:
                    return !float.IsNaN(value) && value + 0.0001f >= storyEvent.seconds;
                case StoryTriggerKind.OnFieldFillAtLeast:
                    return !float.IsNaN(value) && value + 0.0001f >= storyEvent.fill;
                case StoryTriggerKind.OnDialogueFinished:
                case StoryTriggerKind.OnCapsuleTaken:
                    return string.Equals(storyEvent.id, id, StringComparison.Ordinal);
                default:
                    return true;
            }
        }

        private void BeginWork()
        {
            RaiseMatching(StoryTriggerKind.OnEnterWork, null);
            if (_subscriptions != null && Mode == ChapterMode.Work)
                ScheduleWork();
        }

        private void Play(int index)
        {
            _current = _events[index];
            Mode = _current.action == StoryActionKind.Dialogue
                ? ChapterMode.Dialogue
                : ChapterMode.Cutscene;

            if (!string.IsNullOrEmpty(_current.setParameter))
                _context.SetParameter(_current.setParameter, _current.setValue);

            Debug.Log($"[Story] {_current.action} '{_current.actionId}'");
            if (_current.action != StoryActionKind.Summary)
                FinishCurrent();
        }

        private void FinishCurrent()
        {
            if (_current == null)
                return;

            var action = _current.action;
            _current = null;

            if (action == StoryActionKind.Summary)
            {
                End(ChapterResult.NextDay);
                return;
            }

            if (action == StoryActionKind.Ending)
            {
                End(ChapterResult.Ending);
                return;
            }

            if (_queue.Count > 0)
            {
                Play(_queue.Dequeue());
                return;
            }

            Mode = ChapterMode.Work;
        }

        private void End(ChapterResult result)
        {
            Dispose();
            _onFinished?.Invoke(result);
        }

        private void PauseWork()
        {
            if (_workWait == null)
                return;

            _worked += Mathf.Max(0f, Time.time - _segmentStart);
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

            RaiseMatching(StoryTriggerKind.AfterWorkSeconds, null, _worked);
            if (_subscriptions == null || Mode != ChapterMode.Work || _workWait != null)
                return;

            var mark = NextWorkMark();
            if (float.IsPositiveInfinity(mark))
                return;

            var wait = Mathf.Max(0f, mark - _worked);
            var milliseconds = Mathf.Max(1, Mathf.CeilToInt(wait * 1000f));
            _segmentStart = Time.time;
            _workWait = new CancellationTokenSource();
            WaitUntilWork(mark, milliseconds, _workWait.Token).Forget();
        }

        private async UniTaskVoid WaitUntilWork(float mark, int milliseconds, CancellationToken token)
        {
            try
            {
                await UniTask.Delay(milliseconds, cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_subscriptions == null)
                return;

            _workWait.Dispose();
            _workWait = null;
            _worked = mark;
            ScheduleWork();
        }

        private float NextWorkMark()
        {
            var next = float.PositiveInfinity;
            for (var i = 0; i < _events.Length; i++)
            {
                var storyEvent = _events[i];
                if (_fired[i] || storyEvent == null || storyEvent.trigger != StoryTriggerKind.AfterWorkSeconds)
                    continue;

                if (storyEvent.seconds > _worked + 0.0001f && storyEvent.seconds < next)
                    next = storyEvent.seconds;
            }

            return next;
        }
    }
}
