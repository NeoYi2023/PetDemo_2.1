// SPEC §9.14.11：家园页签面板层级构建（运行时回退与预制体生成器共用，保证结构一致）。
using PetDemo.Core;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    public static class HomeTabPanelLayout
    {
        public static readonly string[] InfoTabLabels = { "角色6项属性", "当前" };

        public const string SolidBackgroundResource = "";
        public const string DecorBackgroundResource = "AirUI/common_bg_11";
        public const string LevelBadgeResource = "AirUI/Lv_bg_003";
        public const string ExpTrackResource = "AirUI/Lv_bg_004";
        public const string ExpFillResource = "AirUI/Lv_bg_005";
        public const string ExpFrameResource = "AirUI/Lv_bg_006";
        public const string SpeechBubbleResource = "AirUI/DialogBox_1";
        // SPEC §9.14.11 v3.190：右上功能按钮
        public const string RankingButtonResource = "AirUI/ZJM_PaiHangbang_1";
        public const string DailyTaskButtonResource = "AirUI/ZJM_RenWu_1";

        private static readonly Color SolidBackgroundColor = new Color(0.10f, 0.12f, 0.18f, 1f);
        private static readonly Color TabFallbackColor = new Color(0.3f, 0.28f, 0.24f, 1f);
        private static readonly Color TabActiveColor = new Color(0.26f, 0.55f, 0.85f, 1f);
        private static readonly Color TextColor = Color.black;
        private static readonly Color TopRightButtonFallbackColor = new Color(0.35f, 0.32f, 0.4f, 0.9f);

        private const float CharacterZoneTopAnchorY = CharacterCreationScreenLayout.ContentRegionTopAnchorY;
        private const float LevelExpRowHeight = 110f;
        private const float LevelBadgeWidth = 120f;
        private const float LevelBadgeHeight = 110f;
        private const float ExpBarHeight = 56f;
        private const float InfoTabBarHeight = 72f;
        private static readonly Vector2 RoleMountSize = new Vector2(720f, 1000f);

        // SPEC §9.14.11 v3.190：右上竖排功能按钮
        public static readonly Vector2 TopRightButtonSize = new Vector2(120f, 120f);
        public const float TopRightMargin = 24f;
        public const float TopRightButtonGap = 16f;

        // SPEC §9.14.11 v3.193 / v3.199：根级关闭按钮（与创角 §9.14.6 范式一致，左上角）
        public static readonly Vector2 ScreenCloseButtonSize = new Vector2(72f, 72f);
        public static readonly Vector2 ScreenCloseButtonAnchoredPos = new Vector2(20f, -20f);
        private static readonly Color ScreenCloseButtonColor = new Color(0.25f, 0.22f, 0.32f, 0.95f);
        private const int ScreenCloseLabelFontSize = 44;

        // SPEC §9.14.11 v3.204：左上体力 HUD（关闭钮右侧）
        public static readonly Vector2 TopLeftStaminaHudAnchoredPos = new Vector2(108f, -20f);
        public static readonly Vector2 StaminaBarSlotSize = new Vector2(275f, 116f);
        // SPEC §9.14.11 v3.208：体力条右侧「增加经验」按钮
        public static readonly Vector2 AddExpButtonSize = new Vector2(140f, 64f);
        public const float AddExpButtonGap = 16f;
        public static readonly Vector2 TopLeftStaminaHudSize = new Vector2(
            StaminaBarSlotSize.x + AddExpButtonGap + AddExpButtonSize.x, StaminaBarSlotSize.y);
        private static readonly Color AddExpButtonColor = new Color(0f, 0f, 0f, 1f);
        private const int AddExpButtonFontSize = 28;

        // SPEC §9.14.11 v3.188：双列三行属性网格
        public const int GrowthAttrCount = 6;
        public const int AttrGridColumns = 2;
        public const int AttrGridRows = 3;
        private static readonly Vector2 AttrItemSize = new Vector2(420f, 120f);
        private static readonly Vector2 AttrIconSize = new Vector2(96f, 96f);
        private static readonly Vector2 AttrValueSize = new Vector2(160f, 80f);
        private const float AttrGridCellGapX = 40f;
        private const float AttrGridCellGapY = 28f;
        private const int AttrValueFontSize = 40;

        // SPEC §9.14.11 v3.187：角色左上方固定气泡
        public static readonly Vector2 SpeechBubbleAnchoredPos = new Vector2(-220f, 280f);
        public static readonly Vector2 SpeechBubbleSize = new Vector2(420f, 160f);
        private const int SpeechBubbleFontSize = 30;
        private static readonly Vector2 SpeechBubbleTextPadding = new Vector2(36f, 28f);

        /// <summary>构建完整家园页签面板层级并挂载 <see cref="HomeTabPanelView"/>。</summary>
        public static GameObject BuildRuntime(RectTransform parent)
        {
            var root = new GameObject(HomeTabPanelView.PanelObjectName, typeof(RectTransform));
            var rootRt = root.GetComponent<RectTransform>();
            if (parent != null)
                rootRt.SetParent(parent, false);
            StretchFull(rootRt);
            root.AddComponent<HomeTabPanelView>();

            var solidBg = CreateChild(rootRt, "SolidBackground", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(solidBg);
            var solidImg = solidBg.gameObject.AddComponent<Image>();
            solidImg.color = SolidBackgroundColor;
            solidImg.raycastTarget = true;

            float levelExpMinY = CharacterZoneTopAnchorY - LevelExpRowHeight / 1920f;
            var characterZone = CreateChild(rootRt, "CharacterZone",
                new Vector2(0f, CharacterZoneTopAnchorY), new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(characterZone);

            var decorBg = CreateChild(characterZone, "DecorBackground",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 700f));
            ApplySpriteOrColor(decorBg.gameObject.AddComponent<Image>(), DecorBackgroundResource,
                new Color(0.16f, 0.18f, 0.24f, 0.6f));

            CreateChild(characterZone, "RoleMount",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), Vector2.zero, RoleMountSize);

            BuildSpeechBubble(characterZone);

            BuildLevelExpRow(rootRt,
                new Vector2(0f, levelExpMinY), new Vector2(1f, CharacterZoneTopAnchorY));

            var infoSection = CreateChild(rootRt, "InfoSection",
                Vector2.zero, new Vector2(1f, levelExpMinY),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(infoSection);

            var infoTabBar = CreateChild(infoSection, "InfoTabBar",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -InfoTabBarHeight * 0.5f),
                new Vector2(0f, InfoTabBarHeight));
            infoTabBar.anchorMin = new Vector2(0.04f, 1f);
            infoTabBar.anchorMax = new Vector2(0.96f, 1f);
            infoTabBar.pivot = new Vector2(0.5f, 1f);
            infoTabBar.anchoredPosition = new Vector2(0f, 0f);
            infoTabBar.sizeDelta = new Vector2(0f, InfoTabBarHeight);

            for (int i = 0; i < InfoTabLabels.Length; i++)
            {
                float minX = (float)i / InfoTabLabels.Length + 0.01f;
                float maxX = (float)(i + 1) / InfoTabLabels.Length - 0.01f;
                string tabName = i == 0 ? "HexAttrsTab" : "CurrentTab";
                var tab = CreateChild(infoTabBar, tabName,
                    new Vector2(minX, 0f), new Vector2(maxX, 1f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                StretchFull(tab);
                var tabImg = tab.gameObject.AddComponent<Image>();
                tabImg.color = i == 0 ? TabActiveColor : TabFallbackColor;
                tabImg.raycastTarget = true;
                var tabBtn = tab.gameObject.AddComponent<Button>();
                tabBtn.transition = Selectable.Transition.None;
                tabBtn.targetGraphic = tabImg;
                CreateText(tab, "Label", InfoTabLabels[i],
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 60f), 32, TextAnchor.MiddleCenter);
            }

            var infoContent = CreateChild(infoSection, "InfoContent",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            infoContent.offsetMin = new Vector2(16f, 16f);
            infoContent.offsetMax = new Vector2(-16f, -InfoTabBarHeight - 8f);

            var hexAttrsPage = CreateChild(infoContent, "HexAttrsPage", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(hexAttrsPage);
            BuildAttrGrid(hexAttrsPage);

            var currentPage = CreateChild(infoContent, "CurrentPlaceholderPage", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(currentPage);
            currentPage.gameObject.SetActive(false);
            CreateText(currentPage, "PlaceholderText", "功能开发中",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 80f), 36, TextAnchor.MiddleCenter);

            BuildTopRightActions(rootRt);
            BuildScreenCloseButton(rootRt);
            BuildTopLeftStaminaHud(rootRt);

            root.SetActive(false);
            return root;
        }

        /// <summary>SPEC §9.14.11 v3.193 / v3.199：根级左上关闭按钮（缺则补建；已存在则校正位置/尺寸）。</summary>
        public static RectTransform BuildScreenCloseButton(RectTransform rootRt)
        {
            if (rootRt == null)
                return null;

            var existing = rootRt.Find("ScreenCloseButton") as RectTransform;
            if (existing != null)
            {
                ApplyScreenCloseButtonLayout(existing);
                return existing;
            }

            var closeRt = CreateChild(rootRt, "ScreenCloseButton",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), ScreenCloseButtonAnchoredPos, ScreenCloseButtonSize);
            var closeImg = closeRt.gameObject.AddComponent<Image>();
            closeImg.color = ScreenCloseButtonColor;
            closeImg.raycastTarget = true;
            var closeBtn = closeRt.gameObject.AddComponent<Button>();
            closeBtn.transition = Selectable.Transition.None;
            closeBtn.targetGraphic = closeImg;

            var labelRt = CreateChild(closeRt, "Label", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(labelRt);
            var closeLabel = labelRt.gameObject.AddComponent<Text>();
            closeLabel.text = "×";
            closeLabel.font = FarmGridView.LoadBuiltinFont();
            closeLabel.fontSize = ScreenCloseLabelFontSize;
            closeLabel.alignment = TextAnchor.MiddleCenter;
            closeLabel.color = TextColor;
            closeLabel.raycastTarget = false;

            return closeRt;
        }

        private static void ApplyScreenCloseButtonLayout(RectTransform closeRt)
        {
            if (closeRt == null)
                return;
            closeRt.anchorMin = new Vector2(0f, 1f);
            closeRt.anchorMax = new Vector2(0f, 1f);
            closeRt.pivot = new Vector2(0f, 1f);
            closeRt.anchoredPosition = ScreenCloseButtonAnchoredPos;
            closeRt.sizeDelta = ScreenCloseButtonSize;
            closeRt.SetAsLastSibling();
        }

        /// <summary>
        /// SPEC §9.14.11 / §9.14.13：构建与家园页完全相同的 LevelExpRow（徽章+四层经验条）。
        /// 返回 LevelExpRow 根节点。
        /// </summary>
        public static RectTransform BuildLevelExpRow(
            RectTransform parent,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            if (parent == null)
                return null;

            var existing = parent.Find("LevelExpRow") as RectTransform;
            if (existing != null)
            {
                EnsureLevelExpRowContents(existing);
                return existing;
            }

            var levelExpRow = CreateChild(parent, "LevelExpRow",
                anchorMin, anchorMax,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            levelExpRow.offsetMin = Vector2.zero;
            levelExpRow.offsetMax = Vector2.zero;
            EnsureLevelExpRowContents(levelExpRow);
            return levelExpRow;
        }

        /// <summary>在已有 LevelExpRow 下补齐子节点（兼容旧 prefab / 升级面板复用）。</summary>
        public static void EnsureLevelExpRowContents(RectTransform levelExpRow)
        {
            if (levelExpRow == null)
                return;

            var levelBadge = levelExpRow.Find("LevelBadge") as RectTransform;
            if (levelBadge == null)
            {
                levelBadge = CreateChild(levelExpRow, "LevelBadge",
                    new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(LevelBadgeWidth * 0.5f + 24f, 0f),
                    new Vector2(LevelBadgeWidth, LevelBadgeHeight));
                ApplySpriteOrColor(levelBadge.gameObject.AddComponent<Image>(), LevelBadgeResource,
                    new Color(0.2f, 0.22f, 0.3f, 1f));
                CreateText(levelBadge, "LevelText", "1",
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 80f), 48, TextAnchor.MiddleCenter);
            }
            else if (levelBadge.Find("LevelText") == null)
            {
                CreateText(levelBadge, "LevelText", "1",
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 80f), 48, TextAnchor.MiddleCenter);
            }

            var expBarRoot = levelExpRow.Find("ExpBarRoot") as RectTransform;
            if (expBarRoot == null)
            {
                expBarRoot = CreateChild(levelExpRow, "ExpBarRoot",
                    new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2((LevelBadgeWidth + 48f) * 0.5f, 0f),
                    new Vector2(-LevelBadgeWidth - 72f, ExpBarHeight));
                expBarRoot.anchorMin = new Vector2(0f, 0.5f);
                expBarRoot.anchorMax = new Vector2(1f, 0.5f);
                expBarRoot.pivot = new Vector2(0.5f, 0.5f);
                expBarRoot.offsetMin = new Vector2(LevelBadgeWidth + 48f, -ExpBarHeight * 0.5f);
                expBarRoot.offsetMax = new Vector2(-24f, ExpBarHeight * 0.5f);

                var expTrack = CreateChild(expBarRoot, "ExpTrack", Vector2.zero, Vector2.one,
                    new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                StretchFull(expTrack);
                ApplySpriteOrColor(expTrack.gameObject.AddComponent<Image>(), ExpTrackResource,
                    new Color(0.15f, 0.17f, 0.22f, 1f));

                var expFill = CreateChild(expBarRoot, "ExpFill",
                    new Vector2(0f, 0f), new Vector2(0f, 1f),
                    new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
                expFill.sizeDelta = Vector2.zero;
                ApplySpriteOrColor(expFill.gameObject.AddComponent<Image>(), ExpFillResource,
                    new Color(0.35f, 0.75f, 0.45f, 1f));

                var expFrame = CreateChild(expBarRoot, "ExpFrame", Vector2.zero, Vector2.one,
                    new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                StretchFull(expFrame);
                var frameImg = expFrame.gameObject.AddComponent<Image>();
                ApplySpriteOrColor(frameImg, ExpFrameResource, new Color(0.25f, 0.27f, 0.34f, 0.5f));
                frameImg.raycastTarget = false;

                CreateText(expBarRoot, "ExpText", "0/100",
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 50f), 32, TextAnchor.MiddleCenter);
            }
        }

        /// <summary>SPEC §9.14.11 v3.204 / v3.208：左上体力 HUD + 增加经验按钮（关闭钮右侧；缺则补建）。</summary>
        public static RectTransform BuildTopLeftStaminaHud(RectTransform rootRt)
        {
            if (rootRt == null)
                return null;

            var existing = rootRt.Find("TopLeftStaminaHud") as RectTransform;
            if (existing != null)
            {
                ApplyTopLeftStaminaHudLayout(existing);
                EnsureAddExpButton(existing);
                return existing.Find("StaminaBarSlot") as RectTransform;
            }

            var hudRt = CreateChild(rootRt, "TopLeftStaminaHud",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), TopLeftStaminaHudAnchoredPos, TopLeftStaminaHudSize);

            var slotRt = CreateChild(hudRt, "StaminaBarSlot",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0f),
                StaminaBarSlotSize);
            slotRt.anchoredPosition = new Vector2(0f, 0f);

            EnsureAddExpButton(hudRt);
            ApplyTopLeftStaminaHudLayout(hudRt);
            return slotRt;
        }

        /// <summary>SPEC §9.14.11 v3.208：体力槽右侧「增加经验」按钮。</summary>
        public static RectTransform EnsureAddExpButton(RectTransform hudRt)
        {
            if (hudRt == null)
                return null;

            var existing = hudRt.Find("AddExpButton") as RectTransform;
            if (existing != null)
            {
                ApplyAddExpButtonLayout(existing);
                return existing;
            }

            var btnRt = CreateChild(hudRt, "AddExpButton",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(StaminaBarSlotSize.x + AddExpButtonGap, 0f),
                AddExpButtonSize);
            var img = btnRt.gameObject.AddComponent<Image>();
            img.color = AddExpButtonColor;
            img.raycastTarget = true;
            var btn = btnRt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;

            var labelRt = CreateChild(btnRt, "Label", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(labelRt);
            var label = labelRt.gameObject.AddComponent<Text>();
            label.text = "增加经验";
            label.font = FarmGridView.LoadBuiltinFont();
            label.fontSize = AddExpButtonFontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;

            ApplyAddExpButtonLayout(btnRt);
            return btnRt;
        }

        private static void ApplyAddExpButtonLayout(RectTransform btnRt)
        {
            if (btnRt == null)
                return;
            btnRt.anchorMin = new Vector2(0f, 0.5f);
            btnRt.anchorMax = new Vector2(0f, 0.5f);
            btnRt.pivot = new Vector2(0f, 0.5f);
            btnRt.anchoredPosition = new Vector2(StaminaBarSlotSize.x + AddExpButtonGap, 0f);
            btnRt.sizeDelta = AddExpButtonSize;
        }

        private static void ApplyTopLeftStaminaHudLayout(RectTransform hudRt)
        {
            if (hudRt == null)
                return;
            hudRt.anchorMin = new Vector2(0f, 1f);
            hudRt.anchorMax = new Vector2(0f, 1f);
            hudRt.pivot = new Vector2(0f, 1f);
            hudRt.anchoredPosition = TopLeftStaminaHudAnchoredPos;
            hudRt.sizeDelta = TopLeftStaminaHudSize;

            var slot = hudRt.Find("StaminaBarSlot") as RectTransform;
            if (slot != null)
            {
                slot.anchorMin = new Vector2(0f, 0.5f);
                slot.anchorMax = new Vector2(0f, 0.5f);
                slot.pivot = new Vector2(0f, 0.5f);
                slot.anchoredPosition = Vector2.zero;
                slot.sizeDelta = StaminaBarSlotSize;
            }

            var closeBtn = hudRt.parent != null ? hudRt.parent.Find("ScreenCloseButton") : null;
            if (closeBtn != null)
            {
                int closeIndex = closeBtn.GetSiblingIndex();
                hudRt.SetSiblingIndex(closeIndex + 1);
            }
            else
            {
                hudRt.SetAsLastSibling();
            }
        }

        /// <summary>SPEC §9.14.11 v3.190：右上竖排「排行榜」「每日任务」图标按钮。</summary>
        public static RectTransform BuildTopRightActions(RectTransform rootRt)
        {
            if (rootRt == null)
                return null;

            var existing = rootRt.Find("TopRightActions") as RectTransform;
            if (existing != null)
                return existing;

            float stackHeight = TopRightButtonSize.y * 2f + TopRightButtonGap;
            var actionsRt = CreateChild(rootRt, "TopRightActions",
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-TopRightMargin, -TopRightMargin),
                new Vector2(TopRightButtonSize.x, stackHeight));

            BuildTopRightIconButton(actionsRt, "RankingButton", RankingButtonResource,
                new Vector2(0f, -TopRightButtonSize.y * 0.5f));
            BuildTopRightIconButton(actionsRt, "DailyTaskButton", DailyTaskButtonResource,
                new Vector2(0f, -(TopRightButtonSize.y + TopRightButtonGap + TopRightButtonSize.y * 0.5f)));

            return actionsRt;
        }

        private static void BuildTopRightIconButton(
            RectTransform parent, string name, string resourcePath, Vector2 anchoredPos)
        {
            var btnRt = CreateChild(parent, name,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 0.5f), anchoredPos, TopRightButtonSize);

            var img = btnRt.gameObject.AddComponent<Image>();
            ApplySpriteOrColor(img, resourcePath, TopRightButtonFallbackColor);
            img.preserveAspect = true;
            img.raycastTarget = true;

            var btn = btnRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
        }

        /// <summary>SPEC §9.14.11 v3.188：构建 / 补建 AttrGrid（2 列 × 3 行，图标+数值）。</summary>
        public static RectTransform BuildAttrGrid(RectTransform hexAttrsPage)
        {
            if (hexAttrsPage == null)
                return null;

            DestroyLegacyHexChildren(hexAttrsPage);

            var existing = hexAttrsPage.Find("AttrGrid") as RectTransform;
            if (existing != null)
                return existing;

            float gridW = AttrGridColumns * AttrItemSize.x + (AttrGridColumns - 1) * AttrGridCellGapX;
            float gridH = AttrGridRows * AttrItemSize.y + (AttrGridRows - 1) * AttrGridCellGapY;
            var gridRt = CreateChild(hexAttrsPage, "AttrGrid",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(gridW, gridH));

            for (int i = 0; i < GrowthAttrCount; i++)
            {
                int col = i % AttrGridColumns;
                int row = i / AttrGridColumns;
                float x = (col - 0.5f) * (AttrItemSize.x + AttrGridCellGapX);
                float y = (AttrGridRows - 1) * 0.5f * (AttrItemSize.y + AttrGridCellGapY)
                    - row * (AttrItemSize.y + AttrGridCellGapY);

                var itemRt = CreateChild(gridRt, "AttrItem_" + i,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(x, y), AttrItemSize);

                var iconRt = CreateChild(itemRt, "Icon",
                    new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f), new Vector2(12f, 0f), AttrIconSize);
                var iconImg = iconRt.gameObject.AddComponent<Image>();
                string iconPath = i < RoleLevelConfigCatalog.GrowthAttrIconPaths.Length
                    ? RoleLevelConfigCatalog.GrowthAttrIconPaths[i]
                    : null;
                ApplySpriteOrColor(iconImg, iconPath, new Color(0.45f, 0.5f, 0.6f, 1f));
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;

                var valueRt = CreateChild(itemRt, "Value",
                    new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(1f, 0.5f), new Vector2(-12f, 0f), AttrValueSize);
                var valueTxt = valueRt.gameObject.AddComponent<Text>();
                valueTxt.text = "0";
                valueTxt.font = FarmGridView.LoadBuiltinFont();
                valueTxt.fontSize = AttrValueFontSize;
                valueTxt.alignment = TextAnchor.MiddleLeft;
                valueTxt.color = TextColor;
                valueTxt.raycastTarget = false;
                valueTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                valueTxt.verticalOverflow = VerticalWrapMode.Overflow;
            }

            return gridRt;
        }

        private static void DestroyLegacyHexChildren(RectTransform hexAttrsPage)
        {
            DestroyChildIfExists(hexAttrsPage, "HexRadarChart");
            DestroyChildIfExists(hexAttrsPage, "HexLabels");
        }

        private static void DestroyChildIfExists(RectTransform parent, string childName)
        {
            if (parent == null)
                return;
            var child = parent.Find(childName);
            if (child == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(child.gameObject);
            else
                Object.DestroyImmediate(child.gameObject);
        }

        /// <summary>SPEC §9.14.11：嵌入创角界面时全屏拉伸，底边止于 BottomTabBar 之上。</summary>
        public static void ApplyCharacterCreationEmbedLayout(RectTransform rootRt)
        {
            if (rootRt == null)
                return;
            StretchFull(rootRt);
            rootRt.offsetMin = new Vector2(0f, CharacterCreationScreenLayout.ContentRegionBottomOffset);
            rootRt.offsetMax = Vector2.zero;
        }

        /// <summary>SPEC §9.14.11 v3.187：在 CharacterZone 内构建固定左上角气泡（九宫格 DialogBox_1）。</summary>
        public static RectTransform BuildSpeechBubble(RectTransform characterZone)
        {
            if (characterZone == null)
                return null;

            var existing = characterZone.Find("SpeechBubble") as RectTransform;
            if (existing != null)
                return existing;

            var bubbleRt = CreateChild(characterZone, "SpeechBubble",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0f), SpeechBubbleAnchoredPos, SpeechBubbleSize);

            var bubbleImg = bubbleRt.gameObject.AddComponent<Image>();
            ApplySpriteOrColor(bubbleImg, SpeechBubbleResource, new Color(1f, 1f, 1f, 0.92f));
            bubbleImg.type = Image.Type.Sliced;
            bubbleImg.fillCenter = true;
            bubbleImg.raycastTarget = true;
            bubbleImg.preserveAspect = false;

            var bubbleBtn = bubbleRt.gameObject.AddComponent<Button>();
            bubbleBtn.transition = Selectable.Transition.None;
            bubbleBtn.targetGraphic = bubbleImg;

            var textRt = CreateChild(bubbleRt, "BubbleText",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(textRt);
            textRt.offsetMin = SpeechBubbleTextPadding;
            textRt.offsetMax = -SpeechBubbleTextPadding;
            var txt = textRt.gameObject.AddComponent<Text>();
            txt.text = string.Empty;
            txt.font = FarmGridView.LoadBuiltinFont();
            txt.fontSize = SpeechBubbleFontSize;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = TextColor;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.supportRichText = false;

            bubbleRt.gameObject.SetActive(false);
            return bubbleRt;
        }

        private static void ApplySpriteOrColor(Image img, string resourcePath, Color fallback)
        {
            if (img == null)
                return;
            var sprite = string.IsNullOrEmpty(resourcePath) ? null : Resources.Load<Sprite>(resourcePath);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = false;
            }
            else
            {
                img.color = fallback;
            }
            img.raycastTarget = false;
        }

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
            txt.color = TextColor;
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
