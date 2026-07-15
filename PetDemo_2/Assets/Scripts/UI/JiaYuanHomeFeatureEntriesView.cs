// SPEC §9.8.11：底部导航「家园 / JiaYuan」功能层（订单弹窗残留关闭）。
// 自 v3.253 起：主 HUD 流程下 JiaYuanHomeFeatureLayer 永久隐藏；仓库入口迁至 HomeTabPanel。
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

        private const float EntryButtonSize = 105f;
        private const float OrderEntryPosX = 94f;
        private const float OrderEntryPosY = -674f;

        private RectTransform layerRootRt;
        private RectTransform orderModalRt;
        private RectTransform canvasRectCache;
        private BottomNavBarView bottomNav;

        public static JiaYuanHomeFeatureEntriesView BuildInto(
            RectTransform canvasRect,
            BottomNavBarView barView,
            IPlantingService plantingService)
        {
            if (canvasRect == null || barView == null)
                return null;

            // plantingService 保留入参签名（与 BuildBottomNavBar 透传兼容）；v3.253 后本层不再打开仓库。
            _ = plantingService;

            var rootGo = new GameObject("JiaYuanHomeFeatureLayer", typeof(RectTransform));
            var root = rootGo.GetComponent<RectTransform>();
            root.SetParent(canvasRect, false);
            StretchFull(root);

            var barRt = barView.transform as RectTransform;
            if (barRt != null)
                root.SetSiblingIndex(barRt.GetSiblingIndex());

            // 订单入口保留节点结构（层永久隐藏，本期不可见）。
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

            // 订单弹窗：遮罩 + DingDan_1 + 右上角关闭（层隐藏时不可达；保留结构供未来迁入口）。
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

            // SPEC §9.8.11 v3.253：永久隐藏本层（主 HUD 不再显示订单/仓库浮层）。
            root.gameObject.SetActive(false);

            var view = rootGo.AddComponent<JiaYuanHomeFeatureEntriesView>();
            view.layerRootRt = root;
            view.orderModalRt = orderModal;
            view.canvasRectCache = canvasRect;
            view.bottomNav = barView;

            barView.OnOpenChanged += view.OnBottomNavOpenChanged;
            view.OnBottomNavOpenChanged(barView.OpenIndex, barView.OpenKey);
            return view;
        }

        private void OnBottomNavOpenChanged(int index, string key)
        {
            _ = index;
            _ = key;

            // SPEC §9.8.11 v3.253：主 HUD 流程下层永不激活。
            if (layerRootRt != null)
                layerRootRt.gameObject.SetActive(false);

            if (orderModalRt != null)
                orderModalRt.gameObject.SetActive(false);

            // 切离时主动收起统一仓库，避免残留遮挡（与原 v3.35 语义一致）。
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
