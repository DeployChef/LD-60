using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LD60.UI
{
    public sealed class DeskDrag : MonoBehaviour
    {
        private const int LiftOrder = 4000;
        private const float LiftDistance = 0.2f;

        private WorkstationViewController _views;
        private NewspaperDesk _board;
        private CinemachineBrain _brain;
        private int _rest;
        private bool _held;
        private Vector3 _grab;
        private Vector3 _home;
        private Vector3 _homeScale;
        private Vector3 _lift;

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

            var plane = Surface(transform.position);
            if (!plane.Raycast(ray, out var distance))
                return;

            var piece = GetComponent<ArticlePiece>();
            _home = transform.position;
            _homeScale = transform.localScale;
            if (piece != null && _board != null)
                _board.PrepareLift(piece);

            _lift = SurfaceNormal() * LiftDistance;
            _grab = transform.position - ray.GetPoint(distance);
            _held = true;
            transform.position = _home - _lift;
            Lift(true);
        }

        private void Move(Camera camera, Vector2 screen)
        {
            var ray = camera.ScreenPointToRay(screen);
            var plane = Surface(_home);
            if (!plane.Raycast(ray, out var distance))
                return;

            transform.position = ray.GetPoint(distance) + _grab - _lift;
        }

        private void Drop()
        {
            _held = false;
            transform.position += _lift;
            var piece = GetComponent<ArticlePiece>();
            if (_board == null)
            {
                Lift(false);
                return;
            }

            if (piece == null)
            {
                _board.Raise(gameObject);
                return;
            }

            if (_board.Settle(piece))
                return;

            transform.position = _home;
            transform.localScale = _homeScale;
            _board.Settle(piece);
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

        private void Lift(bool held)
        {
            ArticlePiece.ApplyDrawOrder(gameObject, held ? LiftOrder : _rest);
        }
    }
}
