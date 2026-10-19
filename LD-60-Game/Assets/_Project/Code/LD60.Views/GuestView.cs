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
    [SerializeField] private SpriteRenderer counter;
    [SerializeField, Min(0f)] private float counterOverlap = 0.2f;

    private CancellationTokenSource _run;
    private float _heightOffset;

    private void Awake() => character.position = offscreenPoint.position;
    private void OnDisable() => Cancel();
    public UniTask Enter() => Slide(windowPoint);
    public UniTask Exit() => Slide(offscreenPoint);
    public void Cancel() => _run?.Cancel();

    public void ShowGuest(Sprite sprite)
    {
        if (body != null && sprite != null)
        {
            body.sprite = sprite;
            AlignToCounter();
        }
    }

    private void AlignToCounter()
    {
        if (counter == null || character == null || windowPoint == null)
            return;

        // Keep natural heights, but hide the cropped lower edge of short portraits.
        var bottomFromOrigin = body.bounds.min.y - character.position.y;
        var offset = Mathf.Min(0f, counter.bounds.max.y - counterOverlap -
            windowPoint.position.y - bottomFromOrigin);
        character.position += Vector3.up * (offset - _heightOffset);
        _heightOffset = offset;
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
        var targetPosition = target.position + Vector3.up * _heightOffset;
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
