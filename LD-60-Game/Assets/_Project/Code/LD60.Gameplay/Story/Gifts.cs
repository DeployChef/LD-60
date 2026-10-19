using System;
using LD60.Core.Bus;
using LD60.Gameplay.Context;
using UnityEngine;

namespace LD60.Gameplay.Story
{
    public sealed class Gifts
    {
        private readonly StoryContext _context;
        private readonly IMessageBus _bus;

        public Gifts(StoryContext context, IMessageBus bus)
        {
            _context = context;
            _bus = bus;
        }

        public void Give(string id, int amount = 1)
        {
            if (string.IsNullOrEmpty(id))
                return;

            if (string.Equals(id, "money", StringComparison.OrdinalIgnoreCase))
            {
                if (_context == null)
                    return;

                _context.AddMoney(amount);
                _bus.Publish(new MoneyChanged(_context.Money));
                Debug.Log($"[Story] Gave money {amount}. money={_context.Money}");
                return;
            }

            _bus.Publish(new ArticleGiven(id));
            Debug.Log($"[Story] Gave '{id}'.");
        }
    }
}
