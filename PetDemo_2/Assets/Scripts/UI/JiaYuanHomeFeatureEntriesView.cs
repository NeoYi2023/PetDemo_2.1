// SPEC §9.8.11：底部导航「家园 / JiaYuan」打开时显示的订单入口 + 仓库入口，及对应弹窗。
// 自 v3.41 起，仓库按钮改为实例化 §9.8.13 「统一仓库预制体 `WarehouseHubPanel`」，不再代码搭建全屏背景。
using System;
using PetDemo.Farm;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class JiaYuanHomeFeatureEntriesView : MonoBehaviour
    {
        public const string JiaYuanNavKey = "JiaYuan";
        public const string ResOrderEntryIcon = "AirUI/DingDan";
        public const string ResOrderPanelSprite = "AirUI/DingDan_1";
        public const string ResWarehouseEntryIcon = "AirUI/CangKu";

        private const float EntryButtonSize = 105f;
        private const float OrderEntryPosX = 94f;
        private const float OrderEntryPosY = -674f;
        private const float WarehouseEntryPosX = -808f;
        private const float WarehouseEntryPosY = -674f;

        private RectTransform layerRootRt;
        private RectTransform orderModalRt;
        private RectTransform canvasRectCache;
        private BottomNavBarView bottomNav;
        private IPlantingService plantingService;

        public static JiaYuanHomeFeatureEntriesView BuildInto(
            RectTransform canvasRect,
            BottomNavBarView barView,
            IPlantingService plantingService)
        {
            if (canvasRect == null || barView == null)
                return null;

            var rootGo = new GameObject("JiaYuanHomeFeatureLayer", typeof(RectTransform));
            var root = rootGo.GetComponent<RectTransform>();
            root.SetParent(canvasRect, false);
            StretchFull(root);

            var barRt = barView.transform as RectTransform;
            if (barRt != null)
                root.SetSiblingIndex(barRt.GetSiblingIndex());

            // 左侧：订单（SPEC §9.8.11 v3.83：PosX=94, PosY=-674, 105×105）
            var orderEntryRt = CreateChildRect(root, "OrderEntryButton",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(OrderEntryPosX, OrderEntryPosY), new Vector2(EntryButtonSize, EntryButtonSize));
            orderEntryRt.pivot = new Vector2(0.5f, 0.5f);
            var orderEntryImg = orderEntryRt.gameObject.AddComponent<Image>();
            var orderEntrySprite = Resources.Load<Sprite>(ResOrderEntryIcon);
            if (orderEntrySprite != null)
            {
                orderEntryImg.sprite = orderEntrySprite;
                orderEntryImg.preserveAspect = true;
            }
            else
            {
                orderEntryImg.color = new Color(0.25f, 0.22f, 0.3f, 0.9f);
                UnityEngine.Debug.LogWarning(
                    "[JiaYuanHomeFeatureEntriesView] 缺少订单入口图 Resources/" + ResOrderEntryIcon + "。");
            }

            orderEntryImg.raycastTarget = true;
            var orderEntryBtn = orderEntryRt.gameObject.AddComponent<Button>();
            orderEntryBtn.transition = Selectable.Transition.None;
            orderEntryBtn.targetGraphic = orderEntryImg;

            // 右侧：仓库（SPEC §9.8.11 v3.83：PosX=-808, PosY=-674, 105×105）
            var whEntryRt = CreateChildRect(root, "WarehouseEntryButton",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(WarehouseEntryPosX, WarehouseEntryPosY), new Vector2(EntryButtonSize, EntryButtonSize));
            whEntryRt.pivot = new Vector2(0.5f, 0.5f);
            var whEntryImg = whEntryRt.gameObject.AddComponent<Image>();
            var whEntrySprite = Resources.Load<Sprite>(ResWarehouseEntryIcon);
            if (whEntrySprite != null)
            {
                whEntryImg.sprite = whEntrySprite;
                whEntryImg.preserveAspect = true;
            }
            else
            {
                whEntryImg.color = new Color(0.25f, 0.22f, 0.3f, 0.9f);
                UnityEngine.Debug.LogWarning(
                    "[JiaYuanHomeFeatureEntriesView] 缺少仓库入口图 Resources/" + ResWarehouseEntryIcon + "。");
            }

            whEntryImg.raycastTarget = true;
            var whEntryBtn = whEntryRt.gameObject.AddComponent<Button>();
            whEntryBtn.transition = Selectable.Transition.None;
            whEntryBtn.targetGraphic = whEntryImg;

            // 订单弹窗：遮罩 + DingDan_1 + 右上角关闭
            var orderModal = CreateChildRect(root, "OrderModal",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            StretchFull(orderModal);
            orderModal.gameObject.SetActive(false);
            MainHudLayerRoot.ApplySortTier(orderModal, MainUiSortTier.HudModal);

            var orderDimRt = CreateChildRect(orderModal, "DimCloseArea",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            StretchFull(orderDimRt);
            var orderDimImg = orderDimRt.gameObject.AddComponent<Image>();
            orderDimImg.color = new Color(0f, 0f, 0f, 0.55f);
            orderDimImg.raycastTarget = true;
            var orderDimBtn = orderDimRt.gameObject.AddComponent<Button>();
            orderDimBtn.transition = Selectable.Transition.None;
            orderDimBtn.targetGraphic = orderDimImg;

            var orderPanelRt = CreateChildRect(orderModal, "OrderPanel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(960f, 1500f));
            orderPanelRt.pivot = new Vector2(0.5f, 0.5f);
            var orderPanelImg = orderPanelRt.gameObject.AddComponent<Image>();
            var orderPanelSprite = Resources.Load<Sprite>(ResOrderPanelSprite);
            if (orderPanelSprite != null)
            {
                orderPanelImg.sprite = orderPanelSprite;
                orderPanelImg.preserveAspect = true;
                orderPanelImg.color = Color.white;
            }
            else
            {
                orderPanelImg.color = new Color(0.12f, 0.11f, 0.16f, 1f);
                UnityEngine.Debug.LogWarning(
                    "[JiaYuanHomeFeatureEntriesView] 缺少订单界面图 Resources/" + ResOrderPanelSprite + "。");
            }

            orderPanelImg.raycastTarget = true;

            var orderCloseRt = CreateChildRect(orderPanelRt, "CloseButton",
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-20f, -20f), new Vector2(72f, 72f));
            orderCloseRt.pivot = new Vector2(1f, 1f);
            var orderCloseBg = orderCloseRt.gameObject.AddComponent<Image>();
            orderCloseBg.color = new Color(0f, 0f, 0f, 0.45f);
            orderCloseBg.raycastTarget = true;
            var orderCloseBtn = orderCloseRt.gameObject.AddComponent<Button>();
            orderCloseBtn.transition = Selectable.Transition.None;
            orderCloseBtn.targetGraphic = orderCloseBg;

            var orderCloseLabelRt = CreateChildRect(orderCloseRt, "Label",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            StretchFull(orderCloseLabelRt);
            var orderCloseText = orderCloseLabelRt.gameObject.AddComponent<Text>();
            orderCloseText.text = "×";
            orderCloseText.font = FarmGridView.LoadBuiltinFont();
            orderCloseText.fontSize = 48;
            orderCloseText.alignment = TextAnchor.MiddleCenter;
            orderCloseText.color = Color.white;
            orderCloseText.raycastTarget = false;

            void CloseOrderModal() => orderModal.gameObject.SetActive(false);
            orderDimBtn.onClick.AddListener(CloseOrderModal);
            orderCloseBtn.onClick.AddListener(CloseOrderModal);
            orderEntryBtn.onClick.AddListener(() =>
            {
                orderModal.SetAsLastSibling();
                orderModal.gameObject.SetActive(true);
            });

            root.gameObject.SetActive(false);

            var view = rootGo.AddComponent<JiaYuanHomeFeatureEntriesView>();
            view.layerRootRt = root;
            view.orderModalRt = orderModal;
            view.canvasRectCache = canvasRect;
            view.bottomNav = barView;
            view.plantingService = plantingService;

            // 仓库按钮：自 v3.41 起改为打开 §9.8.13 统一仓库预制体（WarehouseHubPanelView）。
            whEntryBtn.onClick.AddListener(view.OpenUnifiedWarehouse);

            barView.OnOpenChanged += view.OnBottomNavOpenChanged;
            view.OnBottomNavOpenChanged(barView.OpenIndex, barView.OpenKey);
            return view;
        }

        private void OpenUnifiedWarehouse()
        {
            if (canvasRectCache == null)
            {
                UnityEngine.Debug.LogWarning("[JiaYuanHomeFeatureEntriesView] 仓库按钮：canvasRect 为空");
                return;
            }
            if (plantingService == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[JiaYuanHomeFeatureEntriesView] 仓库按钮：plantingService 为空，无法打开统一仓库。" +
                    "请检查 AirMainMenuRuntimeBuilder.BuildInto 是否已透传 IPlantingService。");
                return;
            }
            var hub = WarehouseHubPanelView.GetOrCreate(canvasRectCache);
            if (hub == null)
                return;
            hub.Show(plantingService);
        }

        private void OnBottomNavOpenChanged(int index, string key)
        {
            bool jiaYuan = !string.IsNullOrEmpty(key) &&
                           string.Equals(key, JiaYuanNavKey, StringComparison.Ordinal);
            if (layerRootRt != null)
                layerRootRt.gameObject.SetActive(jiaYuan);

            if (!jiaYuan)
            {
                if (orderModalRt != null)
                    orderModalRt.gameObject.SetActive(false);
                // 自 v3.41 起，仓库面板由 WarehouseHubPanelView 托管，
                // 切离 JiaYuan 时主动收起，避免残留遮挡（与原 v3.35 语义一致）。
                if (canvasRectCache != null)
                {
                    var existing = canvasRectCache.Find(WarehouseHubPanelView.PanelObjectName);
                    if (existing != null)
                    {
                        var hub = existing.GetComponent<WarehouseHubPanelView>();
                        if (hub != null && hub.IsShown)
                            hub.Hide();
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (bottomNav != null)
                bottomNav.OnOpenChanged -= OnBottomNavOpenChanged;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static RectTransform CreateChildRect(
            RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }
    }
}
