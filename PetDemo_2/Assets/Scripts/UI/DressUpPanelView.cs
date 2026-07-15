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
        [SerializeField] private Button useButton;
        [SerializeField] private Text useButtonLabel;
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
        private DressUpItemConfig selectedConfig;
        private SkeletonGraphic equippedPreviewSpine;
        private const string EquippedSpineChildName = "EquippedSpine";
        private const string SkeletonGraphicShaderName = "Spine/SkeletonGraphic";
        // SPEC §9.14.9（v3.249 / v3.250）：装备预览 Scale=0.75、PosY=-200；PlayerRole Top=95。
        private static readonly Vector2 EquippedSpineSize = new Vector2(720f, 1200f);
        private static readonly Vector2 EquippedSpineAnchoredPos = new Vector2(0f, -215f);
        private static readonly Vector3 EquippedSpineScale = new Vector3(0.8f, 0.8f, 1f);
        private static readonly string[] EquippedIdleFallbacks =
        {
            "exclusive_2", "standby_1", "animation", "idle",
        };

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
            // CSV 可能在 Play 中被改过；清缓存避免 applyResource 仍是旧空值。
            DressUpItemCatalog.ClearCache();
            EnsureFieldsFromHierarchy();
            WireOnce();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            SelectTab(0);
        }

        public void Hide()
        {
            TeardownActionSpine();
            TeardownEquippedSpinePreview();
            HideUseButton();
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

            if (useButton != null)
            {
                useButton.onClick.RemoveAllListeners();
                useButton.onClick.AddListener(OnUseClicked);
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
            TeardownEquippedSpinePreview();
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

            TeardownEquippedSpinePreview();

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
            selectedConfig = null;
            HideUseButton();

            for (int i = itemContent.childCount - 1; i >= 0; i--)
                Destroy(itemContent.GetChild(i).gameObject);

            ConfigureGridCellSize();

            var items = DressUpItemCatalog.GetItemsByTab(tabIndex);
            for (int i = 0; i < items.Count; i++)
                BuildItemCell(items[i]);

            // SPEC §9.14.9（v3.160）：Tab0 默认选中排序后第一个道具。
            if (tabIndex == 0 && items.Count > 0)
            {
                SelectItemCell(items[0].itemId);
                ShowUseButton(items[0]);
            }
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

        /// <summary>SPEC §9.14.9：点击道具 → Tab0/Tab1 预览立绘；Tab2 Spine 动作预览；Tab0/1/2 选中叠加；显示「使用」按钮。</summary>
        private void OnItemClicked(DressUpItemConfig config)
        {
            if (config == null)
                return;

            if (activeTab == 0 || activeTab == 1 || activeTab == DressUpPanelLayout.ActionTabIndex)
                SelectItemCell(config.itemId);
            else
            {
                ClearItemSelection();
                selectedItemId = config.itemId;
                selectedConfig = config;
            }

            if ((activeTab == 0 || activeTab == 1) && !string.IsNullOrEmpty(config.icon))
                RefreshPlayerRolePortrait(config.icon);
            else if (activeTab == DressUpPanelLayout.ActionTabIndex)
                PlayActionTabPreview(config);

            ShowUseButton(config);

            UnityEngine.Debug.Log("[DressUpPanelView] 点击道具 " + config.itemId + "（介绍界面后续补充）：" + config.description);
        }

        private void ClearItemSelection()
        {
            if (string.IsNullOrEmpty(selectedItemId))
                return;

            if (itemCells.TryGetValue(selectedItemId, out var cell) && cell != null)
                cell.SetSelected(false);

            selectedItemId = null;
            selectedConfig = null;
        }

        private void SelectItemCell(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return;

            ClearItemSelection();

            if (!itemCells.TryGetValue(itemId, out var cell) || cell == null)
                return;

            selectedItemId = itemId;
            selectedConfig = DressUpItemCatalog.GetById(itemId);
            cell.SetSelected(true);
        }

        /// <summary>SPEC §9.14.9（v3.244）：选中道具后显示 PlayerRole 下方「使用」按钮。</summary>
        private void ShowUseButton(DressUpItemConfig config)
        {
            EnsureUseButton();
            if (useButton == null)
                return;

            selectedConfig = config;
            if (config != null)
                selectedItemId = config.itemId;

            string label = config != null ? config.useButtonLabel : null;
            if (string.IsNullOrEmpty(label))
                label = DressUpPanelLayout.UseButtonDefaultLabel;

            if (useButtonLabel != null)
                useButtonLabel.text = label;

            useButton.gameObject.SetActive(true);
        }

        private void HideUseButton()
        {
            if (useButton != null)
                useButton.gameObject.SetActive(false);
        }

        /// <summary>SPEC §9.14.9（v3.244 / v3.246）：点击「使用」——applyResource 有效则装备 Spine 并在本面板预览。</summary>
        private void OnUseClicked()
        {
            if (selectedConfig == null && !string.IsNullOrEmpty(selectedItemId))
                selectedConfig = DressUpItemCatalog.GetById(selectedItemId);

            if (selectedConfig == null)
            {
                UnityEngine.Debug.LogWarning("[DressUpPanelView] OnUseClicked：无选中道具。");
                return;
            }

            if (string.IsNullOrEmpty(selectedConfig.applyResource))
            {
                UnityEngine.Debug.Log(
                    "[DressUpPanelView] OnUseClicked：" + selectedConfig.itemId + " 的 applyResource 为空，无效果。");
                return;
            }

            if (service == null)
            {
                UnityEngine.Debug.LogWarning("[DressUpPanelView] OnUseClicked：未绑定 IPlantingService。");
                return;
            }

            bool ok = service.TryEquipPlayerSpine(selectedConfig.applyResource);
            if (!ok)
            {
                UnityEngine.Debug.LogWarning(
                    "[DressUpPanelView] OnUseClicked：装备失败 itemId=" + selectedConfig.itemId +
                    " path=" + selectedConfig.applyResource);
                return;
            }

            UnityEngine.Debug.Log(
                "[DressUpPanelView] OnUseClicked：已装备 itemId=" + selectedConfig.itemId +
                " path=" + selectedConfig.applyResource);
            ShowEquippedSpinePreview(selectedConfig.applyResource);
        }

        /// <summary>装备成功后在 PlayerRole 上预览 Spine（创角 DisplayArea 在装扮打开时是隐藏的）。</summary>
        private void ShowEquippedSpinePreview(string spinePath)
        {
            if (playerRole == null)
                return;

            TeardownEquippedSpinePreview();

            var dataAsset = PlayerSpineAppearanceResolver.TryLoadSkeletonData(spinePath);
            if (dataAsset == null)
                return;

            var shader = Shader.Find(SkeletonGraphicShaderName);
            if (shader == null)
            {
                UnityEngine.Debug.LogWarning("[DressUpPanelView] Shader 未找到：" + SkeletonGraphicShaderName);
                return;
            }

            var mount = playerRole.rectTransform;
            var uiMaterial = SkeletonGraphicUiMaterialFactory.CreateForPmaVertexColors(shader);
            var spineGo = new GameObject(EquippedSpineChildName, typeof(RectTransform));
            var spineRt = spineGo.GetComponent<RectTransform>();
            spineRt.SetParent(mount, false);
            spineRt.anchorMin = spineRt.anchorMax = new Vector2(0.5f, 0.5f);
            spineRt.pivot = new Vector2(0.5f, 0.5f);
            spineRt.anchoredPosition = EquippedSpineAnchoredPos;
            spineRt.sizeDelta = EquippedSpineSize;
            spineRt.localScale = EquippedSpineScale;

            var sg = SkeletonGraphic.AddSkeletonGraphicComponent(spineGo, dataAsset, uiMaterial);
            if (sg == null || !sg.IsValid)
            {
                Destroy(spineGo);
                UnityEngine.Debug.LogWarning("[DressUpPanelView] EquippedSpine 构建失败。");
                return;
            }

            sg.raycastTarget = false;
            equippedPreviewSpine = sg;
            playerRole.enabled = false;
            DressUpPanelLayout.ApplyPlayerRoleTopInset(
                playerRole.rectTransform, DressUpPanelLayout.EquippedPlayerRoleTop);

            // 使用按钮保持在最上层可点。
            if (useButton != null)
                useButton.transform.SetAsLastSibling();

            PlayEquippedIdleLoop(sg);
        }

        private void TeardownEquippedSpinePreview()
        {
            if (equippedPreviewSpine != null)
            {
                Destroy(equippedPreviewSpine.gameObject);
                equippedPreviewSpine = null;
            }

            if (playerRole != null)
            {
                var leftover = playerRole.transform.Find(EquippedSpineChildName);
                if (leftover != null)
                    Destroy(leftover.gameObject);
                playerRole.enabled = true;
                DressUpPanelLayout.ClearPlayerRoleTopInset(playerRole.rectTransform);
            }
        }

        private static void PlayEquippedIdleLoop(SkeletonGraphic sg)
        {
            if (sg == null || sg.Skeleton == null || sg.Skeleton.Data == null)
                return;

            string clip = null;
            for (int i = 0; i < EquippedIdleFallbacks.Length; i++)
            {
                if (sg.Skeleton.Data.FindAnimation(EquippedIdleFallbacks[i]) != null)
                {
                    clip = EquippedIdleFallbacks[i];
                    break;
                }
            }

            if (string.IsNullOrEmpty(clip) && sg.Skeleton.Data.Animations.Count > 0)
                clip = sg.Skeleton.Data.Animations.Items[0].Name;

            if (string.IsNullOrEmpty(clip))
                return;

            try
            {
                sg.AnimationState.SetAnimation(0, clip, true);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[DressUpPanelView] 装备预览待机失败: " + e.Message);
            }
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
            EnsureUseButton();
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

        /// <summary>SPEC §9.14.9（v3.244 / v3.246 / v3.249）：缺 UseButton 时创建；并校正为 PosY=-100。</summary>
        private void EnsureUseButton()
        {
            if (useButton == null)
                useButton = FindButton("UseButton");

            if (useButton == null && playerRole != null)
            {
                var built = DressUpPanelLayout.BuildUseButton(playerRole.rectTransform);
                if (built != null)
                    useButton = built.GetComponent<Button>();
            }

            if (useButton != null && playerRole != null)
            {
                var btnRt = useButton.transform as RectTransform;
                // 预制体旧布局可能把按钮挂在 PlayerRole 外；强制挂回并应用 PosY=-100。
                if (btnRt != null && btnRt.parent != playerRole.rectTransform)
                    btnRt.SetParent(playerRole.rectTransform, false);
                DressUpPanelLayout.ApplyUseButtonRect(btnRt);
            }

            if (useButtonLabel == null && useButton != null)
            {
                var labelT = FindDescendantByName(useButton.transform, "Label");
                useButtonLabel = labelT != null ? labelT.GetComponent<Text>() : null;
            }

            if (useButton != null)
            {
                useButton.onClick.RemoveAllListeners();
                useButton.onClick.AddListener(OnUseClicked);
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
