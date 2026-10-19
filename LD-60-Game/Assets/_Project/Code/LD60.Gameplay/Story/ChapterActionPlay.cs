using System;
using Cysharp.Threading.Tasks;
using LD60.Core.Bus;

namespace LD60.Gameplay.Story
{
    public sealed class ChapterActionPlay
    {
        private readonly ISequencePlayer _player;
        private readonly IDialogue _dialogue;
        private readonly Gifts _gifts;
        private readonly IMessageBus _bus;
        private readonly Desk _desk;

        public ChapterActionPlay(ISequencePlayer player, IDialogue dialogue, Gifts gifts, IMessageBus bus, Desk desk)
        {
            _player = player;
            _dialogue = dialogue;
            _gifts = gifts;
            _bus = bus;
            _desk = desk;
        }

        public void Cancel()
        {
            if (Alive(_player))
                _player.Cancel();
            if (Alive(_dialogue))
                _dialogue.Hide();
        }

        private static bool Alive(object target)
        {
            if (target == null)
                return false;

            var unity = target as UnityEngine.Object;
            if (ReferenceEquals(unity, null))
                return true;

            return unity != null;
        }

        public async UniTask Play(ChapterAction action, ChapterRunner chapter)
        {
            var visit = action as VisitAction;
            if (visit != null)
            {
                await PlayVisit(visit, chapter);
                return;
            }

            var capsule = action as CapsuleAction;
            if (capsule != null)
            {
                await WaitForCapsule(capsule.CapsuleId, chapter);
                return;
            }

            var close = action as CloseAction;
            if (close != null)
            {
                if (close.When == ChapterWhen.PaperStamped)
                {
                    var fill = _desk != null ? _desk.Fill : 0f;
                    var politics = _desk != null ? _desk.Politics : 0f;
                    var social = _desk != null ? _desk.Social : 0f;
                    UnityEngine.Debug.Log($"[Story] Summary. fill={fill:0.00} politics={politics:0.00} social={social:0.00}");
                    _bus.Publish(new DaySummary(fill, politics, social));
                    chapter.End(ChapterResult.NextDay);
                    return;
                }

                await chapter.WaitUntilCompleted();
                chapter.End(ChapterResult.NextDay);
                return;
            }

            var ending = action as EndingAction;
            if (ending != null)
            {
                if (ending.Comics != null && Alive(_player))
                    await _player.PlayComics(ending.Comics);

                chapter.End(ChapterResult.Ending);
                return;
            }

            var credits = action as CreditsAction;
            if (credits != null)
            {
                if (Alive(_player))
                    await _player.PlayCredits(chapter.Token);

                chapter.End(ChapterResult.Ending);
                return;
            }

            UnityEngine.Debug.LogWarning("[Story] " + action.name + " has no playable type.");
        }

        private async UniTask PlayVisit(VisitAction visit, ChapterRunner chapter)
        {
            var guest = visit.Guest;
            var steps = visit.Steps;
            var index = 0;
            while (index >= 0 && index < steps.Length)
            {
                var step = steps[index];
                if (step == null || !step.rule.Passes(chapter.Context))
                {
                    index++;
                    continue;
                }

                var following = step.next >= 0 ? step.next : index + 1;
                Remember(chapter, step);
                switch (step.kind)
                {
                    case VisitStepKind.Enter:
                        Show(guest, step.emotion);
                        _player.LookAtRoom();
                        await _player.Enter();
                        break;
                    case VisitStepKind.Say:
                        Show(guest, step.emotion);
                        await _dialogue.Say(step.text, chapter.Token);
                        break;
                    case VisitStepKind.Choose:
                        Show(guest, step.emotion);
                        var choice = await _dialogue.Choose(new[] { step.optionA, step.optionB }, chapter.Token,
                            string.IsNullOrEmpty(step.text) ? null : step.text);
                        following = choice == 0 ? step.nextA : step.nextB;
                        if (following < 0)
                            following = index + 1;
                        break;
                    case VisitStepKind.Give:
                        if (_gifts != null)
                            _gifts.Give(step.giftId, step.amount);
                        break;
                    case VisitStepKind.Exit:
                        if (_dialogue != null)
                            _dialogue.Hide();
                        await _player.Exit();
                        following = -1;
                        break;
                    case VisitStepKind.Remember:
                        break;
                }

                index = following;
            }

            if (_dialogue != null)
                _dialogue.Hide();
        }

        private static void Remember(ChapterRunner chapter, VisitStep step)
        {
            if (chapter.Context == null || string.IsNullOrEmpty(step.remember))
                return;

            chapter.Context.SetParameter(step.remember, step.value);
        }

        private void Show(GuestDefinition guest, string emotion)
        {
            if (_player == null || guest == null)
                return;

            _player.ShowGuest(guest.SpriteFor(emotion));
        }

        private async UniTask WaitForCapsule(string capsuleId, ChapterRunner chapter)
        {
            var taken = new UniTaskCompletionSource();
            IDisposable wait = null;
            wait = _bus.Subscribe<CapsuleTaken>(message =>
            {
                if (message.CapsuleId == capsuleId)
                    taken.TrySetResult();
            });

            try
            {
                _bus.Publish(new CapsuleArrived(capsuleId));
                await taken.Task.AttachExternalCancellation(chapter.Token);
            }
            finally
            {
                if (wait != null)
                    wait.Dispose();
            }
        }
    }
}
