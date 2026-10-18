using FMOD.Studio;
using FMODUnity;
using LD60.Core.Audio;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace LD60.UI
{
    public sealed class VolumeController : MonoBehaviour
    {
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Slider musicSlider;

        private IAudioManager _audio;

        private Bus _masterBus;
        private Bus _sfxBus;
        private Bus _musicBus;
        private Bus _uiBus;
        private Bus _dialogBus;

        [Inject]
        public void Construct(IAudioManager audio)
        {
            _audio = audio;
            Refresh();
        }

        private void Awake()
        {
            masterSlider.onValueChanged.AddListener(OnMasterChanged);
            sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            musicSlider.onValueChanged.AddListener(OnMusicChanged);




            _masterBus = RuntimeManager.GetBus("bus:/");
            _musicBus = RuntimeManager.GetBus("bus:/all_music");
            _dialogBus = RuntimeManager.GetBus("bus:/dialog_gr");
            _sfxBus = RuntimeManager.GetBus("bus:/sfx_gr");
            _uiBus = RuntimeManager.GetBus("bus:/ui");
        }
        private void OnEnable()
        {
            Refresh();
        }

        private void OnDestroy()
        {
            masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
            sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
            musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
        }

        private void OnMasterChanged(float value)
        {
            _audio.SetMasterVolume(value);
        }

        private void OnSfxChanged(float value)
        {
            _audio.SetSfxVolume(value);
        }

        private void OnMusicChanged(float value)
        {
            _audio.SetMusicVolume(value);
        }

        private void Refresh()
        {
            if (_audio == null)
                return;

            masterSlider.SetValueWithoutNotify(_audio.MasterVolume);
            sfxSlider.SetValueWithoutNotify(_audio.SfxVolume);
            musicSlider.SetValueWithoutNotify(_audio.MusicVolume);
        }
    }
}
