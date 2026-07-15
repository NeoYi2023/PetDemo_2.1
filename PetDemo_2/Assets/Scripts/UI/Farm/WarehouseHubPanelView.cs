// SPEC §9.8.13 (v3.41)：统一仓库预制体运行时组件。
// 合并 FoodWarehouseModal（中心子模态）与 JiaYuanWarehouseFullscreen（家园全屏背景）。
// 入口 A：MainStoryLine 「前往 → 确定」 → FoodWarehouseModalView.Show 桥接到本组件；
// 入口 B（自 v3.253）：创角 HomeTabPanel TopRightActions 仓库按钮 → CharacterCreationScreenView 调用。
// 数据：果实槽绑 PlayerFruitBag；体力条绑 RoleStats.stamina；
// 底部「吃 / 一键吃饱」消耗 PlayerFruitBag.activeId 的果实 → RoleStats.stamina（§9.8.13.6 换算）；
// 「开始」在体力 ≥ WarehouseHubPanelView.StartButtonVisibleMinStamina（§9.8.13.5）时显示；
// 带 eatBuffIcon 的作物在吃下后于面板右上 `BuffGainedStack` 竖排展示：同 plantConfigId 合并一行并累加 Count（§9.8.13.3.1）。
using System;
using System.Collections.Generic;
using PetDemo.Battle;
using PetDemo.Core;
using PetDemo.Farm;
using PetDemo.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Farm
{
    /// <summary>
    /// SPEC §9.8.13：统一仓库面板视图。挂在 Resources/Prefabs/Farm/WarehouseHubPanel.prefab 的根上。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WarehouseHubPanelView : MonoBehaviour
    {
        public enum HubTab
        {
            Food,
            Cooking
        }

        public const string ResPrefabPath = "Prefabs/Farm/WarehouseHubPanel";
        public const string PanelObjectName = "WarehouseHubPanel";
        public const int FruitSlotCapacity = 26;
        private const string CookingBackgroundResourcePath = "AirUI/ChiFan_test_PengRen";
        private const string FoodOverlayImageName = "Image (1)";

        private static readonly Vector2 TabFoodAnchoredPosition = new Vector2(-108f, 210f);
        private static readonly Vector2 TabCookingAnchoredPosition = new Vector2(99f, 210f);
        private static readonly Vector2 TabButtonSizeDelta = new Vector2(155f, 104f);
        private static readonly Color TabButtonImageColor = new Color(1f, 1f, 1f, 0f);

        /// <summary>底部 <c>StartButton</c> 可见所需最低当前体力（与 §9.8.13.5 一致）。</summary>
        public const int StartButtonVisibleMinStamina = 10;

        // 选中态高亮颜色（与原食物仓库选中色一致：金黄半透 0.3）。
        private static readonly Color SlotSelectedColor = new Color(1f, 0.82f, 0.31f, 0.3f);
        // ============================================================
        // 序列化字段（由 WarehouseHubPanelPrefabGenerator 通过 SerializedObject 写入）
        // ============================================================
        [SerializeField] private Image foodBackgroundImage;
        [SerializeField] private Image cookingBackgroundImage;
        [SerializeField] private Button tabFoodButton;
        [SerializeField] private Button tabCookingButton;
        [SerializeField] private Image tabFoodBg;
        [SerializeField] private Image tabCookingBg;
        [SerializeField] private RectTransform fruitSlotGrid;
        [SerializeField] private List<RectTransform> fruitSlots; // 长度 26
        [SerializeField] private RectTransform staminaBarSlot;
        [SerializeField] private Text staminaText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button eatButton;
        [SerializeField] private Button eatToFullButton;
        [SerializeField] private Button startButton;
        [SerializeField] private RectTransform buffGainedStackRoot;

        private const float BuffIconCell = 128f;
        private const float BuffIconSpacing = 6f;
        private const int BuffIconMaxEntries = 48;
        private const string BuffEntryNamePrefix = "BuffEntry_";
        // 运行时状态
        // ============================================================
        private IPlantingService service;
        private StaminaBarView staminaBar;
        private bool subscribed;
        private bool wired;
        private HubTab activeTab = HubTab.Food;
        private bool cookingBackgroundSpriteResolved;

        public bool IsShown => gameObject != null && gameObject.activeSelf;

        /// <summary>
        /// SPEC §9.8.8 (v3.47)：面板每次 <see cref="Hide"/> 时触发，供主线层等刷新其下仍可见的 HUD（如左上角体力条）。
        /// </summary>
        public event Action Hidden;

        // ============================================================
        // 入口：在画布下查找已有节点 / 实例化预制体 / 缺失时回退 null
        // ============================================================
        public static WarehouseHubPanelView GetOrCreate(RectTransform canvasRect)
        {
            if (canvasRect == null)
            {
                UnityEngine.Debug.LogWarning("[WarehouseHubPanelView] GetOrCreate: canvasRect 为空");
                return null;
            }

            var existing = canvasRect.Find(PanelObjectName);
            if (existing != null)
            {
                var existView = existing.GetComponent<WarehouseHubPanelView>();
                if (existView != null)
                    return existView;
                // 既有节点但未挂 View（少见，可能是预制体被破坏），补挂一份。
                return existing.gameObject.AddComponent<WarehouseHubPanelView>();
            }

            var prefab = Resources.Load<GameObject>(ResPrefabPath);
            if (prefab == null)
            {
                UnityEngine.Debug.LogError(
                    "[WarehouseHubPanelView] 缺少预制体 Resources/" + ResPrefabPath +
                    "，请在编辑器执行 Tools/PetDemo/Generate Warehouse Hub Panel Prefab。");
                return null;
            }

            var go = Instantiate(prefab, canvasRect, false);
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
            var view = go.GetComponent<WarehouseHubPanelView>();
            if (view == null)
                view = go.AddComponent<WarehouseHubPanelView>();

            if (rt != null)
                MainHudLayerRoot.ApplySortTier(rt, MainUiSortTier.HudModal);

            return view;
        }

        // ============================================================
        // 公共 API
        // ============================================================
        public void Show(IPlantingService plantingService)
        {
            service = plantingService;
            EnsureFieldsFromHierarchy();
            EnsureTabBarAndCookingBackground();
            EnsureBuffGainedStackRoot();
            ClearBuffGainedEntries();
            WireButtonsOnce();
            SubscribeService();

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            SwitchTab(HubTab.Food);
            Rebuild();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            Hidden?.Invoke();
        }

        private void OnDestroy()
        {
            UnsubscribeService();
        }

        // ============================================================
        // 订阅 / 一次性挂事件
        // ============================================================
        private void SubscribeService()
        {
            if (subscribed || service == null)
                return;
            service.OnFruitBagChanged += HandleFruitBagChanged;
            service.OnStaminaChanged += HandleStaminaChanged;
            subscribed = true;
        }

        private void UnsubscribeService()
        {
            if (!subscribed || service == null)
                return;
            service.OnFruitBagChanged -= HandleFruitBagChanged;
            service.OnStaminaChanged -= HandleStaminaChanged;
            subscribed = false;
        }

        private void HandleFruitBagChanged()
        {
            if (activeTab == HubTab.Food)
                Rebuild();
        }

        private void HandleStaminaChanged(int newValue, int max)
        {
            if (activeTab != HubTab.Food)
                return;
            RefreshStaminaText();
            RefreshButtonsVisibility();
        }

        private void WireButtonsOnce()
        {
            if (wired)
                return;
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Hide);
            }
            if (eatButton != null)
            {
                eatButton.onClick.RemoveAllListeners();
                eatButton.onClick.AddListener(OnEatClicked);
            }
            if (eatToFullButton != null)
            {
                eatToFullButton.onClick.RemoveAllListeners();
                eatToFullButton.onClick.AddListener(OnEatToFullClicked);
            }
            if (startButton != null)
            {
                startButton.onClick.RemoveAllListeners();
                startButton.onClick.AddListener(OnStartClicked);
            }
            if (tabFoodButton != null)
            {
                tabFoodButton.onClick.RemoveAllListeners();
                tabFoodButton.onClick.AddListener(() => SwitchTab(HubTab.Food));
            }
            if (tabCookingButton != null)
            {
                tabCookingButton.onClick.RemoveAllListeners();
                tabCookingButton.onClick.AddListener(() => SwitchTab(HubTab.Cooking));
            }
            wired = true;
        }

        private void SwitchTab(HubTab tab)
        {
            activeTab = tab;
            bool food = tab == HubTab.Food;

            if (foodBackgroundImage == null)
            {
                var foodBgT = transform.Find("FullscreenBackground");
                if (foodBgT != null)
                    foodBackgroundImage = foodBgT.GetComponent<Image>();
            }
            if (cookingBackgroundImage == null)
            {
                var cookingBgT = transform.Find("CookingBackground");
                if (cookingBgT != null)
                    cookingBackgroundImage = cookingBgT.GetComponent<Image>();
            }

            if (foodBackgroundImage != null)
                foodBackgroundImage.gameObject.SetActive(food);
            if (cookingBackgroundImage != null)
            {
                EnsureCookingBackgroundSprite();
                cookingBackgroundImage.gameObject.SetActive(!food);
            }

            SetFoodContentActive(food);

            var foodOverlay = transform.Find(FoodOverlayImageName);
            if (foodOverlay != null)
                foodOverlay.gameObject.SetActive(food);

            // Tab 按钮 Image 组件关闭，选中态由预制体美术资源表达，不在此改色。
        }

        private void EnsureCookingBackgroundSprite()
        {
            if (cookingBackgroundSpriteResolved || cookingBackgroundImage == null)
                return;
            cookingBackgroundSpriteResolved = true;
            if (cookingBackgroundImage.sprite != null)
                return;
            var sp = Resources.Load<Sprite>(CookingBackgroundResourcePath);
            if (sp != null)
            {
                cookingBackgroundImage.sprite = sp;
                cookingBackgroundImage.color = Color.white;
            }
            else
            {
                cookingBackgroundImage.sprite = null;
                cookingBackgroundImage.color = new Color(0.08f, 0.07f, 0.12f, 1f);
                UnityEngine.Debug.LogWarning(
                    "[WarehouseHubPanelView] 未找到烹饪背景: Resources/" + CookingBackgroundResourcePath);
            }
        }

        private void SetFoodContentActive(bool active)
        {
            if (buffGainedStackRoot != null)
                buffGainedStackRoot.gameObject.SetActive(active);
            if (staminaBarSlot != null)
                staminaBarSlot.gameObject.SetActive(active);
            if (staminaText != null)
                staminaText.gameObject.SetActive(active);
            if (fruitSlotGrid != null)
                fruitSlotGrid.gameObject.SetActive(active);
            var bottomBar = transform.Find("BottomBar");
            if (bottomBar != null)
                bottomBar.gameObject.SetActive(active);
        }

        // ============================================================
        // 列表 / 体力 / 按钮刷新
        // ============================================================
        private void Rebuild()
        {
            if (service == null || activeTab != HubTab.Food)
                return;

            EnsureStaminaBar();
            RefreshStaminaText();
            RefreshFruitSlots();
            RefreshButtonsVisibility();
        }

        private void EnsureStaminaBar()
        {
            if (staminaBar != null || staminaBarSlot == null || service == null)
                return;
            var role = service.GetRoleStats();
            if (role == null)
                return;
            staminaBar = StaminaBarView.BuildInto(staminaBarSlot, role, service);
        }

        private void RefreshStaminaText()
        {
            if (staminaText == null || service == null)
                return;
            var role = service.GetRoleStats();
            if (role == null)
            {
                staminaText.text = "0 / 100";
                return;
            }
            staminaText.text = role.stamina + " / " + role.staminaMax;
            if (staminaBar != null)
                staminaBar.Refresh();
        }

        private void RefreshFruitSlots()
        {
            if (fruitSlots == null || service == null)
                return;

            var bag = service.GetFruitBag();
            // 收集 count > 0 的果实，按 displayName 字典序（无 displayName 则用 plantConfigId）排序，
            // 保证多次刷新顺序稳定，便于用户预期 26 槽内的位置。
            var entries = new List<FruitStack>();
            if (bag != null && bag.stacks != null)
            {
                for (int i = 0; i < bag.stacks.Count; i++)
                {
                    var s = bag.stacks[i];
                    if (s != null && s.count > 0 && !string.IsNullOrEmpty(s.plantConfigId))
                        entries.Add(s);
                }
            }
            entries.Sort(CompareFruitStackByDisplayOrder);

            string activeId = bag != null ? bag.activeId : null;

            for (int i = 0; i < fruitSlots.Count; i++)
            {
                var slotRt = fruitSlots[i];
                if (slotRt == null)
                    continue;

                var iconImg = FindChildImage(slotRt, "Icon");
                var countText = FindChildText(slotRt, "Count");
                var selectMaskGo = FindChildGameObject(slotRt, "SelectMask");
                var slotBtn = slotRt.GetComponent<Button>();
                if (slotBtn == null)
                    slotBtn = slotRt.gameObject.AddComponent<Button>();
                slotBtn.transition = Selectable.Transition.None;

                if (i < entries.Count)
                {
                    var stack = entries[i];
                    var cfg = service.GetPlantConfig(stack.plantConfigId);

                    if (iconImg != null)
                    {
                        iconImg.gameObject.SetActive(true);
                        ApplyFruitIcon(iconImg, cfg);
                    }
                    if (countText != null)
                    {
                        countText.gameObject.SetActive(true);
                        countText.text = "×" + stack.count;
                    }
                    if (selectMaskGo != null)
                        selectMaskGo.SetActive(activeId == stack.plantConfigId);

                    string captured = stack.plantConfigId;
                    slotBtn.interactable = true;
                    slotBtn.onClick.RemoveAllListeners();
                    slotBtn.onClick.AddListener(() => OnFruitSlotClicked(captured));
                }
                else
                {
                    // 多余的槽：图标/数量隐藏，背景槽根仍保留以保持布局占位。
                    if (iconImg != null)
                        iconImg.gameObject.SetActive(false);
                    if (countText != null)
                    {
                        countText.text = string.Empty;
                        countText.gameObject.SetActive(false);
                    }
                    if (selectMaskGo != null)
                        selectMaskGo.SetActive(false);
                    slotBtn.interactable = false;
                    slotBtn.onClick.RemoveAllListeners();
                }
            }
        }

        private static int CompareFruitStackByDisplayOrder(FruitStack a, FruitStack b)
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            return string.CompareOrdinal(a.plantConfigId, b.plantConfigId);
        }

        private static void ApplyFruitIcon(Image iconImg, PlantConfig cfg)
        {
            if (iconImg == null)
                return;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            // 优先 fruitIconResource，否则成熟态（appearanceSpriteIds 末项）；缺图为灰色占位。
            string spriteId = cfg != null ? cfg.ResolveFruitIconResourcePath() : null;
            Sprite sp = null;
            if (!string.IsNullOrEmpty(spriteId))
                sp = Resources.Load<Sprite>(spriteId);
            if (sp != null)
            {
                iconImg.sprite = sp;
                iconImg.color = Color.white;
            }
            else
            {
                iconImg.sprite = null;
                iconImg.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            }
        }

        private void OnFruitSlotClicked(string plantConfigId)
        {
            if (service == null)
                return;
            var bag = service.GetFruitBag();
            if (bag != null && bag.activeId == plantConfigId)
                service.SelectActiveFruit(null);
            else
                service.SelectActiveFruit(plantConfigId);
        }

        private void RefreshButtonsVisibility()
        {
            if (service == null)
                return;
            bool full = service.IsRoleFull();
            var role = service.GetRoleStats();
            int stamina = role != null ? role.stamina : 0;
            bool showStart = stamina >= StartButtonVisibleMinStamina;
            var bag = service.GetFruitBag();

            bool anyStock = false;
            if (bag != null && bag.stacks != null)
            {
                for (int i = 0; i < bag.stacks.Count; i++)
                {
                    if (bag.stacks[i] != null && bag.stacks[i].count > 0)
                    {
                        anyStock = true;
                        break;
                    }
                }
            }
            bool hasActive = bag != null && !string.IsNullOrEmpty(bag.activeId) && AnyStockOf(bag.activeId);

            if (eatButton != null)
            {
                eatButton.gameObject.SetActive(!full);
                eatButton.interactable = hasActive;
            }
            if (eatToFullButton != null)
            {
                eatToFullButton.gameObject.SetActive(!full);
                eatToFullButton.interactable = anyStock;
            }
            if (startButton != null)
            {
                startButton.gameObject.SetActive(showStart);
            }
        }

        private bool AnyStockOf(string plantConfigId)
        {
            if (string.IsNullOrEmpty(plantConfigId) || service == null)
                return false;
            var bag = service.GetFruitBag();
            if (bag == null || bag.stacks == null)
                return false;
            for (int i = 0; i < bag.stacks.Count; i++)
            {
                if (bag.stacks[i] != null && bag.stacks[i].plantConfigId == plantConfigId && bag.stacks[i].count > 0)
                    return true;
            }
            return false;
        }

        // ============================================================
        // 按钮事件
        // ============================================================
        private void OnEatClicked()
        {
            if (service == null)
                return;
            var bag = service.GetFruitBag();
            string id = bag != null ? bag.activeId : null;
            if (string.IsNullOrEmpty(id))
            {
                UnityEngine.Debug.Log("[WarehouseHubPanelView] 未选中果实");
                return;
            }
            if (service.EatOneFruit(id))
                AppendEatBuffBadges(id, 1);
        }

        private void OnEatToFullClicked()
        {
            if (service == null)
                return;
            var bag = service.GetFruitBag();
            string id = bag != null ? bag.activeId : null;
            if (string.IsNullOrEmpty(id) && bag != null && bag.stacks != null)
            {
                for (int i = 0; i < bag.stacks.Count; i++)
                {
                    if (bag.stacks[i] != null && bag.stacks[i].count > 0)
                    {
                        id = bag.stacks[i].plantConfigId;
                        break;
                    }
                }
            }
            if (string.IsNullOrEmpty(id))
            {
                UnityEngine.Debug.Log("[WarehouseHubPanelView] 一键吃饱：无可用果实");
                return;
            }
            int eaten = service.EatFruitToFull(id);
            if (eaten > 0)
                AppendEatBuffBadges(id, eaten);
        }

        private void OnStartClicked()
        {
            Hide();
            var invasion = InvasionService.Instance;
            if (invasion == null)
            {
                UnityEngine.Debug.LogWarning("[WarehouseHubPanelView] OnStartClicked: InvasionService.Instance 为空，跳过战斗演示。");
                return;
            }
            invasion.OpenBattleFromWarehouseHub();
        }

        private void ClearBuffGainedEntries()
        {
            if (buffGainedStackRoot == null)
                return;
            for (int i = buffGainedStackRoot.childCount - 1; i >= 0; i--)
                Destroy(buffGainedStackRoot.GetChild(i).gameObject);
        }

        private void EnsureTabBarAndCookingBackground()
        {
            RemoveDimLayerIfPresent();

            if (cookingBackgroundImage == null)
            {
                var cookingT = transform.Find("CookingBackground");
                if (cookingT == null)
                {
                    var go = new GameObject("CookingBackground", typeof(RectTransform), typeof(Image));
                    var rt = go.GetComponent<RectTransform>();
                    rt.SetParent(transform, false);
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    rt.SetSiblingIndex(1);
                    cookingBackgroundImage = go.GetComponent<Image>();
                    cookingBackgroundImage.raycastTarget = true;
                    cookingBackgroundImage.preserveAspect = false;
                    go.SetActive(false);
                }
                else
                {
                    cookingBackgroundImage = cookingT.GetComponent<Image>();
                }
            }

            if (tabFoodButton == null || tabCookingButton == null)
            {
                var foodT = transform.Find("TabFood");
                var cookT = transform.Find("TabCooking");
                if (foodT == null)
                    foodT = transform.Find("TabBar/TabFood");
                if (cookT == null)
                    cookT = transform.Find("TabBar/TabCooking");
                if (foodT == null || cookT == null)
                    CreateTabButtonsOnRoot();
                else
                {
                    if (foodT != null)
                    {
                        tabFoodButton = foodT.GetComponent<Button>();
                        tabFoodBg = foodT.GetComponent<Image>();
                    }
                    if (cookT != null)
                    {
                        tabCookingButton = cookT.GetComponent<Button>();
                        tabCookingBg = cookT.GetComponent<Image>();
                    }
                }
            }

            ApplyTabButtonLayout(transform.Find("TabFood"));
            ApplyTabButtonLayout(transform.Find("TabCooking"));
            ApplyTransparentTabButtonImage(tabFoodBg);
            ApplyTransparentTabButtonImage(tabCookingBg);
            if (tabFoodButton == null)
            {
                var t = transform.Find("TabBar/TabFood");
                if (t != null)
                {
                    tabFoodButton = t.GetComponent<Button>();
                    tabFoodBg = t.GetComponent<Image>();
                    ApplyTabButtonLayout(t);
                }
            }
            if (tabCookingButton == null)
            {
                var t = transform.Find("TabBar/TabCooking");
                if (t != null)
                {
                    tabCookingButton = t.GetComponent<Button>();
                    tabCookingBg = t.GetComponent<Image>();
                    ApplyTabButtonLayout(t);
                }
            }
        }

        private void RemoveDimLayerIfPresent()
        {
            var dim = transform.Find("DimLayer");
            if (dim != null)
                Destroy(dim.gameObject);
        }

        private void CreateTabButtonsOnRoot()
        {
            var rootRt = transform as RectTransform;
            if (rootRt == null)
                return;

            tabFoodBg = CreateTabButtonRuntime(rootRt, "TabFood", TabFoodAnchoredPosition, true, out tabFoodButton);
            tabCookingBg = CreateTabButtonRuntime(rootRt, "TabCooking", TabCookingAnchoredPosition, false, out tabCookingButton);
        }

        private static Image CreateTabButtonRuntime(
            RectTransform parent, string name, Vector2 anchoredPosition, bool active, out Button button)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = TabButtonSizeDelta;

            var img = go.GetComponent<Image>();
            ApplyTransparentTabButtonImage(img);
            img.raycastTarget = true;
            button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = img;

            // 保留 Label 子节点结构以兼容预制体，但不显示文字。
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            labelGo.SetActive(false);

            return img;
        }

        private static void ApplyTabButtonLayout(Transform tabTransform)
        {
            if (tabTransform == null)
                return;
            var rt = tabTransform as RectTransform;
            if (rt == null)
                return;

            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = TabButtonSizeDelta;
            if (tabTransform.name == "TabFood")
                rt.anchoredPosition = TabFoodAnchoredPosition;
            else if (tabTransform.name == "TabCooking")
                rt.anchoredPosition = TabCookingAnchoredPosition;

            HideTabLabelText(tabTransform);
            ApplyTransparentTabButtonImage(tabTransform.GetComponent<Image>());
        }

        private static void ApplyTransparentTabButtonImage(Image img)
        {
            if (img == null)
                return;
            img.enabled = true;
            img.color = TabButtonImageColor;
        }

        private static void HideTabLabelText(Transform tabRoot)
        {
            var label = tabRoot.Find("Label");
            if (label != null)
                label.gameObject.SetActive(false);
        }

        private void EnsureBuffGainedStackRoot()
        {
            if (buffGainedStackRoot != null)
                return;
            var t = transform.Find("BuffGainedStack");
            if (t != null)
            {
                buffGainedStackRoot = t as RectTransform;
                return;
            }

            var go = new GameObject("BuffGainedStack", typeof(RectTransform));
            buffGainedStackRoot = go.GetComponent<RectTransform>();
            buffGainedStackRoot.SetParent(transform, false);
            buffGainedStackRoot.anchorMin = new Vector2(1f, 1f);
            buffGainedStackRoot.anchorMax = new Vector2(1f, 1f);
            buffGainedStackRoot.pivot = new Vector2(1f, 1f);
            buffGainedStackRoot.anchoredPosition = new Vector2(-24f, -96f);
            buffGainedStackRoot.sizeDelta = new Vector2(BuffIconCell + 16f, BuffIconCell + 8f);

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.spacing = BuffIconSpacing;
            vlg.padding = new RectOffset(4, 4, 4, 4);

            var fit = go.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            buffGainedStackRoot.SetAsLastSibling();
        }

        private void AppendEatBuffBadges(string plantConfigId, int eatCount)
        {
            if (eatCount <= 0 || service == null)
                return;
            var cfg = service.GetPlantConfig(plantConfigId);
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.eatBuffIconResource))
                return;
            var sp = Resources.Load<Sprite>(cfg.eatBuffIconResource.Trim());
            if (sp == null)
                return;

            EnsureBuffGainedStackRoot();

            string entryName = BuffEntryNamePrefix + plantConfigId;
            var existing = buffGainedStackRoot.Find(entryName);
            if (existing != null)
            {
                var countTf = existing.Find("Count");
                var txt = countTf != null ? countTf.GetComponent<Text>() : null;
                if (txt != null)
                {
                    int cur = 0;
                    int.TryParse(txt.text, out cur);
                    txt.text = (cur + eatCount).ToString();
                }
            }
            else
            {
                while (buffGainedStackRoot.childCount >= BuffIconMaxEntries)
                    Destroy(buffGainedStackRoot.GetChild(0).gameObject);

                CreateBuffStackEntry(buffGainedStackRoot, entryName, sp, eatCount);
            }

            buffGainedStackRoot.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(buffGainedStackRoot);
        }

        private static void CreateBuffStackEntry(Transform parent, string entryName, Sprite sp, int initialCount)
        {
            var root = new GameObject(entryName, typeof(RectTransform));
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.SetParent(parent, false);
            rootRt.sizeDelta = new Vector2(BuffIconCell, BuffIconCell);
            var le = root.AddComponent<LayoutElement>();
            le.preferredWidth = BuffIconCell;
            le.preferredHeight = BuffIconCell;
            le.minWidth = BuffIconCell;
            le.minHeight = BuffIconCell;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.SetParent(rootRt, false);
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.sprite = sp;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            var countGo = new GameObject("Count", typeof(RectTransform), typeof(Text), typeof(Outline));
            var countRt = countGo.GetComponent<RectTransform>();
            countRt.SetParent(rootRt, false);
            countRt.anchorMin = new Vector2(1f, 0f);
            countRt.anchorMax = new Vector2(1f, 0f);
            countRt.pivot = new Vector2(1f, 0f);
            countRt.anchoredPosition = new Vector2(-2f, 2f);
            countRt.sizeDelta = new Vector2(72f, 40f);
            var txt = countGo.GetComponent<Text>();
            txt.font = FarmGridView.LoadBuiltinFont();
            txt.fontSize = 32;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleRight;
            txt.color = Color.white;
            txt.raycastTarget = false;
            txt.text = Mathf.Max(1, initialCount).ToString();
            var outline = countGo.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.88f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        // ============================================================
        // 兜底字段定位：用户在预制体编辑器中改动节点时，按名字回填引用
        // ============================================================
        private void EnsureFieldsFromHierarchy()
        {
            if (foodBackgroundImage == null)
            {
                var t = transform.Find("FullscreenBackground");
                if (t != null)
                    foodBackgroundImage = t.GetComponent<Image>();
            }
            if (cookingBackgroundImage == null)
            {
                var t = transform.Find("CookingBackground");
                if (t != null)
                    cookingBackgroundImage = t.GetComponent<Image>();
            }
            if (tabFoodButton == null || tabFoodBg == null)
            {
                var tabFoodT = transform.Find("TabBar/TabFood");
                if (tabFoodT == null)
                    tabFoodT = FindDescendantByName(transform, "TabFood");
                if (tabFoodT != null)
                {
                    if (tabFoodButton == null)
                        tabFoodButton = tabFoodT.GetComponent<Button>();
                    if (tabFoodBg == null)
                        tabFoodBg = tabFoodT.GetComponent<Image>();
                }
            }
            if (tabCookingButton == null || tabCookingBg == null)
            {
                var tabCookingT = transform.Find("TabBar/TabCooking");
                if (tabCookingT == null)
                    tabCookingT = FindDescendantByName(transform, "TabCooking");
                if (tabCookingT != null)
                {
                    if (tabCookingButton == null)
                        tabCookingButton = tabCookingT.GetComponent<Button>();
                    if (tabCookingBg == null)
                        tabCookingBg = tabCookingT.GetComponent<Image>();
                }
            }
            if (fruitSlotGrid == null)
            {
                var t = transform.Find("FruitSlotGrid");
                if (t != null) fruitSlotGrid = t as RectTransform;
            }
            if (staminaBarSlot == null)
            {
                var t = transform.Find("StaminaBarSlot");
                if (t != null) staminaBarSlot = t as RectTransform;
            }
            if (staminaText == null)
                staminaText = FindDescendantText("StaminaText");
            if (closeButton == null)
                closeButton = FindDescendantButton("CloseButton");
            if (eatButton == null)
                eatButton = FindDescendantButton("EatButton");
            if (eatToFullButton == null)
                eatToFullButton = FindDescendantButton("EatToFullButton");
            if (startButton == null)
                startButton = FindDescendantButton("StartButton");
            if (buffGainedStackRoot == null)
            {
                var bt = transform.Find("BuffGainedStack");
                if (bt != null)
                    buffGainedStackRoot = bt as RectTransform;
            }

            // 26 槽兜底：按 FruitSlot_01..FruitSlot_26 命名查找。
            if (fruitSlots == null || fruitSlots.Count < FruitSlotCapacity)
            {
                fruitSlots = new List<RectTransform>(FruitSlotCapacity);
                var gridT = fruitSlotGrid != null ? fruitSlotGrid : transform;
                for (int i = 1; i <= FruitSlotCapacity; i++)
                {
                    var name = "FruitSlot_" + i.ToString("D2");
                    var t = FindDescendantByName(gridT, name);
                    fruitSlots.Add(t as RectTransform);
                }
            }
        }

        private static Image FindChildImage(Transform parent, string childName)
        {
            if (parent == null) return null;
            var t = parent.Find(childName);
            return t != null ? t.GetComponent<Image>() : null;
        }

        private static Text FindChildText(Transform parent, string childName)
        {
            if (parent == null) return null;
            var t = parent.Find(childName);
            return t != null ? t.GetComponent<Text>() : null;
        }

        private static GameObject FindChildGameObject(Transform parent, string childName)
        {
            if (parent == null) return null;
            var t = parent.Find(childName);
            return t != null ? t.gameObject : null;
        }

        private Text FindDescendantText(string name)
        {
            var t = FindDescendantByName(transform, name);
            return t != null ? t.GetComponent<Text>() ?? t.GetComponentInChildren<Text>(true) : null;
        }

        private Button FindDescendantButton(string name)
        {
            var t = FindDescendantByName(transform, name);
            return t != null ? t.GetComponent<Button>() : null;
        }

        private static Transform FindDescendantByName(Transform root, string name)
        {
            if (root == null)
                return null;
            if (root.name == name)
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var t = FindDescendantByName(root.GetChild(i), name);
                if (t != null)
                    return t;
            }
            return null;
        }

        // 暴露选中颜色，供生成器一处共用。
        public static Color GetSlotSelectedColor() => SlotSelectedColor;
    }
}
