using System;
using System.Collections.Generic;
using UnityEngine;

namespace LD60.Gameplay.Context
{
    [Serializable]
    public sealed class StoryContextData
    {
        public int money;
        public List<string> flags = new();
        public List<string> keptArticleIds = new();

        public StoryContextData Clone()
        {
            return new StoryContextData
            {
                money = money,
                flags = flags != null ? new List<string>(flags) : new List<string>(),
                keptArticleIds = keptArticleIds != null ? new List<string>(keptArticleIds) : new List<string>()
            };
        }
    }

    public sealed class StoryContext
    {
        private StoryContextData _data = new();

        public int Money => _data.money;
        public IReadOnlyList<string> Flags => _data.flags;
        public IReadOnlyList<string> KeptArticleIds => _data.keptArticleIds;

        public void Load(StoryContextData snapshot)
        {
            _data = snapshot != null ? snapshot.Clone() : new StoryContextData();
        }

        public StoryContextData Capture()
        {
            return _data.Clone();
        }

        public bool HasFlag(string id)
        {
            return !string.IsNullOrEmpty(id) && _data.flags.Contains(id);
        }

        public void SetFlag(string id)
        {
            if (string.IsNullOrEmpty(id) || _data.flags.Contains(id))
                return;

            _data.flags.Add(id);
        }

        public void AddMoney(int amount)
        {
            _data.money += amount;
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || _data.money < amount)
                return false;

            _data.money -= amount;
            return true;
        }

        public void SetKeptArticles(IReadOnlyList<string> ids)
        {
            _data.keptArticleIds.Clear();
            if (ids == null)
                return;

            for (var i = 0; i < ids.Count; i++)
            {
                if (!string.IsNullOrEmpty(ids[i]))
                    _data.keptArticleIds.Add(ids[i]);
            }
        }

        public string Describe()
        {
            return $"money={_data.money}, flags=[{string.Join(", ", _data.flags)}], kept=[{string.Join(", ", _data.keptArticleIds)}]";
        }
    }
}
