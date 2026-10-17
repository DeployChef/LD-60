using System;
using LD60.Gameplay.Context;
using UnityEngine;

namespace LD60.Main
{
    public sealed class DayCheckpointStore
    {
        private const string Key = "ld60.day";

        public bool TryLoad(out int chapterIndex, out StoryContextData context)
        {
            chapterIndex = 0;
            context = new StoryContextData();

            var json = PlayerPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(json))
                return false;

            var file = JsonUtility.FromJson<File>(json);
            if (file == null)
                return false;

            chapterIndex = file.chapterIndex;
            context = file.context != null ? file.context.Clone() : new StoryContextData();
            return true;
        }

        public void Save(int chapterIndex, StoryContextData context)
        {
            var file = new File
            {
                chapterIndex = chapterIndex,
                context = context != null ? context.Clone() : new StoryContextData()
            };
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(file));
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        [Serializable]
        private sealed class File
        {
            public int chapterIndex;
            public StoryContextData context = new();
        }
    }
}
