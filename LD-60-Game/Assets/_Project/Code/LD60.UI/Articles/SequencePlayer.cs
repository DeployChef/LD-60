using System.Threading;
using Cysharp.Threading.Tasks;
using LD60.Gameplay.Story;
using UnityEngine;

namespace LD60.UI
{   
    public sealed class SequencePlayer : MonoBehaviour, ISequencePlayer
    {
        [SerializeField] private WorkstationViewController views;
        [SerializeField] private GuestView _guestView;
        
        public void LookAtRoom() => views.ShowTable(false);
        public void Cancel() => _guestView.Cancel();
        public void ShowGuest(Sprite sprite) => _guestView.ShowGuest(sprite);
        public UniTask Enter() => _guestView.Enter();
        public UniTask Exit() => _guestView.Exit();
    }
}
