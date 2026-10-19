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
        [SerializeField] private Button optionsButton;
        [SerializeField] private GameObject background;
        [SerializeField] private GameObject backgroundSound;
        [SerializeField] private GameObject volume;
        [SerializeField] private MainMenuIntro intro;

        private IShellService _shell;
        private IGameSession _session;
        private MenuButtonHover _optionsHover;

        [Inject]
        public void Construct(IShellService shell, IGameSession session)
        {
            _shell = shell;
            _session = session;
        }

        private void Awake()
        {
            _optionsHover = optionsButton.GetComponent<MenuButtonHover>();
            playButton.onClick.AddListener(OnPlayClicked);
            resumeButton.onClick.AddListener(OnResumeClicked);
            optionsButton.onClick.AddListener(OnOptionsClicked);
            SetOptionsOpen(false);
        }

        private void Start()
        {
            ShowMenu();
            if (_shell.ShowIntro && intro != null)
            {
                LD60.UI.Audio.SoundService.PlayCutscene();
                intro.Finished += OnIntroFinished;
                intro.Play();
                return;
            }

            LD60.UI.Audio.SoundService.PlayMenu();
            if (intro != null)
                intro.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (intro != null)
                intro.Finished -= OnIntroFinished;

            playButton.onClick.RemoveListener(OnPlayClicked);
            resumeButton.onClick.RemoveListener(OnResumeClicked);
            optionsButton.onClick.RemoveListener(OnOptionsClicked);
        }

        private void OnIntroFinished()
        {
            if (intro != null)
                intro.Finished -= OnIntroFinished;

            LD60.UI.Audio.SoundService.PlayMenu();
        }

        private void ShowMenu()
        {
            playButton.gameObject.SetActive(true);
            var showResume = _session.HasResumableDay;
            resumeButton.gameObject.SetActive(showResume);
            ((RectTransform)playButton.transform).anchoredPosition = showResume ? new Vector2(0f, -48f) : Vector2.zero;
        }

        private void OnPlayClicked()
        {
            LD60.UI.Audio.SoundService.PlayStart();
            _shell.EnterGameAsync(GameStart.New).Forget();
        }

        private void OnResumeClicked()
        {
            LD60.UI.Audio.SoundService.PlayStart();
            _shell.EnterGameAsync(GameStart.Continue).Forget();
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
