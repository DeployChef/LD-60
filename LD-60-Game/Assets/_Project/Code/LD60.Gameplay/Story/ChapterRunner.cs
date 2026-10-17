using System;
using System.Collections.Generic;
using LD60.Core.Bus;
using LD60.Gameplay.Context;
using UnityEngine;

namespace LD60.Gameplay.Story
{
    public sealed class ChapterRunner : IDisposable
    {
        private readonly ChapterDefinition _definition;
        private readonly IMessageBus _bus;
        private readonly Action _onDayFinished;
        private readonly bool[] _fired;
        private readonly Queue<int> _queue = new();
        private readonly List<IDisposable> _subscriptions = new();

        private ChapterEvent _current;
        private bool _busy;
        private bool _finishNextFrame;
        private bool _enteredWork;
        private bool _modePublished;
        private bool _dayFinished;
        private float _workTime;

        public ChapterRunner(ChapterDefinition definition, IMessageBus bus, Action onDayFinished)
        {
            _definition = definition;
            _bus = bus;
            _onDayFinished = onDayFinished;
            _fired = new bool[_definition.Events.Length];
        }

        public ChapterMode Mode { get; private set; } = ChapterMode.Cutscene;
        public bool IsBusy => _busy;
        public float WorkTime => _workTime;

        public void Begin()
        {
            _subscriptions.Add(_bus.Subscribe<FieldStateChanged>(OnField));
            _subscriptions.Add(_bus.Subscribe<PaperStamped>(OnStamped));
            _subscriptions.Add(_bus.Subscribe<DialogueFinished>(OnDialogueFinished));
            _subscriptions.Add(_bus.Subscribe<CapsuleTaken>(OnCapsuleTaken));

            if (!RaiseMatching(StoryTriggerKind.OnChapterStart, null))
                EnterWork();
        }

        public void Tick(float deltaTime)
        {
            if (_dayFinished)
                return;

            if (_finishNextFrame)
            {
                _finishNextFrame = false;
                FinishCurrent();
                return;
            }

            if (Mode != ChapterMode.Work || _busy)
                return;

            _workTime += deltaTime;
            var events = _definition.Events;
            for (var i = 0; i < events.Length; i++)
            {
                if (_fired[i] || events[i] == null || events[i].trigger != StoryTriggerKind.AfterWorkSeconds)
                    continue;

                if (_workTime + 0.0001f < events[i].seconds)
                    continue;

                _fired[i] = true;
                if (_busy)
                    _queue.Enqueue(i);
                else
                    Play(i);
            }
        }

        public void CompleteCurrent()
        {
            if (_busy)
                FinishCurrent();
        }

        public void Dispose()
        {
            for (var i = 0; i < _subscriptions.Count; i++)
                _subscriptions[i].Dispose();

            _subscriptions.Clear();
        }

        private void OnField(FieldStateChanged message)
        {
            RaiseMatching(StoryTriggerKind.OnFieldFillAtLeast, null, message.Fill);
        }

        private void OnStamped(PaperStamped message)
        {
            if (_dayFinished || _busy || Mode != ChapterMode.Work)
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
            var rules = _definition.Rules;
            for (var i = 0; i < rules.Length; i++)
            {
                var rule = rules[i];
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
            var events = _definition.Events;
            var started = false;
            for (var i = 0; i < events.Length; i++)
            {
                if (_fired[i] || events[i] == null || events[i].trigger != kind)
                    continue;

                if (!TriggerMatches(events[i], id, value))
                    continue;

                _fired[i] = true;
                started = true;
                if (_busy)
                    _queue.Enqueue(i);
                else
                    Play(i);
            }

            return started;
        }

        private static bool TriggerMatches(ChapterEvent storyEvent, string id, float value)
        {
            switch (storyEvent.trigger)
            {
                case StoryTriggerKind.AfterWorkSeconds:
                    return false;
                case StoryTriggerKind.OnFieldFillAtLeast:
                    return !float.IsNaN(value) && value + 0.0001f >= storyEvent.fill;
                case StoryTriggerKind.OnDialogueFinished:
                case StoryTriggerKind.OnCapsuleTaken:
                    return string.Equals(storyEvent.id, id, StringComparison.Ordinal);
                default:
                    return true;
            }
        }

        private void EnterWork()
        {
            _enteredWork = true;
            SetMode(ChapterMode.Work);
            RaiseMatching(StoryTriggerKind.OnEnterWork, null);
        }

        private void Play(int index)
        {
            _current = _definition.Events[index];
            _busy = true;
            var mode = _current.action switch
            {
                StoryActionKind.Dialogue => ChapterMode.Dialogue,
                StoryActionKind.Sequence => ChapterMode.Cutscene,
                StoryActionKind.Capsule => ChapterMode.Cutscene,
                StoryActionKind.Summary => ChapterMode.Cutscene,
                _ => ChapterMode.Cutscene
            };
            SetMode(mode);
            Debug.Log($"[Story] {_current.action} '{_current.actionId}'");
            if (_current.action != StoryActionKind.Summary)
                _finishNextFrame = true;
        }

        private void FinishCurrent()
        {
            if (!_busy)
                return;

            var action = _current.action;
            _current = null;
            _busy = false;
            _finishNextFrame = false;

            if (action == StoryActionKind.Summary)
            {
                _dayFinished = true;
                _onDayFinished?.Invoke();
                return;
            }

            if (_queue.Count > 0)
            {
                Play(_queue.Dequeue());
                return;
            }

            if (!_enteredWork)
                EnterWork();
            else
                SetMode(ChapterMode.Work);
        }

        private void SetMode(ChapterMode mode)
        {
            if (_modePublished && Mode == mode)
                return;

            Mode = mode;
            _modePublished = true;
            _bus.Publish(new ChapterModeChanged(mode));
        }
    }
}
