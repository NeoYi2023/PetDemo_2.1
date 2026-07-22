using System;
using System.Collections.Generic;
using PetDemo.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Battle
{
    /// <summary>
    /// SPEC §12.12.9：老虎机 Reel 匹配高亮辅助。
    /// 负责高亮掩码判定与 MatchHighlight 叠层懒创建。
    /// </summary>
    public static class SlotReelMatchHighlightFx
    {
        public const string OverlayNodeName = "MatchHighlight";
        /// <summary>MatchHighlight 叠层 RGB（#FFE800）。</summary>
        public static readonly Color OverlayTintRgb = new Color(1f, 232f / 255f, 0f, 1f);

        public static bool[] ComputeHighlightMask(
            AttrEnhanceConfig[] results,
            bool[] stopped,
            bool isGrid3x3,
            int[][] gridLines)
        {
            int count = results != null ? results.Length : 0;
            var mask = new bool[count];
            if (count == 0 || stopped == null || stopped.Length < count)
                return mask;

            if (isGrid3x3)
            {
                ComputeGridMask(results, stopped, gridLines, mask);
            }
            else
            {
                ComputeLinearMask(results, stopped, mask);
            }

            return mask;
        }

        public static Image[] EnsureOverlays(Image[] reelIcons)
        {
            if (reelIcons == null)
                return Array.Empty<Image>();

            var overlays = new Image[reelIcons.Length];
            for (int i = 0; i < reelIcons.Length; i++)
            {
                overlays[i] = EnsureOverlay(reelIcons[i]);
            }
            return overlays;
        }

        public static void ApplyStaticVisual(Image icon, Image overlay, bool highlighted, Color baseColor)
        {
            if (icon != null)
                icon.color = baseColor;
            if (overlay != null)
            {
                overlay.color = GetOverlayColor(0f);
                overlay.enabled = highlighted;
                overlay.gameObject.SetActive(highlighted);
            }
        }

        public static Color GetOverlayColor(float alpha)
        {
            var color = OverlayTintRgb;
            color.a = alpha;
            return color;
        }

        private static void ComputeLinearMask(
            AttrEnhanceConfig[] results,
            bool[] stopped,
            bool[] mask)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < results.Length; i++)
            {
                if (!stopped[i])
                    continue;

                string attrId = GetValidAttrId(results[i]);
                if (string.IsNullOrEmpty(attrId))
                    continue;

                counts[attrId] = counts.TryGetValue(attrId, out int prev) ? prev + 1 : 1;
            }

            for (int i = 0; i < results.Length; i++)
            {
                if (!stopped[i])
                    continue;

                string attrId = GetValidAttrId(results[i]);
                if (string.IsNullOrEmpty(attrId))
                    continue;

                if (counts.TryGetValue(attrId, out int count) && count >= 2)
                    mask[i] = true;
            }
        }

        private static void ComputeGridMask(
            AttrEnhanceConfig[] results,
            bool[] stopped,
            int[][] gridLines,
            bool[] mask)
        {
            if (gridLines == null)
                return;

            for (int lineIdx = 0; lineIdx < gridLines.Length; lineIdx++)
            {
                var line = gridLines[lineIdx];
                if (line == null || line.Length < 3)
                    continue;

                int aIdx = line[0];
                int bIdx = line[1];
                int cIdx = line[2];
                if (!IsValidIndex(results, stopped, aIdx)
                    || !IsValidIndex(results, stopped, bIdx)
                    || !IsValidIndex(results, stopped, cIdx))
                    continue;

                if (!stopped[aIdx] || !stopped[bIdx] || !stopped[cIdx])
                    continue;

                string a = GetValidAttrId(results[aIdx]);
                string b = GetValidAttrId(results[bIdx]);
                string c = GetValidAttrId(results[cIdx]);
                if (string.IsNullOrEmpty(a)
                    || !string.Equals(a, b, StringComparison.Ordinal)
                    || !string.Equals(b, c, StringComparison.Ordinal))
                    continue;

                mask[aIdx] = true;
                mask[bIdx] = true;
                mask[cIdx] = true;
            }
        }

        private static bool IsValidIndex(AttrEnhanceConfig[] results, bool[] stopped, int index)
        {
            return results != null
                && stopped != null
                && index >= 0
                && index < results.Length
                && index < stopped.Length;
        }

        private static string GetValidAttrId(AttrEnhanceConfig cfg)
        {
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.attrId))
                return null;
            return cfg.attrId.Trim();
        }

        private static Image EnsureOverlay(Image icon)
        {
            if (icon == null)
                return null;

            var parent = icon.rectTransform;
            if (parent == null)
                return null;

            var existing = parent.Find(OverlayNodeName);
            if (existing != null)
            {
                var existingImage = existing.GetComponent<Image>();
                if (existingImage != null)
                {
                    existingImage.raycastTarget = false;
                    existingImage.color = GetOverlayColor(0f);
                    existingImage.enabled = false;
                    existingImage.gameObject.SetActive(false);
                    return existingImage;
                }
            }

            var go = new GameObject(OverlayNodeName, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetAsLastSibling();

            var overlay = go.AddComponent<Image>();
            overlay.color = GetOverlayColor(0f);
            overlay.raycastTarget = false;
            overlay.enabled = false;
            overlay.gameObject.SetActive(false);
            return overlay;
        }
    }
}
