// SPEC_VideoMattingAtlas.md v1.4 / SPEC_FarmBattleDemo.md §9.14.11 v3.282 —
// Bake CLI diced atlas frames to Unity Sprites, then play on uGUI Image (same path as LangRen_DZ).
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.VideoMatting
{
    /// <summary>
    /// Loads VideoMatting CLI <c>sprites.json</c> + <c>atlas_*.png</c>, rasterizes each frame
    /// into a composed <see cref="Sprite"/>, and plays them on an <see cref="Image"/>.
    /// Avoids Overlay Canvas mesh path (which incorrectly showed the raw atlas sheet).
    /// </summary>
    public static class DicedSpriteAtlasSequencePlayer
    {
        public const float DefaultFps = 15f;
        public const float DefaultPixelsPerUnit = 100f;

        private static readonly Dictionary<string, Sprite[]> Cache =
            new Dictionary<string, Sprite[]>(StringComparer.Ordinal);

        public static void ClearCache()
        {
            foreach (var pair in Cache)
            {
                var sprites = pair.Value;
                if (sprites == null)
                    continue;
                for (int i = 0; i < sprites.Length; i++)
                {
                    if (sprites[i] == null)
                        continue;
                    var tex = sprites[i].texture;
                    UnityEngine.Object.Destroy(sprites[i]);
                    if (tex != null)
                        UnityEngine.Object.Destroy(tex);
                }
            }

            Cache.Clear();
        }

        public static Sprite[] GetOrBake(string resourcesFolder)
        {
            if (string.IsNullOrWhiteSpace(resourcesFolder))
                return Array.Empty<Sprite>();

            string folder = resourcesFolder.Trim().TrimEnd('/');
            if (Cache.TryGetValue(folder, out var cached) && cached != null && cached.Length > 0)
                return cached;

            var baked = BakeFolder(folder);
            Cache[folder] = baked;
            return baked;
        }

        public static IEnumerator PlayOnce(Image target, string resourcesFolder, float fps = DefaultFps, Action onComplete = null)
        {
            if (target == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            var frames = GetOrBake(resourcesFolder);
            if (frames == null || frames.Length == 0)
            {
                Debug.LogWarning("[DicedSpriteAtlasSequencePlayer] no baked frames for Resources/" + resourcesFolder);
                onComplete?.Invoke();
                yield break;
            }

            target.useSpriteMesh = false; // baked sprites use FullRect mesh; useSpriteMesh would build bad geometry
            target.preserveAspect = true;
            target.color = Color.white;
            target.enabled = true;

            float frameInterval = 1f / (fps > 0f ? fps : DefaultFps);
            for (int i = 0; i < frames.Length; i++)
            {
                if (target == null)
                    yield break;

                var frame = frames[i];
                if (frame != null)
                    target.sprite = frame;

                float elapsed = 0f;
                while (elapsed < frameInterval)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            onComplete?.Invoke();
        }

        private static Sprite[] BakeFolder(string folder)
        {
            var json = Resources.Load<TextAsset>(folder + "/sprites");
            if (json == null)
            {
                Debug.LogWarning("[DicedSpriteAtlasSequencePlayer] missing Resources/" + folder + "/sprites");
                return Array.Empty<Sprite>();
            }

            var frames = DicedSpriteJson.ParseAndSort(json.text);
            if (frames == null || frames.Length == 0)
            {
                Debug.LogWarning("[DicedSpriteAtlasSequencePlayer] empty sprites.json at Resources/" + folder);
                return Array.Empty<Sprite>();
            }

            var atlases = LoadAtlases(folder);
            if (atlases.Length == 0)
            {
                Debug.LogWarning("[DicedSpriteAtlasSequencePlayer] no atlas_* at Resources/" + folder);
                return Array.Empty<Sprite>();
            }

            EnsureReadable(atlases);

            var result = new Sprite[frames.Length];
            for (int i = 0; i < frames.Length; i++)
                result[i] = BakeFrame(frames[i], atlases, DefaultPixelsPerUnit);

            int ok = 0;
            for (int i = 0; i < result.Length; i++)
            {
                if (result[i] != null)
                    ok++;
            }

            if (ok == 0)
            {
                Debug.LogWarning("[DicedSpriteAtlasSequencePlayer] bake produced 0 sprites for " + folder);
                return Array.Empty<Sprite>();
            }

            Debug.Log("[DicedSpriteAtlasSequencePlayer] baked " + ok + "/" + result.Length + " frames from Resources/" + folder);
            return result;
        }

        private static Texture2D[] LoadAtlases(string folder)
        {
            var loaded = Resources.LoadAll<Texture2D>(folder);
            var list = new List<Texture2D>();
            if (loaded == null)
                return Array.Empty<Texture2D>();

            for (int i = 0; i < loaded.Length; i++)
            {
                var tex = loaded[i];
                if (tex == null || string.IsNullOrEmpty(tex.name))
                    continue;
                if (!IsAtlasTextureName(tex.name))
                    continue;
                list.Add(tex);
            }

            list.Sort((a, b) => ParseAtlasIndex(a.name).CompareTo(ParseAtlasIndex(b.name)));
            return list.ToArray();
        }

        // SpriteDicing CLI: uv origin = atlas top-left in PNG space (y down): pilRow = v * atlasH.
        // Unity Texture2D.GetPixel is y-up, so sample at (atlasH-1-pilRow). fv unflipped.
        // dst: write rows as-is (row = oy), then flip the WHOLE frame once at the end —
        // per-block row flip (row = texH-1-oy) splits every dice block vertically (fragments).
        // Verified objectively against frames_sprite ground truth: IoU 0.978 (SPEC v1.8).
        private static Sprite BakeFrame(
            DicedSpriteEntry entry,
            Texture2D[] atlases,
            float ppu)
        {
            if (entry == null || entry.vertices == null || entry.uvs == null || entry.indices == null)
                return null;

            int vertCount = entry.vertices.Length;
            if (vertCount == 0 || entry.uvs.Length != vertCount || entry.indices.Length < 3)
                return null;

            int atlasIdx = entry.atlas;
            if (atlases == null || atlasIdx < 0 || atlasIdx >= atlases.Length || atlases[atlasIdx] == null)
            {
                Debug.LogWarning("[DicedSpriteAtlasSequencePlayer] atlas index out of range: " + atlasIdx);
                return null;
            }

            var atlas = atlases[atlasIdx];

            float x0, y0, width, height;
            if (entry.rect != null && entry.rect.width > 0f && entry.rect.height > 0f)
            {
                x0 = entry.rect.x;
                y0 = entry.rect.y;
                width = entry.rect.width;
                height = entry.rect.height;
            }
            else
            {
                float minX = float.MaxValue, minY = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue;
                for (int i = 0; i < vertCount; i++)
                {
                    var v = entry.vertices[i];
                    float x = v != null ? v.x : 0f;
                    float y = v != null ? v.y : 0f;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }

                x0 = minX;
                y0 = minY;
                width = Mathf.Max(0.01f, maxX - minX);
                height = Mathf.Max(0.01f, maxY - minY);
            }

            int texW = Mathf.Max(1, Mathf.CeilToInt(width * ppu));
            int texH = Mathf.Max(1, Mathf.CeilToInt(height * ppu));

            var outPixels = new Color32[texW * texH];
            for (int v0 = 0; v0 + 3 < vertCount; v0 += 4)
            {
                var bl = entry.vertices[v0];
                var tr = entry.vertices[v0 + 2];
                var uvBL = entry.uvs[v0];
                var uvTR = entry.uvs[v0 + 2];
                if (bl == null || tr == null || uvBL == null || uvTR == null)
                    continue;

                int dstX0 = Mathf.RoundToInt((bl.x - x0) * ppu);
                int dstY0 = Mathf.RoundToInt((bl.y - y0) * ppu);
                int dstX1 = Mathf.RoundToInt((tr.x - x0) * ppu);
                int dstY1 = Mathf.RoundToInt((tr.y - y0) * ppu);
                if (dstX1 <= dstX0 || dstY1 <= dstY0)
                    continue;

                float su0 = uvBL.u, su1 = uvTR.u;
                float sv0 = uvBL.v, sv1 = uvTR.v;

                for (int y = dstY0; y < dstY1; y++)
                {
                    int oy = y;
                    if (oy < 0 || oy >= texH)
                        continue;
                    float fv = (y - dstY0) / (float)(dstY1 - dstY0);
                    // uv origin top-left in PNG space (y down); GetPixel is y-up → flip row index.
                    float sv = sv0 + fv * (sv1 - sv0);
                    int srcYPil = Mathf.Clamp((int)(sv * atlas.height), 0, atlas.height - 1);
                    int srcY = atlas.height - 1 - srcYPil;
                    int row = oy * texW; // no per-block flip; flip whole frame at end
                    for (int x = dstX0; x < dstX1; x++)
                    {
                        int ox = x;
                        if (ox < 0 || ox >= texW)
                            continue;
                        float fu = (x - dstX0) / (float)(dstX1 - dstX0);
                        float su = su0 + fu * (su1 - su0);
                        int srcX = Mathf.Clamp((int)(su * atlas.width), 0, atlas.width - 1);
                        var c = atlas.GetPixel(srcX, srcY);
                        if (c.a > 0f)
                            outPixels[row + ox] = c;
                    }
                }
            }

            // Flip the whole frame vertically once (character was upside-down otherwise).
            for (int y = 0; y < texH / 2; y++)
            {
                int top = y * texW;
                int bottom = (texH - 1 - y) * texW;
                for (int x = 0; x < texW; x++)
                {
                    (outPixels[top + x], outPixels[bottom + x]) = (outPixels[bottom + x], outPixels[top + x]);
                }
            }

            var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
            tex.name = entry.id ?? "diced_frame";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixels32(outPixels);
            tex.Apply(false, false);

            return Sprite.Create(
                tex,
                new Rect(0f, 0f, texW, texH),
                new Vector2(0.5f, 0.5f),
                ppu,
                0,
                SpriteMeshType.FullRect);
        }

        private static void EnsureReadable(Texture2D[] atlases)
        {
            if (atlases == null)
                return;

            bool any = false;
            for (int i = 0; i < atlases.Length; i++)
            {
                if (atlases[i] != null && !atlases[i].isReadable)
                    any = true;
            }

            if (any)
            {
                Debug.LogWarning(
                    "[DicedSpriteAtlasSequencePlayer] atlas 需设为 Read/Write Enabled，否则烘焙为空。" +
                    "请把 Resources/VideoMatting/*/atlas_*.png 的 isReadable 打开。");
            }
        }

        private static bool IsAtlasTextureName(string name)
        {
            if (string.IsNullOrEmpty(name) || !name.StartsWith("atlas_", StringComparison.OrdinalIgnoreCase))
                return false;

            string suffix = name.Substring("atlas_".Length);
            if (suffix.Length == 0)
                return false;
            for (int i = 0; i < suffix.Length; i++)
            {
                if (!char.IsDigit(suffix[i]))
                    return false;
            }

            return true;
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
