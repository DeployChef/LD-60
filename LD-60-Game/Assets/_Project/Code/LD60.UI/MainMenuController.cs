using Cysharp.Threading.Tasks;
using LD60.Core;
using LD60.Gameplay.Context;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace LD60.UI
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button resumeButton;

        private IShellService _shell;
        private IGameSession _session;

        [Inject]
        public void Construct(IShellService shell, IGameSession session)
        {
            _shell = shell;
            _session = session;
        }

        private void Awake()
        {
            playButton.onClick.AddListener(OnPlayClicked);
            resumeButton.onClick.AddListener(OnResumeClicked);
        }

        private void Start()
        {
            var showResume = _session.HasResumableDay;
            resumeButton.gameObject.SetActive(showResume);
            ((RectTransform)playButton.transform).anchoredPosition = showResume ? new Vector2(0f, -48f) : Vector2.zero;
        }

        private void OnDestroy()
        {
            playButton.onClick.RemoveListener(OnPlayClicked);
            resumeButton.onClick.RemoveListener(OnResumeClicked);
        }

        private void OnPlayClicked()
        {
            _shell.EnterGameAsync(GameStart.New).Forget();
        }

        private void OnResumeClicked()
        {
            _shell.EnterGameAsync(GameStart.Continue).Forget();
        }
    }
}
