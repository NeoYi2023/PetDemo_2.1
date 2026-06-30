// SPEC §9.14.9：装扮界面（预制体 DressUpPanel）。
// 上下分栏：上部并排展示玩家立绘 + 最高亲密度好友立绘 + 好友头上玩家头像与好友名字 + 亲密度图标/数值；
// 下部商店含 4 个页签（动作/装扮/呼唤/聊天），默认「动作」。
using System;
using System.Collections.Generic;
using PetDemo.Core;
using PetDemo.Farm;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class DressUpPanelView : MonoBehaviour
    {
        public const string ResPrefabPath = "Prefabs/Farm/DressUpPanel";
        public const string ItemCellPrefabPath = "Prefabs/Farm/DressUpItemCell";
        public const string PanelObjectName = "DressUpPanel";

        // SPEC §9.14.9：玩家身份为前端固定值（不读写存档）。
        private const string DressTabDefaultPortrait = "AirUI/WanJia_1";
        private const string TransformTabDefaultPortrait = "AirUI/WanJia_6";
        private const string PlayerAvatarResource = "AirUI/WanJia_icon_1";
        private const string FriendPortraitResource = "AirUI/WanJia_6";
        private const string IntimacyIconResource = "AirUI/Xing_2";
        private const string TabSelectedResource = "AirUI/SheJiao_Sheet_3";
        private static readonly Color TabImageInactiveColor = new Color(1f, 1f, 1f, 0f);

        private static readonly Color PortraitFallback = new Color(0.3f, 0.36f, 0.46f, 1f);
        private static readonly Color IconFallback = new Color(0.9f, 0.78f, 0.3f, 1f);

        private static DressUpPanelView instance;

        [SerializeField] private Button dimButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Image playerRole;
        [SerializeField] private Image friendRole;
        [SerializeField] private RectTransform intimacyPanel;
        [SerializeField] private Image playerAvatar;
        [SerializeField] private Text playerName;
        [SerializeField] private Image intimacyIcon;
        [SerializeField] private Text intimacyText;
        [SerializeField] private RectTransform itemContent;
        [SerializeField] private GameObject itemCellTemplate;
        [SerializeField] private SkeletonDataAsset playerActionSkeletonData;
        [SerializeField] private SkeletonDataAsset friendActionSkeletonData;
        private readonly List<Button> tabButtons = new List<Button>();
        private readonly List<Image> tabImages = new List<Image>();
        private readonly List<Text> tabLabels = new List<Text>();
        private const int TabLabelActiveFontSize = 40;
        private const int TabLabelInactiveFontSize = 36;
        private static readonly Color TabLabelActiveColor = Color.white;
        private static readonly Color TabLabelInactiveColor = new Color(0.5921569f, 0.6392157f, 0.79607844f, 1f);

        private RectTransform panelRt;
        private IPlantingService service;
        private bool wired;
        private int activeTab = -1;
        private DressUpActionSpinePresenter actionSpinePresenter;
        private readonly Dictionary<string, DressUpItemCellView> itemCells = new Dictionary<string, DressUpItemCellView>();
        private string selectedItemId;

        public bool IsShown => gameObject != null && gameObject.activeSelf;

        public static DressUpPanelView GetOrCreate(RectTransform parentRect)
        {
            if (parentRect == null)
            {
                UnityEngine.Debug.LogWarning("[DressUpPanelView] GetOrCreate: parentRect 为空");
                return null;
            }

            if (instance != null && instance.panelRt != null)
                return instance;

            var existing = parentRect.Find(PanelObjectName);
            if (existing != null)
            {
                var existView = existing.GetComponent<DressUpPanelView>();
                if (existView == null)
                    existView = existing.gameObject.AddComponent<DressUpPanelView>();
                existView.panelRt = existing as RectTransform;
                instance = existView;
                return existView;
            }

            var prefab = Resources.Load<GameObject>(ResPrefabPath);
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, parentRect, false);
            }
            else
            {
                UnityEngine.Debug.LogWarning(
                    "[DressUpPanelView] 缺少预制体 Resources/" + ResPrefabPath +
                    "，使用运行时回退 UI；请在编辑器执行 Tools/PetDemo/Generate Dress-Up Panel Prefab。");
                go = DressUpPanelLayout.BuildRuntime(parentRect);
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

            var view = go.GetComponent<DressUpPanelView>();
            if (view == null)
                view = go.AddComponent<DressUpPanelView>();
            view.panelRt = rt;
            instance = view;
            return view;
        }

        public void Bind(IPlantingService plantingService)
        {
            service = plantingService;
        }

        public void Show()
        {
            EnsureFieldsFromHierarchy();
            WireOnce();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            SelectTab(0);
        }

        public void Hide()
        {
            TeardownActionSpine();
            gameObject.SetActive(false);
        }

        private void WireOnce()
        {
            if (wired)
                return;

            if (dimButton != null)
            {
                dimButton.onClick.RemoveAllListeners();
                dimButton.onClick.AddListener(Hide);
            }
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Hide);
            }
            for (int i = 0; i < tabButtons.Count; i++)
            {
                int index = i;
                if (tabButtons[i] == null)
                    continue;
                tabButtons[i].onClick.RemoveAllListeners();
                tabButtons[i].onClick.AddListener(() => SelectTab(index));
            }

            wired = true;
        }

        // ---- 上部展示（SPEC §9.14.9） ----

        private void RefreshTopHalfFriendInfo()
        {
            bool skipRolePortraits = activeTab == DressUpPanelLayout.ActionTabIndex
                && actionSpinePresenter != null
                && actionSpinePresenter.IsActive;

            if (!skipRolePortraits)
            {
                SetSpriteOrFallback(friendRole, FriendPortraitResource, PortraitFallback);
            }

            SetSpriteOrFallback(playerAvatar, PlayerAvatarResource, PortraitFallback);
            SetSpriteOrFallback(intimacyIcon, IntimacyIconResource, IconFallback);

            var friend = GetTopFriendByIntimacy();
            if (playerName != null)
                playerName.text = friend != null ? friend.displayName : "";

            int intimacy = friend != null ? friend.intimacy : 0;
            if (intimacyText != null)
                intimacyText.text = intimacy.ToString();
        }

        /// <summary>SPEC §9.14.9（v3.152）：按页签恢复 PlayerRole 默认立绘。</summary>
        private void RefreshPlayerRoleForTab(int tabIndex)
        {
            if (tabIndex == DressUpPanelLayout.ActionTabIndex)
            {
                if (actionSpinePresenter == null || !actionSpinePresenter.IsActive)
                    RefreshPlayerRolePortrait(GetDefaultPlayerPortraitForTab(tabIndex));
                return;
            }

            RefreshPlayerRolePortrait(GetDefaultPlayerPortraitForTab(tabIndex));
        }

        private static string GetDefaultPlayerPortraitForTab(int tabIndex)
        {
            if (tabIndex == 1)
                return TransformTabDefaultPortrait;
            return DressTabDefaultPortrait;
        }

        private void RefreshPlayerRolePortrait(string resource)
        {
            SetSpriteOrFallback(playerRole, resource, PortraitFallback);
        }

        /// <summary>取亲密度最高的好友（纯亲密度降序，同分按 id 升序兜底；同 §9.14.8）。</summary>
        private FriendProfile GetTopFriendByIntimacy()
        {
            if (service == null)
                return null;

            var friends = service.GetFriends();
            if (friends == null)
                return null;

            FriendProfile best = null;
            for (int i = 0; i < friends.Count; i++)
            {
                var f = friends[i];
                if (f == null || string.IsNullOrEmpty(f.id))
                    continue;
                if (best == null
                    || f.intimacy > best.intimacy
                    || (f.intimacy == best.intimacy && string.CompareOrdinal(f.id, best.id) < 0))
                {
                    best = f;
                }
            }
            return best;
        }

        // ---- 商店页签（SPEC §9.14.9） ----

        private void SelectTab(int index)
        {
            if (index < 0)
                index = 0;

            if (activeTab == DressUpPanelLayout.ActionTabIndex && index != DressUpPanelLayout.ActionTabIndex)
                TeardownActionSpine();

            int previousTab = activeTab;
            activeTab = index;

            if (previousTab != activeTab)
            {
                if (previousTab >= 0)
                {
                    ApplyTabImageSprite(previousTab, false);
                    ApplyTabLabelInactive(previousTab);
                }

                ApplyTabImageSprite(activeTab, true);
                ApplyTabLabelActive(activeTab);
            }

            PopulateItems(index);
            RefreshTopHalfLayout(index);
            RefreshTopHalfFriendInfo();
            RefreshPlayerRoleForTab(index);

            // Tab0 默认选中第一个道具后，RefreshPlayerRoleForTab 会设 WanJia_1，此处恢复为选中道具预览。
            if (index == 0 && !string.IsNullOrEmpty(selectedItemId))
                ApplySelectedItemPortraitPreview();
        }

        /// <summary>SPEC §9.14.9（v3.151）：按页签切换 TopHalf 单/双立绘布局。</summary>
        private void RefreshTopHalfLayout(int tabIndex)
        {
            DressUpPanelLayout.ApplyTopHalfRoleLayout(
                playerRole != null ? playerRole.rectTransform : null,
                friendRole != null ? friendRole.rectTransform : null,
                intimacyPanel,
                tabIndex);
        }

        private void ApplyTabLabelActive(int tabIndex)
        {
            if (tabIndex < 0 || tabIndex >= tabLabels.Count || tabLabels[tabIndex] == null)
                return;

            tabLabels[tabIndex].fontSize = TabLabelActiveFontSize;
            tabLabels[tabIndex].color = TabLabelActiveColor;
        }

        private void ApplyTabLabelInactive(int tabIndex)
        {
            if (tabIndex < 0 || tabIndex >= tabLabels.Count || tabLabels[tabIndex] == null)
                return;

            tabLabels[tabIndex].fontSize = TabLabelInactiveFontSize;
            tabLabels[tabIndex].color = TabLabelInactiveColor;
        }

        private void ApplyTabImageSprite(int tabIndex, bool selected)
        {
            if (tabIndex < 0 || tabIndex >= tabImages.Count || tabImages[tabIndex] == null)
                return;

            if (!selected)
            {
                tabImages[tabIndex].sprite = null;
                tabImages[tabIndex].color = TabImageInactiveColor;
                return;
            }

            var sprite = Resources.Load<Sprite>(TabSelectedResource);
            if (sprite != null)
            {
                tabImages[tabIndex].sprite = sprite;
                tabImages[tabIndex].color = Color.white;
            }
            else
            {
                tabImages[tabIndex].sprite = null;
                tabImages[tabIndex].color = new Color(0.55f, 0.5f, 0.42f, 1f);
            }
        }

        // ---- 道具网格（SPEC §9.14.9 v3.148） ----

        /// <summary>按配置表渲染指定 Tab 的售卖道具网格（每行 3 个、可竖向滑动）。</summary>
        private void PopulateItems(int tabIndex)
        {
            if (itemContent == null)
                return;

            itemCells.Clear();
            selectedItemId = null;

            for (int i = itemContent.childCount - 1; i >= 0; i--)
                Destroy(itemContent.GetChild(i).gameObject);

            ConfigureGridCellSize();

            var items = DressUpItemCatalog.GetItemsByTab(tabIndex);
            for (int i = 0; i < items.Count; i++)
                BuildItemCell(items[i]);

            // SPEC §9.14.9（v3.160）：Tab0 默认选中排序后第一个道具。
            if (tabIndex == 0 && items.Count > 0)
                SelectItemCell(items[0].itemId);
        }

        /// <summary>按视口实际宽度等分 3 列计算单元尺寸（强制刷新一次布局以拿到有效宽度）。</summary>
        private void ConfigureGridCellSize()
        {
            var grid = itemContent.GetComponent<GridLayoutGroup>();
            if (grid == null)
                return;

            Canvas.ForceUpdateCanvases();
            float width = itemContent.rect.width;
            if (width <= 1f)
                return; // 宽度尚未生效则保留默认值。

            float pad = DressUpPanelLayout.ItemGridPadding;
            float spacing = DressUpPanelLayout.ItemGridSpacing;
            int cols = DressUpPanelLayout.ItemColumns;
            float cellWidth = (width - pad * 2f - spacing * (cols - 1)) / cols;
            if (cellWidth <= 1f)
                return;
            grid.cellSize = new Vector2(cellWidth, cellWidth * DressUpPanelLayout.ItemCellAspect);
        }

        private void BuildItemCell(DressUpItemConfig config)
        {
            if (config == null || itemContent == null)
                return;

            EnsureItemCellTemplate();
            if (itemCellTemplate == null)
                return;

            var go = Instantiate(itemCellTemplate, itemContent);
            go.SetActive(true);
            var cell = go.GetComponent<DressUpItemCellView>();
            if (cell == null)
                cell = go.AddComponent<DressUpItemCellView>();
            cell.AutoWire();
            cell.Bind(config, OnItemClicked);
            itemCells[config.itemId] = cell;
        }

        /// <summary>SPEC §9.14.9：点击道具 → Tab0/Tab1 预览立绘；Tab2 Spine 动作预览；Tab0/1/2 选中叠加；介绍界面暂未实现。</summary>
        private void OnItemClicked(DressUpItemConfig config)
        {
            if (config == null)
                return;

            if (activeTab == 0 || activeTab == 1 || activeTab == DressUpPanelLayout.ActionTabIndex)
                SelectItemCell(config.itemId);

            if ((activeTab == 0 || activeTab == 1) && !string.IsNullOrEmpty(config.icon))
                RefreshPlayerRolePortrait(config.icon);
            else if (activeTab == DressUpPanelLayout.ActionTabIndex)
                PlayActionTabPreview(config);

            UnityEngine.Debug.Log("[DressUpPanelView] 点击道具 " + config.itemId + "（介绍界面后续补充）：" + config.description);
        }

        private void ClearItemSelection()
        {
            if (string.IsNullOrEmpty(selectedItemId))
                return;

            if (itemCells.TryGetValue(selectedItemId, out var cell) && cell != null)
                cell.SetSelected(false);

            selectedItemId = null;
        }

        private void SelectItemCell(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return;

            ClearItemSelection();

            if (!itemCells.TryGetValue(itemId, out var cell) || cell == null)
                return;

            selectedItemId = itemId;
            cell.SetSelected(true);
        }

        private void ApplySelectedItemPortraitPreview()
        {
            if (string.IsNullOrEmpty(selectedItemId))
                return;

            var items = DressUpItemCatalog.GetItemsByTab(0);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].itemId == selectedItemId && !string.IsNullOrEmpty(items[i].icon))
                {
                    RefreshPlayerRolePortrait(items[i].icon);
                    return;
                }
            }
        }

        private void EnsureItemCellTemplate()
        {
            if (itemCellTemplate != null)
                return;

            itemCellTemplate = Resources.Load<GameObject>(ItemCellPrefabPath);
            if (itemCellTemplate == null)
            {
                var runtimeTemplate = DressUpPanelLayout.BuildDressUpItemCellRoot(transform as RectTransform);
                if (runtimeTemplate != null)
                {
                    runtimeTemplate.name = "DressUpItemCellRuntimeTemplate";
                    runtimeTemplate.SetActive(false);
                    itemCellTemplate = runtimeTemplate;
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[DressUpPanelView] 无法加载或构建 DressUpItemCell 模板。");
                }
            }
        }

        private void PlayActionTabPreview(DressUpItemConfig config)
        {
            var presenter = EnsureActionSpinePresenter();
            if (presenter == null)
                return;

            if (!presenter.EnsureBuilt())
                return;

            presenter.ShowStandby();
            presenter.PlayActionForItem(config.itemId, this);
        }

        private DressUpActionSpinePresenter EnsureActionSpinePresenter()
        {
            if (actionSpinePresenter != null)
                return actionSpinePresenter;

            actionSpinePresenter = new DressUpActionSpinePresenter(
                playerRole != null ? playerRole.rectTransform : null,
                friendRole != null ? friendRole.rectTransform : null,
                playerRole,
                friendRole,
                playerActionSkeletonData,
                friendActionSkeletonData);
            return actionSpinePresenter;
        }

        private void TeardownActionSpine()
        {
            actionSpinePresenter?.Teardown();
            actionSpinePresenter = null;
        }

        // ---- 工具 ----

        private static void SetSpriteOrFallback(Image img, string resource, Color fallback)
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
                img.color = fallback;
                if (!string.IsNullOrEmpty(resource))
                    UnityEngine.Debug.LogWarning("[DressUpPanelView] 缺少素材 Resources/" + resource + "，回退纯色占位。");
            }
        }

        private void EnsureFieldsFromHierarchy()
        {
            if (dimButton == null)
                dimButton = FindButton("Dim");
            if (closeButton == null)
                closeButton = FindButton("CloseButton");
            if (playerRole == null)
                playerRole = FindImage("PlayerRole");
            if (friendRole == null)
                friendRole = FindImage("FriendRole");
            if (intimacyPanel == null)
            {
                var t = FindDescendantByName(transform, "IntimacyPanel");
                intimacyPanel = t as RectTransform;
            }
            if (playerAvatar == null)
                playerAvatar = FindImage("PlayerAvatar");
            if (playerName == null)
                playerName = FindText("PlayerName");
            if (intimacyIcon == null)
                intimacyIcon = FindImage("IntimacyIcon");
            if (intimacyText == null)
                intimacyText = FindText("IntimacyText");
            if (itemContent == null)
            {
                var t = FindDescendantByName(transform, "ItemContent");
                itemContent = t as RectTransform;
            }

            EnsureItemCellTemplate();

            if (tabButtons.Count == 0)
            {
                for (int i = 0; ; i++)
                {
                    var t = FindDescendantByName(transform, "Tab" + i);
                    if (t == null)
                        break;
                    tabButtons.Add(t.GetComponent<Button>());
                    tabImages.Add(t.GetComponent<Image>());
                    var labelT = FindDescendantByName(t, "Label");
                    tabLabels.Add(labelT != null ? labelT.GetComponent<Text>() : null);
                }
            }
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
            TeardownActionSpine();
            if (instance == this)
                instance = null;
        }
    }
}
