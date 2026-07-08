// SPEC §9.14：创角界面（预制体 CharacterCreationScreen）。
// 三态切换：加号态（未创建）/ 缺好感态（已选好友 intimacy<80）/ 主角态（已创建，显示 720x1000 Spine 主角 + 进入家园）。
using System;
using System.Collections.Generic;
using PetDemo.Core;
using PetDemo.Farm;
using PetDemo.UI.Farm;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class CharacterCreationScreenView : MonoBehaviour
    {
        public const string ResPrefabPath = "Prefabs/Farm/CharacterCreationScreen";
        public const string PanelObjectName = "CharacterCreationScreen";

        // SPEC §9.14.8 第 1 点（v3.158）：TopFriendCell 独立预制体。
        private const string TopFriendCellPrefabPath = "Prefabs/Farm/TopFriendCell";

        private const string RoleResourcesPrefabPath = "Prefabs/Air/Hero_Role_cunmin";
        private const string SkeletonGraphicShaderName = "Spine/SkeletonGraphic";
        private static readonly string[] RoleIdleAnimationFallbacks =
        {
            "exclusive_2", "standby_1", "animation", "idle",
        };
        private static readonly Vector2 RoleDisplaySize = new Vector2(720f, 1000f);
        /// <summary>SPEC §9.14.1（v3.140）：RoleSpine 相对原始尺寸的显示缩放。</summary>
        private const float RoleSpineDisplayScale = 0.75f;

        // SPEC §9.14.8（v3.118）：好友展示。
        private const string QinMiDuResource = "AirUI/QinMiDu_0";
        // SPEC §9.14.8（v3.121）：主角态「加好感」全屏赚钱图。
        private const string ZhuanQianResource = "AirUI/ZhuanQian";
        // SPEC §9.14.8（v3.131）：ZhongDuan 提示弹窗。
        private const string ZhongDuanPopupResource = "AirUI/ZhongDuan_1";
        private static readonly Vector2 PopupCloseButtonSize = new Vector2(72f, 72f);
        private static readonly string[] RankCharacterResources =
        {
            "AirUI/WanJia_6", "AirUI/WanJia_5", "AirUI/WanJia_3",
        };
        private static readonly Color AvatarFallbackColor = new Color(0.3f, 0.36f, 0.46f, 1f);

        private static CharacterCreationScreenView instance;

        [SerializeField] private Image background;
        [SerializeField] private Button addButton;
        // SPEC §9.14.1（v3.166）：加号态全屏纯黑背景（置于 AddButton 之下、其余全部 UI 之上）。
        [SerializeField] private Image addButtonBackdrop;
        [SerializeField] private RectTransform roleMount;
        [SerializeField] private Button enterHomeButton;
        [SerializeField] private GameObject needFavorPanel;
        [SerializeField] private Text needFavorText;
        [SerializeField] private Button addFavorButton;
        [SerializeField] private GameObject friendListPopup;
        [SerializeField] private RectTransform friendListContent;
        [SerializeField] private GameObject friendCellTemplate;
        [SerializeField] private Button friendListCloseButton;
        [SerializeField] private Button screenCloseButton;

        // SPEC §9.14.10（v3.139）：底部页签栏与亲密度好友列表（预制体编排）。
        [SerializeField] private GameObject displayArea;
        [SerializeField] private Button intimacyTab;
        [SerializeField] private GameObject intimacyTopPanel;
        [SerializeField] private RectTransform topFriendContent;
        [SerializeField] private GameObject topFriendCellTemplate;

        // SPEC §9.14.10（v3.141）：进入家园跳转列表（预制体编排）。
        [SerializeField] private GameObject enterHomeTopPanel;
        [SerializeField] private RectTransform enterHomeContent;
        [SerializeField] private GameObject enterHomeNavCellTemplate;

        private static readonly Color TabBarHitColor = new Color(1f, 1f, 1f, 0f);

        private int activeTabIndex = -1;
        private readonly List<TopFriendCellView> topCells = new List<TopFriendCellView>();
        private readonly List<EnterHomeNavCellView> enterHomeCells = new List<EnterHomeNavCellView>();

        private struct EnterHomeNavEntry
        {
            public string navKey;
            public string displayName;
            public string iconResource;
        }

        private static readonly EnterHomeNavEntry[] EnterHomeNavEntries =
        {
            new EnterHomeNavEntry { navKey = "GongHui", displayName = "社区", iconResource = "AirUI/Game_ZuDui" },
            new EnterHomeNavEntry { navKey = "JiaYuan", displayName = "农场", iconResource = "AirUI/Game_NongChang" },
            new EnterHomeNavEntry { navKey = "ZhuXian", displayName = "冒险", iconResource = "AirUI/Game_MaoXian" },
            new EnterHomeNavEntry { navKey = "LangLai", displayName = "狼来了", iconResource = "AirUI/Game_LangLai" },
            new EnterHomeNavEntry { navKey = "FenZheng", displayName = "人狼纷争", iconResource = "AirUI/Game_FenZheng" },
            new EnterHomeNavEntry { navKey = "XiuXian", displayName = "修仙", iconResource = "AirUI/Game_XiuXian" },
        };

        private IPlantingService service;
        private RectTransform panelRt;
        private bool wired;
        private bool roleSpineBuilt;
        private SkeletonGraphic roleSkeletonGraphic;
        private string pendingFriendId;
        private readonly List<CharacterCreationFriendCellView> cells = new List<CharacterCreationFriendCellView>();

        // SPEC §9.14.8（v3.118）：好友展示运行时状态。
        private bool qinMiDuBuilt;
        private GameObject qinMiDuPopup;
        private bool zhuanQianBuilt;
        private GameObject zhuanQianPopup;
        // SPEC §9.14.8（v3.121）：加好感页签任务列表 Demo 面板。
        private TaskListPanelView taskListPanel;
        private bool zhongDuanBuilt;
        private GameObject zhongDuanPopup;
        private bool friendCharacterPopupBuilt;
        private GameObject friendCharacterPopup;
        private Image friendCharacterImage;
        private Image friendCharacterAvatar;
        private Text friendCharacterName;

        // SPEC §9.14.8 第 2 点（v3.158）：好友详情弹窗（中央 Spine + 去找Ta/去Ta家/发消息）。
        private bool friendDetailPopupBuilt;
        private GameObject friendDetailPopup;
        private RectTransform friendDetailSpineMount;
        private SkeletonGraphic friendDetailSkeletonGraphic;
        private FriendProfile friendDetailBound;
        private string friendDetailSpinePath;
        private static readonly Vector2 FriendDetailSpineSize = new Vector2(560f, 780f);
        private const float FriendDetailSpineScale = 0.75f;

        // SPEC §9.14.1 / §9.14.9（v3.119）：「装扮」「加好感」按钮（v3.139 起为底部页签，预制体编排）与装扮界面。
        [SerializeField] private Button dressUpButton;
        [SerializeField] private Button homeTabButton;
        [SerializeField] private Button roleAddFavorButton;
        [SerializeField] private GameObject homeTabPlaceholderPanel;
        private DressUpPanelView dressUpPanel;

        // SPEC §9.14.10（v3.184）：创角内嵌公会场景。
        private GongHuiScreenView embeddedGongHui;
        private RectTransform gongHuiEmbedMount;

        private const int TabIndexIntimacy = 0;
        private const int TabIndexDressUp = 1;
        private const int TabIndexHome = 2;
        private const int TabIndexEnterHomeGongHui = 3;
        private const int TabIndexAddFavor = 4;

        /// <summary>SPEC §9.14.10（v3.141）：进入家园页签某行「跳转」时触发，由装配层恢复 HUD/世界层并切底栏。</summary>
        public event Action<string> OnNavigateToBottomNav;

        /// <summary>SPEC §9.14.6（v3.138）：点击右上角关闭按钮，回退 §9.15 APP 首页。</summary>
        public event Action OnCloseRequested;

        /// <summary>SPEC §9.14.10（v3.139）：亲密度页签某行「去Ta家」时触发，由装配层接入好友家园。</summary>
        public event Action<FriendProfile> OnVisitFriendHome;

        private bool screenCloseButtonBuilt;

        public bool IsShown => gameObject != null && gameObject.activeSelf;

        /// <summary>SPEC §9.14.10（v3.184）：注入主 HUD 公会层，供「进入家园」页签内嵌展示。</summary>
        public void BindEmbeddedGongHui(GongHuiScreenView gongHui)
        {
            embeddedGongHui = gongHui;
        }

        public static CharacterCreationScreenView BuildInto(RectTransform canvasRect, IPlantingService plantingService)
        {
            GetOrCreate(canvasRect);
            if (instance != null)
                instance.service = plantingService;
            return instance;
        }

        public static CharacterCreationScreenView GetOrCreate(RectTransform canvasRect)
        {
            if (canvasRect == null)
            {
                UnityEngine.Debug.LogWarning("[CharacterCreationScreenView] GetOrCreate: canvasRect 为空");
                return null;
            }

            if (instance != null && instance.panelRt != null)
                return instance;

            var existing = canvasRect.Find(PanelObjectName);
            if (existing != null)
            {
                var existView = existing.GetComponent<CharacterCreationScreenView>();
                if (existView == null)
                    existView = existing.gameObject.AddComponent<CharacterCreationScreenView>();
                existView.panelRt = existing as RectTransform;
                instance = existView;
                return existView;
            }

            var prefab = Resources.Load<GameObject>(ResPrefabPath);
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, canvasRect, false);
            }
            else
            {
                UnityEngine.Debug.LogWarning(
                    "[CharacterCreationScreenView] 缺少预制体 Resources/" + ResPrefabPath +
                    "，使用运行时回退 UI；请在编辑器执行 Tools/PetDemo/Generate Character Creation Screen Prefab。");
                go = CharacterCreationScreenLayout.BuildRuntime(canvasRect);
                if (go == null)
                    return null;
            }

            go.name = PanelObjectName;
            go.SetActive(false);
            var rt = go.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            var view = go.GetComponent<CharacterCreationScreenView>();
            if (view == null)
                view = go.AddComponent<CharacterCreationScreenView>();
            view.panelRt = rt;
            instance = view;
            return view;
        }

        public void Show()
        {
            EnsureEnterHomeTopPanelRuntime();
            EnsureHomeTabPlaceholderRuntime();
            if (panelRt != null)
            {
                bool hadHomeTab = panelRt.Find("BottomTabBar/HomeTabButton") != null;
                CharacterCreationScreenLayout.EnsureBottomTabBar(panelRt);
                var bottomTabBar = panelRt.Find("BottomTabBar") as RectTransform;
                if (bottomTabBar != null)
                    CharacterCreationScreenLayout.RefreshBottomTabBarPresentation(bottomTabBar);
                if (!hadHomeTab)
                    wired = false;
            }
            EnsureFieldsFromHierarchy();
            EnsureGongHuiEmbedMount();
            EnsureScreenCloseButton();
            EnsureAddButtonTopLevel();
            WireOnce();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            // SPEC §9.14.1（v3.166）：默认隐藏加号态黑底，由 RefreshState 决定是否显示。
            SetActiveSafe(addButtonBackdrop != null ? addButtonBackdrop.gameObject : null, false);
            HideFriendListPopup();
            HideQinMiDuPopup();
            HideZhuanQianPopup();
            HideZhongDuanPopup();
            HideFriendCharacterPopup();
            HideLegacySideFriends();
            HideEnterHomePanel();
            HideHomeTabPlaceholder();
            HideGongHuiEmbeddedPanel();
            HideDressUpPanel();
            // SPEC §9.14.10（v3.146）：每次 Show 默认打开「亲密度」页签。
            SetActiveTab(0);
            ShowIntimacyPanel();
            RefreshState();
        }

        public void Hide()
        {
            HideGongHuiEmbeddedPanel();
            gameObject.SetActive(false);
        }

        private void WireOnce()
        {
            if (wired)
                return;

            if (addButton != null)
            {
                addButton.onClick.RemoveAllListeners();
                addButton.onClick.AddListener(OnAddButtonClicked);
            }
            if (intimacyTab != null)
            {
                EnsureBottomTabButton(intimacyTab);
                intimacyTab.onClick.RemoveAllListeners();
                intimacyTab.onClick.AddListener(OnIntimacyTabClicked);
            }
            if (dressUpButton != null)
            {
                EnsureBottomTabButton(dressUpButton);
                dressUpButton.onClick.RemoveAllListeners();
                dressUpButton.onClick.AddListener(OnDressUpClicked);
            }
            if (homeTabButton != null)
            {
                EnsureBottomTabButton(homeTabButton);
                homeTabButton.onClick.RemoveAllListeners();
                homeTabButton.onClick.AddListener(OnHomeTabClicked);
            }
            if (enterHomeButton != null)
            {
                EnsureBottomTabButton(enterHomeButton);
                enterHomeButton.onClick.RemoveAllListeners();
                enterHomeButton.onClick.AddListener(OnEnterHomeTabClicked);
            }
            if (roleAddFavorButton != null)
            {
                EnsureBottomTabButton(roleAddFavorButton);
                roleAddFavorButton.onClick.RemoveAllListeners();
                roleAddFavorButton.onClick.AddListener(OnRoleAddFavorClicked);
            }
            if (addFavorButton != null)
            {
                addFavorButton.onClick.RemoveAllListeners();
                addFavorButton.onClick.AddListener(OnAddFavorClicked);
            }
            if (friendListCloseButton != null)
            {
                friendListCloseButton.onClick.RemoveAllListeners();
                friendListCloseButton.onClick.AddListener(HideFriendListPopup);
            }
            if (screenCloseButton != null)
            {
                screenCloseButton.onClick.RemoveAllListeners();
                screenCloseButton.onClick.AddListener(OnScreenCloseClicked);
            }
            if (friendCellTemplate != null)
                friendCellTemplate.SetActive(false);
            // SPEC §9.14.8 第 1 点（v3.158）：topFriendCellTemplate 现为 Resources 预制体资源，不在此处 SetActive。
            if (enterHomeNavCellTemplate != null)
                enterHomeNavCellTemplate.SetActive(false);

            wired = true;
        }

        private void OnScreenCloseClicked()
        {
            HideFriendListPopup();
            HideQinMiDuPopup();
            HideZhuanQianPopup();
            HideZhongDuanPopup();
            HideFriendCharacterPopup();
            HideFriendDetailPopup();
            Hide();
            OnCloseRequested?.Invoke();
        }

        // ---- 状态机（SPEC §9.14.1） ----

        private void RefreshState()
        {
            var state = service != null ? service.GetCharacterCreation() : null;
            bool created = state != null && state.created;

            if (created)
            {
                ShowRoleState();
                return;
            }

            if (!string.IsNullOrEmpty(pendingFriendId))
                ShowNeedFavorState();
            else
                ShowPlusState();
        }

        private void ShowPlusState()
        {
            // SPEC §9.14.1（v3.166）：加号态显示全屏纯黑背景，加号浮于黑底之上（仅露 AddButton）。
            EnsureAddButtonTopLevel();
            SetActiveSafe(addButtonBackdrop != null ? addButtonBackdrop.gameObject : null, true);
            SetActiveSafe(addButton != null ? addButton.gameObject : null, true);
            SetAddButtonTopSiblingOrder();
            SetActiveSafe(needFavorPanel, false);
            SetRoleVisible(false);
        }

        private void ShowNeedFavorState()
        {
            SetActiveSafe(addButtonBackdrop != null ? addButtonBackdrop.gameObject : null, false);
            SetActiveSafe(addButton != null ? addButton.gameObject : null, false);
            SetActiveSafe(needFavorPanel, true);
            SetRoleVisible(false);
            UpdateNeedFavorText();
        }

        private void ShowRoleState()
        {
            SetActiveSafe(addButtonBackdrop != null ? addButtonBackdrop.gameObject : null, false);
            SetActiveSafe(addButton != null ? addButton.gameObject : null, false);
            SetActiveSafe(needFavorPanel, false);
            EnsureRoleSpine();
            SetRoleVisible(true);
            HideLegacySideFriends();
            PlayRoleIdleLoop();
        }

        private void UpdateNeedFavorText()
        {
            if (needFavorText == null)
                return;
            var friend = FindFriend(pendingFriendId);
            int current = friend != null ? friend.intimacy : 0;
            string name = friend != null ? friend.displayName : "好友";
            needFavorText.text = name + "\n需要好感度 " + FriendCatalog.IntimacyThreshold + "\n当前 " + current;
        }

        // ---- 好友列表弹窗（SPEC §9.14.3） ----

        private void ShowFriendListPopup()
        {
            if (friendListPopup == null)
                return;
            friendListPopup.SetActive(true);
            friendListPopup.transform.SetAsLastSibling();
            RefreshFriendList();
        }

        private void HideFriendListPopup()
        {
            if (friendListPopup != null)
                friendListPopup.SetActive(false);
        }

        private void RefreshFriendList()
        {
            if (service == null || friendListContent == null || friendCellTemplate == null)
                return;

            var sorted = FriendCatalog.SortForDisplay(service.GetFriends());
            EnsureCellCount(sorted.Count);

            for (int i = 0; i < cells.Count; i++)
            {
                if (i < sorted.Count)
                {
                    cells[i].gameObject.SetActive(true);
                    cells[i].Bind(sorted[i], OnFriendSelected);
                }
                else
                {
                    cells[i].gameObject.SetActive(false);
                }
            }
        }

        private void EnsureCellCount(int count)
        {
            while (cells.Count < count)
            {
                var cellGo = Instantiate(friendCellTemplate, friendListContent);
                cellGo.SetActive(true);
                var cell = cellGo.GetComponent<CharacterCreationFriendCellView>();
                if (cell == null)
                    cell = cellGo.AddComponent<CharacterCreationFriendCellView>();
                cell.AutoWire();
                cells.Add(cell);
            }
        }

        private void OnFriendSelected(FriendProfile friend)
        {
            if (friend == null || service == null)
                return;

            HideFriendListPopup();

            if (friend.intimacy >= FriendCatalog.IntimacyThreshold)
            {
                if (service.CreateCharacterWith(friend.id))
                {
                    pendingFriendId = null;
                    PersistSave();
                    ShowRoleState();
                    return;
                }
            }

            // 亲密度不足：进入缺好感态，记录待选好友。
            pendingFriendId = friend.id;
            ShowNeedFavorState();
        }

        // SPEC §9.14.1（v3.117）：加号直接创角（无伙伴），立即切主角态。
        private void OnAddButtonClicked()
        {
            if (service == null)
                return;
            if (service.CreateCharacterDirect())
            {
                pendingFriendId = null;
                PersistSave();
                ShowRoleState();
            }
        }

        private void OnAddFavorClicked()
        {
            if (service == null || string.IsNullOrEmpty(pendingFriendId))
                return;

            service.AddFriendFavor(pendingFriendId, 10);

            var friend = FindFriend(pendingFriendId);
            if (friend != null && friend.intimacy >= FriendCatalog.IntimacyThreshold)
            {
                if (service.CreateCharacterWith(friend.id))
                {
                    pendingFriendId = null;
                    PersistSave();
                    ShowRoleState();
                    return;
                }
            }

            UpdateNeedFavorText();
        }

        private void OnEnterHomeTabClicked()
        {
            if (activeTabIndex == TabIndexEnterHomeGongHui)
            {
                SetActiveTab(-1);
                HideGongHuiEmbeddedPanel();
                return;
            }
            SetActiveTab(TabIndexEnterHomeGongHui);
            ShowGongHuiEmbeddedPanel();
        }

        private void OnHomeTabClicked()
        {
            if (activeTabIndex == TabIndexHome)
            {
                SetActiveTab(-1);
                HideHomeTabPlaceholder();
                return;
            }
            SetActiveTab(TabIndexHome);
            ShowHomeTabPlaceholder();
        }

        // SPEC §9.14.10（v3.142）：内容区嵌入辅助 ——
        // DisplayArea 保持全屏可见（仅上方 40% 露出），各页签内容置于下方 60% 内容区（位于 BottomTabBar 之上）。
        private static void EmbedIntoContentRegion(RectTransform rt, float sideMargin = 0f)
        {
            if (rt == null)
                return;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, CharacterCreationScreenLayout.ContentRegionTopAnchorY);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(sideMargin, CharacterCreationScreenLayout.ContentRegionBottomOffset);
            rt.offsetMax = new Vector2(-sideMargin, 0f);
        }

        private static void DisableChildByName(Transform root, string name)
        {
            var t = FindDescendantByName(root, name);
            if (t != null)
                t.gameObject.SetActive(false);
        }

        private void OnEnterHomeNavClicked(string navKey)
        {
            if (string.IsNullOrEmpty(navKey))
                return;
            Hide();
            OnNavigateToBottomNav?.Invoke(navKey);
        }

        private void PersistSave()
        {
            // SPEC §9.14.4：创角完成后立即落盘，避免退出前丢失。
            Save.GameSaveCoordinator.TrySaveActiveSlot();
        }

        // ---- 底部页签栏（SPEC §9.14.10 v3.139） ----

        /// <summary>设置当前激活页签并刷新 5 个页签的高亮（互斥）。index=-1 表示无激活。</summary>
        private void SetActiveTab(int index)
        {
            activeTabIndex = index;
            SetTabHighlight(intimacyTab, index == TabIndexIntimacy);
            SetTabHighlight(dressUpButton, index == TabIndexDressUp);
            SetTabHighlight(homeTabButton, index == TabIndexHome);
            SetTabHighlight(enterHomeButton, index == TabIndexEnterHomeGongHui);
            SetTabHighlight(roleAddFavorButton, index == TabIndexAddFavor);
        }

        private static void SetTabHighlight(Button button, bool active)
        {
            if (button == null)
                return;

            // SPEC §9.14.10（v3.159+）：仅切换 IconOpen/IconClosed，背景 Image 不变色。
            var iconOpen = FindDescendantByName(button.transform, "IconOpen");
            var iconClosed = FindDescendantByName(button.transform, "IconClosed");
            if (iconOpen != null)
                iconOpen.gameObject.SetActive(active);
            if (iconClosed != null)
                iconClosed.gameObject.SetActive(!active);
        }

        /// <summary>
        /// 底部页签按钮点击修复：预制体可能禁用根 Image（仅显示 IconOpen/IconClosed），
        /// 须启用透明命中区并关闭 ColorTint，避免图标抢射线。
        /// </summary>
        private static void EnsureBottomTabButton(Button button)
        {
            if (button == null)
                return;
            button.transition = Selectable.Transition.None;

            var hitImg = button.targetGraphic as Image;
            if (hitImg == null)
                hitImg = button.GetComponent<Image>();
            if (hitImg != null)
            {
                hitImg.enabled = true;
                hitImg.raycastTarget = true;
                if (hitImg.sprite == null)
                    hitImg.color = TabBarHitColor;
                button.targetGraphic = hitImg;
            }

            var iconOpen = FindDescendantByName(button.transform, "IconOpen");
            var iconClosed = FindDescendantByName(button.transform, "IconClosed");
            if (iconOpen != null)
            {
                var img = iconOpen.GetComponent<Image>();
                if (img != null)
                    img.raycastTarget = false;
            }
            if (iconClosed != null)
            {
                var img = iconClosed.GetComponent<Image>();
                if (img != null)
                    img.raycastTarget = false;
            }

            var label = FindDescendantByName(button.transform, "Label");
            if (label != null)
                label.gameObject.SetActive(false);
        }

        /// <summary>「亲密度」页签：切换显示好友列表（隐藏中央展示区）；再次点击恢复展示区。</summary>
        private void OnIntimacyTabClicked()
        {
            if (activeTabIndex == 0)
            {
                SetActiveTab(-1);
                HideIntimacyPanel();
                return;
            }
            SetActiveTab(0);
            HideEnterHomePanel();
            HideHomeTabPlaceholder();
            HideGongHuiEmbeddedPanel();
            ShowIntimacyPanel();
        }

        // SPEC §9.14.10（v3.142）：各页签内容显示于下方 60% 内容区，上方 40% 持续显示 DisplayArea（不再隐藏展示区）。
        private void ShowIntimacyPanel()
        {
            HideEnterHomePanel();
            HideHomeTabPlaceholder();
            HideGongHuiEmbeddedPanel();
            HideDressUpPanel();
            HideZhuanQianPopup();
            HideFriendCharacterPopup();
            HideFriendDetailPopup();
            HideQinMiDuPopup();
            HideZhongDuanPopup();
            RefreshTopFriendList();
            // 兼容尚未重生成的旧 prefab：运行时强制内容区锚定（下方 60%，位于 BottomTabBar 之上）。
            if (intimacyTopPanel != null)
                EmbedIntoContentRegion(intimacyTopPanel.transform as RectTransform, CharacterCreationScreenLayout.ContentRegionSideMargin);
            SetActiveSafe(intimacyTopPanel, true);
        }

        private void HideIntimacyPanel()
        {
            SetActiveSafe(intimacyTopPanel, false);
        }

        /// <summary>「进入家园」页签：在内容区切换显示跳转列表（DisplayArea 保持可见）。</summary>
        private void ShowEnterHomePanel()
        {
            HideIntimacyPanel();
            HideDressUpPanel();
            HideZhuanQianPopup();
            HideFriendCharacterPopup();
            HideQinMiDuPopup();
            HideZhongDuanPopup();
            RefreshEnterHomeNavList();
            // 兼容尚未重生成的旧 prefab：运行时强制内容区锚定（下方 60%，位于 BottomTabBar 之上）。
            if (enterHomeTopPanel != null)
                EmbedIntoContentRegion(enterHomeTopPanel.transform as RectTransform, CharacterCreationScreenLayout.ContentRegionSideMargin);
            SetActiveSafe(enterHomeTopPanel, true);
        }

        private void HideEnterHomePanel()
        {
            SetActiveSafe(enterHomeTopPanel, false);
        }

        /// <summary>SPEC §9.14.10（v3.184）：「家园」页签占位面板。</summary>
        private void ShowHomeTabPlaceholder()
        {
            HideIntimacyPanel();
            HideEnterHomePanel();
            HideDressUpPanel();
            HideGongHuiEmbeddedPanel();
            HideZhuanQianPopup();
            HideFriendCharacterPopup();
            HideFriendDetailPopup();
            HideQinMiDuPopup();
            HideZhongDuanPopup();
            UnityEngine.Debug.Log("[CharacterCreationScreenView] 「家园」页签占位（功能待实现）。");
            if (homeTabPlaceholderPanel != null)
                EmbedIntoContentRegion(homeTabPlaceholderPanel.transform as RectTransform, CharacterCreationScreenLayout.ContentRegionSideMargin);
            SetActiveSafe(homeTabPlaceholderPanel, true);
            SetActiveSafe(displayArea, true);
        }

        private void HideHomeTabPlaceholder()
        {
            SetActiveSafe(homeTabPlaceholderPanel, false);
        }

        /// <summary>SPEC §9.14.10（v3.184）：「进入家园」页签内嵌公会场景。</summary>
        private void ShowGongHuiEmbeddedPanel()
        {
            HideIntimacyPanel();
            HideEnterHomePanel();
            HideHomeTabPlaceholder();
            HideDressUpPanel();
            HideZhuanQianPopup();
            HideFriendCharacterPopup();
            HideFriendDetailPopup();
            HideQinMiDuPopup();
            HideZhongDuanPopup();
            SetActiveSafe(displayArea, false);
            EnsureGongHuiEmbedMount();
            if (embeddedGongHui != null && gongHuiEmbedMount != null)
                embeddedGongHui.EnterCharacterCreationEmbed(gongHuiEmbedMount, CharacterCreationScreenLayout.ContentRegionBottomOffset);
            else
                UnityEngine.Debug.LogWarning("[CharacterCreationScreenView] 未注入 GongHuiScreenView，无法内嵌公会场景。");
        }

        private void HideGongHuiEmbeddedPanel()
        {
            if (embeddedGongHui != null && embeddedGongHui.IsEmbeddedInCharacterCreation)
                embeddedGongHui.ExitCharacterCreationEmbed();
            if (activeTabIndex != TabIndexDressUp)
                SetActiveSafe(displayArea, true);
        }

        /// <summary>SPEC §9.14.10（v3.142）：「装扮」页签内容收起（嵌入内容区的 DressUpPanel）。</summary>
        private void HideDressUpPanel()
        {
            if (dressUpPanel != null)
                dressUpPanel.Hide();
            SetActiveSafe(displayArea, true);
        }

        private void RefreshEnterHomeNavList()
        {
            if (enterHomeContent == null || enterHomeNavCellTemplate == null)
                return;

            EnsureEnterHomeCellCount(EnterHomeNavEntries.Length);

            for (int i = 0; i < enterHomeCells.Count; i++)
            {
                if (i < EnterHomeNavEntries.Length)
                {
                    var entry = EnterHomeNavEntries[i];
                    enterHomeCells[i].gameObject.SetActive(true);
                    enterHomeCells[i].Bind(entry.navKey, entry.displayName, entry.iconResource, OnEnterHomeNavClicked);
                }
                else
                {
                    enterHomeCells[i].gameObject.SetActive(false);
                }
            }
        }

        private void EnsureEnterHomeCellCount(int count)
        {
            while (enterHomeCells.Count < count)
            {
                var go = Instantiate(enterHomeNavCellTemplate, enterHomeContent);
                go.SetActive(true);
                var cell = go.GetComponent<EnterHomeNavCellView>();
                if (cell == null)
                    cell = go.AddComponent<EnterHomeNavCellView>();
                cell.AutoWire();
                enterHomeCells.Add(cell);
            }
        }

        // SPEC §9.14.8 第 1 点（v3.158）：列表由 TopFriendCatalog 配置表驱动，单元为独立预制体（双列网格）。
        private void RefreshTopFriendList()
        {
            if (topFriendContent == null || topFriendCellTemplate == null)
                return;

            EnsureTopFriendGridLayout();

            var sorted = GetTopFriendsForList();
            EnsureTopCellCount(sorted.Count);

            for (int i = 0; i < topCells.Count; i++)
            {
                if (i < sorted.Count)
                {
                    topCells[i].gameObject.SetActive(true);
                    topCells[i].Bind(sorted[i], OpenFriendDetailPopup, HandleIntimacyBgClick);
                }
                else
                {
                    topCells[i].gameObject.SetActive(false);
                }
            }
        }

        // SPEC §9.14.8：点击 IntimacyBg 区域。
        // Xing_2（intimacyInterrupted=false）→ QinMiDu_0 弹窗；
        // Xing_2_1（intimacyInterrupted=true）→ ZhongDuan_1 弹窗。
        private void HandleIntimacyBgClick(FriendProfile friend)
        {
            if (friend == null)
                return;
            if (friend.intimacyInterrupted)
                ShowZhongDuanPopup();
            else
                ShowQinMiDuPopup();
        }

        /// <summary>SPEC §9.14.8 第 1 点（v3.158）：从配置表读取好友并按纯亲密度降序（同分按 id 升序兜底）。</summary>
        private List<FriendProfile> GetTopFriendsForList()
        {
            var list = new List<FriendProfile>();
            var configs = TopFriendCatalog.Load();
            if (configs != null)
            {
                for (int i = 0; i < configs.Count; i++)
                {
                    if (configs[i] != null && !string.IsNullOrEmpty(configs[i].id))
                        list.Add(configs[i]);
                }
            }

            list.Sort((a, b) =>
            {
                if (a.intimacy != b.intimacy)
                    return b.intimacy.CompareTo(a.intimacy);
                return string.CompareOrdinal(a.id, b.id);
            });
            return list;
        }

        private void EnsureTopCellCount(int count)
        {
            while (topCells.Count < count)
            {
                var go = Instantiate(topFriendCellTemplate, topFriendContent);
                go.SetActive(true);
                var cell = go.GetComponent<TopFriendCellView>();
                if (cell == null)
                    cell = go.AddComponent<TopFriendCellView>();
                cell.AutoWire();
                topCells.Add(cell);
            }
        }

        // SPEC §9.14.8 第 1 点（v3.158）：运行时确保 TopFriendContent 为双列 GridLayoutGroup（兼容尚未重生成的旧 prefab）。
        private bool topFriendGridEnsured;

        private void EnsureTopFriendGridLayout()
        {
            if (topFriendGridEnsured || topFriendContent == null)
                return;
            topFriendGridEnsured = true;

            // 清除旧 prefab 内嵌的 VerticalLayoutGroup 与 TopFriendCellTemplate 模板节点。
            var vlg = topFriendContent.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
                Destroy(vlg);

            var staleTemplate = topFriendContent.Find("TopFriendCellTemplate");
            if (staleTemplate != null)
                Destroy(staleTemplate.gameObject);

            var grid = topFriendContent.GetComponent<GridLayoutGroup>();
            if (grid == null)
                grid = topFriendContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(490f, 430f);
            grid.spacing = new Vector2(16f, 16f);
            grid.padding = new RectOffset(12, 12, 12, 12);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            var fitter = topFriendContent.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = topFriendContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        // SPEC §9.14.10：去Ta家 → 由装配层接入好友家园；去找Ta / 发消息暂为占位。
        private void OnTopVisitHome(FriendProfile friend)
        {
            if (friend == null)
                return;
            OnVisitFriendHome?.Invoke(friend);
        }

        private void OnTopGoFind(FriendProfile friend)
        {
            UnityEngine.Debug.Log("[CharacterCreationScreenView] 「去找Ta」占位按钮：" + (friend != null ? friend.displayName : ""));
        }

        private void OnTopMessage(FriendProfile friend)
        {
            UnityEngine.Debug.Log("[CharacterCreationScreenView] 「发消息」占位按钮：" + (friend != null ? friend.displayName : ""));
        }

        private void OnDressUpClicked()
        {
            if (activeTabIndex == TabIndexDressUp)
            {
                SetActiveTab(-1);
                HideDressUpPanel();
                return;
            }
            SetActiveTab(TabIndexDressUp);
            HideIntimacyPanel();
            HideEnterHomePanel();
            HideHomeTabPlaceholder();
            HideGongHuiEmbeddedPanel();
            HideZhuanQianPopup();
            HideFriendCharacterPopup();
            HideQinMiDuPopup();
            OpenDressUpPanel();
        }

        /// <summary>SPEC §9.14.9 / §9.14.10（v3.144）：分屏展示装扮界面——隐藏 DisplayArea，TopHalf 屏上 40%，BottomHalf 屏下 60%。</summary>
        private void OpenDressUpPanel()
        {
            if (panelRt == null)
                return;
            if (dressUpPanel == null)
                dressUpPanel = DressUpPanelView.GetOrCreate(panelRt);
            if (dressUpPanel == null)
                return;
            dressUpPanel.Bind(service);
            dressUpPanel.Show();

            SetActiveSafe(displayArea, false);

            var dressRt = dressUpPanel.transform as RectTransform;
            DressUpPanelLayout.ApplyScreenSplitLayout(dressRt);
            // 兼容尚未重生成的旧 prefab：运行时禁用全屏遮罩与关闭按钮。
            DisableChildByName(dressRt, "Dim");
            DisableChildByName(dressRt, "CloseButton");
        }

        /// <summary>SPEC §9.14.10（v3.142）：在内容区切换赚钱介绍图 ZhuanQian（底部对齐，无关闭按钮）。</summary>
        private void OnRoleAddFavorClicked()
        {
            if (activeTabIndex == TabIndexAddFavor)
            {
                SetActiveTab(-1);
                HideZhuanQianPopup();
                return;
            }
            SetActiveTab(TabIndexAddFavor);
            HideIntimacyPanel();
            HideEnterHomePanel();
            HideHomeTabPlaceholder();
            HideGongHuiEmbeddedPanel();
            HideDressUpPanel();
            HideFriendCharacterPopup();
            HideQinMiDuPopup();
            ShowZhuanQianPopup();
        }

        // ---- 主角 Spine（SPEC §9.14.1，复用 Hero_Role_cunmin） ----

        private SkeletonDataAsset cachedRoleDataAsset;
        private bool roleDataAssetResolved;

        private void EnsureRoleSpine()
        {
            if (roleSpineBuilt || roleMount == null)
                return;
            roleSpineBuilt = true;

            SkeletonGraphic sg;
            if (TryBuildSpineInto(roleMount, RoleDisplaySize, RoleSpineDisplayScale, out sg))
                roleSkeletonGraphic = sg;
            else
                BuildRolePlaceholder(roleMount, RoleSpineDisplayScale);
        }

        /// <summary>
        /// SPEC §9.14.1：在挂点构建一份复用 <see cref="RoleResourcesPrefabPath"/> 的 Spine，并立即播放待机循环。
        /// </summary>
        private bool TryBuildSpineInto(RectTransform mount, Vector2 size, float scale, out SkeletonGraphic skeletonGraphic)
        {
            skeletonGraphic = null;
            if (mount == null)
                return false;

            var dataAsset = ResolveRoleSkeletonDataAsset();
            if (dataAsset == null)
                return false;

            var shader = Shader.Find(SkeletonGraphicShaderName);
            if (shader == null)
                return false;

            var uiMaterial = SkeletonGraphicUiMaterialFactory.CreateForPmaVertexColors(shader);
            var roleGo = new GameObject("RoleSpine");
            var rt = roleGo.AddComponent<RectTransform>();
            rt.SetParent(mount, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            rt.localScale = new Vector3(scale, scale, 1f);

            var sg = SkeletonGraphic.AddSkeletonGraphicComponent(roleGo, dataAsset, uiMaterial);
            if (sg == null || !sg.IsValid)
            {
                Destroy(roleGo);
                return false;
            }

            sg.raycastTarget = false;
            skeletonGraphic = sg;
            PlaySpineIdleLoop(sg);
            return true;
        }

        private SkeletonDataAsset ResolveRoleSkeletonDataAsset()
        {
            if (roleDataAssetResolved)
                return cachedRoleDataAsset;
            roleDataAssetResolved = true;

            var prefab = ResolveRolePrefab();
            if (prefab == null)
                return null;

            var probe = Instantiate(prefab);
            probe.SetActive(false);
            var srcAnim = probe.GetComponent<SkeletonAnimation>()
                ?? probe.GetComponentInChildren<SkeletonAnimation>(true);
            cachedRoleDataAsset = srcAnim != null ? srcAnim.skeletonDataAsset : null;
            Destroy(probe);
            return cachedRoleDataAsset;
        }

        /// <summary>SPEC §9.14.1：主角持续循环待机。</summary>
        private void PlayRoleIdleLoop()
        {
            PlaySpineIdleLoop(roleSkeletonGraphic);
        }

        /// <summary>待机动画名解析复用 §9.5 候选链；loop=true 持续播放。</summary>
        private static void PlaySpineIdleLoop(SkeletonGraphic skeletonGraphic)
        {
            if (skeletonGraphic == null)
                return;

            var idleClip = ResolveRoleIdleClip(skeletonGraphic);
            if (string.IsNullOrEmpty(idleClip))
            {
                UnityEngine.Debug.LogWarning("[CharacterCreationScreenView] 未找到可播放的待机动画。");
                return;
            }

            try
            {
                skeletonGraphic.AnimationState.SetAnimation(0, idleClip, true);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[CharacterCreationScreenView] 播放待机动画失败: " + e.Message);
            }
        }

        private static string ResolveRoleIdleClip(SkeletonGraphic skeletonGraphic)
        {
            if (skeletonGraphic?.Skeleton?.Data == null)
                return null;

            var data = skeletonGraphic.Skeleton.Data;
            for (int i = 0; i < RoleIdleAnimationFallbacks.Length; i++)
            {
                var name = RoleIdleAnimationFallbacks[i];
                if (!string.IsNullOrEmpty(name) && data.FindAnimation(name) != null)
                    return name;
            }

            var anims = data.Animations;
            if (anims != null && anims.Count > 0 && anims.Items[0] != null)
                return anims.Items[0].Name;
            return null;
        }

        private static GameObject ResolveRolePrefab()
        {
            var fromResources = Resources.Load<GameObject>(RoleResourcesPrefabPath);
            if (fromResources != null)
                return fromResources;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Scenes/Air/Role/Hero_Role_cunmin.prefab");
#else
            return null;
#endif
        }

        private void BuildRolePlaceholder(RectTransform mount, float scale = 1f)
        {
            var go = new GameObject("RolePlaceholder", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(mount, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = RoleDisplaySize;
            rt.localScale = new Vector3(scale, scale, 1f);
            go.GetComponent<Image>().color = new Color(0.3f, 0.45f, 0.7f, 1f);
        }

        private void SetRoleVisible(bool visible)
        {
            if (roleMount != null)
                roleMount.gameObject.SetActive(visible);
        }

        // ---- 好友展示（SPEC §9.14.8） ----

        /// <summary>兼容隐藏旧版侧栏好友节点（v3.117 及以前会话残留）。</summary>
        private void HideLegacySideFriends()
        {
            var left = FindDescendantByName(transform, "SideFriendLeft");
            if (left != null)
                left.gameObject.SetActive(false);
            var right = FindDescendantByName(transform, "SideFriendRight");
            if (right != null)
                right.gameObject.SetActive(false);
        }

        // ---- 好友角色立绘弹窗（SPEC §9.14.8 v3.118） ----

        private void ShowFriendCharacterPopup(int rankIndex, FriendProfile friend)
        {
            if (friend == null)
                return;

            EnsureFriendCharacterPopup();
            HideQinMiDuPopup();
            HideZhongDuanPopup();

            string characterRes = RankCharacterResources[Mathf.Clamp(rankIndex, 0, RankCharacterResources.Length - 1)];
            SetSpriteOrFallback(friendCharacterImage, characterRes, false);
            if (friendCharacterImage != null && friendCharacterImage.sprite == null)
            {
                friendCharacterImage.color = new Color(0.2f, 0.22f, 0.3f, 0.98f);
                UnityEngine.Debug.LogWarning("[CharacterCreationScreenView] 缺少角色立绘 Resources/" + characterRes + "。");
            }

            SetSpriteOrFallback(friendCharacterAvatar, friend.avatarResource, false);
            if (friendCharacterName != null)
                friendCharacterName.text = friend.displayName ?? "";

            if (friendCharacterPopup != null)
            {
                friendCharacterPopup.SetActive(true);
                friendCharacterPopup.transform.SetAsLastSibling();
            }
        }

        private void HideFriendCharacterPopup()
        {
            if (friendCharacterPopup != null)
                friendCharacterPopup.SetActive(false);
        }

        private void EnsureFriendCharacterPopup()
        {
            if (friendCharacterPopupBuilt || panelRt == null)
                return;
            friendCharacterPopupBuilt = true;

            var center = new Vector2(0.5f, 0.5f);
            var popup = CreateRect(panelRt, "FriendCharacterPopup", Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero);

            var dim = CreateRect(popup, "Dim", Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.65f);
            dimImg.raycastTarget = true;
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(HideFriendCharacterPopup);

            friendCharacterImage = CreateImageNode(popup, "CharacterImage", center, center, new Vector2(0f, 80f), new Vector2(720f, 1000f));
            friendCharacterImage.preserveAspect = true;
            friendCharacterImage.raycastTarget = true;

            friendCharacterAvatar = CreateImageNode(popup, "FriendAvatar", center, center, new Vector2(0f, -480f), new Vector2(120f, 120f));

            friendCharacterName = CreateTextNode(popup, "FriendName", "", center, center, new Vector2(0f, -580f),
                new Vector2(400f, 50f), 36, TextAnchor.MiddleCenter);

            friendCharacterPopup = popup.gameObject;
            friendCharacterPopup.SetActive(false);
        }

        // ---- 好友详情弹窗（SPEC §9.14.8 第 2 点 v3.158：中央 Spine + 三按钮） ----

        private void OpenFriendDetailPopup(FriendProfile friend)
        {
            if (friend == null)
                return;

            EnsureFriendDetailPopup();
            HideQinMiDuPopup();
            HideZhongDuanPopup();
            HideFriendCharacterPopup();

            friendDetailBound = friend;
            RebuildFriendDetailSpine(friend);

            if (friendDetailPopup != null)
            {
                friendDetailPopup.SetActive(true);
                friendDetailPopup.transform.SetAsLastSibling();
            }
        }

        private void HideFriendDetailPopup()
        {
            if (friendDetailPopup != null)
                friendDetailPopup.SetActive(false);
        }

        private void EnsureFriendDetailPopup()
        {
            if (friendDetailPopupBuilt || panelRt == null)
                return;
            friendDetailPopupBuilt = true;

            var center = new Vector2(0.5f, 0.5f);
            var popup = CreateRect(panelRt, "FriendDetailPopup", Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero);

            var dim = CreateRect(popup, "Dim", Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.65f);
            dimImg.raycastTarget = true;
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(HideFriendDetailPopup);

            // 中央 Spine 挂点（运行时按好友 spinePrefabPath 构建）。
            friendDetailSpineMount = CreateRect(popup, "SpineMount", center, center, center, new Vector2(0f, 80f), FriendDetailSpineSize);

            // 底部三按钮（去找Ta / 去Ta家 / 发消息）。
            var btnSize = new Vector2(220f, 110f);
            CreatePopupButton(popup, "GoFindButton", "去找Ta", new Vector2(0.5f, 0f), new Vector2(-260f, 120f), btnSize, OnFriendDetailGoFind);
            CreatePopupButton(popup, "VisitHomeButton", "去Ta家", new Vector2(0.5f, 0f), new Vector2(0f, 120f), btnSize, OnFriendDetailVisitHome);
            CreatePopupButton(popup, "MessageButton", "发消息", new Vector2(0.5f, 0f), new Vector2(260f, 120f), btnSize, OnFriendDetailMessage);

            friendDetailPopup = popup.gameObject;
            friendDetailPopup.SetActive(false);
        }

        private void CreatePopupButton(RectTransform parent, string name, string label,
            Vector2 anchorPivot, Vector2 anchoredPos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var rt = CreateRect(parent, name, anchorPivot, anchorPivot, anchorPivot, anchoredPos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.26f, 0.55f, 0.85f, 1f);
            img.raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;
            if (onClick != null)
                btn.onClick.AddListener(onClick);
            CreateTextNode(rt, "Label", label, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, 40, TextAnchor.MiddleCenter);
        }

        private void OnFriendDetailGoFind()
        {
            OnTopGoFind(friendDetailBound);
        }

        private void OnFriendDetailVisitHome()
        {
            OnTopVisitHome(friendDetailBound);
        }

        private void OnFriendDetailMessage()
        {
            OnTopMessage(friendDetailBound);
        }

        /// <summary>SPEC §9.14.8 第 2 点：按好友配置 spinePrefabPath 重建中央 Spine（失败回退占位）。</summary>
        private void RebuildFriendDetailSpine(FriendProfile friend)
        {
            if (friendDetailSpineMount == null)
                return;

            string prefabPath = friend != null ? friend.spinePrefabPath : null;
            // 同一 Spine 已构建则不重建。
            if (friendDetailSkeletonGraphic != null && friendDetailSpinePath == prefabPath)
            {
                PlaySpineIdleLoop(friendDetailSkeletonGraphic);
                return;
            }

            for (int i = friendDetailSpineMount.childCount - 1; i >= 0; i--)
                Destroy(friendDetailSpineMount.GetChild(i).gameObject);
            friendDetailSkeletonGraphic = null;
            friendDetailSpinePath = prefabPath;

            if (TryBuildSpineFromPrefab(friendDetailSpineMount, prefabPath, FriendDetailSpineSize, FriendDetailSpineScale, out var sg))
                friendDetailSkeletonGraphic = sg;
            else
                BuildRolePlaceholder(friendDetailSpineMount, FriendDetailSpineScale);
        }

        /// <summary>从任意 Resources 预制体路径探针取 SkeletonDataAsset 并构建 SkeletonGraphic（仿 §9.14.1 TryBuildSpineInto）。</summary>
        private bool TryBuildSpineFromPrefab(RectTransform mount, string prefabPath, Vector2 size, float scale, out SkeletonGraphic skeletonGraphic)
        {
            skeletonGraphic = null;
            if (mount == null || string.IsNullOrEmpty(prefabPath))
                return false;

            var prefab = Resources.Load<GameObject>(prefabPath);
            if (prefab == null)
            {
                UnityEngine.Debug.LogWarning("[CharacterCreationScreenView] 缺少 Spine 预制体 Resources/" + prefabPath + "。");
                return false;
            }

            var probe = Instantiate(prefab);
            probe.SetActive(false);
            var srcAnim = probe.GetComponent<SkeletonAnimation>()
                ?? probe.GetComponentInChildren<SkeletonAnimation>(true);
            var dataAsset = srcAnim != null ? srcAnim.skeletonDataAsset : null;
            Destroy(probe);
            if (dataAsset == null)
                return false;

            var shader = Shader.Find(SkeletonGraphicShaderName);
            if (shader == null)
                return false;

            var uiMaterial = SkeletonGraphicUiMaterialFactory.CreateForPmaVertexColors(shader);
            var go = new GameObject("FriendDetailSpine");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(mount, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            rt.localScale = new Vector3(scale, scale, 1f);

            var sg = SkeletonGraphic.AddSkeletonGraphicComponent(go, dataAsset, uiMaterial);
            if (sg == null || !sg.IsValid)
            {
                Destroy(go);
                return false;
            }

            sg.raycastTarget = false;
            skeletonGraphic = sg;
            PlaySpineIdleLoop(sg);
            return true;
        }

        // ---- 介绍图弹窗 QinMiDu_0（SPEC §9.14.8） ----

        private void ShowQinMiDuPopup()
        {
            HideFriendCharacterPopup();
            HideZhongDuanPopup();
            EnsureQinMiDuPopup();
            if (qinMiDuPopup != null)
            {
                qinMiDuPopup.SetActive(true);
                qinMiDuPopup.transform.SetAsLastSibling();
            }
        }

        private void HideQinMiDuPopup()
        {
            if (qinMiDuPopup != null)
                qinMiDuPopup.SetActive(false);
        }

        private void EnsureQinMiDuPopup()
        {
            if (qinMiDuBuilt || panelRt == null)
                return;
            qinMiDuBuilt = true;

            var center = new Vector2(0.5f, 0.5f);
            var popup = CreateRect(panelRt, "QinMiDuPopup", Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero);

            // 半透明遮罩（点击关闭）。
            var dim = CreateRect(popup, "Dim", Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.65f);
            dimImg.raycastTarget = true;
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(HideQinMiDuPopup);

            // 居中大图（拦截点击，避免穿透到遮罩）。
            var img = CreateImageNode(popup, "QinMiDuImage", center, center, Vector2.zero, new Vector2(900f, 1200f));
            var sprite = Resources.Load<Sprite>(QinMiDuResource);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = true;
            }
            else
            {
                img.color = new Color(0.2f, 0.22f, 0.3f, 0.98f);
                UnityEngine.Debug.LogWarning("[CharacterCreationScreenView] 缺少介绍图 Resources/" + QinMiDuResource + "。");
            }
            img.raycastTarget = true;

            qinMiDuPopup = popup.gameObject;
            qinMiDuPopup.SetActive(false);
        }

        // ---- ZhongDuan 提示弹窗（SPEC §9.14.8 v3.131） ----

        private void ShowZhongDuanPopup()
        {
            HideQinMiDuPopup();
            HideFriendCharacterPopup();
            EnsureZhongDuanPopup();
            if (zhongDuanPopup != null)
            {
                zhongDuanPopup.SetActive(true);
                zhongDuanPopup.transform.SetAsLastSibling();
            }
        }

        private void HideZhongDuanPopup()
        {
            if (zhongDuanPopup != null)
                zhongDuanPopup.SetActive(false);
        }

        private void EnsureZhongDuanPopup()
        {
            if (zhongDuanBuilt || panelRt == null)
                return;
            zhongDuanBuilt = true;

            var center = new Vector2(0.5f, 0.5f);
            var popup = CreateRect(panelRt, "ZhongDuanPopup", Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero);

            var dim = CreateRect(popup, "Dim", Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.65f);
            dimImg.raycastTarget = true;
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(HideZhongDuanPopup);

            var img = CreateImageNode(popup, "ZhongDuanImage", center, center, Vector2.zero, new Vector2(900f, 1200f));
            var sprite = Resources.Load<Sprite>(ZhongDuanPopupResource);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = true;
            }
            else
            {
                img.color = new Color(0.2f, 0.22f, 0.3f, 0.98f);
                UnityEngine.Debug.LogWarning("[CharacterCreationScreenView] 缺少提示图 Resources/" + ZhongDuanPopupResource + "。");
            }
            img.raycastTarget = true;

            zhongDuanPopup = popup.gameObject;
            zhongDuanPopup.SetActive(false);
        }

        // ---- 赚钱介绍图弹窗 ZhuanQian（SPEC §9.14.8 v3.121） ----

        private void ShowZhuanQianPopup()
        {
            EnsureZhuanQianPopup();
            if (zhuanQianPopup != null)
            {
                zhuanQianPopup.SetActive(true);
                zhuanQianPopup.transform.SetAsLastSibling();
            }
            // 任务列表随弹窗显示刷新（恢复行状态）。
            if (taskListPanel != null)
                taskListPanel.Show();
        }

        private void HideZhuanQianPopup()
        {
            if (zhuanQianPopup != null)
                zhuanQianPopup.SetActive(false);
        }

        // SPEC §9.14.10（v3.142）：ZhuanQian 由全屏覆盖层改为内容区面板（下方 60%，位于 BottomTabBar 之上），
        // 背景图底部对齐（pivot.y=0、贴内容区底边、preserveAspect），删除右上角关闭按钮。
        private void EnsureZhuanQianPopup()
        {
            if (zhuanQianBuilt || panelRt == null)
                return;
            zhuanQianBuilt = true;

            var popup = CreateContentRegionRect(panelRt, "ZhuanQianPopup");

            // SPEC §9.14.8（v3.121）：加好感页签任务列表 Demo —— 弹窗内直接承载可滑动任务列表容器。
            // 容器水平拉伸、四周留边距，铺满弹窗。
            var taskListContainer = CreateRect(popup, "TaskListContainer",
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            taskListContainer.offsetMin = new Vector2(24f, 24f);
            taskListContainer.offsetMax = new Vector2(-24f, -24f);

            var canvas = GetComponentInParent<Canvas>();
            var canvasRect = canvas != null ? canvas.transform as RectTransform : panelRt;
            taskListPanel = TaskListPanelView.BuildInto(taskListContainer, canvasRect, OnTaskGoToClicked);

            zhuanQianPopup = popup.gameObject;
            zhuanQianPopup.SetActive(false);
        }

        /// <summary>任务列表「前往」点击回调：隐藏创角界面并复用 OnNavigateToBottomNav 跳转到已存在界面。</summary>
        private void OnTaskGoToClicked(string navKey)
        {
            if (string.IsNullOrEmpty(navKey))
                return;
            // 跳转走开 CharacterCreationScreen，返回后重新打开 ZhuanQianPopup 时行已显示「领取奖励」。
            Hide();
            OnNavigateToBottomNav?.Invoke(navKey);
        }

        /// <summary>SPEC §9.14.10（v3.142）：创建占据屏幕下方 60% 内容区（位于 BottomTabBar 之上）的容器。</summary>
        private static RectTransform CreateContentRegionRect(RectTransform parent, string name)
        {
            var rt = CreateRect(parent, name,
                new Vector2(0f, 0f), new Vector2(1f, CharacterCreationScreenLayout.ContentRegionTopAnchorY),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.offsetMin = new Vector2(0f, CharacterCreationScreenLayout.ContentRegionBottomOffset);
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        // ---- 编程构建辅助 ----

        private static RectTransform CreateStretchRect(RectTransform parent, string name)
        {
            var center = new Vector2(0.5f, 0.5f);
            var rt = CreateRect(parent, name, Vector2.zero, Vector2.one, center, Vector2.zero, Vector2.zero);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static RectTransform CreateRect(
            RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return rt;
        }

        private static Image CreateImageNode(
            RectTransform parent, string name,
            Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var rt = CreateRect(parent, name, anchor, anchor, pivot, anchoredPos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        private static Text CreateTextNode(
            RectTransform parent, string name, string content,
            Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, Vector2 size, int fontSize, TextAnchor align)
        {
            var rt = CreateRect(parent, name, anchor, anchor, pivot, anchoredPos, size);
            var txt = rt.gameObject.AddComponent<Text>();
            txt.text = content;
            txt.font = FarmGridView.LoadBuiltinFont();
            txt.fontSize = fontSize;
            txt.alignment = align;
            txt.color = Color.black;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        private static void SetSpriteOrFallback(Image img, string resource, bool isIcon)
        {
            if (img == null)
                return;
            Sprite sprite = !string.IsNullOrEmpty(resource) ? Resources.Load<Sprite>(resource) : null;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = true;
            }
            else
            {
                img.sprite = null;
                img.color = isIcon ? new Color(0.9f, 0.78f, 0.3f, 1f) : AvatarFallbackColor;
            }
        }

        // ---- 工具 ----

        private FriendProfile FindFriend(string friendId)
        {
            if (service == null || string.IsNullOrEmpty(friendId))
                return null;
            var friends = service.GetFriends();
            for (int i = 0; i < friends.Count; i++)
            {
                if (friends[i] != null && string.Equals(friends[i].id, friendId, StringComparison.Ordinal))
                    return friends[i];
            }
            return null;
        }

        private static void SetActiveSafe(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
                go.SetActive(active);
        }

        private void EnsureFieldsFromHierarchy()
        {
            if (background == null)
                background = FindImage("Background");
            if (addButton == null)
                addButton = FindButton("AddButton");
            if (addButtonBackdrop == null)
                addButtonBackdrop = FindImage("AddButtonBackdrop");
            if (roleMount == null)
                roleMount = FindRect("RoleMount");
            if (enterHomeButton == null)
                enterHomeButton = FindButton("EnterHomeButton");
            if (needFavorPanel == null)
                needFavorPanel = FindGo("NeedFavorPanel");
            if (needFavorText == null)
                needFavorText = FindText("NeedFavorText");
            if (addFavorButton == null)
                addFavorButton = FindButton("AddFavorButton");
            if (friendListPopup == null)
                friendListPopup = FindGo("FriendListPopup");
            if (friendListContent == null)
                friendListContent = FindRect("Content");
            if (friendCellTemplate == null)
            {
                var t = FindDescendantByName(transform, "FriendCellTemplate");
                friendCellTemplate = t != null ? t.gameObject : null;
            }
            if (friendListCloseButton == null)
                friendListCloseButton = FindButton("CloseButton");
            if (screenCloseButton == null)
            {
                var t = transform.Find("ScreenCloseButton");
                if (t != null)
                    screenCloseButton = t.GetComponent<Button>();
            }

            // SPEC §9.14.10（v3.139）：底部页签栏与亲密度好友列表节点。
            if (displayArea == null)
                displayArea = FindGo("DisplayArea");
            if (intimacyTab == null)
                intimacyTab = FindButton("IntimacyTab");
            if (dressUpButton == null)
                dressUpButton = FindButton("DressUpButton");
            if (homeTabButton == null)
                homeTabButton = FindButton("HomeTabButton");
            if (roleAddFavorButton == null)
                roleAddFavorButton = FindButton("RoleAddFavorButton");
            if (homeTabPlaceholderPanel == null)
                homeTabPlaceholderPanel = FindGo("HomeTabPlaceholderPanel");
            if (intimacyTopPanel == null)
                intimacyTopPanel = FindGo("IntimacyTopPanel");
            if (topFriendContent == null)
                topFriendContent = FindRect("TopFriendContent");
            if (topFriendCellTemplate == null)
            {
                // SPEC §9.14.8 第 1 点（v3.158）：单元改为独立预制体，从 Resources 加载作为实例化模板。
                topFriendCellTemplate = Resources.Load<GameObject>(TopFriendCellPrefabPath);
                if (topFriendCellTemplate == null)
                {
                    // 兼容尚未运行预制体生成器：用共享布局代码运行时构建一个隐藏模板（与 prefab 结构一致）。
                    var runtimeTemplate = CharacterCreationScreenLayout.BuildTopFriendCellRoot(transform as RectTransform);
                    if (runtimeTemplate != null)
                    {
                        runtimeTemplate.name = "TopFriendCellRuntimeTemplate";
                        runtimeTemplate.SetActive(false);
                        topFriendCellTemplate = runtimeTemplate;
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning("[CharacterCreationScreenView] 无法加载或构建 TopFriendCell 模板。");
                    }
                }
            }
            if (enterHomeTopPanel == null)
                enterHomeTopPanel = FindGo("EnterHomeTopPanel");
            if (enterHomeContent == null)
                enterHomeContent = FindRect("EnterHomeContent");
            if (enterHomeNavCellTemplate == null)
            {
                var t = FindDescendantByName(transform, "EnterHomeNavCellTemplate");
                enterHomeNavCellTemplate = t != null ? t.gameObject : null;
            }
        }

        /// <summary>旧 prefab 无 EnterHomeTopPanel 时运行时补建（与 EnsureScreenCloseButton 一致）。</summary>
        private void EnsureEnterHomeTopPanelRuntime()
        {
            if (enterHomeTopPanel != null || panelRt == null)
                return;
            CharacterCreationScreenLayout.EnsureEnterHomeTopPanel(panelRt);
        }

        /// <summary>旧 prefab 无 HomeTabPlaceholderPanel 时运行时补建。</summary>
        private void EnsureHomeTabPlaceholderRuntime()
        {
            if (homeTabPlaceholderPanel != null || panelRt == null)
                return;
            CharacterCreationScreenLayout.EnsureHomeTabPlaceholderPanel(panelRt);
            homeTabPlaceholderPanel = FindGo("HomeTabPlaceholderPanel");
        }

        /// <summary>SPEC §9.14.10（v3.184）：公会内嵌挂点，位于 BottomTabBar 之下（渲染顺序）。</summary>
        private void EnsureGongHuiEmbedMount()
        {
            if (gongHuiEmbedMount != null || panelRt == null)
                return;

            var existing = panelRt.Find("GongHuiEmbedMount") as RectTransform;
            if (existing != null)
            {
                gongHuiEmbedMount = existing;
                return;
            }

            gongHuiEmbedMount = CreateRect(panelRt, "GongHuiEmbedMount",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            gongHuiEmbedMount.offsetMin = new Vector2(0f, CharacterCreationScreenLayout.ContentRegionBottomOffset);
            gongHuiEmbedMount.offsetMax = Vector2.zero;

            var bottomTabBar = FindDescendantByName(transform, "BottomTabBar");
            if (bottomTabBar != null)
                gongHuiEmbedMount.SetSiblingIndex(bottomTabBar.GetSiblingIndex());
        }

        /// <summary>旧版 prefab 无 ScreenCloseButton 时运行时补建（与 Layout 一致）。</summary>
        private void EnsureScreenCloseButton()
        {
            if (screenCloseButton != null || screenCloseButtonBuilt || panelRt == null)
                return;

            var existing = panelRt.Find("ScreenCloseButton");
            if (existing != null)
            {
                screenCloseButton = existing.GetComponent<Button>();
                return;
            }

            var closeRt = CreateRect(panelRt, "ScreenCloseButton",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-20f, -20f), PopupCloseButtonSize);
            var closeImg = closeRt.gameObject.AddComponent<Image>();
            closeImg.color = new Color(0.25f, 0.22f, 0.32f, 0.95f);
            closeImg.raycastTarget = true;
            screenCloseButton = closeRt.gameObject.AddComponent<Button>();
            screenCloseButton.transition = Selectable.Transition.None;
            screenCloseButton.targetGraphic = closeImg;

            var labelRt = CreateStretchRect(closeRt, "Label");
            var closeLabel = labelRt.gameObject.AddComponent<Text>();
            closeLabel.text = "×";
            closeLabel.font = FarmGridView.LoadBuiltinFont();
            closeLabel.fontSize = 44;
            closeLabel.alignment = TextAnchor.MiddleCenter;
            closeLabel.color = Color.black;
            closeLabel.raycastTarget = false;

            screenCloseButtonBuilt = true;
            screenCloseButton.onClick.RemoveAllListeners();
            screenCloseButton.onClick.AddListener(OnScreenCloseClicked);
        }

        /// <summary>
        /// SPEC §9.14.1（v3.166）：确保 AddButton 位于根节点最高层级，并补建/置顶全屏纯黑背景。
        /// 兼容 AddButton 仍位于 DisplayArea 下的旧预制体（运行时重挂到 panelRt）。
        /// </summary>
        private void EnsureAddButtonTopLevel()
        {
            if (panelRt == null)
                return;

            if (addButton != null && addButton.transform.parent != panelRt)
                addButton.transform.SetParent(panelRt, false);

            if (addButtonBackdrop == null)
            {
                var existing = FindDescendantByName(transform, "AddButtonBackdrop");
                if (existing != null)
                    addButtonBackdrop = existing.GetComponent<Image>();
                if (addButtonBackdrop == null)
                    addButtonBackdrop = CharacterCreationScreenLayout.CreateAddButtonBackdrop(panelRt);
            }

            if (addButtonBackdrop != null && addButtonBackdrop.transform.parent != panelRt)
                addButtonBackdrop.transform.SetParent(panelRt, false);

            SetAddButtonTopSiblingOrder();
        }

        /// <summary>黑底在 AddButton 之下、其余全部 UI 之上：先置顶黑底，再置顶加号。</summary>
        private void SetAddButtonTopSiblingOrder()
        {
            if (addButtonBackdrop != null)
                addButtonBackdrop.transform.SetAsLastSibling();
            if (addButton != null)
                addButton.transform.SetAsLastSibling();
        }

        private Image FindImage(string name)
        {
            var t = FindDescendantByName(transform, name);
            return t != null ? t.GetComponent<Image>() : null;
        }

        private Button FindButton(string name)
        {
            var t = FindDescendantByName(transform, name);
            return t != null ? t.GetComponent<Button>() : null;
        }

        private Text FindText(string name)
        {
            var t = FindDescendantByName(transform, name);
            return t != null ? t.GetComponent<Text>() : null;
        }

        private RectTransform FindRect(string name)
        {
            var t = FindDescendantByName(transform, name);
            return t as RectTransform;
        }

        private GameObject FindGo(string name)
        {
            var t = FindDescendantByName(transform, name);
            return t != null ? t.gameObject : null;
        }

        private static Transform FindDescendantByName(Transform root, string name)
        {
            if (root == null)
                return null;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (string.Equals(child.name, name, StringComparison.Ordinal))
                    return child;
                var found = FindDescendantByName(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
