using System;
using System.Collections.Generic;
using UnityEngine;

namespace LD60.Gameplay.Context
{
    [Serializable]
    public struct StoryParameter
    {
        public string id;
        public float value;
    }

    [Serializable]
    public sealed class StoryContextData
    {
        public int money;
        public List<StoryParameter> parameters = new();
        public List<string> keptArticleIds = new();

        public StoryContextData Clone()
        {
            var copy = new StoryContextData
            {
                money = money,
                keptArticleIds = keptArticleIds != null ? new List<string>(keptArticleIds) : new List<string>()
            };

            if (parameters == null)
                return copy;

            for (var i = 0; i < parameters.Count; i++)
                copy.parameters.Add(parameters[i]);

            return copy;
        }
    }

    public sealed class StoryContext
    {
        private StoryContextData _data = new();

        public int Money => _data.money;
        public IReadOnlyList<StoryParameter> Parameters => _data.parameters;
        public IReadOnlyList<string> KeptArticleIds => _data.keptArticleIds;

        public void Load(StoryContextData snapshot)
        {
            _data = snapshot != null ? snapshot.Clone() : new StoryContextData();
        }

        public StoryContextData Capture()
        {
            return _data.Clone();
        }

        public bool Meets(string parameterId, float minimum)
        {
            if (string.IsNullOrEmpty(parameterId))
                return true;

            return GetParameter(parameterId) + 0.0001f >= minimum;
        }

        public float GetParameter(string id)
        {
            if (string.Equals(id, "money", StringComparison.OrdinalIgnoreCase))
                return _data.money;

            var parameters = _data.parameters;
            for (var i = 0; i < parameters.Count; i++)
            {
                if (string.Equals(parameters[i].id, id, StringComparison.Ordinal))
                    return parameters[i].value;
            }

            return 0f;
        }

        public void SetParameter(string id, float value)
        {
            if (string.IsNullOrEmpty(id))
                return;

            if (string.Equals(id, "money", StringComparison.OrdinalIgnoreCase))
            {
                _data.money = Mathf.RoundToInt(value);
                return;
            }

            var parameters = _data.parameters;
            for (var i = 0; i < parameters.Count; i++)
            {
                if (!string.Equals(parameters[i].id, id, StringComparison.Ordinal))
                    continue;

                parameters[i] = new StoryParameter { id = id, value = value };
                return;
            }

            parameters.Add(new StoryParameter { id = id, value = value });
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
            var parts = new string[_data.parameters.Count];
            for (var i = 0; i < _data.parameters.Count; i++)
                parts[i] = $"{_data.parameters[i].id}={_data.parameters[i].value}";

            return $"money={_data.money}, parameters=[{string.Join(", ", parts)}], kept=[{string.Join(", ", _data.keptArticleIds)}]";
        }
    }
}
