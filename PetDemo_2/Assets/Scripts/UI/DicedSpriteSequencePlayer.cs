// SPEC §9.14.11 v3.273 / v3.283：SpriteDicing diced 序列 15fps 播放（支持任意 Resources 路径）。
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    /// <summary>
    /// 按帧名排序加载 diced（或回退源帧）Sprite，并以固定 fps 切换 uGUI Image。
    /// </summary>
    public static class DicedSpriteSequencePlayer
    {
        public const string DicedSpritesResourcePath = "SpriteDicing/LangRen_DZ _1/diced_sprites";
        public const string FallbackFramesResourcePath = "SpriteDicing/LangRen_DZ _1/frames_sprite";
        public const float Fps = 15f;

        private static readonly Dictionary<string, Sprite[]> PathCache =
            new Dictionary<string, Sprite[]>(StringComparer.Ordinal);

        private static Sprite[] cachedFrames;
        private static string cachedSourcePath;

        public static Sprite[] GetOrLoadFrames()
        {
            if (cachedFrames != null && cachedFrames.Length > 0)
                return cachedFrames;

            cachedFrames = LoadSortedSprites(DicedSpritesResourcePath);
            if (cachedFrames != null && cachedFrames.Length > 0)
            {
                cachedSourcePath = DicedSpritesResourcePath;
                return cachedFrames;
            }

            cachedFrames = LoadSortedSprites(FallbackFramesResourcePath);
            if (cachedFrames != null && cachedFrames.Length > 0)
            {
                cachedSourcePath = FallbackFramesResourcePath;
                UnityEngine.Debug.LogWarning(
                    "[DicedSpriteSequencePlayer] 未找到 diced_sprites，回退加载 frames_sprite。" +
                    "请在编辑器执行 Tools/PetDemo/Build LangRen DZ Diced Atlas。");
                return cachedFrames;
            }

            UnityEngine.Debug.LogWarning(
                "[DicedSpriteSequencePlayer] 未找到任何序列帧 Resources/" + DicedSpritesResourcePath +
                " 或 " + FallbackFramesResourcePath);
            cachedFrames = Array.Empty<Sprite>();
            cachedSourcePath = null;
            return cachedFrames;
        }

        /// <summary>
        /// 按 Resources 相对路径加载并缓存。优先 <paramref name="resourcesPath"/>；
        /// 若为空且路径以 /diced_sprites 结尾，则回退同级 frames_sprite。
        /// </summary>
        public static Sprite[] GetOrLoadFrames(string resourcesPath)
        {
            if (string.IsNullOrWhiteSpace(resourcesPath))
                return GetOrLoadFrames();

            string path = resourcesPath.Trim().TrimEnd('/');
            if (PathCache.TryGetValue(path, out var cached) && cached != null && cached.Length > 0)
                return cached;

            var frames = LoadSortedSprites(path);
            if (frames != null && frames.Length > 0)
            {
                PathCache[path] = frames;
                return frames;
            }

            string fallback = DeriveFramesSpriteFallback(path);
            if (!string.IsNullOrEmpty(fallback))
            {
                frames = LoadSortedSprites(fallback);
                if (frames != null && frames.Length > 0)
                {
                    UnityEngine.Debug.LogWarning(
                        "[DicedSpriteSequencePlayer] 未找到 " + path + "，回退 " + fallback +
                        "。请执行 Tools/PetDemo/Build ZJDH Unity Package Atlases 或对应 Diced Atlas 菜单。");
                    PathCache[path] = frames;
                    return frames;
                }
            }

            UnityEngine.Debug.LogWarning(
                "[DicedSpriteSequencePlayer] 未找到任何序列帧 Resources/" + path);
            PathCache[path] = Array.Empty<Sprite>();
            return PathCache[path];
        }

        public static void ClearCache()
        {
            cachedFrames = null;
            cachedSourcePath = null;
            PathCache.Clear();
        }

        public static string CachedSourcePath => cachedSourcePath;

        public static IEnumerator PlayOnce(Image target, Action onComplete = null)
        {
            yield return PlayOnce(target, null, Fps, onComplete);
        }

        public static IEnumerator PlayOnce(Image target, string resourcesPath, float fps = Fps, Action onComplete = null)
        {
            if (target == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            var frames = string.IsNullOrWhiteSpace(resourcesPath)
                ? GetOrLoadFrames()
                : GetOrLoadFrames(resourcesPath);
            if (frames == null || frames.Length == 0)
            {
                onComplete?.Invoke();
                yield break;
            }

            target.useSpriteMesh = true;
            target.preserveAspect = true;
            target.color = Color.white;
            target.enabled = true;

            float interval = 1f / (fps > 0f ? fps : Fps);
            for (int i = 0; i < frames.Length; i++)
            {
                if (target == null)
                    yield break;

                var frame = frames[i];
                if (frame != null)
                    target.sprite = frame;

                float elapsed = 0f;
                while (elapsed < interval)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            onComplete?.Invoke();
        }

        private static string DeriveFramesSpriteFallback(string dicedOrAnyPath)
        {
            const string suffix = "/diced_sprites";
            if (dicedOrAnyPath.EndsWith(suffix, StringComparison.Ordinal))
                return dicedOrAnyPath.Substring(0, dicedOrAnyPath.Length - suffix.Length) + "/frames_sprite";
            return null;
        }

        private static Sprite[] LoadSortedSprites(string resourcesPath)
        {
            var loaded = Resources.LoadAll<Sprite>(resourcesPath);
            if (loaded == null || loaded.Length == 0)
                return Array.Empty<Sprite>();

            var list = new List<Sprite>(loaded.Length);
            for (int i = 0; i < loaded.Length; i++)
            {
                if (loaded[i] != null)
                    list.Add(loaded[i]);
            }

            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list.ToArray();
        }
    }
}
