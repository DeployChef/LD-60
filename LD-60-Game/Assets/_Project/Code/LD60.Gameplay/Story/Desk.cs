using System.Collections.Generic;
using LD60.Core.Bus;
using UnityEngine;

namespace LD60.Gameplay.Story
{
    public sealed class Desk
    {
        private readonly IMessageBus _bus;
        private readonly List<string> _kept = new();

        public Desk(IMessageBus bus)
        {
            _bus = bus;
        }

        public float Fill { get; private set; }
        public IReadOnlyList<string> KeptArticleIds => _kept;

        public void ResetDay()
        {
            Fill = 0f;
            _kept.Clear();
        }

        public void SetFill(float fill)
        {
            Fill = Mathf.Clamp01(fill);
            _bus.Publish(new FieldStateChanged(Fill));
        }

        public void SetKeptArticles(IReadOnlyList<string> ids)
        {
            _kept.Clear();
            if (ids == null)
                return;

            for (var i = 0; i < ids.Count; i++)
            {
                if (!string.IsNullOrEmpty(ids[i]))
                    _kept.Add(ids[i]);
            }
        }

        public void Stamp()
        {
            _bus.Publish(new PaperStamped(Fill));
        }
    }
}
