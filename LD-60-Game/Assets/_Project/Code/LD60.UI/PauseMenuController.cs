using Cysharp.Threading.Tasks;
using LD60.Core;
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

        private IShellService _shell;
        private InputAction _pause;
        private bool _open;

        [Inject]
        public void Construct(IShellService shell)
        {
            _shell = shell;
        }

        private void Awake()
        {
            _pause = input.FindAction("Game/Pause", true);
            exitButton.onClick.AddListener(OnExitClicked);
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
        }

        private void OnDestroy()
        {
            exitButton.onClick.RemoveListener(OnExitClicked);
        }

        private void OnPause(InputAction.CallbackContext context)
        {
            SetOpen(!_open);
        }

        private void SetOpen(bool open)
        {
            _open = open;
            menu.SetActive(open);
            Time.timeScale = open ? 0f : 1f;
        }

        private void OnExitClicked()
        {
            Time.timeScale = 1f;
            _shell.ReturnToMainMenuAsync().Forget();
        }
    }
}
