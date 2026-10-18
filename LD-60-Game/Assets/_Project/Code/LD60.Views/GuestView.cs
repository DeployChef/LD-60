using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class GuestView : MonoBehaviour
{
    [SerializeField] private Transform character;
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private Transform offscreenPoint;
    [SerializeField] private Transform windowPoint;
    [SerializeField] private float walkSeconds = 0.75f;
    
    private CancellationTokenSource _run;
    
    private void Awake() => character.position = offscreenPoint.position;
    private void OnDisable() => Cancel();
    public UniTask Enter() => Slide(windowPoint);
    public UniTask Exit() => Slide(offscreenPoint);
    public void Cancel() => _run?.Cancel();
    
    public void ShowGuest(Sprite sprite)
    {
        if (body != null && sprite != null)
            body.sprite = sprite;
    }
    
    private async UniTask Slide(Transform target)
    {   
        if (character == null ||  target == null)
            return;
        
        Cancel();
        var cts = new CancellationTokenSource();
        _run = cts;
        var token = cts.Token;
        var startPosition = character.position;
        var targetPosition = target.position;
        var elapsed = 0f;
        try
        {
            while (elapsed < walkSeconds)
            {
                token.ThrowIfCancellationRequested();
                if (character == null)
                    return;

                elapsed += Time.deltaTime;
                character.position =
                    Vector3.Lerp(startPosition, targetPosition, Mathf.Clamp01(elapsed / walkSeconds));
                await UniTask.Yield(token);
            }

            if (character != null)
                character.position  = targetPosition;
        }
        finally
        {
            if (ReferenceEquals(_run, cts))
                _run = null;

            cts.Dispose();
        }
    }
}
