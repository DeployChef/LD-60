using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace LD60.UI
{
    public sealed class DeskDrag : MonoBehaviour
    {
        private const int LiftOrder = 300;
        private const float LiftDistance = 0.2f;

        private WorkstationViewController _views;
        private CinemachineBrain _brain;
        private Renderer[] _renderers;
        private int[] _orders;
        private bool _held;
        private Vector3 _grab;
        private Vector3 _rest;

        private void Awake()
        {
            _views = FindAnyObjectByType<WorkstationViewController>();
            if (_views != null)
                _brain = _views.GetComponent<CinemachineBrain>();
            if (_brain == null)
                _brain = FindAnyObjectByType<CinemachineBrain>();
            RememberOrder();
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
            return EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
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
            if (!Physics.Raycast(ray, out var hit, 100f))
                return;
            if (hit.transform != transform && !hit.transform.IsChildOf(transform))
                return;

            var plane = new Plane(transform.forward, transform.position);
            if (!plane.Raycast(ray, out var distance))
                return;

            _grab = transform.position - ray.GetPoint(distance);
            _rest = transform.position;
            _held = true;
            transform.position = _rest - transform.forward * LiftDistance;
            Lift(true);
        }

        private void Move(Camera camera, Vector2 screen)
        {
            var ray = camera.ScreenPointToRay(screen);
            var plane = new Plane(transform.forward, _rest);
            if (!plane.Raycast(ray, out var distance))
                return;

            transform.position = ray.GetPoint(distance) + _grab - transform.forward * LiftDistance;
        }

        private void Drop()
        {
            _held = false;
            var lifted = transform.position + transform.forward * LiftDistance;
            transform.position = lifted;
            Lift(false);
        }

        private void RememberOrder()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _orders = new int[_renderers.Length];
            for (var i = 0; i < _renderers.Length; i++)
                _orders[i] = _renderers[i].sortingOrder;
        }

        private void Lift(bool held)
        {
            for (var i = 0; i < _renderers.Length; i++)
                _renderers[i].sortingOrder = held ? LiftOrder + i : _orders[i];
        }
    }
}
