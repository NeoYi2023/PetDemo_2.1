// SPEC_VideoMattingAtlas.md v1.3 / SPEC_FarmBattleDemo.md §9.14.11 v3.281 —
// Canvas Overlay–compatible CLI diced sprite player (sprites.json + atlas_*.png).
// Uses CanvasRenderer.SetMesh (same mesh path as DicedSpriteAnimationPlayer) so UVs
// sample diced atlas tiles into a composed character — not a full-atlas UV quad.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.VideoMatting
{
    /// <summary>
    /// Plays a VideoMatting CLI atlas under Screen Space Overlay.
    /// Geometry is built as a real Mesh (vertices + atlas UVs + indices) and pushed via
    /// <see cref="CanvasRenderer.SetMesh"/>; avoids the default Graphic rect which would
    /// stretch the entire atlas texture (looks like scattered dice fragments).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    [RequireComponent(typeof(RectTransform))]
    public sealed class DicedSpriteCanvasPlayer : MaskableGraphic
    {
        public const float DefaultFps = 15f;
        public const float DefaultPixelsPerUnit = 100f;

        private DicedSpriteEntry[] _frames = Array.Empty<DicedSpriteEntry>();
        private Texture2D[] _atlases = Array.Empty<Texture2D>();
        private Mesh _mesh;
        private float _fps = DefaultFps;
        private float _pixelsPerUnit = DefaultPixelsPerUnit;
        private bool _playing;
        private bool _loop;
        private bool _flipUvV;
        private float _time;
        private int _frameIndex = -1;
        private int _atlasIndex = -1;
        private Texture _currentTexture;

        public int FrameCount => _frames != null ? _frames.Length : 0;
        public int CurrentFrameIndex => _frameIndex;
        public bool IsPlaying => _playing;
        public float Fps => _fps;

        public override Texture mainTexture =>
            _currentTexture != null ? _currentTexture : s_WhiteTexture;

        protected override void Awake()
        {
            base.Awake();
            // Custom mesh path only; never fall back to legacy VBO quad.
            useLegacyMeshGeneration = false;
            raycastTarget = false;
            EnsureMesh();
        }

        protected override void OnDestroy()
        {
            if (_mesh != null)
            {
                if (Application.isPlaying)
                    Destroy(_mesh);
                else
                    DestroyImmediate(_mesh);
                _mesh = null;
            }

            base.OnDestroy();
        }

        /// <summary>
        /// Load <c>sprites.json</c> + <c>atlas_*.png</c> from a Resources folder
        /// (e.g. <c>VideoMatting/ZJDH_rest_2</c>).
        /// </summary>
        public bool LoadFromResources(string resourcesFolder)
        {
            if (string.IsNullOrWhiteSpace(resourcesFolder))
            {
                Debug.LogWarning("[DicedSpriteCanvasPlayer] empty resources folder");
                return false;
            }

            string folder = resourcesFolder.Trim().TrimEnd('/');
            var json = Resources.Load<TextAsset>(folder + "/sprites");
            if (json == null)
            {
                Debug.LogWarning("[DicedSpriteCanvasPlayer] missing TextAsset at Resources/" + folder + "/sprites");
                _frames = Array.Empty<DicedSpriteEntry>();
                _atlases = Array.Empty<Texture2D>();
                return false;
            }

            var loaded = Resources.LoadAll<Texture2D>(folder);
            var atlasList = new List<Texture2D>();
            if (loaded != null)
            {
                for (int i = 0; i < loaded.Length; i++)
                {
                    var tex = loaded[i];
                    if (tex == null || string.IsNullOrEmpty(tex.name))
                        continue;
                    // Exact atlas_N name only (ignore sprite sub-assets like atlas_0_0).
                    if (IsAtlasTextureName(tex.name))
                        atlasList.Add(tex);
                }
            }

            atlasList.Sort((a, b) => ParseAtlasIndex(a.name).CompareTo(ParseAtlasIndex(b.name)));

            _atlases = atlasList.ToArray();
            _frames = DicedSpriteJson.ParseAndSort(json.text);
            _frameIndex = -1;
            _atlasIndex = -1;
            _currentTexture = null;
            _playing = false;
            _time = 0f;

            if (FrameCount <= 0)
            {
                Debug.LogWarning("[DicedSpriteCanvasPlayer] no frames in Resources/" + folder + "/sprites");
                return false;
            }

            if (_atlases.Length == 0)
            {
                Debug.LogWarning("[DicedSpriteCanvasPlayer] no atlas_* textures in Resources/" + folder);
                return false;
            }

            ApplyFrame(0);
            return true;
        }

        public void Play(bool loop, float fps = DefaultFps)
        {
            if (FrameCount <= 0)
            {
                Debug.LogWarning("[DicedSpriteCanvasPlayer] no frames to play");
                return;
            }

            _loop = loop;
            _fps = fps > 0f ? fps : DefaultFps;
            _time = 0f;
            _playing = true;
            ApplyFrame(0);
        }

        public void Stop()
        {
            _playing = false;
            _time = 0f;
            if (FrameCount > 0)
                ApplyFrame(0);
        }

        /// <summary>Play once at <paramref name="fps"/>; yields until finished or stopped.</summary>
        public IEnumerator PlayOnceRoutine(float fps = DefaultFps)
        {
            if (FrameCount <= 0)
                yield break;

            Play(loop: false, fps);
            while (_playing)
                yield return null;
        }

        public void SetFlipUvV(bool flip)
        {
            _flipUvV = flip;
            if (_frameIndex >= 0)
                ApplyFrame(_frameIndex);
        }

        public void SetPixelsPerUnit(float ppu)
        {
            _pixelsPerUnit = ppu > 0f ? ppu : DefaultPixelsPerUnit;
            if (_frameIndex >= 0)
                ApplyFrame(_frameIndex);
        }

        private void Update()
        {
            if (!_playing || FrameCount <= 0 || _fps <= 0f)
                return;

            _time += Time.deltaTime;
            int index;
            if (_loop)
            {
                index = Mathf.FloorToInt(_time * _fps) % FrameCount;
                if (index < 0)
                    index = 0;
            }
            else
            {
                index = Mathf.FloorToInt(_time * _fps);
                if (index >= FrameCount)
                {
                    index = FrameCount - 1;
                    _playing = false;
                }
            }

            if (index != _frameIndex)
                ApplyFrame(index);
        }

        private void ApplyFrame(int index)
        {
            if (_frames == null || index < 0 || index >= _frames.Length)
                return;

            var entry = _frames[index];
            if (entry == null)
                return;

            int atlasIdx = entry.atlas;
            Texture2D atlas = null;
            if (_atlases != null && atlasIdx >= 0 && atlasIdx < _atlases.Length)
                atlas = _atlases[atlasIdx];

            if (atlas != null)
            {
                if (atlasIdx != _atlasIndex || _currentTexture != atlas)
                {
                    _currentTexture = atlas;
                    _atlasIndex = atlasIdx;
                    SetMaterialDirty();
                }
            }
            else
            {
                Debug.LogWarning("[DicedSpriteCanvasPlayer] atlas index out of range: " + atlasIdx);
            }

            _frameIndex = index;
            SetVerticesDirty();
        }

        /// <summary>
        /// Push diced mesh directly to CanvasRenderer — same vertex/UV/index layout as
        /// <see cref="DicedSpriteAnimationPlayer"/> (world MeshRenderer path).
        /// </summary>
        protected override void UpdateGeometry()
        {
            if (!IsActive() || FrameCount <= 0 || _frameIndex < 0 || _frameIndex >= FrameCount)
            {
                canvasRenderer.Clear();
                return;
            }

            var entry = _frames[_frameIndex];
            if (entry == null || entry.vertices == null || entry.uvs == null || entry.indices == null)
            {
                canvasRenderer.Clear();
                return;
            }

            int vertCount = entry.vertices.Length;
            if (vertCount == 0 || entry.uvs.Length != vertCount)
            {
                canvasRenderer.Clear();
                return;
            }

            EnsureMesh();
            float ppu = _pixelsPerUnit > 0f ? _pixelsPerUnit : DefaultPixelsPerUnit;

            var verts = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var colors = new Color32[vertCount];
            Color32 color32 = color;

            for (int i = 0; i < vertCount; i++)
            {
                var v = entry.vertices[i];
                verts[i] = new Vector3(
                    (v != null ? v.x : 0f) * ppu,
                    (v != null ? v.y : 0f) * ppu,
                    0f);

                var uv = entry.uvs[i];
                float u = uv != null ? uv.u : 0f;
                float vv = uv != null ? uv.v : 0f;
                if (_flipUvV)
                    vv = 1f - vv;
                uvs[i] = new Vector2(u, vv);
                colors[i] = color32;
            }

            _mesh.Clear();
            _mesh.vertices = verts;
            _mesh.uv = uvs;
            _mesh.colors32 = colors;
            _mesh.triangles = entry.indices;
            _mesh.RecalculateBounds();

            canvasRenderer.SetMesh(_mesh);
        }

        protected override void UpdateMaterial()
        {
            base.UpdateMaterial();
            if (_currentTexture != null)
                canvasRenderer.SetTexture(_currentTexture);
        }

        /// <summary>Disable default rect fill so a failed mesh never shows the raw atlas.</summary>
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }

        private void EnsureMesh()
        {
            if (_mesh != null)
                return;
            _mesh = new Mesh { name = "DicedSpriteCanvasMesh" };
            _mesh.MarkDynamic();
        }

        private static bool IsAtlasTextureName(string name)
        {
            if (string.IsNullOrEmpty(name) || !name.StartsWith("atlas_", StringComparison.OrdinalIgnoreCase))
                return false;

            // atlas_0 / atlas_12 — reject atlas_0_something sprite sub-assets
            string suffix = name.Substring("atlas_".Length);
            for (int i = 0; i < suffix.Length; i++)
            {
                if (!char.IsDigit(suffix[i]))
                    return false;
            }

            return suffix.Length > 0;
        }

        private static int ParseAtlasIndex(string name)
        {
            if (string.IsNullOrEmpty(name))
                return int.MaxValue;

            int underscore = name.LastIndexOf('_');
            if (underscore < 0 || underscore >= name.Length - 1)
                return int.MaxValue;

            if (int.TryParse(name.Substring(underscore + 1), out int value))
                return value;
            return int.MaxValue;
        }
    }
}
