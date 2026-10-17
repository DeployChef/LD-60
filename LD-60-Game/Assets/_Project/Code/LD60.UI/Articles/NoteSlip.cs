using TMPro;
using UnityEngine;

namespace LD60.UI
{
    public sealed class NoteSlip : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;

        public void Bind(string note)
        {
            if (text != null)
                text.text = note ?? string.Empty;
        }
    }
}
