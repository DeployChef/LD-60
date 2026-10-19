using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LD60.UI
{
    [DefaultExecutionOrder(-100)]
    public sealed class CursorManager : MonoBehaviour
    {
        private const string DefaultCursorResource = "Cursors/Cursor_Default";
        private const string HoverCursorResource = "Cursors/Cursor_Hover";
        private const float WebGlCursorScale = 1f / 1.5f;

        [Header("Textures")]
        [SerializeField] private Texture2D defaultCursor;
        [SerializeField] private Vector2 defaultHotspot = new(1f, 1f);
        [SerializeField] private Texture2D hoverCursor;
        [SerializeField] private Vector2 hoverHotspot = new(1f, 1f);

        [Header("Settings")]
        [SerializeField] private CursorMode cursorMode = CursorMode.Auto;

        private static CursorManager _instance;
        private static bool _hoverOverride;

        private CinemachineBrain _brain;
        private WorkstationViewController _views;
        private PointerEventData _pointerEventData;
        private readonly List<RaycastResult> _uiRaycastResults = new(16);
        private bool _isCurrentlyHovering;
        private Texture2D _activeTexture;
        private readonly Dictionary<int, Texture2D> _scaledCursors = new();

        public static CursorManager Instance
        {
            get
            {
                if (_instance == null)
                    EnsureCreated();
                return _instance;
            }
        }

        public static void EnsureCreated()
        {
            if (_instance != null)
                return;

            _instance = FindAnyObjectByType<CursorManager>();
            if (_instance != null)
                return;

            var go = new GameObject(nameof(CursorManager));
            if (Application.isPlaying)
                DontDestroyOnLoad(go);
            _instance = go.AddComponent<CursorManager>();
        }

        public static void SetHoverOverride(bool hover)
        {
            _hoverOverride = hover;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);

            LoadTexturesIfMissing();
            ApplyCursor(defaultCursor, defaultHotspot);
        }

        private void Start()
        {
            ApplyCursor(defaultCursor, defaultHotspot);
        }

        private void OnEnable()
        {
            ApplyCursor(_isCurrentlyHovering ? hoverCursor : defaultCursor,
                        _isCurrentlyHovering ? hoverHotspot : defaultHotspot);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                ApplyCursor(_isCurrentlyHovering ? hoverCursor : defaultCursor,
                            _isCurrentlyHovering ? hoverHotspot : defaultHotspot);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                _instance = null;
            }
        }

        private void LoadTexturesIfMissing()
        {
            if (defaultCursor == null)
                defaultCursor = Resources.Load<Texture2D>(DefaultCursorResource);

            if (hoverCursor == null)
                hoverCursor = Resources.Load<Texture2D>(HoverCursorResource);
        }

        private void LateUpdate()
        {
            var screenPos = GetMousePosition(out var hasMouse);
            if (!hasMouse)
                return;

            if (screenPos.x < 0f || screenPos.y < 0f ||
                screenPos.x > Screen.width || screenPos.y > Screen.height)
            {
                return;
            }

            var shouldHover = EvaluateHoverState(screenPos);
            if (shouldHover != _isCurrentlyHovering || _activeTexture == null)
            {
                _isCurrentlyHovering = shouldHover;
                var targetTex = _isCurrentlyHovering ? hoverCursor : defaultCursor;
                var targetHotspot = _isCurrentlyHovering ? hoverHotspot : defaultHotspot;
                ApplyCursor(targetTex, targetHotspot);
            }
        }

        private bool EvaluateHoverState(Vector2 screenPos)
        {
            if (_hoverOverride)
                return true;

            if (DeskDrag.IsAnyHeld)
                return true;

            if (CheckUiHover(screenPos))
                return true;

            if (CheckWorldHover(screenPos))
                return true;

            return false;
        }

        private bool CheckUiHover(Vector2 screenPos)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            if (_pointerEventData == null)
                _pointerEventData = new PointerEventData(eventSystem);

            _pointerEventData.position = screenPos;
            _uiRaycastResults.Clear();
            eventSystem.RaycastAll(_pointerEventData, _uiRaycastResults);

            for (var i = 0; i < _uiRaycastResults.Count; i++)
            {
                var go = _uiRaycastResults[i].gameObject;
                if (go == null)
                    continue;

                var selectable = go.GetComponentInParent<Selectable>();
                if (selectable != null && selectable.isActiveAndEnabled && selectable.IsInteractable())
                    return true;

                var hoverable = go.GetComponentInParent<ICursorHoverable>();
                if (hoverable != null && hoverable.CanHoverCursor)
                    return true;

                var click = go.GetComponentInParent<IPointerClickHandler>();
                if (click != null && click is MonoBehaviour mb && mb.isActiveAndEnabled && !(click is GraphicRaycaster))
                    return true;
            }

            return false;
        }

        private bool CheckWorldHover(Vector2 screenPos)
        {
            var camera = GetActiveCamera();
            if (camera == null)
                return false;

            var ray = camera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out var hit, 200f))
            {
                var col = hit.collider;
                if (col != null)
                {
                    var hoverable = col.GetComponentInParent<ICursorHoverable>();
                    if (hoverable != null && hoverable.CanHoverCursor)
                        return true;

                    var deskDrag = col.GetComponentInParent<DeskDrag>();
                    if (deskDrag != null && deskDrag.isActiveAndEnabled)
                    {
                        if (_views == null)
                            _views = FindAnyObjectByType<WorkstationViewController>();

                        if (_views == null || _views.IsShowingTable)
                        {
                            var stamp = deskDrag.GetComponent<DeskStamp>();
                            if (stamp == null || !stamp.IsBusy)
                                return true;
                        }
                    }

                    var capsule = col.GetComponentInParent<CapsuleSlot>();
                    if (capsule != null && capsule.isActiveAndEnabled && capsule.HasPendingCapsule)
                        return true;

                    var choice = col.GetComponentInParent<DialogueChoiceButton>();
                    if (choice != null && choice.isActiveAndEnabled && choice.gameObject.activeInHierarchy)
                        return true;

                    var goName = col.gameObject.name;
                    if (goName == "PowerSwitch" || goName.StartsWith("TrackButton"))
                        return true;
                }
            }

            var hit2D = Physics2D.Raycast(camera.ScreenToWorldPoint(screenPos), Vector2.zero);
            if (hit2D.collider != null)
            {
                var hoverable2D = hit2D.collider.GetComponentInParent<ICursorHoverable>();
                if (hoverable2D != null && hoverable2D.CanHoverCursor)
                    return true;
            }

            return false;
        }

        private Camera GetActiveCamera()
        {
            if (_brain != null && _brain.OutputCamera != null)
                return _brain.OutputCamera;

            _brain = FindAnyObjectByType<CinemachineBrain>();
            if (_brain != null && _brain.OutputCamera != null)
                return _brain.OutputCamera;

            return Camera.main;
        }

        private static Vector2 GetMousePosition(out bool hasMouse)
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                hasMouse = true;
                return mouse.position.ReadValue();
            }

            if (Input.mousePresent)
            {
                hasMouse = true;
                return Input.mousePosition;
            }

            hasMouse = false;
            return Vector2.zero;
        }

        private void ApplyCursor(Texture2D texture, Vector2 hotspot)
        {
            if (texture == null)
                LoadTexturesIfMissing();

            if (texture == null)
                return;

            _activeTexture = texture;
            texture = ScaleCursorForWebGl(texture, ref hotspot);
            Cursor.SetCursor(texture, hotspot, cursorMode);
        }

        private Texture2D ScaleCursorForWebGl(Texture2D source, ref Vector2 hotspot)
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer)
                return source;

            var id = source.GetInstanceID();
            if (!_scaledCursors.TryGetValue(id, out var scaled))
            {
                scaled = ScaleCursor(source, WebGlCursorScale);
                _scaledCursors.Add(id, scaled);
            }

            hotspot *= WebGlCursorScale;
            return scaled;
        }

        private static Texture2D ScaleCursor(Texture2D source, float scale)
        {
            var width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            var height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
            var pixels = source.GetPixels32();
            var scaledPixels = new Color32[width * height];
            var sourceWidth = source.width;
            var sourceHeight = source.height;

            for (var y = 0; y < height; y++)
            {
                var sourceY = Mathf.Clamp(
                    Mathf.RoundToInt((y + 0.5f) * sourceHeight / height - 0.5f),
                    0,
                    sourceHeight - 1);
                for (var x = 0; x < width; x++)
                {
                    var sourceX = Mathf.Clamp(
                        Mathf.RoundToInt((x + 0.5f) * sourceWidth / width - 0.5f),
                        0,
                        sourceWidth - 1);
                    scaledPixels[y * width + x] = pixels[sourceY * sourceWidth + sourceX];
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(scaledPixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
