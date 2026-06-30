// SPEC §9.1.4（v3.111）：家园世界 Y 轴深度排序 — 主角/精灵/植物/田格 UI 统一按局部 Y 决定 Canvas 绘制顺序。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    /// <summary>§9.1.4 次排序键：同 Y 时保持格内/实体类型先后。</summary>
    public static class JiaYuanWorldDepthLayer
    {
        public const int Soil = 0;
        public const int WaterBadge = 5;
        public const int Plant = 10;
        public const int NeedWater = 20;
        public const int PendingWater = 30;
        public const int StatusBadges = 40;
        public const int PestMoleIcon = 50;
        public const int FocusRing = 60;
        public const int Villager = 70;
        public const int Pet = 80;
        public const int MutationIcon = 90;
    }

    public struct DepthSortEntry
    {
        public RectTransform Visual;
        public float SortY;
        public int LayerOffset;
        public bool Active;
        /// <summary>主角/精灵为 true：每帧从 <see cref="Visual"/> 中心点重算 sortY。</summary>
        public bool UseVisualCenterY;
    }

    [DisallowMultipleComponent]
    public sealed class JiaYuanWorldDepthSorter : MonoBehaviour
    {
        public const int BaseOrder = MainUiSortTier.WorldMax;
        public const int SortPrecision = 2;

        public static JiaYuanWorldDepthSorter Instance { get; private set; }

        private RectTransform worldContent;
        private readonly Dictionary<int, DepthSortEntry> entriesByVisualId = new Dictionary<int, DepthSortEntry>(128);
        private readonly Dictionary<int, Canvas> canvasByVisualId = new Dictionary<int, Canvas>(128);
        private bool dirty = true;

        public void Initialize(RectTransform worldContentRt)
        {
            worldContent = worldContentRt;
            Instance = this;
            dirty = true;
        }

        public float ResolveSortYFromWorldPoint(Vector3 worldPosition)
        {
            if (worldContent == null)
                return 0f;
            return worldContent.InverseTransformPoint(worldPosition).y;
        }

        public void RegisterOrUpdate(in DepthSortEntry entry)
        {
            if (entry.Visual == null)
                return;

            int id = entry.Visual.GetInstanceID();
            entriesByVisualId[id] = entry;
            dirty = true;
        }

        public void Unregister(RectTransform visual)
        {
            if (visual == null)
                return;

            int id = visual.GetInstanceID();
            entriesByVisualId.Remove(id);
            if (canvasByVisualId.TryGetValue(id, out var canvas) && canvas != null)
                MainHudLayerRoot.DestroyNestedCanvasComponents(canvas.gameObject);
            canvasByVisualId.Remove(id);
            dirty = true;
        }

        public void MarkDirty()
        {
            dirty = true;
        }

        private void LateUpdate()
        {
            if (!dirty)
                return;
            RefreshAll();
            dirty = false;
        }

        private void RefreshAll()
        {
            if (worldContent == null)
                return;

            var staleIds = new List<int>();

            foreach (var kv in entriesByVisualId)
            {
                var entry = kv.Value;
                if (!IsEntryRenderable(entry))
                {
                    staleIds.Add(kv.Key);
                    continue;
                }

                float sortY = entry.UseVisualCenterY
                    ? ResolveSortYFromWorldPoint(entry.Visual.position)
                    : entry.SortY;

                int sortingOrder = Mathf.Clamp(
                    BaseOrder - Mathf.RoundToInt(sortY * SortPrecision) + entry.LayerOffset,
                    0, MainUiSortTier.WorldMax);
                ApplySortingOrder(entry.Visual, kv.Key, sortingOrder);
            }

            for (int i = 0; i < staleIds.Count; i++)
            {
                int id = staleIds[i];
                entriesByVisualId.Remove(id);
                if (canvasByVisualId.TryGetValue(id, out var canvas) && canvas != null)
                    MainHudLayerRoot.DestroyNestedCanvasComponents(canvas.gameObject);
                canvasByVisualId.Remove(id);
            }
        }

        private static bool IsEntryRenderable(in DepthSortEntry entry)
        {
            if (!entry.Active || entry.Visual == null)
                return false;
            if (!entry.Visual.gameObject.activeInHierarchy)
                return false;
            return true;
        }

        private void ApplySortingOrder(RectTransform visual, int visualId, int sortingOrder)
        {
            if (!canvasByVisualId.TryGetValue(visualId, out var canvas) || canvas == null)
            {
                canvas = visual.GetComponent<Canvas>();
                if (canvas == null)
                    canvas = visual.gameObject.AddComponent<Canvas>();
                canvas.overrideSorting = true;
                canvasByVisualId[visualId] = canvas;
            }

            canvas.sortingOrder = sortingOrder;
            MainHudLayerRoot.EnsureGraphicRaycaster(visual.gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            foreach (var kv in canvasByVisualId)
            {
                if (kv.Value != null)
                    MainHudLayerRoot.DestroyNestedCanvasComponents(kv.Value.gameObject);
            }

            entriesByVisualId.Clear();
            canvasByVisualId.Clear();
        }
    }
}
