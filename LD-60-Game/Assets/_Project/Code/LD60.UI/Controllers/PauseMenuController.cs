using Cysharp.Threading.Tasks;
using LD60.Core;
using LD60.UI.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

namespace LD60.UI
{
    public sealed class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private InputActionAsset input;
        [SerializeField] private GameObject menu;
        [SerializeField] private Button exitButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private GameObject background;
        [SerializeField] private GameObject backgroundSound;
        [SerializeField] private GameObject volume;

        private IShellService _shell;
        private InputAction _pause;
        private bool _open;
        private EditionPreview _editionPreview;
        private MenuButtonHover _optionsHover;

        [Inject]
        public void Construct(IShellService shell)
        {
            _shell = shell;
        }

        private void Awake()
        {
            _editionPreview = FindAnyObjectByType<EditionPreview>(FindObjectsInactive.Include);
            _optionsHover = optionsButton.GetComponent<MenuButtonHover>();
            _pause = input.FindAction("Game/Pause", true);
            exitButton.onClick.AddListener(OnExitClicked);
            restartButton.onClick.AddListener(OnRestartClicked);
            resumeButton.onClick.AddListener(OnResumeClicked);
            optionsButton.onClick.AddListener(OnOptionsClicked);
            SetOpen(false);
        }

        private void OnEnable()
        {
            _pause.performed += OnPause;
            _pause.Enable();
        }

        private void OnDisable()
        {
            _pause.performed -= OnPause;
            _pause.Disable();
            Time.timeScale = 1f;
            SoundService.SetPaused(false);
        }

        private void OnDestroy()
        {
            exitButton.onClick.RemoveListener(OnExitClicked);
            restartButton.onClick.RemoveListener(OnRestartClicked);
            resumeButton.onClick.RemoveListener(OnResumeClicked);
            optionsButton.onClick.RemoveListener(OnOptionsClicked);
        }

        private void OnPause(InputAction.CallbackContext context)
        {
            if (_editionPreview != null && _editionPreview.IsOpen)
                return;
            SetOpen(!_open);
        }

        private void SetOpen(bool open)
        {
            _open = open;
            SetOptionsOpen(false);
            menu.SetActive(open);
            Time.timeScale = open ? 0f : 1f;
            SoundService.SetPaused(open);
        }

        private void OnExitClicked()
        {
            Time.timeScale = 1f;
            SoundService.SetPaused(false);
            _shell.ReturnToMainMenuAsync().Forget();
        }

        private void OnRestartClicked()
        {
            SetOpen(false);
            _shell.RestartFromSaveAsync().Forget();
        }

        private void OnResumeClicked()
        {
            SetOpen(false);
        }

        private void OnOptionsClicked()
        {
            SetOptionsOpen(!volume.activeSelf);
        }

        private void SetOptionsOpen(bool open)
        {
            background.SetActive(!open);
            backgroundSound.SetActive(open);
            volume.SetActive(open);
            _optionsHover?.SetActiveState(open);
        }
    }
}
