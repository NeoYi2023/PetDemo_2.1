// FarmGridView：在主画布上构建 5×4 = 20 田网格（SPEC §9.1）。
// 自 v0.9 起优先使用 FarmGridRoot/TileSlot 预制体实例化，便于在编辑器直接调整布局。
// 自 v1.0 起支持「手动布局模式」：当 FarmGridRoot 预制体下已含 ≥ FarmTileCount 个 TileSlotView 子节点时，
// 直接按 sibling order 绑定，不再实例化；不足时先绑定已有手动格，仅按 5×4 公式补齐缺失 orderIndex。
using System.Collections.Generic;
using PetDemo.Core;
using PetDemo.Farm;
using PetDemo.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Farm
{
    public class FarmGridView : MonoBehaviour
    {
        public const string DefaultGridPrefabResPath = "Prefabs/Farm/FarmGridRoot";
        public const string DefaultTilePrefabResPath = "Prefabs/Farm/TileSlot";
        public const string ZhongTianAnchorName = "ZhongTian";
        public const int Cols = 4;
        public const int Rows = 5;

        private static readonly Color SoilColor = new Color(0xA0 / 255f, 0x76 / 255f, 0x3A / 255f, 1f);
        private static readonly Color FocusColor = new Color(1.00f, 0.82f, 0.31f, 1f); // #FFD24F

        private IPlantingService service;
        private readonly Dictionary<string, TileSlotView> slotByTileId = new Dictionary<string, TileSlotView>(20);
        private RectTransform zhongTianAnchorRt;
        public static FarmGridView Instance { get; private set; }

        public RectTransform ZhongTianAnchor => zhongTianAnchorRt;

        public static FarmGridView BuildInto(
            RectTransform canvasRect,
            IPlantingService svc,
            RectTransform farmGridRootPrefab = null,
            RectTransform tileSlotPrefab = null,
            RectTransform worldParent = null)
        {
            var parent = worldParent != null ? worldParent : canvasRect;
            var resolvedGridPrefab = farmGridRootPrefab != null
                ? farmGridRootPrefab
                : Resources.Load<RectTransform>(DefaultGridPrefabResPath);
            var resolvedTilePrefab = tileSlotPrefab != null
                ? tileSlotPrefab
                : Resources.Load<RectTransform>(DefaultTilePrefabResPath);

            RectTransform root;
            if (resolvedGridPrefab != null)
            {
                root = Object.Instantiate(resolvedGridPrefab, parent, false);
                root.name = "FarmGridRoot";
            }
            else
            {
                root = BuildFallbackGridRoot(parent);
                UnityEngine.Debug.LogWarning("FarmGridView: 未找到 FarmGridRoot 预制体，回退为运行时代码布局。");
            }

            var view = root.GetComponent<FarmGridView>();
            if (view == null)
                view = root.gameObject.AddComponent<FarmGridView>();
            view.service = svc;
            Instance = view;
            view.BuildSlots(root, resolvedTilePrefab);
            view.SubscribeEvents();
            // 首次刷新推迟到 OnEnable：农田挂在未激活的 JiaYuanWorld 下时，避免 Spine 在 Canvas 未就绪时初始化。
            if (view.isActiveAndEnabled)
                view.RefreshAllSlots();
            return view;
        }

        private void OnEnable()
        {
            if (service == null || slotByTileId.Count == 0)
                return;
            RefreshAllSlots();
        }

        private static RectTransform BuildFallbackGridRoot(RectTransform canvasRect)
        {
            var go = new GameObject("FarmGridRoot");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(canvasRect, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 60f);
            rt.sizeDelta = new Vector2(952f, 1020f);
            return rt;
        }

        private void BuildSlots(RectTransform root, RectTransform tilePrefab)
        {
            int tileCount = service.FarmTileCount;
            PruneExcessTileSlotViews(root, tileCount);
            var manualSlots = CollectExistingSlotViews(root);
            if (manualSlots.Count >= tileCount)
            {
                BindManualSlots(manualSlots, tileCount);
            }
            else if (manualSlots.Count > 0)
            {
                BindManualSlots(manualSlots, manualSlots.Count);
                BuildAutoSlots(root, tilePrefab, tileCount, manualSlots.Count);
            }
            else
            {
                BuildAutoSlots(root, tilePrefab, tileCount);
            }

            PruneExcessTileSlotViews(root, tileCount);
            ResolveZhongTianAnchor(root);
        }

        private static void PruneExcessTileSlotViews(RectTransform root, int tileCount)
        {
            if (root == null || tileCount < 1)
                return;

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (child.GetComponent<TileSlotView>() == null)
                    continue;
                if (!TryParseTileSlotOrderIndex(child.name, out int orderIndex) || orderIndex <= tileCount)
                    continue;
                Object.Destroy(child.gameObject);
            }
        }

        private static bool TryParseTileSlotOrderIndex(string slotName, out int orderIndex)
        {
            orderIndex = 0;
            const string prefix = "TileSlot_";
            if (string.IsNullOrEmpty(slotName) || !slotName.StartsWith(prefix))
                return false;
            return int.TryParse(slotName.Substring(prefix.Length), out orderIndex);
        }

        private void ResolveZhongTianAnchor(RectTransform root)
        {
            zhongTianAnchorRt = null;
            if (root == null)
                return;

            var found = root.Find(ZhongTianAnchorName);
            if (found is RectTransform rt)
            {
                zhongTianAnchorRt = rt;
                return;
            }

            UnityEngine.Debug.LogWarning(
                "FarmGridView: 未找到锚点 " + ZhongTianAnchorName +
                "，播种镜头将回退为跟随村民。请在 FarmGridRoot 预制体中添加该子节点。");
        }

        private static List<TileSlotView> CollectExistingSlotViews(RectTransform root)
        {
            var result = new List<TileSlotView>(20);
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                var view = child.GetComponent<TileSlotView>();
                if (view != null)
                    result.Add(view);
            }
            return result;
        }

        private void BindManualSlots(List<TileSlotView> manualSlots, int tileCount)
        {
            for (int i = 0; i < tileCount; i++)
            {
                int orderIndex = i + 1;
                var tile = service.GetTileByOrder(orderIndex);
                if (tile == null)
                    continue;

                var slotView = manualSlots[i];
                slotView.gameObject.name = "TileSlot_" + orderIndex.ToString("D2");
                slotView.SetTileId(tile.tileId);
                slotView.SetOrderLabel(orderIndex);
                slotByTileId[tile.tileId] = slotView;
            }
        }

        private void BuildAutoSlots(
            RectTransform root,
            RectTransform tilePrefab,
            int tileCount,
            int skipThroughOrderIndex = 0)
        {
            var layout = root.GetComponent<GridLayoutGroup>();
            bool useLayoutGroup = layout != null;
            float tileWidth = tilePrefab != null ? tilePrefab.sizeDelta.x : 220f;
            float tileHeight = tilePrefab != null ? tilePrefab.sizeDelta.y : 160f;
            float colGap = useLayoutGroup ? layout.spacing.x : 24f;
            float rowGap = useLayoutGroup ? layout.spacing.y : 12f;
            float totalW = Cols * tileWidth + (Cols - 1) * colGap;
            float totalH = Rows * tileHeight + (Rows - 1) * rowGap;
            float startX = -totalW * 0.5f + tileWidth * 0.5f;
            float startY = totalH * 0.5f - tileHeight * 0.5f;

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    int orderIndex = r * Cols + c + 1;
                    if (orderIndex <= skipThroughOrderIndex || orderIndex > tileCount)
                        continue;

                    float x = startX + c * (tileWidth + colGap);
                    float y = startY - r * (tileHeight + rowGap);

                    var tile = service.GetTileByOrder(orderIndex);
                    if (tile == null)
                        continue;

                    var slotRt = InstantiateSlot(root, tilePrefab, orderIndex);
                    if (!useLayoutGroup)
                        slotRt.anchoredPosition = new Vector2(x, y);

                    var slotView = slotRt.GetComponent<TileSlotView>();
                    if (slotView == null)
                        slotView = slotRt.gameObject.AddComponent<TileSlotView>();
                    slotView.SetTileId(tile.tileId);
                    slotView.SetOrderLabel(orderIndex);
                    slotByTileId[tile.tileId] = slotView;
                }
            }
        }

        private RectTransform InstantiateSlot(RectTransform root, RectTransform tilePrefab, int orderIndex)
        {
            if (tilePrefab != null)
            {
                var slotRt = Object.Instantiate(tilePrefab, root, false);
                slotRt.name = "TileSlot_" + orderIndex.ToString("D2");
                return slotRt;
            }

            UnityEngine.Debug.LogWarning("FarmGridView: 未找到 TileSlot 预制体，回退为运行时格子。");
            return BuildFallbackSlot(root, orderIndex);
        }

        private RectTransform BuildFallbackSlot(RectTransform root, int orderIndex)
        {
            var slotGo = new GameObject("TileSlot_" + orderIndex.ToString("D2"));
            var slotRt = slotGo.AddComponent<RectTransform>();
            slotRt.SetParent(root, false);
            slotRt.anchorMin = new Vector2(0.5f, 0.5f);
            slotRt.anchorMax = new Vector2(0.5f, 0.5f);
            slotRt.pivot = new Vector2(0.5f, 0.5f);
            slotRt.sizeDelta = new Vector2(220f, 160f);

            var soil = AddChildImage(slotRt, "SoilImage", Vector2.zero, new Vector2(220f, 160f));
            soil.color = SoilColor;
            soil.raycastTarget = false;

            var plant = AddChildImage(slotRt, "PlantImage", Vector2.zero, new Vector2(200f, 140f));
            plant.preserveAspect = true;
            plant.raycastTarget = false;
            plant.enabled = false;

            var water = AddChildImage(slotRt, "WaterBadge", new Vector2(-78f, -64f), new Vector2(20f, 20f));
            var fert = AddChildImage(slotRt, "FertilizerBadge", new Vector2(-26f, -64f), new Vector2(20f, 20f));
            var pest = AddChildImage(slotRt, "PestBadge", new Vector2(26f, -64f), new Vector2(20f, 20f));
            var harvest = AddChildImage(slotRt, "HarvestBadge", new Vector2(78f, -64f), new Vector2(20f, 20f));
            foreach (var b in new[] { water, fert, pest, harvest })
            {
                b.raycastTarget = false;
                b.enabled = false;
            }

            var focus = AddChildImage(slotRt, "FocusRing", Vector2.zero, new Vector2(220f, 160f));
            focus.color = new Color(FocusColor.r, FocusColor.g, FocusColor.b, 0.35f);
            focus.raycastTarget = false;
            focus.enabled = false;

            var focusArrow = AddChildText(slotRt, "FocusArrow",
                new Vector2(0f, 160f * 0.5f + 14f),
                new Vector2(28f, 28f),
                "v");
            focusArrow.fontSize = 24;
            focusArrow.color = FocusColor;
            focusArrow.gameObject.SetActive(false);

            AddChildText(slotRt, "OrderLabel",
                new Vector2(-220f * 0.5f + 18f, 160f * 0.5f - 14f),
                new Vector2(48f, 24f),
                orderIndex.ToString());
            return slotRt;
        }

        private void SubscribeEvents()
        {
            service.OnTileFlagsChanged += HandleTileChanged;
            service.OnWaterPendingChanged += HandleTileChanged;
            service.OnAppearanceNodeChanged += HandleAppearanceChanged;
            service.OnPlantStateChanged += HandlePlantStateChanged;
            service.OnFocusChanged += HandleFocusChanged;
            service.OnPlantTileInteracted += HandlePlantTileInteracted;
            JiaYuanWindEffectController.OnWindStateChanged += HandleWindStateChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            JiaYuanWindEffectController.OnWindStateChanged -= HandleWindStateChanged;
            if (service == null)
                return;
            service.OnTileFlagsChanged -= HandleTileChanged;
            service.OnWaterPendingChanged -= HandleTileChanged;
            service.OnAppearanceNodeChanged -= HandleAppearanceChanged;
            service.OnPlantStateChanged -= HandlePlantStateChanged;
            service.OnFocusChanged -= HandleFocusChanged;
            service.OnPlantTileInteracted -= HandlePlantTileInteracted;
        }

        private void HandleTileChanged(string tileId)
        {
            if (slotByTileId.TryGetValue(tileId, out var slot))
                slot.Refresh(service);
        }

        private void HandleAppearanceChanged(string plantInstanceId, int node)
        {
            // 根据 plantInstanceId 找到所在 tile
            for (int i = 1; i <= service.FarmTileCount; i++)
            {
                var t = service.GetTileByOrder(i);
                if (t != null && t.plantInstanceId == plantInstanceId)
                {
                    HandleTileChanged(t.tileId);
                    return;
                }
            }
        }

        private void HandlePlantTileInteracted(string tileId)
        {
            if (slotByTileId.TryGetValue(tileId, out var slot))
                slot.NotifyPlantTileInteracted();
        }

        private void HandleWindStateChanged(bool active)
        {
            foreach (var pair in slotByTileId)
            {
                if (pair.Value != null)
                    pair.Value.SetWindWork2Active(active);
            }
        }

        private void HandlePlantStateChanged(string plantInstanceId, PlantState newState)
        {
            for (int i = 1; i <= service.FarmTileCount; i++)
            {
                var t = service.GetTileByOrder(i);
                if (t != null && t.plantInstanceId == plantInstanceId)
                {
                    HandleTileChanged(t.tileId);
                    return;
                }
            }
        }

        private void HandleFocusChanged(string tileId)
        {
            foreach (var kv in slotByTileId)
                kv.Value.SetFocus(kv.Key == tileId);
        }

        public void RefreshAllSlots()
        {
            if (service == null)
                return;
            foreach (var kv in slotByTileId)
                kv.Value.Refresh(service);
        }

        public bool TryGetTileScreenPosition(string tileId, out Vector2 screenPos)
        {
            screenPos = Vector2.zero;
            if (string.IsNullOrEmpty(tileId))
                return false;
            if (!slotByTileId.TryGetValue(tileId, out var slot) || slot == null)
                return false;
            var rt = slot.transform as RectTransform;
            if (rt == null)
                return false;
            var canvas = rt.GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            screenPos = RectTransformUtility.WorldToScreenPoint(cam, rt.position);
            return true;
        }

        /// <summary>
        /// 将指定田格中心换算到 <paramref name="space"/> 的局部 anchored 坐标（供村民/精灵寻路）。
        /// </summary>
        public bool TryGetTileLocalPositionIn(RectTransform space, string tileId, out Vector2 localPos)
        {
            localPos = Vector2.zero;
            if (space == null || string.IsNullOrEmpty(tileId))
                return false;
            if (!slotByTileId.TryGetValue(tileId, out var slot) || slot == null)
                return false;
            var tileRt = slot.transform as RectTransform;
            if (tileRt == null)
                return false;

            var canvas = space.GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            var screen = RectTransformUtility.WorldToScreenPoint(cam, tileRt.position);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(space, screen, cam, out localPos);
        }

        /// <summary>SPEC §9.1.2：对指定田格播放收获摆动并在结束后 TryHarvestTile。</summary>
        public bool TryRequestHarvestWithSwing(string tileId, IPlantingService svc)
        {
            if (string.IsNullOrEmpty(tileId) || svc == null)
                return false;
            if (!slotByTileId.TryGetValue(tileId, out var slot) || slot == null)
                return false;
            return slot.RequestHarvestWithSwing(svc);
        }

        // SPEC §4.1.10 / §5.2：MutationOverlayView 在 4 田中心点放置 4 格植物图标，
        // 以 FarmGridRoot 为父节点时需要拿到每个 TileSlot 的 RectTransform 计算 anchoredPosition。
        public RectTransform GetTileSlotRect(string tileId)
        {
            if (string.IsNullOrEmpty(tileId))
                return null;
            if (!slotByTileId.TryGetValue(tileId, out var slot) || slot == null)
                return null;
            return slot.transform as RectTransform;
        }

        private static Image AddChildImage(RectTransform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return go.AddComponent<Image>();
        }

        private static Text AddChildText(RectTransform parent, string name, Vector2 pos, Vector2 size, string content)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var text = go.AddComponent<Text>();
            text.text = content;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 1f, 1f, 0.85f);
            text.font = LoadBuiltinFont();
            text.fontSize = 16;
            text.raycastTarget = false;
            return text;
        }

        // Unity 2021.3 LTS 内置字体名为 "Arial.ttf"；Unity 2022+ 改名 "LegacyRuntime.ttf"。
        // 加 fallback 同时兼容两个 LTS。
        public static Font LoadBuiltinFont()
        {
            var font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }
}
