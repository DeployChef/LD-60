using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LD60.UI
{
    public sealed class WorkstationViewController : MonoBehaviour
    {
        [SerializeField] private InputActionAsset input;
        [SerializeField] private CinemachineBrain brain;
        [SerializeField] private CinemachineCamera windowCamera;
        [SerializeField] private CinemachineCamera tableCamera;

        [Header("Hover zones (% of screen height)")]
        [Tooltip("Height of the bottom zone, measured up from the bottom edge. Switches to the table.")]
        [SerializeField, Range(1f, 49f)] private float bottomZoneHeightPercent = 12f;
        [Tooltip("Height of the top zone, measured down from the top edge. Returns to the window.")]
        [SerializeField, Range(1f, 49f)] private float topZoneHeightPercent = 12f;
        [Tooltip("Draw the bottom (cyan) and top (orange) hover zones when this object is selected. Enable Gizmos in the Scene/Game view.")]
        [SerializeField] private bool showZoneGizmos = true;

        private InputAction _point;
        private bool _showingTable;
        private bool _lookDown = true;
        private bool _lookUp = true;
        private bool _holdLookUp;

        public bool IsShowingTable => _showingTable;

        public void AllowLookDown(bool allow)
        {
            _lookDown = allow;
        }

        public void AllowLookUp(bool allow)
        {
            _lookUp = allow;
        }

        public void HoldLookUp(bool hold)
        {
            _holdLookUp = hold;
            if (hold)
                ShowTable(false, true);
        }

        private void Awake()
        {
            if (brain == null || windowCamera == null || tableCamera == null)
            {
                Debug.LogError("Workstation cameras are not assigned.", this);
                enabled = false;
                return;
            }

            ShowTable(false);
            _point = input.FindAction("Game/Point", true);
        }

        private void OnEnable()
        {
            _point.performed += OnPoint;
            _point.Enable();
        }

        private void OnDisable()
        {
            _point.performed -= OnPoint;
            _point.Disable();
        }

        private void OnPoint(InputAction.CallbackContext context)
        {
            if (Time.timeScale == 0f || !Application.isFocused)
                return;

            var position = context.ReadValue<Vector2>();
            if (position.x < 0f || position.x >= Screen.width ||
                position.y < 0f || position.y >= Screen.height)
                return;

            var height = position.y / Screen.height;
            if (_lookDown && !_holdLookUp && !_showingTable && height <= bottomZoneHeightPercent * 0.01f)
                ShowTable(true);
            else if (_lookUp && !_holdLookUp && _showingTable && height >= 1f - topZoneHeightPercent * 0.01f)
                ShowTable(false);
        }

        private void LateUpdate()
        {
            if (_holdLookUp && _showingTable)
                ShowTable(false, true);
        }

        public void ShowTable(bool show, bool cut = false)
        {
            _showingTable = show;
            windowCamera.Priority = show ? 0 : 10;
            tableCamera.Priority = show ? 10 : 0;
            if (cut)
                CutBlend();
        }

        private void CutBlend()
        {
            if (brain == null)
                return;

            var custom = brain.CustomBlends;
            var previous = brain.DefaultBlend;
            brain.CustomBlends = null;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            brain.ManualUpdate();
            brain.DefaultBlend = previous;
            brain.CustomBlends = custom;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!showZoneGizmos || brain == null || brain.OutputCamera == null)
                return;

            var camera = brain.OutputCamera;
            DrawZone(camera, 0f, bottomZoneHeightPercent * 0.01f, new Color(0f, 0.8f, 1f));
            DrawZone(camera, 1f - topZoneHeightPercent * 0.01f, 1f, new Color(1f, 0.65f, 0f));
        }

        private static void DrawZone(Camera camera, float bottom, float top, Color color)
        {
            // A thin plane in front of the output camera keeps the gizmo aligned
            // with the screen throughout Cinemachine blends and FOV changes.
            var depth = camera.nearClipPlane + 0.2f;
            var lowerLeft = camera.ViewportToWorldPoint(new Vector3(0f, bottom, depth));
            var lowerRight = camera.ViewportToWorldPoint(new Vector3(1f, bottom, depth));
            var upperLeft = camera.ViewportToWorldPoint(new Vector3(0f, top, depth));
            var upperRight = camera.ViewportToWorldPoint(new Vector3(1f, top, depth));
            var previousMatrix = Gizmos.matrix;
            var previousColor = Gizmos.color;
            Gizmos.matrix = Matrix4x4.TRS((lowerLeft + upperRight) * 0.5f,
                camera.transform.rotation, Vector3.one);
            var size = new Vector3(Vector3.Distance(lowerLeft, lowerRight),
                Vector3.Distance(lowerLeft, upperLeft), 0f);
            Gizmos.color = new Color(color.r, color.g, color.b, 0.12f);
            Gizmos.DrawCube(Vector3.zero, size);
            Gizmos.color = color;
            Gizmos.DrawWireCube(Vector3.zero, size);
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
#endif
    }
}
