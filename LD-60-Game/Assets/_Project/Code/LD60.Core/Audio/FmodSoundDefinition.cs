using UnityEngine;

namespace LD60.Core
{
    [System.Serializable]
    public sealed class FmodSoundDefinition
    { 
        [SerializeField] private string id;
        [SerializeField] private string eventPath;

        [SerializeField] private string parameterName;
        [SerializeField] private string parameterLabel;
        
        public string Id => id;
        public string EventPath => eventPath;
        public string ParameterName => parameterName;
        public string ParameterLabel => parameterLabel;
    }
}
