using UnityEngine;
using UnityEngine.Rendering;

namespace LD60.UI
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteHintOutline : MonoBehaviour
    {
        [SerializeField] private Material material;
        [SerializeField] private Color color = new(1f, 0.84f, 0.18f, 1f);
        [SerializeField, Range(1f, 12f)] private float width = 5f;
        [SerializeField] private float pulseSeconds = 1.1f;

        private SpriteRenderer _source;
        private MeshRenderer _outline;
        private Mesh _mesh;
        private MaterialPropertyBlock _properties;
        private Sprite _sprite;
        private float _meshWidth;
        private bool _highlighted;

        public void SetHighlighted(bool highlighted)
        {
            _highlighted = highlighted;
            if (highlighted)
            {
                EnsureVisual();
                RefreshVisual();
            }
            else if (_outline != null)
                _outline.enabled = false;
        }

        private void OnEnable()
        {
            if (_highlighted)
                SetHighlighted(true);
        }

        private void OnDisable()
        {
            if (_outline != null)
                _outline.enabled = false;
        }

        private void LateUpdate()
        {
            if (_highlighted)
                RefreshVisual();
        }

        private void EnsureVisual()
        {
            if (_outline != null || material == null)
                return;

            _source = GetComponent<SpriteRenderer>();
            var visual = new GameObject("FirstActionOutline");
            visual.layer = gameObject.layer;
            visual.transform.SetParent(transform, false);
            _mesh = new Mesh { name = "Sprite hint outline" };
            visual.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _outline = visual.AddComponent<MeshRenderer>();
            _outline.sharedMaterial = material;
            _outline.shadowCastingMode = ShadowCastingMode.Off;
            _outline.receiveShadows = false;
            _properties = new MaterialPropertyBlock();
        }

        private void RefreshVisual()
        {
            if (_outline == null)
                return;

            var sprite = _source.sprite;
            _outline.enabled = isActiveAndEnabled && _source.enabled && sprite != null;
            if (sprite == null)
                return;

            if (_sprite != sprite || _meshWidth != width)
            {
                _sprite = sprite;
                _meshWidth = width;
                var texture = sprite.texture;
                var rect = sprite.rect;
                var bounds = sprite.bounds;
                var padding = width / sprite.pixelsPerUnit;
                var min = bounds.min - new Vector3(padding, padding, 0f);
                var max = bounds.max + new Vector3(padding, padding, 0f);
                var u0 = (rect.xMin - width) / texture.width;
                var v0 = (rect.yMin - width) / texture.height;
                var u1 = (rect.xMax + width) / texture.width;
                var v1 = (rect.yMax + width) / texture.height;
                // A full quad with padding also outlines pixels at the sprite's edges.
                _mesh.Clear();
                _mesh.vertices = new[]
                {
                    new Vector3(min.x, min.y, 0f), new Vector3(max.x, min.y, 0f),
                    new Vector3(max.x, max.y, 0f), new Vector3(min.x, max.y, 0f)
                };
                _mesh.uv = new[] { new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1) };
                _mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                _mesh.RecalculateBounds();
                _properties.SetTexture("_MainTex", texture);
                _properties.SetVector("_SpriteRect", new Vector4(
                    rect.xMin / texture.width, rect.yMin / texture.height,
                    rect.xMax / texture.width, rect.yMax / texture.height));
            }

            var textureWidth = sprite.texture.width;
            var textureHeight = sprite.texture.height;
            var wave = 0.5f + 0.5f * Mathf.Sin(Time.time * (Mathf.PI * 2f / Mathf.Max(0.05f, pulseSeconds)));
            var step = width * Mathf.Lerp(0.4f, 1f, wave);
            var pulsed = color;
            pulsed.a *= Mathf.Lerp(0.12f, 1f, wave);
            _properties.SetVector("_OutlineStep", new Vector4(step / textureWidth, step / textureHeight, 0f, 0f));
            _properties.SetColor("_OutlineColor", pulsed);
            _outline.SetPropertyBlock(_properties);
            _outline.sortingLayerID = _source.sortingLayerID;
            _outline.sortingOrder = _source.sortingOrder - 1;
            _outline.transform.localScale = new Vector3(_source.flipX ? -1f : 1f, _source.flipY ? -1f : 1f, 1f);
        }

        private void OnDestroy()
        {
            if (_outline != null)
            {
                if (Application.isPlaying)
                    Destroy(_outline.gameObject);
                else
                    DestroyImmediate(_outline.gameObject);
            }
            if (_mesh != null)
            {
                if (Application.isPlaying)
                    Destroy(_mesh);
                else
                    DestroyImmediate(_mesh);
            }
        }
    }
}
