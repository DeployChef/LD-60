using DG.Tweening;
using UnityEngine;

namespace LD60.UI
{
    public sealed class StampMark : MonoBehaviour
    {
    }

    public sealed class DeskStamp : MonoBehaviour
    {
        private Tween _motion;
        private Vector3 _home;
        private Vector3 _homeScale;

        private void OnDestroy()
        {
            _motion?.Kill();
        }

        public void Release(NewspaperDesk board, Vector3 home, Vector3 homeScale)
        {
            _home = home;
            _homeScale = homeScale;
            _motion?.Kill();

            if (board == null || !board.OverPage(transform.position))
            {
                Rest(board);
                return;
            }

            if (!board.EditionReady(out var reason))
            {
                board.Notify(reason, transform.position);
                _motion = Refuse(board);
                return;
            }

            _motion = Press(board);
        }

        private Sequence Press(NewspaperDesk board)
        {
            var from = transform.position;
            var into = from + board.transform.forward * 0.16f;
            var renderer = GetComponent<SpriteRenderer>();
            return DOTween.Sequence()
                .SetTarget(this)
                .Append(transform.DOMove(into, 0.1f).SetEase(Ease.InQuad))
                .AppendCallback(() =>
                {
                    board.LeaveMark(
                        from,
                        renderer != null ? renderer.sprite : null,
                        renderer != null ? renderer.sharedMaterial : null,
                        transform.localScale,
                        renderer != null ? renderer.sortingLayerID : 0);
                })
                .Append(transform.DOMove(_home, 0.18f).SetEase(Ease.OutQuad))
                .OnComplete(() =>
                {
                    Rest(board);
                    board.CommitStamp();
                    _motion = null;
                });
        }

        private Tween Refuse(NewspaperDesk board)
        {
            var origin = transform.position;
            var right = board.transform.right;
            return DOTween.To(() => 0f, time =>
                {
                    var fade = 1f - time;
                    transform.position = origin + right * (Mathf.Sin(time * 14.72f) * 0.14f * fade);
                }, 1f, 0.32f)
                .SetEase(Ease.Linear)
                .SetTarget(this)
                .OnComplete(() =>
                {
                    Rest(board);
                    _motion = null;
                });
        }

        private void Rest(NewspaperDesk board)
        {
            transform.position = _home;
            transform.localScale = _homeScale;
            if (board != null)
                board.Raise(gameObject);
        }
    }
}
