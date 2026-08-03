// SPEC_VideoMattingAtlas.md v1.0 — mesh player for SpriteDicing CLI atlas + sprites.json.
using UnityEngine;

namespace PetDemo.UI.VideoMatting
{
    /// <summary>
    /// Plays a diced sprite animation from CLI <c>sprites.json</c> + <c>atlas_*.png</c> textures.
    /// Does not allocate new textures per frame; only swaps mesh / material main texture.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class DicedSpriteAnimationPlayer : MonoBehaviour
    {
        [SerializeField] private TextAsset spritesJson;
        [SerializeField] private Texture2D[] atlases;
        [SerializeField] private float defaultFps = 15f;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool loopOnEnable = true;
        [SerializeField] private bool flipUvV;
        [SerializeField] private string shaderName = "Sprites/Default";

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private Material _material;
        private DicedSpriteEntry[] _frames;
        private float _fps = 15f;
        private bool _playing;
        private bool _loop = true;
        private float _time;
        private int _frameIndex = -1;
        private int _atlasIndex = -1;

        public int FrameCount => _frames != null ? _frames.Length : 0;
        public int CurrentFrameIndex => _frameIndex;
        public bool IsPlaying => _playing;
        public float Fps => _fps;

        public TextAsset SpritesJson
        {
            get => spritesJson;
            set
            {
                spritesJson = value;
                Reload();
            }
        }

        public Texture2D[] Atlases
        {
            get => atlases;
            set => atlases = value;
        }

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            EnsureMeshAndMaterial();
            Reload();
        }

        private void OnEnable()
        {
            if (playOnEnable && FrameCount > 0)
                Play(loopOnEnable, defaultFps);
        }

        private void OnDestroy()
        {
            if (_mesh != null)
                Destroy(_mesh);
            if (_material != null)
                Destroy(_material);
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

        public void Reload()
        {
            _frames = spritesJson != null
                ? DicedSpriteJson.ParseAndSort(spritesJson.text)
                : System.Array.Empty<DicedSpriteEntry>();
            _frameIndex = -1;
            _atlasIndex = -1;
            if (FrameCount > 0)
                ApplyFrame(0);
        }

        public void Play(bool loop, float fps = 15f)
        {
            if (FrameCount <= 0)
                Reload();
            if (FrameCount <= 0)
            {
                Debug.LogWarning("[DicedSpriteAnimationPlayer] no frames to play");
                return;
            }

            _loop = loop;
            _fps = fps > 0f ? fps : defaultFps;
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

        public void Pause()
        {
            _playing = false;
        }

        public void Resume()
        {
            if (FrameCount > 0)
                _playing = true;
        }

        private void EnsureMeshAndMaterial()
        {
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "DicedSpriteAnimMesh" };
                _mesh.MarkDynamic();
                _meshFilter.sharedMesh = _mesh;
            }

            if (_material == null)
            {
                var shader = Shader.Find(shaderName);
                if (shader == null)
                    shader = Shader.Find("Unlit/Transparent");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");
                _material = new Material(shader) { name = "DicedSpriteAnimMat" };
                // Non-premultiplied PNG alpha blend (Sprites/Default already blends).
                if (_material.HasProperty("_Color"))
                    _material.color = Color.white;
                _meshRenderer.sharedMaterial = _material;
            }
        }

        private void ApplyFrame(int index)
        {
            if (_frames == null || index < 0 || index >= _frames.Length)
                return;

            EnsureMeshAndMaterial();
            var entry = _frames[index];
            if (entry == null || entry.vertices == null || entry.uvs == null || entry.indices == null)
            {
                Debug.LogWarning("[DicedSpriteAnimationPlayer] incomplete frame entry at " + index);
                return;
            }

            int vertCount = entry.vertices.Length;
            if (entry.uvs.Length != vertCount)
            {
                Debug.LogWarning("[DicedSpriteAnimationPlayer] vertices/uvs length mismatch at " + index);
                return;
            }

            var verts = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            for (int i = 0; i < vertCount; i++)
            {
                var v = entry.vertices[i];
                verts[i] = new Vector3(v != null ? v.x : 0f, v != null ? v.y : 0f, 0f);
                var uv = entry.uvs[i];
                float u = uv != null ? uv.u : 0f;
                float vv = uv != null ? uv.v : 0f;
                if (flipUvV)
                    vv = 1f - vv;
                uvs[i] = new Vector2(u, vv);
            }

            _mesh.Clear();
            _mesh.vertices = verts;
            _mesh.uv = uvs;
            _mesh.triangles = entry.indices;
            _mesh.RecalculateBounds();

            int atlasIdx = entry.atlas;
            if (atlases != null && atlasIdx >= 0 && atlasIdx < atlases.Length)
            {
                if (atlasIdx != _atlasIndex || _material.mainTexture != atlases[atlasIdx])
                {
                    _material.mainTexture = atlases[atlasIdx];
                    _atlasIndex = atlasIdx;
                }
            }
            else if (atlases == null || atlases.Length == 0)
            {
                Debug.LogWarning("[DicedSpriteAnimationPlayer] atlases not assigned");
            }
            else
            {
                Debug.LogWarning("[DicedSpriteAnimationPlayer] atlas index out of range: " + atlasIdx);
            }

            _frameIndex = index;
        }
    }
}
