// SPEC §13.2：好友列表弹窗 — 头像/名称/亲密度/在线，点击行展开「去找Ta / 去Ta家 / 发消息」。
// 数据复用 §9.14.2 FriendCatalog（经 IPlantingService.GetFriends()）与 §9.14.3 SortForDisplay 排序。
using System;
using System.Collections.Generic;
using PetDemo.Core;
using PetDemo.Farm;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Friend
{
    [DisallowMultipleComponent]
    public sealed class FriendListPanelView : MonoBehaviour
    {
        public const string ResPanelBackground = "AirUI/HaoYouList_0";
        public const string ResRowBackground = "AirUI/HaoYouList_2";

        private const float RowHeight = 150f;
        private const float ActionRowHeight = 110f;
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color PanelFallbackColor = new Color(0.13f, 0.15f, 0.21f, 0.98f);
        private static readonly Color RowFallbackColor = new Color(0.16f, 0.18f, 0.24f, 0.95f);
        private static readonly Color ActionRowColor = new Color(0.10f, 0.12f, 0.17f, 0.95f);
        private static readonly Color ActionButtonColor = new Color(0.26f, 0.55f, 0.85f, 1f);
        private static readonly Color SummonButtonColor = new Color(0.85f, 0.45f, 0.22f, 1f);
        private static readonly Color AvatarFallbackColor = new Color(0.3f, 0.36f, 0.46f, 1f);
        private static readonly Color OnlineColor = new Color(0.35f, 0.82f, 0.4f, 1f);
        private static readonly Color OfflineColor = new Color(0.5f, 0.5f, 0.55f, 1f);

        private IPlantingService service;
        private Action<FriendProfile> onVisitHome;
        private Action<FriendProfile> onSummon;

        private RectTransform rootRt;
        private RectTransform contentRt;
        private RectTransform expandedActionRow;
        private string expandedFriendId;
        private readonly List<GameObject> rowObjects = new List<GameObject>();

        public bool IsShown => rootRt != null && rootRt.gameObject.activeSelf;

        /// <summary>SPEC §13.2：在 HUD 根下构建好友列表弹窗（默认隐藏）。</summary>
        public static FriendListPanelView BuildInto(
            RectTransform hudRoot,
            IPlantingService plantingService,
            Action<FriendProfile> onVisitHome,
            Action<FriendProfile> onSummon = null)
        {
            if (hudRoot == null || plantingService == null)
                return null;

            var rootGo = new GameObject("FriendListPanel", typeof(RectTransform));
            var root = rootGo.GetComponent<RectTransform>();
            root.SetParent(hudRoot, false);
            StretchFull(root);
            MainHudLayerRoot.ApplySortTier(root, MainUiSortTier.HudModal);

            var view = rootGo.AddComponent<FriendListPanelView>();
            view.rootRt = root;
            view.service = plantingService;
            view.onVisitHome = onVisitHome;
            view.onSummon = onSummon;
            view.BuildHierarchy(root);

            rootGo.SetActive(false);
            return view;
        }

        public void Show()
        {
            if (rootRt == null)
                return;
            rootRt.SetAsLastSibling();
            rootRt.gameObject.SetActive(true);
            RefreshList();
        }

        public void Hide()
        {
            if (rootRt != null)
                rootRt.gameObject.SetActive(false);
        }

        private void BuildHierarchy(RectTransform root)
        {
            // Dim 遮罩（点击关闭）。
            var dim = CreateChild(root, "Dim", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(dim);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = DimColor;
            dimImg.raycastTarget = true;
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(Hide);

            // 列表面板。
            var listPanel = CreateChild(root, "ListPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(840f, 1280f));
            var panelImg = listPanel.gameObject.AddComponent<Image>();
            var panelSprite = Resources.Load<Sprite>(ResPanelBackground);
            if (panelSprite != null)
            {
                panelImg.sprite = panelSprite;
                panelImg.color = Color.white;
            }
            else
            {
                panelImg.color = PanelFallbackColor;
            }
            panelImg.raycastTarget = true;

            CreateText(listPanel, "Title", "好友", new Vector2(0.5f, 1f), new Vector2(0f, -50f),
                new Vector2(700f, 80f), 48, TextAnchor.MiddleCenter);

            // ScrollView。
            var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.SetParent(listPanel, false);
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.pivot = new Vector2(0.5f, 0.5f);
            scrollRt.offsetMin = new Vector2(24f, 130f);
            scrollRt.offsetMax = new Vector2(-24f, -110f);
            scrollGo.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.6f);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.SetParent(scrollRt, false);
            StretchFull(viewportRt);
            viewportGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.SetParent(viewportRt, false);
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;

            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 12f;
            vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.childAlignment = TextAnchor.UpperCenter;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewportRt;
            scroll.content = contentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            CreateButton(listPanel, "CloseButton", "关闭",
                new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(300f, 90f),
                new Color(0.4f, 0.4f, 0.46f, 1f), 40, Hide);
        }

        private void RefreshList()
        {
            if (contentRt == null || service == null)
                return;

            expandedActionRow = null;
            expandedFriendId = null;
            for (int i = 0; i < rowObjects.Count; i++)
            {
                if (rowObjects[i] != null)
                    Destroy(rowObjects[i]);
            }
            rowObjects.Clear();

            var sorted = FriendCatalog.SortForDisplay(service.GetFriends());
            for (int i = 0; i < sorted.Count; i++)
                BuildFriendRow(sorted[i]);
        }

        private void BuildFriendRow(FriendProfile friend)
        {
            if (friend == null)
                return;

            // ---- 好友行 ----
            var row = CreateChild(contentRt, "FriendRow_" + friend.id, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, RowHeight));
            var rowImg = row.gameObject.AddComponent<Image>();
            var rowSprite = Resources.Load<Sprite>(ResRowBackground);
            if (rowSprite != null)
            {
                rowImg.sprite = rowSprite;
                rowImg.color = Color.white;
            }
            else
            {
                rowImg.color = RowFallbackColor;
            }
            var rowLe = row.gameObject.AddComponent<LayoutElement>();
            rowLe.preferredHeight = RowHeight;
            rowLe.minHeight = RowHeight;

            // 头像（左）。
            var avatar = CreateChild(row, "Avatar", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(90f, 0f), new Vector2(120f, 120f));
            var avatarImg = avatar.gameObject.AddComponent<Image>();
            avatarImg.raycastTarget = false;
            avatarImg.preserveAspect = true;
            var avatarSprite = !string.IsNullOrEmpty(friend.avatarResource)
                ? Resources.Load<Sprite>(friend.avatarResource)
                : null;
            if (avatarSprite != null)
            {
                avatarImg.sprite = avatarSprite;
                avatarImg.color = Color.white;
            }
            else
            {
                avatarImg.color = AvatarFallbackColor;
            }

            // 名称 + 亲密度。
            CreateText(row, "NameText", friend.displayName, new Vector2(0f, 1f), new Vector2(170f, -18f),
                new Vector2(420f, 60f), 42, TextAnchor.UpperLeft);
            CreateText(row, "IntimacyText", "亲密度 " + friend.intimacy, new Vector2(0f, 0f), new Vector2(170f, 18f),
                new Vector2(420f, 50f), 32, TextAnchor.LowerLeft);

            // 在线状态（右）。
            bool online = friend.online;
            var dot = CreateChild(row, "OnlineDot", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(-180f, 0f), new Vector2(28f, 28f));
            var dotImg = dot.gameObject.AddComponent<Image>();
            dotImg.color = online ? OnlineColor : OfflineColor;
            dotImg.raycastTarget = false;
            var onlineText = CreateText(row, "OnlineText", online ? "在线" : "离线",
                new Vector2(1f, 0.5f), new Vector2(-90f, 0f), new Vector2(130f, 50f), 32, TextAnchor.MiddleCenter);
            onlineText.color = online ? OnlineColor : OfflineColor;

            // ---- 展开操作行（默认隐藏，紧贴本行正下方） ----
            var actionRow = CreateChild(contentRt, "ActionRow_" + friend.id, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, ActionRowHeight));
            var actionImg = actionRow.gameObject.AddComponent<Image>();
            actionImg.color = ActionRowColor;
            var actionLe = actionRow.gameObject.AddComponent<LayoutElement>();
            actionLe.preferredHeight = ActionRowHeight;
            actionLe.minHeight = ActionRowHeight;

            var captured = friend;

            // SPEC §13.8：亲密度 ≥ 80 的好友额外显示「召唤Ta」按钮（共 4 个，等分窄排）；否则维持原 3 按钮布局。
            bool canSummon = friend.intimacy >= FriendCatalog.IntimacyThreshold;
            if (canSummon)
            {
                var btnSize = new Vector2(190f, 80f);
                CreateButton(actionRow, "GoFindButton", "去找Ta",
                    new Vector2(0.5f, 0.5f), new Vector2(-300f, 0f), btnSize, ActionButtonColor, 30,
                    () => UnityEngine.Debug.Log("[FriendListPanelView] 「去找Ta」占位按钮：" + captured.displayName));
                CreateButton(actionRow, "VisitHomeButton", "去Ta家",
                    new Vector2(0.5f, 0.5f), new Vector2(-100f, 0f), btnSize, ActionButtonColor, 30,
                    () =>
                    {
                        Hide();
                        onVisitHome?.Invoke(captured);
                    });
                CreateButton(actionRow, "MessageButton", "发消息",
                    new Vector2(0.5f, 0.5f), new Vector2(100f, 0f), btnSize, ActionButtonColor, 30,
                    () => UnityEngine.Debug.Log("[FriendListPanelView] 「发消息」占位按钮：" + captured.displayName));
                CreateButton(actionRow, "SummonButton", "召唤Ta",
                    new Vector2(0.5f, 0.5f), new Vector2(300f, 0f), btnSize, SummonButtonColor, 30,
                    () =>
                    {
                        Hide();
                        onSummon?.Invoke(captured);
                    });
            }
            else
            {
                var btnSize = new Vector2(220f, 80f);
                CreateButton(actionRow, "GoFindButton", "去找Ta",
                    new Vector2(0.5f, 0.5f), new Vector2(-250f, 0f), btnSize, ActionButtonColor, 34,
                    () => UnityEngine.Debug.Log("[FriendListPanelView] 「去找Ta」占位按钮：" + captured.displayName));
                CreateButton(actionRow, "VisitHomeButton", "去Ta家",
                    new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), btnSize, ActionButtonColor, 34,
                    () =>
                    {
                        Hide();
                        onVisitHome?.Invoke(captured);
                    });
                CreateButton(actionRow, "MessageButton", "发消息",
                    new Vector2(0.5f, 0.5f), new Vector2(250f, 0f), btnSize, ActionButtonColor, 34,
                    () => UnityEngine.Debug.Log("[FriendListPanelView] 「发消息」占位按钮：" + captured.displayName));
            }

            actionRow.gameObject.SetActive(false);

            // 行点击：展开/收起本行操作行；同一时刻至多一行展开（SPEC §13.2）。
            var rowBtn = row.gameObject.AddComponent<Button>();
            rowBtn.transition = Selectable.Transition.ColorTint;
            rowBtn.targetGraphic = rowImg;
            rowBtn.onClick.AddListener(() => ToggleActionRow(captured.id, actionRow));

            rowObjects.Add(row.gameObject);
            rowObjects.Add(actionRow.gameObject);
        }

        private void ToggleActionRow(string friendId, RectTransform actionRow)
        {
            if (actionRow == null)
                return;

            bool wasExpanded = string.Equals(expandedFriendId, friendId, StringComparison.Ordinal);
            if (expandedActionRow != null)
                expandedActionRow.gameObject.SetActive(false);
            expandedActionRow = null;
            expandedFriendId = null;

            if (wasExpanded)
                return;

            actionRow.gameObject.SetActive(true);
            expandedActionRow = actionRow;
            expandedFriendId = friendId;
        }

        // ---- 构建工具（同 §9.14 CharacterCreationScreenLayout 范式） ----

        private static RectTransform CreateChild(
            RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return rt;
        }

        private static Button CreateButton(
            RectTransform parent, string name, string label,
            Vector2 anchorPivot, Vector2 anchoredPos, Vector2 size, Color color, int fontSize,
            UnityEngine.Events.UnityAction onClick)
        {
            var rt = CreateChild(parent, name, anchorPivot, anchorPivot, anchorPivot, anchoredPos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;
            if (onClick != null)
                btn.onClick.AddListener(onClick);

            var labelRt = CreateChild(rt, "Label", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(labelRt);
            var txt = labelRt.gameObject.AddComponent<Text>();
            txt.text = label;
            txt.font = FarmGridView.LoadBuiltinFont();
            txt.fontSize = fontSize;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.raycastTarget = false;
            return btn;
        }

        private static Text CreateText(
            RectTransform parent, string name, string content,
            Vector2 anchorPivot, Vector2 anchoredPos, Vector2 size, int fontSize, TextAnchor align)
        {
            var rt = CreateChild(parent, name, anchorPivot, anchorPivot, anchorPivot, anchoredPos, size);
            var txt = rt.gameObject.AddComponent<Text>();
            txt.text = content;
            txt.font = FarmGridView.LoadBuiltinFont();
            txt.fontSize = fontSize;
            txt.alignment = align;
            txt.color = Color.white;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
