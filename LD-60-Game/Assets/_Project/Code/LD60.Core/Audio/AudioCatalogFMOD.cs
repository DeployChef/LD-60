using System.Collections.Generic;
using LD60.Core.Audio;
using UnityEngine;

namespace LD60.Core
{   
    [CreateAssetMenu(fileName = "AudioCatalogFMOD", menuName = "LD-60/Audio CatalogFMOD")]
    public class AudioCatalogFMOD : ScriptableObject
    {
        public static class Ids
        {
            public static class Events
            {
                // Меню
                public const string MenuCut = "MenuCut";
                public const string MenuMusic = "MenuMusic";
                public const string MenuStart = "MenuStart";
                public const string MenuUi = "MenuUi";

                // OneShot
                public const string Convert = "Convert";
                public const string Door = "Door";
                public const string PaperHanding = "PaperHanding";

                // Диалоги
                public const string DialogGranny = "DialogGranny";
                public const string DialogOfficialWoman = "DialogOfficialWoman";
                public const string DialogChikist = "DialogChikist";
                public const string DialogFatOfficial = "DialogFatOfficial";
                public const string DialogAnonymousMask = "DialogAnonymousMask";
                public const string DialogAnonymousNoMask = "DialogAnonymousNoMask";
                public const string DialogFishman = "DialogFishman";

                // Музыка
                public const string Score = "Score";

                // SFX
                public const string SfxTyping = "SfxTyping";
                public const string SfxType = "SfxType";
                public const string SfxConvert = "SfxConvert";
            }
        }

        [SerializeField] private List<FmodSoundDefinition> sounds = new();

        public bool TryGet(string id, out FmodSoundDefinition definition)
        {
            foreach (var sound in sounds)
            {
                if (sound != null && sound.Id == id)
                {
                    definition = sound;
                    return true;
                }
            }

            definition = null;
            return false;
        }
    }
}
