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
        public float Politics { get; private set; }
        public float Social { get; private set; }
        public IReadOnlyList<string> KeptArticleIds => _kept;

        public void ResetDay()
        {
            Fill = 0f;
            Politics = 0f;
            Social = 0f;
            _kept.Clear();
            _bus.Publish(new FieldStateChanged(0f, 0f, 0f));
        }

        public void SetFill(float fill)
        {
            SetScores(fill, Politics, Social);
        }

        public void SetScores(float fill, float politics, float social)
        {
            Fill = Mathf.Max(0f, fill);
            Politics = Mathf.Max(0f, politics);
            Social = Mathf.Max(0f, social);
            _bus.Publish(new FieldStateChanged(Fill, Politics, Social));
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
