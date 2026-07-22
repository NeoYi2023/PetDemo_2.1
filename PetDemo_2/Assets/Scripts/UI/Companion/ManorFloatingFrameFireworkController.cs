// SPEC §9.8.19.2.3 / §9.8.19.7（v3.264）：FloatingFrame_1 Panel 门控持续烟花。
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Companion
{
    [DisallowMultipleComponent]
    public sealed class ManorFloatingFrameFireworkController : MonoBehaviour
    {
        public const string ResourceFolder = "SpecialEffects/yanhua";
        public const string FxLayerName = "FireworkFxLayer";
        public const float SpawnIntervalSeconds = 0.2f;
        public const float FrameIntervalSeconds = 0.1f;
        public const float CenterOffsetY = 450f;
        public const float SpawnRadius = 200f;
        public const float DisplayScale = 4f;
        public const int ExpectedVariantCount = 6;

        private static readonly Regex FrameNameRegex = new Regex(
            @"^yanhua(\d+)__(\d+)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly List<Sprite[]> sequences = new List<Sprite[]>(ExpectedVariantCount);
        private readonly List<ActiveFirework> active = new List<ActiveFirework>(16);
        private readonly Stack<Image> pool = new Stack<Image>(16);

        private RectTransform playerRt;
        private RectTransform worldContentRt;
        private RectTransform fxLayerRt;
        private ManorFloatingFrameMarker targetMarker;
        private bool initialized;
        private bool spawningEnabled;
        private float nextSpawnTime;
        private int fireworkSerial;

        public void Initialize(
            RectTransform player,
            RectTransform worldContent,
            ManorFloatingFrameMarker marker,
            RectTransform fxLayer)
        {
            UnbindMarker();

            playerRt = player;
            worldContentRt = worldContent;
            fxLayerRt = fxLayer;
            targetMarker = marker;
            initialized = playerRt != null && worldContentRt != null && fxLayerRt != null && targetMarker != null;

            LoadSequencesIfNeeded();

            if (targetMarker != null)
            {
                targetMarker.VisibilityChanged += OnMarkerVisibilityChanged;
                if (targetMarker.IsPanelVisible)
                    BeginSpawning();
                else
                    StopSpawning();
            }
        }

        private void Update()
        {
            if (!initialized)
                return;

            float now = Time.unscaledTime;
            UpdateActiveFireworks(now);

            if (!spawningEnabled)
                return;
            if (sequences.Count == 0)
                return;
            if (now < nextSpawnTime)
                return;

            SpawnOne(now);
            nextSpawnTime = now + SpawnIntervalSeconds;
        }

        private void OnDisable()
        {
            StopSpawning();
            ClearActiveVisuals();
        }

        private void OnDestroy()
        {
            UnbindMarker();
            ClearActiveVisuals();
        }

        private void OnMarkerVisibilityChanged(bool visible)
        {
            if (visible)
                BeginSpawning();
            else
                StopSpawning();
        }

        private void BeginSpawning()
        {
            if (!initialized || sequences.Count == 0)
                return;

            spawningEnabled = true;
            float now = Time.unscaledTime;
            SpawnOne(now);
            nextSpawnTime = now + SpawnIntervalSeconds;
        }

        private void StopSpawning()
        {
            spawningEnabled = false;
        }

        private void SpawnOne(float now)
        {
            if (playerRt == null || worldContentRt == null || fxLayerRt == null)
                return;
            if (sequences.Count == 0)
                return;

            int variantIndex = UnityEngine.Random.Range(0, sequences.Count);
            Sprite[] frames = sequences[variantIndex];
            if (frames == null || frames.Length == 0)
                return;

            Vector2 playerPos = GuildSceneGeometry.PointInContentSpace(playerRt, worldContentRt);
            Vector2 center = playerPos + new Vector2(0f, CenterOffsetY);
            Vector2 spawnPos = center + RandomInDisk(SpawnRadius);

            Image image = RentImage();
            ApplyFrame(image, frames[0], spawnPos);
            image.gameObject.SetActive(true);

            active.Add(new ActiveFirework
            {
                Image = image,
                Frames = frames,
                FrameIndex = 0,
                NextFrameTime = now + FrameIntervalSeconds
            });
        }

        private void UpdateActiveFireworks(float now)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                ActiveFirework fw = active[i];
                if (fw.Image == null)
                {
                    active.RemoveAt(i);
                    continue;
                }

                if (now < fw.NextFrameTime)
                    continue;

                int nextIndex = fw.FrameIndex + 1;
                if (nextIndex >= fw.Frames.Length)
                {
                    RecycleImage(fw.Image);
                    active.RemoveAt(i);
                    continue;
                }

                fw.FrameIndex = nextIndex;
                ApplyFrame(fw.Image, fw.Frames[nextIndex], fw.Image.rectTransform.anchoredPosition);
                fw.NextFrameTime = now + FrameIntervalSeconds;
                active[i] = fw;
            }
        }

        private void ClearActiveVisuals()
        {
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].Image != null)
                    RecycleImage(active[i].Image);
            }
            active.Clear();
        }

        private void LoadSequencesIfNeeded()
        {
            if (sequences.Count > 0)
                return;

            var buckets = new Dictionary<int, List<KeyValuePair<int, Sprite>>>(ExpectedVariantCount);
            Sprite[] loaded = Resources.LoadAll<Sprite>(ResourceFolder);
            if (loaded == null || loaded.Length == 0)
            {
                UnityEngine.Debug.LogWarning(
                    "[ManorFloatingFrameFireworkController] 未找到烟花序列 Resources/" + ResourceFolder);
                return;
            }

            for (int i = 0; i < loaded.Length; i++)
            {
                Sprite sprite = loaded[i];
                if (sprite == null || string.IsNullOrEmpty(sprite.name))
                    continue;

                Match match = FrameNameRegex.Match(sprite.name);
                if (!match.Success)
                    continue;

                int variant;
                int frame;
                if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out variant)
                    || !int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out frame))
                {
                    continue;
                }

                List<KeyValuePair<int, Sprite>> list;
                if (!buckets.TryGetValue(variant, out list))
                {
                    list = new List<KeyValuePair<int, Sprite>>(16);
                    buckets[variant] = list;
                }
                list.Add(new KeyValuePair<int, Sprite>(frame, sprite));
            }

            var variantIds = new List<int>(buckets.Keys);
            variantIds.Sort();
            for (int i = 0; i < variantIds.Count; i++)
            {
                int variantId = variantIds[i];
                List<KeyValuePair<int, Sprite>> list = buckets[variantId];
                list.Sort((a, b) => a.Key.CompareTo(b.Key));
                var frames = new Sprite[list.Count];
                for (int f = 0; f < list.Count; f++)
                    frames[f] = list[f].Value;
                if (frames.Length == 0)
                {
                    UnityEngine.Debug.LogWarning(
                        "[ManorFloatingFrameFireworkController] yanhua" + variantId + " 无有效帧，已跳过。");
                    continue;
                }
                sequences.Add(frames);
            }

            if (sequences.Count == 0)
            {
                UnityEngine.Debug.LogWarning(
                    "[ManorFloatingFrameFireworkController] 烟花分组为空（期望 yanhua1~yanhua6）。");
            }
            else if (sequences.Count < ExpectedVariantCount)
            {
                UnityEngine.Debug.LogWarning(
                    "[ManorFloatingFrameFireworkController] 仅加载到 "
                    + sequences.Count + " 组烟花（期望 " + ExpectedVariantCount + "）。");
            }
        }

        private Image RentImage()
        {
            while (pool.Count > 0)
            {
                Image pooled = pool.Pop();
                if (pooled != null)
                    return pooled;
            }

            fireworkSerial++;
            var go = new GameObject("Firework_" + fireworkSerial, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(fxLayerRt, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = new Vector3(DisplayScale, DisplayScale, 1f);
            rt.anchoredPosition = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.color = Color.white;
            go.SetActive(false);
            return image;
        }

        private void RecycleImage(Image image)
        {
            if (image == null)
                return;
            image.sprite = null;
            image.gameObject.SetActive(false);
            pool.Push(image);
        }

        private static void ApplyFrame(Image image, Sprite sprite, Vector2 anchoredPosition)
        {
            if (image == null)
                return;
            var rt = image.rectTransform;
            rt.anchoredPosition = anchoredPosition;
            rt.localScale = new Vector3(DisplayScale, DisplayScale, 1f);
            image.sprite = sprite;
            // 原图像素尺寸；相对原图放大 2 倍（最终 3 倍）靠 localScale。
            if (sprite != null)
                rt.sizeDelta = sprite.rect.size;
        }

        private static Vector2 RandomInDisk(float radius)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float distance = radius * Mathf.Sqrt(UnityEngine.Random.Range(0f, 1f));
            return new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
        }

        private void UnbindMarker()
        {
            if (targetMarker != null)
                targetMarker.VisibilityChanged -= OnMarkerVisibilityChanged;
            targetMarker = null;
        }

        private struct ActiveFirework
        {
            public Image Image;
            public Sprite[] Frames;
            public int FrameIndex;
            public float NextFrameTime;
        }
    }
}
