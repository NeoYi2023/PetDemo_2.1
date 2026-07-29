// SPEC §9.14.11 v3.273：SpriteDicing diced 序列 15fps 播放。
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

        public static void ClearCache()
        {
            cachedFrames = null;
            cachedSourcePath = null;
        }

        public static string CachedSourcePath => cachedSourcePath;

        public static IEnumerator PlayOnce(Image target, Action onComplete = null)
        {
            if (target == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            var frames = GetOrLoadFrames();
            if (frames == null || frames.Length == 0)
            {
                onComplete?.Invoke();
                yield break;
            }

            target.useSpriteMesh = true;
            target.preserveAspect = true;
            target.color = Color.white;
            target.enabled = true;

            float frameInterval = 1f / Fps;
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
