using System;
using LD60.Core.Bus;
using LD60.Gameplay.Story;
using TMPro;
using UnityEngine;
using VContainer;

namespace LD60.UI
{
    public sealed class ChapterHeadline : MonoBehaviour
    {
        [SerializeField] private TMP_Text line;

        private IDisposable _day;

        [Inject]
        public void Construct(IMessageBus bus)
        {
            _day?.Dispose();
            _day = bus.Subscribe<DayStarted>(OnDay);
        }

        private void OnDestroy()
        {
            _day?.Dispose();
        }

        private void OnDay(DayStarted message)
        {
            if (line != null)
                line.text = message.Title ?? string.Empty;
        }
    }
}
