using Cysharp.Threading.Tasks;
using LD60.Core;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace LD60.UI
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button playButton;

        private IShellService _shell;

        [Inject]
        public void Construct(IShellService shell)
        {
            _shell = shell;
        }

        private void Awake()
        {
            playButton.onClick.AddListener(OnPlayClicked);
        }

        private void OnDestroy()
        {
            playButton.onClick.RemoveListener(OnPlayClicked);
        }

        private void OnPlayClicked()
        {
            _shell.EnterGameAsync().Forget();
        }
    }
}
