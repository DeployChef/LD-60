using LD60.Gameplay.Story;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace LD60.UI
{
    public sealed class StoryDebugController : MonoBehaviour
    {
        [SerializeField] private InputActionAsset input;

        private StoryDriver _story;
        private InputAction _fill;
        private InputAction _stamp;
        private InputAction _complete;
        private InputAction _status;
        private bool _listening;

        [Inject]
        public void Construct(StoryDriver story)
        {
            _story = story;
            Listen();
        }

        private void OnEnable()
        {
            Listen();
        }

        private void OnDisable()
        {
            if (!_listening)
                return;

            _fill.performed -= OnFill;
            _stamp.performed -= OnStamp;
            _complete.performed -= OnComplete;
            _status.performed -= OnStatus;
            _fill.Disable();
            _stamp.Disable();
            _complete.Disable();
            _status.Disable();
            _listening = false;
        }

        private void Start()
        {
            if (_story != null && _story.DebugKeys)
                Debug.Log("[Story] Keys: F +10% fill, S stamp, C complete the current event.");
        }

        private void Listen()
        {
            if (_listening || !isActiveAndEnabled || _story == null || !_story.DebugKeys)
                return;

            _fill = input.FindAction("Debug/Fill", true);
            _stamp = input.FindAction("Debug/Stamp", true);
            _complete = input.FindAction("Debug/Complete", true);
            _status = input.FindAction("Debug/Status", true);
            _fill.performed += OnFill;
            _stamp.performed += OnStamp;
            _complete.performed += OnComplete;
            _status.performed += OnStatus;
            _fill.Enable();
            _stamp.Enable();
            _complete.Enable();
            _status.Enable();
            _listening = true;
        }

        private void OnFill(InputAction.CallbackContext context)
        {
            _story.AddFill(0.1f);
        }

        private void OnStamp(InputAction.CallbackContext context)
        {
            _story.Stamp();
        }

        private void OnComplete(InputAction.CallbackContext context)
        {
            _story.CompleteCurrent();
        }

        private void OnStatus(InputAction.CallbackContext context)
        {
            _story.LogStatus();
        }
    }
}
