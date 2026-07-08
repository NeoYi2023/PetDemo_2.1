// SPEC §9.14：创角界面层级构建（运行时回退与预制体生成器共用，保证结构一致）。
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    public static class CharacterCreationScreenLayout
    {
        private static readonly Vector2 RoleMountSize = new Vector2(720f, 1000f);
        private static readonly Color BackgroundColor = new Color(0.10f, 0.12f, 0.18f, 1f);
        private static readonly Color PanelColor = new Color(0.13f, 0.15f, 0.21f, 0.98f);
        private static readonly Color ButtonColor = new Color(0.26f, 0.55f, 0.85f, 1f);
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Vector2 ScreenCloseButtonSize = new Vector2(72f, 72f);

        // SPEC §9.14.1（v3.166）：加号按钮精灵与尺寸；加号态全屏纯黑背景。
        private const string AddButtonResource = "AirUI/AddButton";
        private static readonly Vector2 AddButtonSize = new Vector2(1080f, 1076f);
        private static readonly Vector2 AddButtonAnchoredPos = new Vector2(0f, 80f);

        // SPEC §9.14.10（v3.139 / v3.184）：底部页签栏。
        private const float BottomTabBarHeight = 160f;
        private const int BottomTabCount = 5;
        private const float BottomTabHorizontalInset = 5f;
        public const string HomeTabIconClosedResource = "AirUI/bottom_bar_c_1";
        public const string HomeTabIconOpenResource = "AirUI/bottom_bar_c_2";
        public const string IntimacyTabIconClosedResource = "AirUI/bottom_bar_a_1";
        public const string IntimacyTabIconOpenResource = "AirUI/bottom_bar_a_2";
        public const string DressUpTabIconClosedResource = "AirUI/bottom_bar_b_1";
        public const string DressUpTabIconOpenResource = "AirUI/bottom_bar_b_2";
        public const string EnterHomeTabIconClosedResource = "AirUI/bottom_bar_e_1";
        public const string EnterHomeTabIconOpenResource = "AirUI/bottom_bar_e_2";
        public const string RoleAddFavorTabIconClosedResource = "AirUI/bottom_bar_d_1";
        public const string RoleAddFavorTabIconOpenResource = "AirUI/bottom_bar_d_2";

        // SPEC §9.14.10（v3.142）：页签内容区为屏幕下方 60%（位于 BottomTabBar 之上），上方 40% 持续显示 DisplayArea。
        public const float ContentRegionTopAnchorY = 0.6f;
        public const float ContentRegionBottomOffset = BottomTabBarHeight;
        public const float ContentRegionSideMargin = 40f;
        private static readonly Color TabBarColor = new Color(0.10f, 0.12f, 0.18f, 0.98f);
        private static readonly Color TabNormalColor = new Color(0.20f, 0.23f, 0.30f, 1f);
        // SPEC §9.14.8 第 1 点（v3.158）：亲密度好友双列网格。
        private const string TopFriendCellBackground = "AirUI/friends_bg_1";   // 九宫格背景
        private const string TopFriendIntimacyBg = "AirUI/friends_bg_2";       // 左上亲密度底框
        private const float TopFriendCellWidth = 490f;
        private const float TopFriendCellHeight = 430f;
        private const float TopFriendCellSpacing = 16f;
        private const int TopFriendGridPadding = 12;
        private const int TopFriendGridColumns = 2;
        private static readonly Color TopFriendCellFallbackColor = new Color(0.16f, 0.18f, 0.24f, 0.95f);
        // SPEC §9.14.10：进入家园跳转列表长框（v3.163：1014×290）。
        private const string EnterHomeCellBackground = "AirUI/TopFriendCellBJ";
        private const float EnterHomeCellWidth = 1014f;
        private const float EnterHomeCellHeight = 290f;
        private static readonly Color ActionButtonColor = new Color(0.26f, 0.55f, 0.85f, 1f);
        private static readonly Color TextColor = Color.black;

        /// <summary>旧 prefab 无 EnterHomeTopPanel 时运行时补建（与 EnsureScreenCloseButton 一致）。</summary>
        public static void EnsureEnterHomeTopPanel(RectTransform rootRt)
        {
            if (rootRt == null || rootRt.Find("EnterHomeTopPanel") != null)
                return;
            BuildEnterHomeTopPanel(rootRt);
        }

        /// <summary>
        /// 构建完整创角界面层级并挂载 <see cref="CharacterCreationScreenView"/>。
        /// 字段在运行时由 View.EnsureFieldsFromHierarchy 按节点名绑定；prefab 生成器直接复用此结构。
        /// </summary>
        public static GameObject BuildRuntime(RectTransform parent)
        {
            var root = new GameObject(CharacterCreationScreenView.PanelObjectName, typeof(RectTransform));
            var rootRt = root.GetComponent<RectTransform>();
            if (parent != null)
                rootRt.SetParent(parent, false);
            StretchFull(rootRt);
            root.AddComponent<CharacterCreationScreenView>();

            // 背景（全屏，阻挡其下射线）。
            var bg = CreateChild(rootRt, "Background", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(bg);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = BackgroundColor;
            bgImg.raycastTarget = true;

            // 展示区。
            var displayArea = CreateChild(rootRt, "DisplayArea", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(displayArea);

            // SPEC §9.14.1（v3.166）：加号按钮不再置于展示区，改挂根节点最高层级（见文件末尾 BuildAddButton）。

            // 主角挂点（720x1000；Spine 运行时挂入）。
            CreateChild(displayArea, "RoleMount", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), RoleMountSize);

            // 缺好感态面板。
            var needPanel = CreateChild(displayArea, "NeedFavorPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 560f));
            CreateText(needPanel, "NeedFavorText", "", new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(720f, 280f), 44, TextAnchor.UpperCenter);
            CreateButton(needPanel, "AddFavorButton", "增加好感度",
                new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(380f, 130f),
                new Color(0.9f, 0.5f, 0.25f, 1f), 44);

            // SPEC §9.14.8/§9.14.10（v3.139）：亲密度好友列表（默认隐藏，由「亲密度」页签切换）。
            BuildIntimacyTopPanel(rootRt);

            // SPEC §9.14.10（v3.141）：进入家园跳转列表（默认隐藏，供任务列表等入口）。
            BuildEnterHomeTopPanel(rootRt);

            // SPEC §9.14.10（v3.184）：家园页签占位面板。
            BuildHomeTabPlaceholderPanel(rootRt);

            // SPEC §9.14.10（v3.184）：底部常驻页签栏（亲密度 / 装扮 / 家园 / 进入家园 / 加好感）。
            BuildBottomTabBar(rootRt);

            // 好友列表弹窗（默认隐藏）。
            BuildFriendListPopup(rootRt);

            // 右上角关闭（回退 §9.15 APP 首页）。
            BuildScreenCloseButton(rootRt);

            // SPEC §9.14.1（v3.166）：加号态全屏纯黑背景 + 加号按钮置于根节点最高层级
            // （最后创建 = 最上渲染，黑底在其下、其余全部 UI 之上）。
            CreateAddButtonBackdrop(rootRt);
            BuildAddButton(rootRt);

            return root;
        }

        /// <summary>SPEC §9.14.1（v3.166）：加号态全屏纯黑背景（默认隐藏，仅阻挡点击）。运行时补建旧预制体亦复用。</summary>
        public static Image CreateAddButtonBackdrop(RectTransform rootRt)
        {
            var backdrop = CreateChild(rootRt, "AddButtonBackdrop", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(backdrop);
            var img = backdrop.gameObject.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = true;
            backdrop.gameObject.SetActive(false);
            return img;
        }

        /// <summary>SPEC §9.14.1（v3.166）：加号按钮（精灵 AirUI/AddButton），置于根节点最高层级。</summary>
        public static Button BuildAddButton(RectTransform rootRt)
        {
            var btn = CreateButton(rootRt, "AddButton", "+",
                new Vector2(0.5f, 0.5f), AddButtonAnchoredPos, AddButtonSize, Color.white, 180);
            var img = btn.GetComponent<Image>();
            var sprite = Resources.Load<Sprite>(AddButtonResource);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = true;
                // 有图时隐藏「+」文字占位。
                var label = btn.transform.Find("Label");
                if (label != null)
                    label.gameObject.SetActive(false);
            }
            else
            {
                img.color = ButtonColor;
            }
            return btn;
        }

        // ---- SPEC §9.14.10（v3.139）底部页签栏 ----

        /// <summary>旧 prefab 缺少 HomeTabButton 时重建 5 页签底栏（v3.184）。</summary>
        public static void EnsureBottomTabBar(RectTransform rootRt)
        {
            if (rootRt == null)
                return;
            var bar = rootRt.Find("BottomTabBar") as RectTransform;
            if (bar != null && bar.Find("HomeTabButton") != null)
            {
                RefreshBottomTabBarPresentation(bar);
                return;
            }

            if (bar == null)
            {
                BuildBottomTabBar(rootRt);
                return;
            }

            for (int i = bar.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(bar.GetChild(i).gameObject);
            PopulateBottomTabButtons(bar);
        }

        /// <summary>SPEC §9.14.10（v3.185）：刷新底栏页签图标并隐藏废弃 Label。</summary>
        public static void RefreshBottomTabBarPresentation(RectTransform bar)
        {
            if (bar == null)
                return;

            ApplyTabButtonIconsByName(bar, "IntimacyTab", IntimacyTabIconClosedResource, IntimacyTabIconOpenResource);
            ApplyTabButtonIconsByName(bar, "DressUpButton", DressUpTabIconClosedResource, DressUpTabIconOpenResource);
            ApplyTabButtonIconsByName(bar, "HomeTabButton", HomeTabIconClosedResource, HomeTabIconOpenResource);
            ApplyTabButtonIconsByName(bar, "EnterHomeButton", EnterHomeTabIconClosedResource, EnterHomeTabIconOpenResource);
            ApplyTabButtonIconsByName(bar, "RoleAddFavorButton", RoleAddFavorTabIconClosedResource, RoleAddFavorTabIconOpenResource);
            HideTabButtonLabels(bar);
        }

        private static void BuildBottomTabBar(RectTransform rootRt)
        {
            var bar = CreateChild(rootRt, "BottomTabBar", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, BottomTabBarHeight));
            bar.anchoredPosition = Vector2.zero;
            var barImg = bar.gameObject.AddComponent<Image>();
            barImg.color = TabBarColor;
            barImg.raycastTarget = true;
            PopulateBottomTabButtons(bar);
        }

        private static void PopulateBottomTabButtons(RectTransform bar)
        {
            // 5 个等宽互斥页签（与 §9.10 RoleGrowthTabBar 等宽槽位范式一致）。
            var intimacyTab = CreateTabButton(bar, "IntimacyTab", 0);
            ApplyTabButtonIcons(intimacyTab, IntimacyTabIconClosedResource, IntimacyTabIconOpenResource);
            var dressUpButton = CreateTabButton(bar, "DressUpButton", 1);
            ApplyTabButtonIcons(dressUpButton, DressUpTabIconClosedResource, DressUpTabIconOpenResource);
            var homeTab = CreateTabButton(bar, "HomeTabButton", 2);
            ApplyTabButtonIcons(homeTab, HomeTabIconClosedResource, HomeTabIconOpenResource);
            var enterHomeButton = CreateTabButton(bar, "EnterHomeButton", 3);
            ApplyTabButtonIcons(enterHomeButton, EnterHomeTabIconClosedResource, EnterHomeTabIconOpenResource);
            var roleAddFavorButton = CreateTabButton(bar, "RoleAddFavorButton", 4);
            ApplyTabButtonIcons(roleAddFavorButton, RoleAddFavorTabIconClosedResource, RoleAddFavorTabIconOpenResource);
            HideTabButtonLabels(bar);
        }

        /// <summary>旧 prefab 无 HomeTabPlaceholderPanel 时运行时补建。</summary>
        public static void EnsureHomeTabPlaceholderPanel(RectTransform rootRt)
        {
            if (rootRt == null || rootRt.Find("HomeTabPlaceholderPanel") != null)
                return;
            BuildHomeTabPlaceholderPanel(rootRt);
        }

        private static void BuildHomeTabPlaceholderPanel(RectTransform rootRt)
        {
            var panel = CreateChild(rootRt, "HomeTabPlaceholderPanel", new Vector2(0f, 0f), new Vector2(1f, ContentRegionTopAnchorY),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            panel.offsetMin = new Vector2(ContentRegionSideMargin, ContentRegionBottomOffset);
            panel.offsetMax = new Vector2(-ContentRegionSideMargin, 0f);
            panel.gameObject.SetActive(false);
        }

        private static void ApplyTabButtonIcons(Button tab, string closedResource, string openResource)
        {
            if (tab == null)
                return;
            ApplyTabButtonIconSprite(tab.transform, "IconClosed", closedResource);
            ApplyTabButtonIconSprite(tab.transform, "IconOpen", openResource);
        }

        private static void ApplyTabButtonIconsByName(Transform bar, string buttonName, string closedResource, string openResource)
        {
            if (bar == null)
                return;
            var buttonT = bar.Find(buttonName);
            if (buttonT == null)
                return;
            ApplyTabButtonIconSprite(buttonT, "IconClosed", closedResource);
            ApplyTabButtonIconSprite(buttonT, "IconOpen", openResource);
        }

        private static void HideTabButtonLabels(Transform bar)
        {
            if (bar == null)
                return;
            for (int i = 0; i < bar.childCount; i++)
            {
                var label = bar.GetChild(i).Find("Label");
                if (label != null)
                    label.gameObject.SetActive(false);
            }
        }

        private static void ApplyTabButtonIconSprite(Transform buttonRoot, string childName, string resourcePath)
        {
            if (buttonRoot == null)
                return;
            var iconT = buttonRoot.Find(childName);
            if (iconT == null)
                return;
            if (iconT is RectTransform iconRt)
                StretchFull(iconRt);
            var img = iconT.GetComponent<Image>();
            if (img == null)
                return;
            var sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = true;
            }
        }

        /// <summary>在底部页签栏内创建一个按 1/5 等宽拉伸的页签按钮。</summary>
        private static Button CreateTabButton(RectTransform parent, string name, int index)
        {
            float min = (float)index / BottomTabCount;
            float max = (float)(index + 1) / BottomTabCount;

            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(min, 0f);
            rt.anchorMax = new Vector2(max, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(BottomTabHorizontalInset, 16f);
            rt.offsetMax = new Vector2(-BottomTabHorizontalInset, -16f);

            var img = go.AddComponent<Image>();
            img.color = TabNormalColor;
            img.raycastTarget = true;
            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;

            // SPEC §9.14.10（v3.159 / v3.185）：页签双态图标，Label 已废弃。
            CreateTabIconPlaceholder(rt, "IconOpen", active: false);
            CreateTabIconPlaceholder(rt, "IconClosed", active: true);
            return btn;
        }

        /// <summary>页签按钮内 IconOpen / IconClosed 占位 Image（拉伸填满槽位，不挡点击）。</summary>
        private static void CreateTabIconPlaceholder(RectTransform parent, string name, bool active)
        {
            var iconRt = CreateChild(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(iconRt);
            var iconImg = iconRt.gameObject.AddComponent<Image>();
            iconImg.color = new Color(1f, 1f, 1f, 0f);
            iconImg.raycastTarget = false;
            iconImg.preserveAspect = true;
            iconRt.gameObject.SetActive(active);
        }

        // ---- SPEC §9.14.8（v3.139）亲密度好友列表 ----

        private static void BuildIntimacyTopPanel(RectTransform rootRt)
        {
            // SPEC §9.14.10（v3.142）：占据屏幕下方 60% 内容区（位于底部页签栏之上），上方 40% 保留 DisplayArea；默认隐藏。
            var panel = CreateChild(rootRt, "IntimacyTopPanel", new Vector2(0f, 0f), new Vector2(1f, ContentRegionTopAnchorY),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            panel.offsetMin = new Vector2(ContentRegionSideMargin, ContentRegionBottomOffset);
            panel.offsetMax = new Vector2(-ContentRegionSideMargin, 0f);

            // ScrollView。
            var scrollGo = new GameObject("TopFriendScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.SetParent(panel, false);
            StretchFull(scrollRt);
            scrollGo.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.6f);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.SetParent(scrollRt, false);
            StretchFull(viewportRt);
            viewportGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            // SPEC §9.14.8 第 1 点（v3.158）：双列网格（每行 2 个，单元 500×430），单元改为独立预制体由 View 运行时实例化。
            var contentGo = new GameObject("TopFriendContent", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.SetParent(viewportRt, false);
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;

            var grid = contentGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(TopFriendCellWidth, TopFriendCellHeight);
            grid.spacing = new Vector2(TopFriendCellSpacing, TopFriendCellSpacing);
            grid.padding = new RectOffset(TopFriendGridPadding, TopFriendGridPadding, TopFriendGridPadding, TopFriendGridPadding);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = TopFriendGridColumns;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewportRt;
            scroll.content = contentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// SPEC §9.14.8 第 1 点（v3.158）：构建 TopFriendCell 独立预制体根（500×430，九宫格背景）。
        /// 供 <see cref="PetDemo.EditorTools.TopFriendCellPrefabGenerator"/> 生成预制体，
        /// 子节点名与 <see cref="TopFriendCellView.AutoWire"/> 对应；运行时由 View 实例化并 Bind。
        /// </summary>
        public static GameObject BuildTopFriendCellRoot(RectTransform parent)
        {
            var cell = CreateChild(parent, "TopFriendCell", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TopFriendCellWidth, TopFriendCellHeight));
            var cellImg = cell.gameObject.AddComponent<Image>();
            var bgSprite = Resources.Load<Sprite>(TopFriendCellBackground);
            if (bgSprite != null)
            {
                cellImg.sprite = bgSprite;
                cellImg.color = Color.white;
                cellImg.type = Image.Type.Sliced;   // 九宫格：边缘不变、中心拉伸
                cellImg.fillCenter = true;
            }
            else
            {
                cellImg.color = TopFriendCellFallbackColor;
            }
            cellImg.raycastTarget = true;

            var rootBtn = cell.gameObject.AddComponent<Button>();
            rootBtn.transition = Selectable.Transition.None;
            rootBtn.targetGraphic = cellImg;
            cell.gameObject.AddComponent<TopFriendCellView>();

            // 头像（左上区域）+ 头像框（叠加，默认隐藏，由 Bind 控制）。
            var avatar = CreateChild(cell, "Avatar", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(130f, -150f), new Vector2(180f, 180f));
            var avatarImg = avatar.gameObject.AddComponent<Image>();
            avatarImg.color = new Color(0.3f, 0.36f, 0.46f, 1f);
            avatarImg.raycastTarget = false;
            avatarImg.preserveAspect = true;

            var frame = CreateChild(cell, "AvatarFrame", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(130f, -150f), new Vector2(216f, 216f));
            var frameImg = frame.gameObject.AddComponent<Image>();
            frameImg.color = Color.white;
            frameImg.raycastTarget = false;
            frameImg.preserveAspect = true;
            frame.gameObject.SetActive(false);

            // 名字（头像右侧）。
            CreateText(cell, "NameText", "", new Vector2(0f, 1f), new Vector2(240f, -110f), new Vector2(230f, 60f), 40, TextAnchor.UpperLeft);

            // 性别图标 + 在线图标（名字下方一排）。
            var gender = CreateChild(cell, "GenderIcon", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(266f, -185f), new Vector2(56f, 56f));
            var genderImg = gender.gameObject.AddComponent<Image>();
            genderImg.color = new Color(0.9f, 0.78f, 0.3f, 1f);
            genderImg.raycastTarget = false;
            genderImg.preserveAspect = true;

            var online = CreateChild(cell, "OnlineIcon", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(340f, -185f), new Vector2(40f, 40f));
            var onlineImg = online.gameObject.AddComponent<Image>();
            onlineImg.color = new Color(0.9f, 0.78f, 0.3f, 1f);
            onlineImg.raycastTarget = false;
            onlineImg.preserveAspect = true;

            // SPEC §9.14.8（v3.159）：OnlineIcon 右侧在线/离线文字。
            CreateText(cell, "OnlineText", "", new Vector2(0f, 1f), new Vector2(388f, -185f), new Vector2(80f, 40f), 32, TextAnchor.MiddleLeft);

            // 左上角亲密度底框 friends_bg_2，其上（子节点）显示 IntimacyIcon + IntimacyText。
            var intimacyBg = CreateChild(cell, "IntimacyBg", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(8f, -8f), new Vector2(220f, 96f));
            var intimacyBgImg = intimacyBg.gameObject.AddComponent<Image>();
            var bg2Sprite = Resources.Load<Sprite>(TopFriendIntimacyBg);
            if (bg2Sprite != null)
            {
                intimacyBgImg.sprite = bg2Sprite;
                intimacyBgImg.color = Color.white;
            }
            else
            {
                intimacyBgImg.color = new Color(0f, 0f, 0f, 0.5f);
            }
            intimacyBgImg.raycastTarget = false;
            intimacyBgImg.preserveAspect = false;

            var intimacyIcon = CreateChild(intimacyBg, "IntimacyIcon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(38f, 0f), new Vector2(50f, 50f));
            var intimacyIconImg = intimacyIcon.gameObject.AddComponent<Image>();
            intimacyIconImg.color = new Color(0.9f, 0.78f, 0.3f, 1f);
            intimacyIconImg.raycastTarget = false;
            intimacyIconImg.preserveAspect = true;

            CreateText(intimacyBg, "IntimacyText", "", new Vector2(0f, 0.5f), new Vector2(72f, 0f), new Vector2(140f, 50f), 28, TextAnchor.MiddleLeft);

            return cell.gameObject;
        }

        // ---- SPEC §9.14.10（v3.141）进入家园跳转列表 ----

        private static void BuildEnterHomeTopPanel(RectTransform rootRt)
        {
            // SPEC §9.14.10（v3.142）：占据屏幕下方 60% 内容区（位于底部页签栏之上），上方 40% 保留 DisplayArea；默认隐藏。
            var panel = CreateChild(rootRt, "EnterHomeTopPanel", new Vector2(0f, 0f), new Vector2(1f, ContentRegionTopAnchorY),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            panel.offsetMin = new Vector2(ContentRegionSideMargin, ContentRegionBottomOffset);
            panel.offsetMax = new Vector2(-ContentRegionSideMargin, 0f);

            var scrollGo = new GameObject("EnterHomeScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.SetParent(panel, false);
            StretchFull(scrollRt);
            scrollGo.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.6f);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.SetParent(scrollRt, false);
            StretchFull(viewportRt);
            viewportGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            var contentGo = new GameObject("EnterHomeContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.SetParent(viewportRt, false);
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;

            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
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

            BuildEnterHomeNavCellTemplate(contentRt);

            panel.gameObject.SetActive(false);
        }

        private static void BuildEnterHomeNavCellTemplate(RectTransform content)
        {
            var cell = CreateChild(content, "EnterHomeNavCellTemplate", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero, new Vector2(EnterHomeCellWidth, EnterHomeCellHeight));
            var cellImg = cell.gameObject.AddComponent<Image>();
            var bgSprite = Resources.Load<Sprite>(EnterHomeCellBackground);
            if (bgSprite != null)
            {
                cellImg.sprite = bgSprite;
                cellImg.color = Color.white;
            }
            else
            {
                cellImg.color = TopFriendCellFallbackColor;
            }
            cellImg.raycastTarget = true;
            var le = cell.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = EnterHomeCellWidth;
            le.minWidth = EnterHomeCellWidth;
            le.preferredHeight = EnterHomeCellHeight;
            le.minHeight = EnterHomeCellHeight;
            cell.gameObject.AddComponent<EnterHomeNavCellView>();

            var icon = CreateChild(cell, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(100f, 0f), new Vector2(130f, 130f));
            var iconImg = icon.gameObject.AddComponent<Image>();
            iconImg.color = new Color(0.3f, 0.36f, 0.46f, 1f);
            iconImg.raycastTarget = false;
            iconImg.preserveAspect = true;

            CreateText(cell, "NameText", "", new Vector2(0f, 0.5f), new Vector2(190f, 0f), new Vector2(320f, 60f), 40, TextAnchor.MiddleLeft);

            var btnSize = new Vector2(160f, 80f);
            CreateButton(cell, "NavigateButton", "前往",
                new Vector2(1f, 0.5f), new Vector2(-110f, 0f), btnSize, ActionButtonColor, 30);

            cell.gameObject.SetActive(false);
        }

        private static void BuildScreenCloseButton(RectTransform rootRt)
        {
            var closeRt = CreateChild(rootRt, "ScreenCloseButton",
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-20f, -20f), ScreenCloseButtonSize);
            var closeImg = closeRt.gameObject.AddComponent<Image>();
            closeImg.color = new Color(0.25f, 0.22f, 0.32f, 0.95f);
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
            closeLabel.fontSize = 44;
            closeLabel.alignment = TextAnchor.MiddleCenter;
            closeLabel.color = TextColor;
            closeLabel.raycastTarget = false;
        }

        private static void BuildFriendListPopup(RectTransform rootRt)
        {
            var popup = CreateChild(rootRt, "FriendListPopup", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(popup);

            var dim = CreateChild(popup, "Dim", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(dim);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = DimColor;
            dimImg.raycastTarget = true;

            var listPanel = CreateChild(popup, "ListPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(840f, 1280f));
            var panelImg = listPanel.gameObject.AddComponent<Image>();
            panelImg.color = PanelColor;
            panelImg.raycastTarget = true;

            CreateText(listPanel, "Title", "选择好友", new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(700f, 80f), 48, TextAnchor.MiddleCenter);

            // ScrollView。
            var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.SetParent(listPanel, false);
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
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
            var contentRt = contentGo.GetComponent<RectTransform>();
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

            BuildFriendCellTemplate(contentRt);

            CreateButton(listPanel, "CloseButton", "关闭",
                new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(300f, 90f),
                new Color(0.4f, 0.4f, 0.46f, 1f), 40);
        }

        private static void BuildFriendCellTemplate(RectTransform content)
        {
            var cell = CreateChild(content, "FriendCellTemplate", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 150f));
            var cellImg = cell.gameObject.AddComponent<Image>();
            cellImg.color = new Color(0.16f, 0.18f, 0.24f, 0.95f);
            var btn = cell.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = cellImg;
            var le = cell.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 150f;
            le.minHeight = 150f;
            cell.gameObject.AddComponent<CharacterCreationFriendCellView>();

            // 头像（左）。
            var avatar = CreateChild(cell, "Avatar", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(90f, 0f), new Vector2(120f, 120f));
            var avatarImg = avatar.gameObject.AddComponent<Image>();
            avatarImg.color = new Color(0.3f, 0.36f, 0.46f, 1f);
            avatarImg.raycastTarget = false;
            avatarImg.preserveAspect = true;

            // 名字。
            CreateText(cell, "NameText", "", new Vector2(0f, 1f), new Vector2(170f, -18f), new Vector2(420f, 60f), 42, TextAnchor.UpperLeft);
            // 亲密度。
            CreateText(cell, "IntimacyText", "", new Vector2(0f, 0f), new Vector2(170f, 18f), new Vector2(420f, 50f), 32, TextAnchor.LowerLeft);

            // 在线点 + 文字（右）。
            var dot = CreateChild(cell, "OnlineDot", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(-180f, 0f), new Vector2(28f, 28f));
            var dotImg = dot.gameObject.AddComponent<Image>();
            dotImg.color = new Color(0.5f, 0.5f, 0.55f, 1f);
            dotImg.raycastTarget = false;
            CreateText(cell, "OnlineText", "", new Vector2(1f, 0.5f), new Vector2(-90f, 0f), new Vector2(130f, 50f), 32, TextAnchor.MiddleCenter);

            cell.gameObject.SetActive(false);
        }

        // ---- 构建工具 ----

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
            Vector2 anchorPivot, Vector2 anchoredPos, Vector2 size, Color color, int fontSize)
        {
            var rt = CreateChild(parent, name, anchorPivot, anchorPivot, anchorPivot, anchoredPos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;

            var labelRt = CreateChild(rt, "Label", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(labelRt);
            var txt = labelRt.gameObject.AddComponent<Text>();
            txt.text = label;
            txt.font = FarmGridView.LoadBuiltinFont();
            txt.fontSize = fontSize;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = TextColor;
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
