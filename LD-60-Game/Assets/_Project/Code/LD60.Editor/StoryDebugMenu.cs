using LD60.Gameplay.Context;
using UnityEditor;
using UnityEngine;

namespace LD60.Editor
{
    public static class StoryDebugMenu
    {
        [MenuItem("LD60/Story/Write Context Into Selected Preset")]
        public static void WriteContextIntoSelectedPreset()
        {
            var preset = Selection.activeObject as DayStartPreset;
            if (preset == null)
            {
                Debug.LogWarning("[Story] Select a Day Start preset asset.");
                return;
            }

            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Story] Enter play mode and reach the day you want to capture.");
                return;
            }

            var bootstrap = Object.FindAnyObjectByType<StoryBootstrap>();
            if (bootstrap == null || bootstrap.Context == null)
            {
                Debug.LogWarning("[Story] Story is not running.");
                return;
            }

            var context = bootstrap.Context;
            preset.OverwriteContext(context.Capture());
            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Story] Wrote context into '{preset.name}' for chapter {preset.ChapterIndex}. {context.Describe()}");
        }
    }
}
