using System;
using UnityEngine;

namespace LD60.UI
{
    [Serializable]
    public sealed class DialogueChoice
    {
        public GameObject root;
        public TMPro.TMP_Text label;
        public Collider collider;
    }
}
