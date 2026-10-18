using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LD60.UI
{
    public sealed class DeskDrag : MonoBehaviour
    {
        private const int LiftOrder = 4000;
        private const float LiftDistance = 0.28f;

        private WorkstationViewController _views;
        private NewspaperDesk _board;
        private CinemachineBrain _brain;
        private int _rest;
        private bool _held;
        private Vector3 _grab;
        private Vector3 _home;
        private Vector3 _homeScale;
        private Vector3 _lift;
        private float _risen;
        private Coroutine _shake;

        private void Awake()
        {
            _views = FindAnyObjectByType<WorkstationViewController>();
            _board = FindAnyObjectByType<NewspaperDesk>();
            if (_views != null)
                _brain = _views.GetComponentInChildren<CinemachineBrain>(true);
            if (_brain == null)
                _brain = FindAnyObjectByType<CinemachineBrain>();
        }

        public void Hold(int order)
        {
            _rest = order;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || !CanUsePointer())
            {
                if (_held)
                    Drop();
                return;
            }

            var camera = OutputCamera();
            if (camera == null)
                return;

            if (mouse.leftButton.wasPressedThisFrame)
                TryGrab(camera, mouse.position.ReadValue());
            else if (_held && mouse.leftButton.isPressed)
                Move(camera, mouse.position.ReadValue());
            else if (_held && mouse.leftButton.wasReleasedThisFrame)
                Drop();
        }

        private bool CanUsePointer()
        {
            if (_views != null && !_views.IsShowingTable)
                return false;
            return Time.timeScale > 0f;
        }

        private Camera OutputCamera()
        {
            if (_brain != null && _brain.OutputCamera != null)
                return _brain.OutputCamera;
            return Camera.main;
        }

        private void TryGrab(Camera camera, Vector2 screen)
        {
            var ray = camera.ScreenPointToRay(screen);
            var hits = Physics.RaycastAll(ray, 100f);
            if (hits.Length == 0)
                return;

            DeskDrag top = null;
            var topOrder = int.MinValue;
            for (var i = 0; i < hits.Length; i++)
            {
                var owner = hits[i].transform.GetComponentInParent<DeskDrag>();
                if (owner == null)
                    continue;

                var order = owner.TopOrder();
                if (order < topOrder)
                    continue;

                topOrder = order;
                top = owner;
            }

            if (top != this)
                return;

            if (_shake != null)
            {
                StopCoroutine(_shake);
                _shake = null;
            }

            var plane = Surface(transform.position);
            if (!plane.Raycast(ray, out var distance))
                return;

            var piece = GetComponent<ArticlePiece>();
            _home = transform.position;
            _homeScale = transform.localScale;
            if (piece != null && _board != null)
                _board.PrepareLift(piece);

            _lift = SurfaceNormal() * LiftDistance;
            _risen = 0f;
            _grab = transform.position - ray.GetPoint(distance);
            _held = true;
            if (piece != null && _views != null)
                _views.AllowLookUp(false);
            transform.position = _home;
            Lift(true);
        }

        private void Move(Camera camera, Vector2 screen)
        {
            var ray = camera.ScreenPointToRay(screen);
            var plane = Surface(_home);
            if (!plane.Raycast(ray, out var distance))
                return;

            _risen = Mathf.MoveTowards(_risen, 1f, Time.deltaTime / 0.16f);
            var desired = ray.GetPoint(distance) + _grab - _lift * _risen;
            transform.position = KeepOnScreen(camera, desired);
            var piece = GetComponent<ArticlePiece>();
            if (piece != null)
                _board?.Hover(piece);
        }

        private void Drop()
        {
            _held = false;
            transform.position += _lift * _risen;
            var piece = GetComponent<ArticlePiece>();
            if (piece != null && _views != null)
                _views.AllowLookUp(true);
            var stamp = GetComponent<DeskStamp>();
            if (_board == null)
            {
                Lift(false);
                return;
            }

            if (stamp != null)
            {
                stamp.Release(_board, _home, _homeScale);
                return;
            }

            if (piece == null)
            {
                _board.Raise(gameObject);
                return;
            }

            _board.EndHover(piece);
            if (_board.Settle(piece, out var reason))
                return;

            if (!string.IsNullOrEmpty(reason))
                _board.Notify(reason, transform.position);

            if (_shake != null)
                StopCoroutine(_shake);
            _shake = StartCoroutine(ShakeHome());
        }

        private IEnumerator ShakeHome()
        {
            var piece = GetComponent<ArticlePiece>();
            var origin = transform.position;
            var right = SurfaceTransform().right;
            var time = 0f;
            const float length = 0.32f;
            while (time < length)
            {
                time += Time.deltaTime;
                var fade = 1f - time / length;
                transform.position = origin + right * (Mathf.Sin(time * 46f) * 0.14f * fade);
                yield return null;
            }

            transform.position = _home;
            transform.localScale = _homeScale;
            if (piece != null && _board != null && !_board.Settle(piece, out _))
                _board.PlaceLoose(piece);
            _shake = null;
        }

        private int TopOrder()
        {
            var top = _rest;
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].sortingOrder > top)
                    top = renderers[i].sortingOrder;
            }

            return top;
        }

        private Transform SurfaceTransform()
        {
            return _board != null ? _board.transform : transform;
        }

        private Vector3 SurfaceNormal()
        {
            return SurfaceTransform().forward;
        }

        private Plane Surface(Vector3 through)
        {
            return new Plane(SurfaceNormal(), through);
        }

        private Vector3 KeepOnScreen(Camera camera, Vector3 desired)
        {
            var bounds = VisualBounds();
            var center = bounds.center + (desired - transform.position);
            var ext = bounds.extents;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var x = -1; x <= 1; x += 2)
            {
                for (var z = -1; z <= 1; z += 2)
                {
                    var corner = center + new Vector3(ext.x * x, 0f, ext.z * z);
                    var pixel = camera.WorldToScreenPoint(corner);
                    if (pixel.z <= 0.01f)
                        return desired;

                    min = Vector2.Min(min, pixel);
                    max = Vector2.Max(max, pixel);
                }
            }

            const float margin = 16f;
            var overflow = Vector2.zero;
            var pastLeft = min.x < margin;
            var pastRight = max.x > Screen.width - margin;
            var pastBottom = min.y < margin;
            var pastTop = max.y > Screen.height - margin;
            if (pastLeft && !pastRight)
                overflow.x = margin - min.x;
            else if (pastRight && !pastLeft)
                overflow.x = Screen.width - margin - max.x;
            if (pastBottom && !pastTop)
                overflow.y = margin - min.y;
            else if (pastTop && !pastBottom)
                overflow.y = Screen.height - margin - max.y;
            if (overflow.sqrMagnitude < 0.01f)
                return desired;

            var screen = camera.WorldToScreenPoint(desired);
            var ray = camera.ScreenPointToRay(new Vector2(screen.x + overflow.x, screen.y + overflow.y));
            var plane = new Plane(SurfaceNormal(), desired);
            if (!plane.Raycast(ray, out var distance))
                return desired;

            return ray.GetPoint(distance);
        }

        private Bounds VisualBounds()
        {
            var bounds = new Bounds(transform.position, Vector3.zero);
            var renderers = GetComponentsInChildren<Renderer>(true);
            var found = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                    continue;

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                    bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }

        private void Lift(bool held)
        {
            ArticlePiece.ApplyDrawOrder(gameObject, held ? LiftOrder : _rest);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var bounds = VisualBounds();
            if (bounds.size.sqrMagnitude < 0.0001f)
                return;

            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            var center = bounds.center;
            var ext = bounds.extents;
            var a = center + new Vector3(ext.x, 0f, ext.z);
            var b = center + new Vector3(ext.x, 0f, -ext.z);
            var c = center + new Vector3(-ext.x, 0f, -ext.z);
            var d = center + new Vector3(-ext.x, 0f, ext.z);
            Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.95f);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }
#endif
    }
}
