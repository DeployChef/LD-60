using LD60.Core.Audio;
using LD60.Core.Bus;
using LD60.UI.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

namespace LD60.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class UiButtonSound : MonoBehaviour, IPointerEnterHandler
    {
        [SerializeField] private bool playHover = true;
        [SerializeField] private bool playClick = true;
        [SerializeField] private string hoverSoundId = AudioCatalog.Ids.UiMove;
        [SerializeField] private string clickSoundId = AudioCatalog.Ids.UiClick;

        private Button _button;
        private IMessageBus _bus;

        [Inject]
        public void Construct(IMessageBus bus)
        {
            _bus = bus;
        }

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (playClick)
                _button.onClick.AddListener(PlayClick);
        }

        private void OnDestroy()
        {
            if (_button != null && playClick)
                _button.onClick.RemoveListener(PlayClick);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!playHover || !IsReady())
                return;

            SoundService.PlayButtonHover();
            Publish(hoverSoundId);
        }

        private void PlayClick()
        {
            if (!playClick || !IsReady())
                return;

            SoundService.PlayButtonClick();
            Publish(clickSoundId);
        }

        private bool IsReady()
        {
            return isActiveAndEnabled && _button != null && _button.interactable;
        }

        private void Publish(string soundId)
        {
            if (_bus == null || string.IsNullOrEmpty(soundId))
                return;

            _bus.Publish(new PlaySound(soundId));
        }
    }
}
