using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LD60.UI
{
    /// <summary>
    /// Soft pointer chase for a Cinemachine camera. Rotation matches the GMTK CameraFlow follow.
    /// A small slide is added because a turn around the camera does not separate depth layers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraMouseFollow : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera vcam;
        [SerializeField] private float yawDegrees = 4f;
        [SerializeField] private float pitchDegrees = 2.5f;
        [SerializeField] private float slideUnits = 0.75f;
        [SerializeField] private float followSpeed = 4f;
        [SerializeField] private float deadZone = 0.03f;

        private Vector3 _restLocalPosition;
        private Quaternion _restLocalRotation;
        private Vector2 _current;

        private void Awake()
        {
            if (vcam == null)
                vcam = GetComponent<CinemachineCamera>();
            _restLocalPosition = transform.localPosition;
            _restLocalRotation = transform.localRotation;
        }

        private void OnDisable()
        {
            Rest();
        }

        private void LateUpdate()
        {
            if (vcam == null || !vcam.IsLive)
            {
                Rest();
                return;
            }

            var target = ReadPointerOffset(deadZone);
            var dt = Time.unscaledDeltaTime;
            _current = Vector2.Lerp(_current, target, 1f - Mathf.Exp(-followSpeed * dt));

            transform.localPosition = _restLocalPosition + new Vector3(_current.x, _current.y, 0f) * slideUnits;
            transform.localRotation =
                _restLocalRotation * Quaternion.Euler(-_current.y * pitchDegrees, _current.x * yawDegrees, 0f);
        }

        private void Rest()
        {
            transform.localPosition = _restLocalPosition;
            transform.localRotation = _restLocalRotation;
            _current = Vector2.zero;
        }

        private static Vector2 ReadPointerOffset(float deadZone)
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return Vector2.zero;

            var cam = Camera.main;
            var screen = mouse.position.ReadValue();
            Vector2 offset;
            if (cam != null)
            {
                var vp = cam.ScreenToViewportPoint(screen);
                offset = new Vector2(vp.x * 2f - 1f, vp.y * 2f - 1f);
            }
            else
            {
                offset = new Vector2(
                    screen.x / Mathf.Max(1, Screen.width) * 2f - 1f,
                    screen.y / Mathf.Max(1, Screen.height) * 2f - 1f);
            }

            if (offset.sqrMagnitude < deadZone * deadZone)
                return Vector2.zero;
            return Vector2.ClampMagnitude(offset, 1f);
        }
    }
}
