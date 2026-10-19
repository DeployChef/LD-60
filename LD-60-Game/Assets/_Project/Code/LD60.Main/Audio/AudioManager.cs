using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using LD60.Core;
using LD60.Core.Audio;
using UnityEngine;

namespace LD60.Main.Audio
{
    public sealed class AudioManager : MonoBehaviour, IAudioManager
    {
        private const string MasterKey = "ld60.audio.master";
        private const string SfxKey = "ld60.audio.sfx";
        private const string MusicKey = "ld60.audio.music";
        
        [SerializeField] private AudioCatalogFMOD catalogFMOD;
   
        private float _master = 1f;
        private float _sfx = 1f;
        private float _music = 1f;
        
        private Bus _masterBus;
        private Bus _sfxBus;
        private Bus _musicBus;
        private Bus _uiBus;
        private Bus _dialogBus;

        
        public float MasterVolume => _master;
        public float SfxVolume => _sfx;
        public float MusicVolume => _music;

        private void Awake()
        {
            _master = PlayerPrefs.GetFloat(MasterKey, 1f);
            _sfx = PlayerPrefs.GetFloat(SfxKey, 1f);
            _music = PlayerPrefs.GetFloat(MusicKey, 1f);
            InitializeFmod();
        }

        private void InitializeFmod()
        {
            _masterBus = RuntimeManager.GetBus("bus:/");
            _musicBus = RuntimeManager.GetBus("bus:/all_music");

            _sfxBus = RuntimeManager.GetBus("bus:/sfx_gr");
            _uiBus = RuntimeManager.GetBus("bus:/ui");
            _dialogBus = RuntimeManager.GetBus("bus:/dialog_gr");

            SetMasterVolume(_master);
            SetMusicVolume(_music);
            SetSfxVolume(_sfx);
        }

        public void SetMasterVolume(float volume)
        {
            _master = Mathf.Clamp01(volume);

            PlayerPrefs.SetFloat(MasterKey, _master);

            if (_masterBus.isValid())
                _masterBus.setVolume(_master);
        }

        public void SetSfxVolume(float volume)
        {
            _sfx = Mathf.Clamp01(volume);

            PlayerPrefs.SetFloat(SfxKey, _sfx);

            if (_sfxBus.isValid())
                _sfxBus.setVolume(_sfx);

            if (_uiBus.isValid())
                _uiBus.setVolume(_sfx);

            if (_dialogBus.isValid())
                _dialogBus.setVolume(_sfx);
        }

        public void SetMusicVolume(float volume)
        {
            _music = Mathf.Clamp01(volume);

            PlayerPrefs.SetFloat(MusicKey, _music);

            if (_musicBus.isValid())
                _musicBus.setVolume(_music);
        }

       public void Play(string soundId)
{
    if (catalogFMOD == null || !catalogFMOD.TryGet(soundId, out var sound))
    {
        Debug.LogWarning($"Звук не найден: {soundId}");
        return;
    }

    if (string.IsNullOrWhiteSpace(sound.EventPath))
        return;

    var instance = RuntimeManager.CreateInstance(sound.EventPath);

    if (!instance.isValid())
    {
        Debug.LogError($"FMOD Event не найден: {sound.EventPath}");
        return;
    }

    try
    {
        if (!string.IsNullOrWhiteSpace(sound.ParameterName))
        {
            var result = instance.getDescription(out var description);

            if (result != FMOD.RESULT.OK)
            {
                Debug.LogError($"FMOD Description Error: {result}");
                return;
            }

            result = description.getParameterDescriptionCount(out int count);

            if (result != FMOD.RESULT.OK)
            {
                Debug.LogError($"FMOD Parameter Count Error: {result}");
                return;
            }

            Debug.Log($"Событие: {sound.EventPath}");
            Debug.Log($"Ищем параметр: '{sound.ParameterName}'");
            Debug.Log($"Количество параметров: {count}");

            bool parameterFound = false;

            for (int i = 0; i < count; i++)
            {
                result = description.getParameterDescriptionByIndex(
                    i,
                    out var parameter
                );

                if (result != FMOD.RESULT.OK)
                    continue;

                // Преобразуем FMOD.StringWrapper в обычную строку
                string realName = (string)parameter.name;

                Debug.Log($"РЕАЛЬНОЕ ИМЯ ПАРАМЕТРА: '{realName}'");

                Debug.Log(
                    $"Совпадает с каталогом: " +
                    $"{realName == sound.ParameterName}"
                );

                if (realName != sound.ParameterName)
                    continue;

                parameterFound = true;

                // Выводим доступные Label
                for (int j = 0; j <= (int)parameter.maximum; j++)
                {
                    var labelResult = description.getParameterLabelByIndex(
                        i,
                        j,
                        out var label
                    );

                    if (labelResult == FMOD.RESULT.OK)
                    {
                        Debug.Log($"Доступный Label: '{label}'");
                    }
                }
            }

            if (!parameterFound)
            {
                Debug.LogError(
                    $"Параметр '{sound.ParameterName}' " +
                    $"не найден в событии '{sound.EventPath}'"
                );

                return;
            }

            // Устанавливаем параметр
            result = instance.setParameterByNameWithLabel(
                sound.ParameterName,
                sound.ParameterLabel
            );

            if (result != FMOD.RESULT.OK)
            {
                Debug.LogError(
                    $"Ошибка установки Label '{sound.ParameterLabel}': {result}"
                );

                return;
            }
        }

        // Запускаем звук
        var startResult = instance.start();

        if (startResult != FMOD.RESULT.OK)
        {
            Debug.LogError($"FMOD Start Error: {startResult}");
            return;
        }

        Debug.Log($"FMOD успешно запущен: {soundId}");
    }
    finally
    {
        instance.release();
    }
}
    }
}
