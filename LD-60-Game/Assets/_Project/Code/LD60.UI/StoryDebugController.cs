using LD60.Gameplay.Context;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LD60.UI
{
    public sealed class StoryDebugController : MonoBehaviour
    {
        private StoryBootstrap _story;

        private void Start()
        {
            _story = GetComponent<StoryBootstrap>();
            if (_story != null && _story.DebugKeys)
                Debug.Log("[Story] Keys: F +10% fill, S stamp, C complete the current event.");
        }

        private void Update()
        {
            if (_story == null || !_story.DebugKeys)
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.fKey.wasPressedThisFrame)
                _story.AddFill(0.1f);

            if (keyboard.sKey.wasPressedThisFrame)
                _story.Stamp();

            if (keyboard.cKey.wasPressedThisFrame)
                _story.CompleteCurrent();

            if (keyboard.numpadPlusKey.wasPressedThisFrame || (keyboard.equalsKey.wasPressedThisFrame && keyboard.shiftKey.isPressed))
                _story.LogStatus();
        }
    }
}
