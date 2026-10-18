using System.Collections;
using UnityEngine;

namespace LD60.UI
{
    public sealed class StampMark : MonoBehaviour
    {
    }

    public sealed class DeskStamp : MonoBehaviour
    {
        private Coroutine _motion;
        private Vector3 _home;
        private Vector3 _homeScale;

        public void Release(NewspaperDesk board, Vector3 home, Vector3 homeScale)
        {
            _home = home;
            _homeScale = homeScale;
            if (_motion != null)
                StopCoroutine(_motion);

            if (board == null || !board.OverPage(transform.position))
            {
                Rest(board);
                return;
            }

            if (!board.EditionReady(out var reason))
            {
                board.Notify(reason, transform.position);
                _motion = StartCoroutine(Refuse(board));
                return;
            }

            _motion = StartCoroutine(Press(board));
        }

        private IEnumerator Press(NewspaperDesk board)
        {
            var from = transform.position;
            var into = from + board.transform.forward * 0.16f;
            yield return Slide(from, into, 0.1f);

            var renderer = GetComponent<SpriteRenderer>();
            board.LeaveMark(
                from,
                renderer != null ? renderer.sprite : null,
                renderer != null ? renderer.sharedMaterial : null,
                transform.localScale,
                renderer != null ? renderer.sortingLayerID : 0);

            yield return Slide(into, _home, 0.18f);
            Rest(board);
            board.CommitStamp();
            _motion = null;
        }

        private IEnumerator Refuse(NewspaperDesk board)
        {
            var origin = transform.position;
            var right = board.transform.right;
            var time = 0f;
            const float length = 0.32f;
            while (time < length)
            {
                time += Time.deltaTime;
                var fade = 1f - time / length;
                transform.position = origin + right * (Mathf.Sin(time * 46f) * 0.14f * fade);
                yield return null;
            }

            Rest(board);
            _motion = null;
        }

        private IEnumerator Slide(Vector3 from, Vector3 to, float length)
        {
            var time = 0f;
            while (time < length)
            {
                time += Time.deltaTime;
                transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(time / length));
                yield return null;
            }

            transform.position = to;
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
