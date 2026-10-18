using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace LD60.UI
{
    public sealed class MainMenuIntro : MonoBehaviour
    {
        [SerializeField] private InputActionAsset input;
        [SerializeField] private PlayableDirector director;
        [SerializeField] private GameObject skipHint;

        private InputAction _skip;
        private bool _playing;

        public event Action Finished;

        private void Awake()
        {
            _skip = input.FindAction("Menu/Skip", true);
            if (director != null)
                director.playOnAwake = false;
        }

        private void OnEnable()
        {
            _skip.performed += OnSkip;
            _skip.Enable();
            if (director != null)
                director.stopped += OnStopped;
        }

        private void OnDisable()
        {
            _skip.performed -= OnSkip;
            _skip.Disable();
            if (director != null)
                director.stopped -= OnStopped;
        }

        public void Play()
        {
            gameObject.SetActive(true);
            _playing = true;
            if (skipHint != null)
                skipHint.SetActive(true);

            if (director == null || director.playableAsset == null)
            {
                Finish();
                return;
            }

            director.extrapolationMode = DirectorWrapMode.None;
            director.stopped -= OnStopped;
            director.time = 0d;
            director.Play();
            director.time = 0d;
            director.stopped += OnStopped;
        }

        public void Skip()
        {
            if (!_playing || director == null || director.playableAsset == null)
            {
                Finish();
                return;
            }

            director.time = director.duration;
            director.Evaluate();
            Finish();
        }

        private void OnSkip(InputAction.CallbackContext context)
        {
            if (!_playing)
                return;

            Skip();
        }

        private void OnStopped(PlayableDirector _)
        {
            Finish();
        }

        private void Finish()
        {
            if (!_playing)
                return;

            _playing = false;
            if (director != null && director.state == PlayState.Playing)
                director.Stop();

            gameObject.SetActive(false);
            Finished?.Invoke();
        }
    }
}
