using System.Collections;
using PetDemo.Battle;
using PetDemo.Core;
using PetDemo.Farm;
using PetDemo.Save;
using PetDemo.UI;
using PetDemo.UI.Battle;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Builds main menu UI (1080x1920 baseline) from Resources/AirUI sprites at runtime.
/// 自 v0.8 起在 Build 流程末端构建 20 田网格、统一操作按钮、仓库内 Tab+种子列表。
/// </summary>
[DisallowMultipleComponent]
public class AirMainMenuRuntimeBuilder : MonoBehaviour
{
    private const string ResMainBg = "AirUI/UI0";
    private const string ResSeedButton = "AirUI/ZhongZi-1";
    private const string ResFertilizeButton = "AirUI/ShiFei-1";
    private const string ResHarvestViewEntryIcon = "AirUI/ShouHuo_0";
    private const string ResHarvestViewCloseIcon = "AirUI/ShouHuo_1";
    private const string ResHarvestViewFreePanIcon = "AirUI/ShouHuo_2";
    private const string ResFruitBagButton = "AirUI/ShouHuo-0";
    private const string ResWarehouseBg = "AirUI/ZhongZiCangKu_1";
    private const string ResWarehouseBackgroundPrefab = "Prefabs/Farm/WarehouseBackground";
    private const string ResFertilizerWarehousePanelPrefab = "Prefabs/Farm/FertilizerWarehousePanel";
    // SPEC §9.8.5：底部一级导航切换栏预制件。脚本不绑定 5×4 个 Sprite，由用户在预制件 Inspector 配置。
    private const string ResBottomNavBarPrefab = "Prefabs/Farm/BottomNavBar";
    // SPEC §9.10：「角色」成长全屏层预制体。
    private const string ResRoleGrowthPanelPrefab = "Prefabs/Farm/RoleGrowthPanel";
    // SPEC §9.10.5：精灵背包子页预制体。
    private const string ResRoleGrowthPetBagPagePrefab = "Prefabs/Farm/RoleGrowthPetBagPage";
    private const string ResHeroStatsRowPrefab = "Prefabs/Air/HeroStatsRow";

    [Header("Farm Grid Prefab Overrides (Optional)")]
    [SerializeField] private RectTransform farmGridRootPrefab;
    [SerializeField] private RectTransform tileSlotPrefab;

    [Header("Unified Action Button Prefab Override (Optional)")]
    [SerializeField] private RectTransform unifiedActionButtonPrefab;

    [Header("Warehouse Background Prefab Override (Optional)")]
    [SerializeField] private RectTransform warehouseBackgroundPrefab;

    [Header("Sow Action Button Prefab Override (Optional)")]
    [SerializeField] private RectTransform sowActionButtonPrefab;

    [Header("Fertilizer Warehouse Panel Prefab Override (Optional)")]
    [SerializeField] private RectTransform fertilizerWarehousePanelPrefab;

    [Header("Bottom Nav Bar Prefab Override (Optional)")]
    [SerializeField] private RectTransform bottomNavBarPrefab;

    [Header("Role Growth Panel Prefab Override (Optional)")]
    [SerializeField] private RectTransform roleGrowthPanelPrefab;

    // SPEC §9.12 (v3.82)：附魔转盘玩法全屏界面预制件（Tools/PetDemo/Generate Enchant Screen Prefab 生成）。
    [Header("Enchant Screen Prefab (Optional)")]
    [SerializeField] private GameObject enchantScreenPrefab;

    [Header("Hero Stats Display (Main Menu)")]
    [SerializeField] private int heroAttack = 120;
    [SerializeField] private int heroDefense = 80;
    [SerializeField] private int heroHp = 1000;
    [SerializeField] private int heroAgility = 60;
    [SerializeField] private Vector2 heroStatsRowAnchoredPosition = new Vector2(0f, 106f);
    [SerializeField] private float heroAtkPosX = -378f;
    [SerializeField] private float heroDefPosX = -105f;
    [SerializeField] private float heroHpPosX = 154f;
    [SerializeField] private float heroAgilityPosX = 421f;
    [SerializeField] private int heroStatsFontSize = 36;
    private const float HeroStatsDefaultPosY = 106f;
    /// <summary>历史默认 Y；若场景里仍序列化该值，构建时升级为 <see cref="HeroStatsDefaultPosY"/>。</summary>
    private const float LegacyHeroStatsRowDefaultPosY = 182f;

    private SaveSlotPickerView saveSlotPicker;
    private GongHuiScreenView builtGongHuiScreen;

    private void Awake()
    {
        EnsureEventSystem();
        EnsureSaveCoordinator();

        if (!GameBootContext.HasEnteredGame)
        {
            saveSlotPicker = SaveSlotPickerView.Show(transform, OnSaveSlotChosen);
            return;
        }

        EnsureFarmBootstrap();
        Build();
    }

    private void EnsureSaveCoordinator()
    {
        if (GetComponent<GameSaveCoordinator>() == null)
            gameObject.AddComponent<GameSaveCoordinator>();
    }

    private void OnSaveSlotChosen(int slotIndex)
    {
        GameSaveSnapshot snapshot = null;
        bool isNewGame;
        if (GameSaveRepository.Exists(slotIndex))
        {
            snapshot = GameSaveRepository.Load(slotIndex);
            isNewGame = snapshot == null;
        }
        else
        {
            isNewGame = true;
        }

        GameBootContext.EnterSlot(slotIndex, snapshot, isNewGame);

        if (saveSlotPicker != null)
        {
            saveSlotPicker.DestroyPicker();
            saveSlotPicker = null;
        }

        EnsureFarmBootstrap();
        Build();
    }

    private void EnsureFarmBootstrap()
    {
        if (PlantingService.Instance != null)
            return;
        if (GetComponent<FarmBootstrap>() == null)
            gameObject.AddComponent<FarmBootstrap>();
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null)
            return;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    private void Build()
    {
        var mainSprite = Resources.Load<Sprite>(ResMainBg);
        var seedSprite = Resources.Load<Sprite>(ResSeedButton);
        var warehouseSprite = Resources.Load<Sprite>(ResWarehouseBg);
        var fertilizeSprite = Resources.Load<Sprite>(ResFertilizeButton);
        if (mainSprite == null || seedSprite == null || warehouseSprite == null)
        {
            UnityEngine.Debug.LogError(
                "AirMainMenuRuntimeBuilder: Missing sprites. Expected Resources paths: " +
                ResMainBg + ", " + ResSeedButton + ", " + ResWarehouseBg);
            return;
        }
        if (fertilizeSprite == null)
        {
            // SPEC §9.7：肥料入口图标缺失时不阻断主流程，但需明确告警便于补图。
            UnityEngine.Debug.LogWarning(
                "AirMainMenuRuntimeBuilder: Fertilize entry sprite missing at Resources/" +
                ResFertilizeButton + " — entry button will be hidden.");
        }

        var harvestEntrySprite = Resources.Load<Sprite>(ResHarvestViewEntryIcon);
        var harvestCloseSprite = Resources.Load<Sprite>(ResHarvestViewCloseIcon);
        var harvestFreePanSprite = Resources.Load<Sprite>(ResHarvestViewFreePanIcon);
        if (harvestEntrySprite == null)
        {
            UnityEngine.Debug.LogWarning(
                "AirMainMenuRuntimeBuilder: Harvest view entry sprite missing at Resources/" +
                ResHarvestViewEntryIcon + " — harvest view entry will be hidden.");
        }
        if (harvestCloseSprite == null)
        {
            UnityEngine.Debug.LogWarning(
                "AirMainMenuRuntimeBuilder: Harvest view close sprite missing at Resources/" +
                ResHarvestViewCloseIcon + " — close icon will fall back to entry sprite.");
        }
        if (harvestFreePanSprite == null)
        {
            UnityEngine.Debug.LogWarning(
                "AirMainMenuRuntimeBuilder: Harvest view free-pan sprite missing at Resources/" +
                ResHarvestViewFreePanIcon + " — free-pan icon will fall back to entry sprite.");
        }

        FarmGridView harvestFarmGrid = null;
        JiaYuanViewportFollowController harvestViewportFollow = null;

        var canvasGo = new GameObject("MainCanvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        // SPEC §9.8.14 (v3.99)：Screen Space Camera 使粒子风效可与 UI 同管线排序；
        // Overlay 模式下粒子总在 Game 视图 UI 层之下（Scene 可见、Game 不可见）。
        var uiCamera = ResolveMainUiCamera();
        if (uiCamera != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = 100f;
        }
        else
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            UnityEngine.Debug.LogWarning(
                "[AirMainMenuRuntimeBuilder] 未找到 Main Camera，MainCanvas 回退为 ScreenSpaceOverlay；家园风效可能不可见。");
        }
        canvas.sortingOrder = 0;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        var canvasRect = canvasGo.GetComponent<RectTransform>();
        StretchFull(canvasRect);

        // SPEC §9.8.17（v3.112）：HUD 与世界层 sortingOrder 频段分离。
        var hudRoot = MainHudLayerRoot.BuildUnder(canvasRect);

        var bgRt = CreateChildRect(canvasRect, "MainBackground", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        StretchFull(bgRt);
        var bgImage = bgRt.gameObject.AddComponent<Image>();
        bgImage.sprite = mainSprite;
        bgImage.preserveAspect = false;
        bgRt.gameObject.SetActive(false);

        var jiaYuanWorld = JiaYuanWorldScreenView.BuildWorldInto(canvasRect, bgRt);
        var jiaYuanWorldContent = jiaYuanWorld != null ? jiaYuanWorld.WorldContent : null;

        BuildHeroStatsDisplay(hudRoot, out var atkText, out var defText, out var hpText, out var agilityText,
            out var heroStatsRowRt);

        var seedRt = CreateChildRect(hudRoot, "SeedWarehouseButton",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(285f, 290f), new Vector2(150f, 150f));
        var seedImage = seedRt.gameObject.AddComponent<Image>();
        seedImage.sprite = seedSprite;
        seedImage.preserveAspect = true;
        seedImage.raycastTarget = true;
        var seedButton = seedRt.gameObject.AddComponent<Button>();
        seedButton.transition = Selectable.Transition.None;

        var modalRt = CreateChildRect(hudRoot, "SeedWarehouseModal",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        StretchFull(modalRt);
        modalRt.gameObject.SetActive(false);
        MainHudLayerRoot.ApplySortTier(modalRt, MainUiSortTier.HudModal);

        var dimRt = CreateChildRect(modalRt, "DimCloseArea",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        StretchFull(dimRt);
        var dimImage = dimRt.gameObject.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.55f);
        dimImage.raycastTarget = true;
        var dimButton = dimRt.gameObject.AddComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        dimButton.targetGraphic = dimImage;

        var panelRt = BuildWarehouseBackground(modalRt, warehouseSprite);

        seedButton.onClick.AddListener(() =>
        {
            modalRt.SetAsLastSibling();
            modalRt.gameObject.SetActive(true);
        });
        dimButton.onClick.AddListener(() => modalRt.gameObject.SetActive(false));

        // SPEC §9.7：肥料入口按钮 + 肥料仓库弹窗。位置紧贴 SeedWarehouseButton 右侧。
        // 弹窗复用 WarehouseBackground.prefab 实例化（与种子仓库为独立第二实例）。
        var fertilizeButton = BuildFertilizeEntryButton(hudRoot, fertilizeSprite);
        var fertilizeModalRt = CreateChildRect(hudRoot, "FertilizeWarehouseModal",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        StretchFull(fertilizeModalRt);
        fertilizeModalRt.gameObject.SetActive(false);
        MainHudLayerRoot.ApplySortTier(fertilizeModalRt, MainUiSortTier.HudModal);

        var fertilizeDimRt = CreateChildRect(fertilizeModalRt, "DimCloseArea",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        StretchFull(fertilizeDimRt);
        var fertilizeDimImage = fertilizeDimRt.gameObject.AddComponent<Image>();
        fertilizeDimImage.color = new Color(0f, 0f, 0f, 0.55f);
        fertilizeDimImage.raycastTarget = true;
        var fertilizeDimButton = fertilizeDimRt.gameObject.AddComponent<Button>();
        fertilizeDimButton.transition = Selectable.Transition.None;
        fertilizeDimButton.targetGraphic = fertilizeDimImage;

        var fertilizePanelRt = BuildFertilizerWarehousePanel(fertilizeModalRt, warehouseSprite);
        if (fertilizePanelRt != null)
            fertilizePanelRt.name = "FertilizeWarehouseBackground";

        if (fertilizeButton != null)
        {
            fertilizeButton.onClick.AddListener(() =>
            {
                fertilizeModalRt.SetAsLastSibling();
                fertilizeModalRt.gameObject.SetActive(true);
            });
        }
        fertilizeDimButton.onClick.AddListener(() => fertilizeModalRt.gameObject.SetActive(false));

        // SPEC §4.1.11 / §9.9：果实背包弹窗（独立遮罩 + 仓库底板）。
        var fruitModalRt = CreateChildRect(hudRoot, "FruitWarehouseModal",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        StretchFull(fruitModalRt);
        fruitModalRt.gameObject.SetActive(false);
        MainHudLayerRoot.ApplySortTier(fruitModalRt, MainUiSortTier.HudModal);
        var fruitDimRt = CreateChildRect(fruitModalRt, "DimCloseArea",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        StretchFull(fruitDimRt);
        var fruitDimImage = fruitDimRt.gameObject.AddComponent<Image>();
        fruitDimImage.color = new Color(0f, 0f, 0f, 0.55f);
        fruitDimImage.raycastTarget = true;
        var fruitDimButton = fruitDimRt.gameObject.AddComponent<Button>();
        fruitDimButton.transition = Selectable.Transition.None;
        fruitDimButton.targetGraphic = fruitDimImage;
        var fruitPanelRt = BuildWarehouseBackground(fruitModalRt, warehouseSprite);
        if (fruitPanelRt != null)
        {
            fruitPanelRt.name = "FruitWarehouseBackground";
            var seedOnly = fruitPanelRt.Find("SeedWarehouseContent");
            if (seedOnly != null)
                seedOnly.gameObject.SetActive(false);
        }
        fruitDimButton.onClick.AddListener(() => fruitModalRt.gameObject.SetActive(false));

        var fruitBagSprite = Resources.Load<Sprite>(ResFruitBagButton);
        if (fruitBagSprite == null)
            fruitBagSprite = seedSprite;
        var fruitBagEntryRt = CreateChildRect(hudRoot, "FruitBagButton",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(615f, 290f), new Vector2(150f, 150f));
        var fruitBagImage = fruitBagEntryRt.gameObject.AddComponent<Image>();
        fruitBagImage.sprite = fruitBagSprite;
        fruitBagImage.preserveAspect = true;
        fruitBagImage.raycastTarget = true;
        var fruitBagButton = fruitBagEntryRt.gameObject.AddComponent<Button>();
        fruitBagButton.transition = Selectable.Transition.None;
        fruitBagButton.targetGraphic = fruitBagImage;
        fruitBagButton.onClick.AddListener(() =>
        {
            fruitModalRt.SetAsLastSibling();
            fruitModalRt.gameObject.SetActive(true);
        });

        // ---- v0.8：仓库面板内追加双 Tab + 种子列表 ----
        var service = PlantingService.Instance;
        var invasionService = InvasionService.GetOrCreate(gameObject);
        if (service != null)
        {
            SeedWarehouseListView.BuildInto(panelRt, service);

            if (fruitPanelRt != null)
                FruitWarehouseListView.BuildInto(fruitPanelRt, service);

            // SPEC §9.7 / §3.12：肥料弹窗预制体 `FertilizerWarehousePanelView`。
            if (fertilizePanelRt != null)
            {
                var fertilizerPanel = fertilizePanelRt.GetComponent<FertilizerWarehousePanelView>();
                if (fertilizerPanel != null)
                    fertilizerPanel.Initialize(service, fertilizeModalRt);
            }

            // ---- SPEC §9.1.4（v3.111）：家园世界 Y 轴深度排序 ----
            JiaYuanWorldDepthSorter depthSorter = null;
            if (jiaYuanWorldContent != null)
            {
                depthSorter = GetComponent<JiaYuanWorldDepthSorter>();
                if (depthSorter == null)
                    depthSorter = gameObject.AddComponent<JiaYuanWorldDepthSorter>();
                depthSorter.Initialize(jiaYuanWorldContent);
            }

            // ---- v0.8：主画布上追加 20 田网格 + 统一操作按钮 ----
            var farmParent = jiaYuanWorldContent != null ? jiaYuanWorldContent : canvasRect;
            var farmGrid = FarmGridView.BuildInto(
                canvasRect, service, farmGridRootPrefab, tileSlotPrefab, farmParent);
            var unifiedActionView = UnifiedActionButtonView.BuildInto(
                hudRoot, service, unifiedActionButtonPrefab, invasionService);
            // SPEC §9.2 / §9.8.14：统一按钮展示层级在 SeedWarehouseButton 之后（更低 siblingIndex，被种子入口遮挡时不挡点击）。
            if (unifiedActionView != null && seedRt != null)
                unifiedActionView.transform.SetSiblingIndex(seedRt.GetSiblingIndex());

            // ---- SPEC §9.11 (v3.50)：虫灾「打虫子」全屏演示 ----
            var pestControlScreen = PestControlScreenView.BuildInto(hudRoot, service);
            if (pestControlScreen != null)
                MainHudLayerRoot.ApplySortTier(pestControlScreen.transform as RectTransform, MainUiSortTier.HudOverlay);
            // ---- SPEC §9.12 (v3.82)：地鼠偷窃触发「附魔」转盘玩法（正式版，取代 v3.52 打地鼠演示） ----
            var enchantScreen = EnchantScreenView.BuildInto(hudRoot, service, enchantScreenPrefab);
            if (enchantScreen != null)
                MainHudLayerRoot.ApplySortTier(enchantScreen.transform as RectTransform, MainUiSortTier.HudOverlay);
            // ---- SPEC §9.13 (v3.59)：转盘抽奖（仅 §9.11 捉虫胜利后使用，须在 Pest 之后装配） ----
            var wheelLotteryScreen = WheelLotteryScreenView.BuildInto(hudRoot, service);
            if (wheelLotteryScreen != null)
                MainHudLayerRoot.ApplySortTier(wheelLotteryScreen.transform as RectTransform, MainUiSortTier.HudOverlay);
            var tileTipsPresenter = GetComponent<TilePlantTipsPresenter>();
            if (tileTipsPresenter == null)
                tileTipsPresenter = gameObject.AddComponent<TilePlantTipsPresenter>();
            tileTipsPresenter.Init(service, hudRoot);

            // ---- v3.18 SPEC §4.1.10：农田变异覆盖层 + 收获弹窗 ----
            if (farmGrid != null)
                MutationOverlayView.Attach(farmGrid, service);
            MutationRevealPopupView.CreateAndAttach(service, hudRoot);

            // ---- v2.9 SPEC §9.4.6：仓库内播种按钮 + 手势控制器 ----
            // 控制器与按钮挂在 HUD 根（不进入 modal 子树），保证按下按钮关闭 modal
            // 后仍能接收后续 IDrag/IPointerUp/IEndDrag。
            var sowController = SowGestureController.GetOrCreate(hudRoot);
            sowController.Init(service, modalRt);
            SowActionButtonView.BuildInto(hudRoot, service, sowController, sowActionButtonPrefab);

            if (jiaYuanWorld != null)
            {
                var viewportFollow = jiaYuanWorld.Viewport != null
                    ? jiaYuanWorld.Viewport.GetComponent<JiaYuanViewportFollowController>()
                    : null;
                harvestFarmGrid = farmGrid;
                harvestViewportFollow = viewportFollow;
                if (viewportFollow != null && farmGrid != null)
                    sowController.BindCameraFollow(viewportFollow, () => farmGrid.ZhongTianAnchor);
            }

            var rolePresenter = GetComponent<MainRoleCunminPresenter>();
            if (rolePresenter == null)
                rolePresenter = gameObject.AddComponent<MainRoleCunminPresenter>();
            var roleParent = jiaYuanWorldContent != null ? jiaYuanWorldContent : canvasRect;
            rolePresenter.Build(roleParent, service);

            if (jiaYuanWorld != null)
            {
                jiaYuanWorld.BindFollowTarget(rolePresenter.VillagerRoleRectTransform);
                rolePresenter.BindJiaYuanWorld(jiaYuanWorld);
            }

            // SPEC §9.5.1（v3.24）：精灵伴侣展示层。须在 MainRoleCunminPresenter 之后构建，
            // 与 VillagerRoleRoot 同挂 JiaYuanWorldContent（§9.8.14）。
            var petCompanionPresenter = GetComponent<PetCompanionPresenter>();
            if (petCompanionPresenter == null)
                petCompanionPresenter = gameObject.AddComponent<PetCompanionPresenter>();
            petCompanionPresenter.Build(roleParent, service);

            if (depthSorter != null)
            {
                rolePresenter.BindDepthSorter(depthSorter);
                petCompanionPresenter.BindDepthSorter(depthSorter);
                if (farmGrid != null)
                {
                    farmGrid.RefreshAllSlots();
                    var mutationOverlay = farmGrid.GetComponent<MutationOverlayView>();
                    mutationOverlay?.RefreshDepthSortAll();
                }
            }

            var statsPresenter = GetComponent<MainHeroStatsPresenter>();
            if (statsPresenter == null)
                statsPresenter = gameObject.AddComponent<MainHeroStatsPresenter>();
            statsPresenter.Init(service, hudRoot, atkText, defText, hpText, agilityText, fruitBagEntryRt);
        }
        else
        {
            UnityEngine.Debug.LogError("AirMainMenuRuntimeBuilder: PlantingService.Instance 为空，跳过种植系统 UI 构建。");
        }

        var bottomNavBar = BuildBottomNavBar(hudRoot, jiaYuanWorld, heroStatsRowRt);

        // SPEC §9.8.9.7 (v3.129)：公会跟随 NPC 进入家园来访（须在底栏创建后绑定 Tab 切换）。
        if (bottomNavBar != null && jiaYuanWorldContent != null)
        {
            var rolePresenterForVisit = GetComponent<MainRoleCunminPresenter>();
            var visitorPresenter = GetComponent<JiaYuanGuildVisitorPresenter>();
            if (visitorPresenter == null)
                visitorPresenter = gameObject.AddComponent<JiaYuanGuildVisitorPresenter>();
            visitorPresenter.Build(
                jiaYuanWorldContent,
                rolePresenterForVisit != null ? rolePresenterForVisit.VillagerRoleRectTransform : null,
                bottomNavBar);
        }

        // SPEC §9.7.1 (v3.90/v3.110)：收获视角入口 + 自由拖动镜头（须在底栏创建后绑定 JiaYuan Tab 显隐）。
        if (harvestEntrySprite != null && harvestFarmGrid != null && harvestViewportFollow != null &&
            PlantingService.Instance != null)
        {
            HarvestViewEntryView.BuildInto(
                hudRoot,
                PlantingService.Instance,
                harvestViewportFollow,
                jiaYuanWorld != null ? jiaYuanWorld.Viewport : null,
                () => harvestFarmGrid.ZhongTianAnchor,
                harvestEntrySprite,
                harvestCloseSprite,
                harvestFreePanSprite,
                bottomNavBar,
                () =>
                {
                    if (modalRt != null && modalRt.gameObject.activeSelf)
                        return true;
                    if (fertilizeModalRt != null && fertilizeModalRt.gameObject.activeSelf)
                        return true;
                    if (fruitModalRt != null && fruitModalRt.gameObject.activeSelf)
                        return true;
                    var sow = SowGestureController.Instance;
                    if (sow != null && sow.IsSlideMode)
                        return true;
                    return false;
                });
        }

        // SPEC §12：怪物入侵系统 — 顶部入口图标 + 全屏战斗，独立于种植服务运行。
        if (invasionService != null)
        {
            var entryFxConfig = InvasionEntryView.InvadingFxConfig.Default;
            InvasionEntryView.BuildInto(hudRoot, invasionService, entryFxConfig);
            var invasionBattle = InvasionBattleView.BuildInto(hudRoot, invasionService, bottomNavBar, jiaYuanWorld);
            if (invasionBattle != null)
                MainHudLayerRoot.ApplySortTier(invasionBattle.transform as RectTransform, MainUiSortTier.HudOverlay);
        }
        else
        {
            UnityEngine.Debug.LogWarning("AirMainMenuRuntimeBuilder: 无法创建 InvasionService，跳过怪物入侵系统 UI 构建。");
        }

        // SPEC §13 (v3.125)：好友系统 — 家园「好友」入口 + 好友列表弹窗 + 好友家园场景层 + 自家协助事件。
        // SPEC §9.14.10 (v3.139)：创角界面亲密度页签「去Ta家」复用同一好友家园层，故提升作用域。
        PetDemo.UI.Friend.FriendHomeScreenView friendHomeScreen = null;
        if (PlantingService.Instance != null)
        {
            // SPEC §13.8 (v3.132)：好友召唤 — 常驻组件，亲密度 ≥ 80 好友可被召唤到主角右侧 200px。
            var summonRoleParent = jiaYuanWorldContent != null ? jiaYuanWorldContent : canvasRect;
            var summonRolePresenter = GetComponent<MainRoleCunminPresenter>();
            var summonedFriendPresenter = GetComponent<PetDemo.UI.Friend.SummonedFriendPresenter>();
            if (summonedFriendPresenter == null)
                summonedFriendPresenter = gameObject.AddComponent<PetDemo.UI.Friend.SummonedFriendPresenter>();
            summonedFriendPresenter.Build(
                summonRoleParent, PlantingService.Instance, summonRolePresenter, bottomNavBar);

            friendHomeScreen = PetDemo.UI.Friend.FriendHomeScreenView.BuildInto(hudRoot, invasionService);
            var friendListPanel = PetDemo.UI.Friend.FriendListPanelView.BuildInto(
                hudRoot, PlantingService.Instance,
                friend =>
                {
                    if (friendHomeScreen != null)
                        friendHomeScreen.ShowFor(friend);
                },
                friend =>
                {
                    if (summonedFriendPresenter != null)
                        summonedFriendPresenter.Summon(friend);
                });
            PetDemo.UI.Friend.FriendEntryView.BuildInto(hudRoot, bottomNavBar, () =>
            {
                if (friendListPanel != null)
                    friendListPanel.Show();
            });
            PetDemo.UI.Friend.HomeAssistEventController.Attach(
                gameObject, PlantingService.Instance, hudRoot,
                GetComponent<MainRoleCunminPresenter>(), bottomNavBar);
        }

        // SPEC §9.8.15：顶部 DingUI（须在全部 UI 之后构建）。
        TopDingBarView topDing = null;
        if (bottomNavBar != null)
        {
            topDing = TopDingBarView.BuildInto(hudRoot, bottomNavBar);
            if (topDing != null)
                MainHudLayerRoot.ApplySortTier(topDing.transform as RectTransform, MainUiSortTier.HudTop);
        }

        // SPEC §9.8.15.1：公会拉手后在 TopDingBar 左下角登记 NPC 头像。
        if (topDing != null && builtGongHuiScreen != null)
            builtGongHuiScreen.BindTopDingBar(topDing);

        // SPEC §9.8.9.10：公会 Tab 下 TopDingBar 左下方「打开社区」入口。
        var gongHuiCommunityEntry = GongHuiCommunityEntryView.BuildInto(hudRoot, bottomNavBar, canvasRect);
        if (gongHuiCommunityEntry != null)
            MainHudLayerRoot.ApplySortTier(
                gongHuiCommunityEntry.transform as RectTransform, MainUiSortTier.HudTop);

        // SPEC §9.15 / §9.14.6（v3.143）：选档后默认打开创角界面；APP 预构建但不自动显示。
        // APP/创角期间隐藏 MainHudLayerRoot 与 JiaYuanWorldScreen；进入家园后恢复并切 Tab。
        var appScreen = AppScreenView.BuildInto(canvasRect, PlantingService.Instance);
        var singleChatPanel = AppSingleChatPanelView.GetOrCreate(canvasRect);
        var characterCreationScreen = CharacterCreationScreenView.BuildInto(canvasRect, PlantingService.Instance);

        if (appScreen != null || characterCreationScreen != null)
        {
            if (appScreen != null)
                MainHudLayerRoot.ApplySortTier(appScreen.transform as RectTransform, MainUiSortTier.HudPopup);
            if (singleChatPanel != null)
                MainHudLayerRoot.ApplySortTier(singleChatPanel.transform as RectTransform, MainUiSortTier.HudPopup);
            if (characterCreationScreen != null)
                MainHudLayerRoot.ApplySortTier(characterCreationScreen.transform as RectTransform, MainUiSortTier.HudPopup);

            void RestoreFromOverlay(string navKey)
            {
                if (appScreen != null)
                    appScreen.Hide();
                if (singleChatPanel != null)
                    singleChatPanel.Hide();
                if (characterCreationScreen != null)
                    characterCreationScreen.Hide();
                MainHudLayerRoot.SetVisible(true);
                if (jiaYuanWorld != null)
                    jiaYuanWorld.SetWorldScreenEnabled(true);
                if (bottomNavBar != null && !string.IsNullOrEmpty(navKey))
                    bottomNavBar.SetOpenKey(navKey);
            }

            void OpenCharacterCreationScreen()
            {
                if (characterCreationScreen == null || characterCreationScreen.IsShown)
                    return;
                if (appScreen != null)
                    appScreen.Hide();
                if (singleChatPanel != null)
                    singleChatPanel.Hide();
                MainHudLayerRoot.SetVisible(false);
                if (jiaYuanWorld != null)
                    jiaYuanWorld.SetWorldScreenEnabled(false);
                characterCreationScreen.Show();
            }

            void ReturnToAppFromCharacterCreation()
            {
                if (singleChatPanel != null)
                    singleChatPanel.Hide();
                MainHudLayerRoot.SetVisible(false);
                if (jiaYuanWorld != null)
                    jiaYuanWorld.SetWorldScreenEnabled(false);
                if (appScreen != null)
                    appScreen.Show();
            }

            if (topDing != null)
                topDing.BindNavigateToCharacterCreation(OpenCharacterCreationScreen);

            if (appScreen != null)
            {
                appScreen.OnOpenCharacterCreationRequested += OpenCharacterCreationScreen;
                appScreen.OnOpenSingleChatRequested += () =>
                {
                    if (singleChatPanel != null)
                        singleChatPanel.Show();
                };
            }

            if (singleChatPanel != null)
            {
                singleChatPanel.OnWolfClicked += () =>
                {
                    if (singleChatPanel != null)
                        singleChatPanel.Hide();
                    if (appScreen != null)
                        appScreen.Hide();
                    OpenCharacterCreationScreen();
                };
            }

            if (characterCreationScreen != null)
            {
                characterCreationScreen.OnNavigateToBottomNav += RestoreFromOverlay;
                characterCreationScreen.OnCloseRequested += ReturnToAppFromCharacterCreation;
                // SPEC §9.14.10 (v3.139)：亲密度页签「去Ta家」→ 恢复家园层后打开好友家园。
                characterCreationScreen.OnVisitFriendHome += friend =>
                {
                    RestoreFromOverlay(JiaYuanHomeFeatureEntriesView.JiaYuanNavKey);
                    if (friendHomeScreen != null)
                        friendHomeScreen.ShowFor(friend);
                };
            }

            MainHudLayerRoot.SetVisible(false);
            if (jiaYuanWorld != null)
                jiaYuanWorld.SetWorldScreenEnabled(false);

            OpenCharacterCreationScreen();
        }
    }

    // SPEC §9.8：主界面底部一级导航切换栏（5 按钮：公会 / 角色 / 家园 / 主线 / 商店）。
    // 优先使用 Inspector Override，其次 Resources.Load 预制件，两者均缺失时建无图占位以保 Play 不空跑。
    private BottomNavBarView BuildBottomNavBar(
        RectTransform canvasRect,
        JiaYuanWorldScreenView jiaYuanWorld = null,
        RectTransform heroStatsRowRt = null)
    {
        var prefab = bottomNavBarPrefab != null
            ? bottomNavBarPrefab
            : Resources.Load<RectTransform>(ResBottomNavBarPrefab);

        BottomNavBarView barView;
        if (prefab != null)
        {
            var instance = Instantiate(prefab, canvasRect, false);
            instance.name = "BottomNavBar";
            barView = instance.GetComponent<BottomNavBarView>();
        }
        else
        {
            UnityEngine.Debug.LogError(
                "AirMainMenuRuntimeBuilder: 缺少底部导航栏预制体 Resources/" + ResBottomNavBarPrefab +
                " — 请执行菜单 Tools/PetDemo/Generate Bottom Nav Bar Prefab。已使用纯代码占位回退。");
            barView = BuildBottomNavBarFallback(canvasRect);
        }

        if (barView != null)
        {
            var roleGrowthScreen = BuildRoleGrowthPanelIntoCanvas(canvasRect, barView);
            ApplyHudScreenTier(roleGrowthScreen);

            var levelUpDialog = ProtagonistLevelUpDialogView.BuildInto(canvasRect, roleGrowthScreen, barView);
            if (levelUpDialog != null)
                MainHudLayerRoot.ApplySortTier(levelUpDialog.transform as RectTransform, MainUiSortTier.HudPopup);

            // SPEC §9.8.8 / §9.8.12 (v3.40)：将 IPlantingService 透传给主线层，
            // 用于「饿肚子提示框 → 食物仓库」流程访问 RoleStats.stamina / PlayerFoodBag。
            var mainStoryScreen = MainStoryLineScreenView.BuildInto(canvasRect, barView, PlantingService.Instance);
            ApplyHudScreenTier(mainStoryScreen);

            // SPEC §9.8.8 (v3.167)：主线「选择关卡」全屏层暂时停用（保留脚本与预制体，不再预建）。
            // 主线「前往」自 v3.167 起直接打开 §12.11 InvasionBattleModal_2；需恢复时取消下方注释即可。
            // var levelSelectScreen = LevelSelectScreenPanelView.BuildInto(canvasRect, barView, mainStoryScreen);
            // ApplyHudScreenTier(levelSelectScreen);

            // SPEC §9.8.16：竞技场全屏界面（预制体）+ 底栏 Tab 清理订阅。
            var arenaScreen = ArenaScreenPanelView.BuildInto(canvasRect, barView);
            ApplyHudScreenTier(arenaScreen);

            // SPEC §9.8.9 (v3.123)：公会场景层（预制体 + 摇杆/碰撞/建筑/NPC）；§9.8.10：商店全屏背景层。
            builtGongHuiScreen = GongHuiScreenView.BuildInto(canvasRect, barView);
            ApplyHudScreenTier(builtGongHuiScreen);

            var shangDianScreen = BottomNavSimpleBackgroundScreenView.BuildInto(canvasRect, barView, "ShangDianScreen",
                BottomNavSimpleBackgroundScreenView.ShangDianNavKey,
                BottomNavSimpleBackgroundScreenView.ResShangDianBackground);
            ApplyHudScreenTier(shangDianScreen);

            // SPEC §9.8.11 / §9.8.13 (v3.41)：家园 Tab 上的订单弹窗 + 仓库入口；
            // 仓库按钮自 v3.41 起打开 §9.8.13 统一仓库预制体，需透传 IPlantingService。
            if (jiaYuanWorld != null)
                jiaYuanWorld.BindBottomNavBar(barView);

            JiaYuanHomeFeatureEntriesView.BuildInto(
                canvasRect, barView, PlantingService.Instance);

            barView.OnOpenChanged += (index, key) =>
            {
                UnityEngine.Debug.Log("AirMainMenuRuntimeBuilder: BottomNavBar open -> " + key + " (index=" + index + ")");
            };

            var invasion = InvasionService.Instance;
            if (invasion != null)
            {
                invasion.SetBattleDeniedNavigation(barView);
                invasion.OnAutoChainBattleStartDenied += () =>
                {
                    LevelSelectScreenPanelView.HideIfAny();
                    mainStoryScreen.ShowHungryDialog();
                };
            }

            // SPEC §9.5.2 (v3.62)：精灵巡逻需订阅底栏 OpenKey（JiaYuan 暂停/重抽签）。
            var petCompanionPresenter = GetComponent<PetCompanionPresenter>();
            if (petCompanionPresenter != null)
                petCompanionPresenter.BindBottomNavBar(barView);

            // SPEC §9.5.4 (v3.91)：主角拖动需订阅底栏，离开家园时结束拖动。
            var rolePresenter = GetComponent<MainRoleCunminPresenter>();
            if (rolePresenter != null)
                rolePresenter.BindBottomNavBar(barView);
        }

        return barView;
    }

    // SPEC §9.10：实例化「角色」层；优先 Inspector / Resources 预制体，缺失时纯代码兜底。
    private RoleGrowthScreenView BuildRoleGrowthPanelIntoCanvas(RectTransform canvasRect, BottomNavBarView barView)
    {
        var prefab = roleGrowthPanelPrefab != null
            ? roleGrowthPanelPrefab
            : Resources.Load<RectTransform>(ResRoleGrowthPanelPrefab);

        RoleGrowthScreenView screen;
        if (prefab != null)
        {
            var inst = Instantiate(prefab, canvasRect, false);
            inst.name = "RoleGrowthPanel";
            screen = inst.GetComponent<RoleGrowthScreenView>();
            if (screen == null)
            {
                UnityEngine.Debug.LogError(
                    "AirMainMenuRuntimeBuilder: RoleGrowthPanel 预制体根节点缺少 RoleGrowthScreenView — 已销毁实例并回退纯代码占位。");
                Destroy(inst.gameObject);
                screen = BuildRoleGrowthPanelFallback(canvasRect, barView);
            }
            else
            {
                var barRt = barView.transform as RectTransform;
                if (barRt != null)
                    inst.SetSiblingIndex(barRt.GetSiblingIndex());
            }
        }
        else
        {
            UnityEngine.Debug.LogError(
                "AirMainMenuRuntimeBuilder: 缺少角色成长预制体 Resources/" + ResRoleGrowthPanelPrefab +
                " — 请执行菜单 Tools/PetDemo/Generate Role Growth Panel Prefab。已使用纯代码占位回退。");
            screen = BuildRoleGrowthPanelFallback(canvasRect, barView);
        }

        if (screen != null)
        {
            screen.BindBottomNavBar(barView);
            screen.ApplyMainBottomNavKey(barView.OpenKey);
            WireRoleGrowthPetBagPage(screen);
        }

        return screen;
    }

    private void WireRoleGrowthPetBagPage(RoleGrowthScreenView screen)
    {
        if (screen == null)
            return;

        var petBagPrefab = Resources.Load<GameObject>(ResRoleGrowthPetBagPagePrefab);
        if (petBagPrefab == null)
        {
            UnityEngine.Debug.LogError(
                "AirMainMenuRuntimeBuilder: 缺少精灵背包页预制体 Resources/" + ResRoleGrowthPetBagPagePrefab +
                " — 请执行 Tools/PetDemo/Generate Role Growth Pet Bag Page Prefab。");
            return;
        }

        var service = PlantingService.Instance;
        if (service == null)
        {
            UnityEngine.Debug.LogWarning("AirMainMenuRuntimeBuilder: PlantingService 未就绪，跳过精灵背包页挂载。");
            return;
        }

        screen.WirePetBagPage(service, petBagPrefab);
    }

    private static readonly string[] RoleGrowthTabFallbackKeys = { "ShuXing", "JiNeng", "TianFu", "JingLing" };
    private static readonly string[] RoleGrowthPageFallbackNames = { "Page_ShuXing", "Page_JiNeng", "Page_TianFu", "Page_JingLing" };

    private RoleGrowthScreenView BuildRoleGrowthPanelFallback(RectTransform canvasRect, BottomNavBarView barView)
    {
        const float tabBarH = RoleGrowthTabBarView.BarHeight;
        const float tabSlotW = RoleGrowthTabBarView.TabSlotWidth;

        var rootRt = CreateChildRect(canvasRect, "RoleGrowthPanel",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        StretchFull(rootRt);

        var dimRt = CreateChildRect(rootRt, "DimLayer",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        StretchFull(dimRt);
        var dimImage = dimRt.gameObject.AddComponent<Image>();
        dimImage.color = new Color(0.04f, 0.05f, 0.08f, 0.88f);
        dimImage.raycastTarget = true;

        var pageAreaRt = CreateChildRect(rootRt, "PageArea",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        StretchFull(pageAreaRt);
        pageAreaRt.offsetMax = new Vector2(0f, -tabBarH);

        var pages = new System.Collections.Generic.List<RectTransform>(RoleGrowthPageFallbackNames.Length);
        for (int p = 0; p < RoleGrowthPageFallbackNames.Length; p++)
        {
            var pageRt = CreateChildRect(pageAreaRt, RoleGrowthPageFallbackNames[p],
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            StretchFull(pageRt);
            pageRt.gameObject.SetActive(p == 0);
            if (RoleGrowthPageFallbackNames[p] == "Page_JingLing")
            {
                var mountRt = CreateChildRect(pageRt, "PetBagPageMount",
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                StretchFull(mountRt);
            }
            pages.Add(pageRt);
        }

        var tabBarRt = CreateChildRect(rootRt, "RoleGrowthTabBar",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(1080f, tabBarH));
        tabBarRt.pivot = new Vector2(0.5f, 1f);

        var tabBarView = tabBarRt.gameObject.AddComponent<RoleGrowthTabBarView>();
        var tabButtons = new System.Collections.Generic.List<BottomNavButtonView>(RoleGrowthTabFallbackKeys.Length);

        const int defaultTab = 0;
        for (int i = 0; i < RoleGrowthTabFallbackKeys.Length; i++)
        {
            bool isOpen = (i == defaultTab);

            var slotRt = CreateChildRect(tabBarRt, "RoleGrowthTabSlot_" + RoleGrowthTabFallbackKeys[i],
                new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(i * tabSlotW, 0f), new Vector2(tabSlotW, tabBarH));
            slotRt.pivot = new Vector2(0f, 0f);

            var openRt = CreateFallbackStateRect(slotRt, "OpenState", tabSlotW, tabBarH);
            openRt.gameObject.SetActive(isOpen);
            var closedRt = CreateFallbackStateRect(slotRt, "ClosedState", tabSlotW, tabBarH);
            closedRt.gameObject.SetActive(!isOpen);

            var hitGo = new GameObject("HitArea", typeof(RectTransform), typeof(Image), typeof(Button));
            var hitRt = hitGo.GetComponent<RectTransform>();
            hitRt.SetParent(slotRt, false);
            hitRt.anchorMin = Vector2.zero;
            hitRt.anchorMax = Vector2.one;
            hitRt.pivot = new Vector2(0.5f, 0.5f);
            hitRt.offsetMin = Vector2.zero;
            hitRt.offsetMax = Vector2.zero;
            var hitImage = hitGo.GetComponent<Image>();
            hitImage.color = new Color(1f, 1f, 1f, 0f);
            hitImage.raycastTarget = true;
            var hitButton = hitGo.GetComponent<Button>();
            hitButton.transition = Selectable.Transition.None;
            hitButton.targetGraphic = hitImage;

            var btnView = slotRt.gameObject.AddComponent<BottomNavButtonView>();
            InjectButtonViewFields(btnView, RoleGrowthTabFallbackKeys[i], slotRt, openRt, closedRt, hitButton);
            tabButtons.Add(btnView);
        }

        InjectRoleGrowthTabBarViewFields(tabBarView, defaultTab, tabButtons);

        var screenView = rootRt.gameObject.AddComponent<RoleGrowthScreenView>();
        InjectRoleGrowthScreenViewFields(screenView, rootRt, tabBarView, pages);

        var barRt = barView.transform as RectTransform;
        if (barRt != null)
            rootRt.SetSiblingIndex(barRt.GetSiblingIndex());

        rootRt.gameObject.SetActive(false);
        WireRoleGrowthPetBagPage(screenView);
        return screenView;
    }

    private void InjectRoleGrowthTabBarViewFields(RoleGrowthTabBarView view, int defaultOpenIndex,
        System.Collections.Generic.List<BottomNavButtonView> tabs)
    {
        var t = typeof(RoleGrowthTabBarView);
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        t.GetField("defaultOpenIndex", flags)?.SetValue(view, defaultOpenIndex);
        t.GetField("tabButtons", flags)?.SetValue(view, tabs);
    }

    private void InjectRoleGrowthScreenViewFields(RoleGrowthScreenView view, RectTransform rootRt,
        RoleGrowthTabBarView tabBar, System.Collections.Generic.List<RectTransform> pageList)
    {
        var t = typeof(RoleGrowthScreenView);
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        t.GetField("rootRt", flags)?.SetValue(view, rootRt);
        t.GetField("tabBar", flags)?.SetValue(view, tabBar);
        t.GetField("pages", flags)?.SetValue(view, pageList);
    }

    private static readonly string[] BottomNavFallbackKeys = { "GongHui", "JueSe", "JiaYuan", "ZhuXian", "ShangDian" };

    // SPEC §9.8.5：预制件缺失时的最小化纯代码兜底，仅保证 Play 模式有可见占位结构。
    private BottomNavBarView BuildBottomNavBarFallback(RectTransform canvasRect)
    {
        var rootRt = CreateChildRect(canvasRect, "BottomNavBar",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 0f), new Vector2(1080f, 160f));
        rootRt.pivot = new Vector2(0.5f, 0f);

        var barView = rootRt.gameObject.AddComponent<BottomNavBarView>();
        var buttons = new System.Collections.Generic.List<BottomNavButtonView>(BottomNavFallbackKeys.Length);

        float runningX = 0f;
        const int DefaultOpenIndex = 2; // JiaYuan
        for (int i = 0; i < BottomNavFallbackKeys.Length; i++)
        {
            bool isOpen = (i == DefaultOpenIndex);
            float width = isOpen ? 364f : 179f;

            var slotRt = CreateChildRect(rootRt, "BottomNavSlot_" + BottomNavFallbackKeys[i],
                new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(runningX, 0f), new Vector2(width, 160f));
            slotRt.pivot = new Vector2(0f, 0f);

            var openRt = CreateFallbackStateRect(slotRt, "OpenState", 364f);
            openRt.gameObject.SetActive(isOpen);
            var closedRt = CreateFallbackStateRect(slotRt, "ClosedState", 179f);
            closedRt.gameObject.SetActive(!isOpen);

            var hitGo = new GameObject("HitArea", typeof(RectTransform), typeof(Image), typeof(Button));
            var hitRt = hitGo.GetComponent<RectTransform>();
            hitRt.SetParent(slotRt, false);
            hitRt.anchorMin = Vector2.zero;
            hitRt.anchorMax = Vector2.one;
            hitRt.pivot = new Vector2(0.5f, 0.5f);
            hitRt.offsetMin = Vector2.zero;
            hitRt.offsetMax = Vector2.zero;
            var hitImage = hitGo.GetComponent<Image>();
            hitImage.color = new Color(1f, 1f, 1f, 0f);
            hitImage.raycastTarget = true;
            var hitButton = hitGo.GetComponent<Button>();
            hitButton.transition = Selectable.Transition.None;
            hitButton.targetGraphic = hitImage;

            var btnView = slotRt.gameObject.AddComponent<BottomNavButtonView>();
            // 通过反射兜底注入字段（运行时无法用 SerializedObject）。
            InjectButtonViewFields(btnView, BottomNavFallbackKeys[i], slotRt, openRt, closedRt, hitButton);
            buttons.Add(btnView);

            runningX += width;
        }

        InjectBarViewFields(barView, DefaultOpenIndex, buttons);
        return barView;
    }

    private RectTransform CreateFallbackStateRect(RectTransform parent, string name, float width, float height = 160f)
    {
        var rt = CreateChildRect(parent, name,
            new Vector2(0f, 0f), new Vector2(0f, 0f),
            Vector2.zero, new Vector2(width, height));
        rt.pivot = new Vector2(0f, 0f);

        var bgGo = new GameObject(name == "OpenState" ? "OpenBg" : "ClosedBg",
            typeof(RectTransform), typeof(Image));
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.SetParent(rt, false);
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var bgImage = bgGo.GetComponent<Image>();
        bgImage.color = name == "OpenState"
            ? new Color(0.18f, 0.45f, 0.22f, 0.65f)
            : new Color(0.12f, 0.12f, 0.12f, 0.55f);
        bgImage.raycastTarget = false;

        return rt;
    }

    private void InjectBarViewFields(BottomNavBarView view, int defaultOpenIndex,
        System.Collections.Generic.List<BottomNavButtonView> buttons)
    {
        var t = typeof(BottomNavBarView);
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        t.GetField("defaultOpenIndex", flags)?.SetValue(view, defaultOpenIndex);
        t.GetField("buttons", flags)?.SetValue(view, buttons);
    }

    private void InjectButtonViewFields(BottomNavButtonView view, string key,
        RectTransform selfRt, RectTransform openState, RectTransform closedState, Button hitButton)
    {
        var t = typeof(BottomNavButtonView);
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        t.GetField("key", flags)?.SetValue(view, key);
        t.GetField("selfRt", flags)?.SetValue(view, selfRt);
        t.GetField("openState", flags)?.SetValue(view, openState);
        t.GetField("closedState", flags)?.SetValue(view, closedState);
        t.GetField("hitButton", flags)?.SetValue(view, hitButton);
    }

    // SPEC §9.7：肥料入口按钮，紧贴 SeedWarehouseButton 右侧。
    private Button BuildFertilizeEntryButton(RectTransform canvasRect, Sprite fertilizeSprite)
    {
        if (fertilizeSprite == null)
            return null;

        var rt = CreateChildRect(canvasRect, "FertilizeEntryButton",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(450f, 290f), new Vector2(150f, 150f));
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = fertilizeSprite;
        image.preserveAspect = true;
        image.raycastTarget = true;
        var button = rt.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;
        return button;
    }

    // SPEC §9.7 / §3.12：肥料仓库独立预制体；缺失时回退种子仓库同款底板并打 Error（需执行菜单生成预制体）。
    private RectTransform BuildFertilizerWarehousePanel(RectTransform modalRt, Sprite fallbackWarehouseSprite)
    {
        var overrideGo = fertilizerWarehousePanelPrefab != null ? fertilizerWarehousePanelPrefab.gameObject : null;
        var loadedGo = Resources.Load<GameObject>(ResFertilizerWarehousePanelPrefab);
        var loadedRt = Resources.Load<RectTransform>(ResFertilizerWarehousePanelPrefab);
        var prefabGo = overrideGo ?? loadedGo ?? (loadedRt != null ? loadedRt.gameObject : null);

        if (prefabGo == null)
        {
            UnityEngine.Debug.LogError(
                "AirMainMenuRuntimeBuilder: 缺少肥料仓库预制体 Resources/" + ResFertilizerWarehousePanelPrefab +
                " — 请在 Unity 菜单执行 Tools/PetDemo/Generate Fertilizer Warehouse Prefab。暂时使用仓库底板占位。");
            return BuildWarehouseBackground(modalRt, fallbackWarehouseSprite);
        }

        var instance = Instantiate(prefabGo, modalRt, false);
        var rt = instance.GetComponent<RectTransform>();
        return rt;
    }

    private RectTransform BuildWarehouseBackground(RectTransform modalRt, Sprite warehouseSprite)
    {
        var prefab = warehouseBackgroundPrefab != null
            ? warehouseBackgroundPrefab
            : Resources.Load<RectTransform>(ResWarehouseBackgroundPrefab);

        if (prefab != null)
        {
            var panel = Instantiate(prefab, modalRt, false);
            panel.name = "WarehouseBackground";
            var image = panel.GetComponent<Image>();
            if (image != null && image.sprite == null)
                image.sprite = warehouseSprite;
            return panel;
        }

        var panelRt = CreateChildRect(modalRt, "WarehouseBackground",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -355f), new Vector2(1080f, 800f));
        var panelImage = panelRt.gameObject.AddComponent<Image>();
        panelImage.sprite = warehouseSprite;
        panelImage.preserveAspect = true;
        panelImage.raycastTarget = true;
        return panelRt;
    }

    private void BuildHeroStatsDisplay(
        RectTransform canvasRect,
        out Text atkText,
        out Text defText,
        out Text hpText,
        out Text agilityText,
        out RectTransform heroStatsRowRt)
    {
        var rowAnchoredPosition = heroStatsRowAnchoredPosition;
        if (Mathf.Approximately(rowAnchoredPosition.y, 0f))
            rowAnchoredPosition.y = HeroStatsDefaultPosY;
        else if (Mathf.Approximately(rowAnchoredPosition.y, LegacyHeroStatsRowDefaultPosY))
            rowAnchoredPosition.y = HeroStatsDefaultPosY;

        var prefab = Resources.Load<RectTransform>(ResHeroStatsRowPrefab);
        RectTransform rowRt;
        if (prefab != null)
        {
            rowRt = Instantiate(prefab, canvasRect, false);
            rowRt.name = "HeroStatsRow";
            rowRt.anchoredPosition = rowAnchoredPosition;
        }
        else
        {
            rowRt = CreateChildRect(
                canvasRect, "HeroStatsRow",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                rowAnchoredPosition, new Vector2(900f, 80f));
        }

        rowRt.gameObject.SetActive(false);
        heroStatsRowRt = rowRt;

        atkText = FindHeroStatText(rowRt, "AtkText", heroAttack.ToString(), heroAtkPosX);
        defText = FindHeroStatText(rowRt, "DefText", heroDefense.ToString(), heroDefPosX);
        hpText = FindHeroStatText(rowRt, "HpText", heroHp.ToString(), heroHpPosX);
        agilityText = FindHeroStatText(rowRt, "AgilityText", heroAgility.ToString(), heroAgilityPosX);
    }

    private Text FindHeroStatText(RectTransform rowRt, string childName, string defaultContent, float xOffset)
    {
        var child = rowRt.Find(childName);
        if (child != null)
        {
            var existing = child.GetComponent<Text>();
            if (existing != null)
            {
                existing.text = defaultContent;
                return existing;
            }
        }

        return CreateHeroStatText(rowRt, childName, defaultContent, xOffset);
    }

    private Text CreateHeroStatText(RectTransform parent, string name, string content, float xOffset)
    {
        var textRt = CreateChildRect(
            parent, name,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(xOffset, 0f), new Vector2(210f, 72f));
        var text = textRt.gameObject.AddComponent<Text>();
        text.text = content;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.fontSize = heroStatsFontSize;
        text.font = FarmGridView.LoadBuiltinFont();
        text.raycastTarget = false;
        return text;
    }

    private static Camera ResolveMainUiCamera()
    {
        var cam = Camera.main;
        if (cam != null)
            return cam;

        var cameras = Object.FindObjectsOfType<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].enabled)
                return cameras[i];
        }

        return null;
    }

    private static void ApplyHudScreenTier(Component view)
    {
        if (view == null)
            return;
        MainHudLayerRoot.ApplySortTier(view.transform as RectTransform, MainUiSortTier.HudScreen);
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

namespace PetDemo.UI
{
    public class MainHeroStatsPresenter : MonoBehaviour
    {
        private IPlantingService service;
        private RectTransform canvasRect;
        private RectTransform fruitFlyTarget;
        private Text atkText;
        private Text defText;
        private Text hpText;
        private Text agilityText;
        private bool isPlayingReward;

        public void Init(IPlantingService svc, RectTransform canvas, Text atk, Text def, Text hp, Text agility,
            RectTransform fruitHarvestFlyTarget = null)
        {
            service = svc;
            canvasRect = canvas;
            fruitFlyTarget = fruitHarvestFlyTarget;
            atkText = atk;
            defText = def;
            hpText = hp;
            agilityText = agility;
            RefreshTexts();
            if (service != null)
            {
                service.OnRoleStatsChanged += OnRoleStatsChanged;
                service.OnHarvestFruitReady += OnHarvestFruitReady;
            }
        }

        private void OnDestroy()
        {
            if (service != null)
            {
                service.OnRoleStatsChanged -= OnRoleStatsChanged;
                service.OnHarvestFruitReady -= OnHarvestFruitReady;
            }
        }

        private void OnRoleStatsChanged() { RefreshTexts(); }
        private void OnHarvestFruitReady(string tileId, string plantConfigId, int amount)
        {
            if (!isPlayingReward) StartCoroutine(PlayHarvestFruitFx(tileId, plantConfigId, amount));
        }

        private IEnumerator PlayHarvestFruitFx(string tileId, string plantConfigId, int amount)
        {
            isPlayingReward = true;
            Sprite iconSprite = null;
            if (service != null)
            {
                var cfg = service.GetPlantConfig(plantConfigId);
                if (cfg != null)
                {
                    var path = cfg.ResolveFruitIconResourcePath();
                    if (!string.IsNullOrEmpty(path))
                        iconSprite = Resources.Load<Sprite>(path);
                }
            }
            if (iconSprite == null)
                iconSprite = Resources.Load<Sprite>("AirUI/ShouHuo-0");

            if (iconSprite == null || canvasRect == null || fruitFlyTarget == null)
            {
                isPlayingReward = false;
                yield break;
            }

            var fxGo = new GameObject("HarvestFruitFx");
            var fxRt = fxGo.AddComponent<RectTransform>();
            fxRt.SetParent(canvasRect, false);
            fxRt.sizeDelta = new Vector2(56f, 56f);
            var fxImage = fxGo.AddComponent<Image>();
            fxImage.sprite = iconSprite;
            fxImage.preserveAspect = true;

            Vector2 fromScreen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (FarmGridView.Instance != null)
                FarmGridView.Instance.TryGetTileScreenPosition(tileId, out fromScreen);
            var toScreen = RectTransformUtility.WorldToScreenPoint(null, fruitFlyTarget.transform.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, fromScreen, null, out var fromLocal);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, toScreen, null, out var toLocal);

            const float duration = 0.55f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);
                float eased = 1f - Mathf.Pow(1f - p, 3f);
                fxRt.anchoredPosition = Vector2.LerpUnclamped(fromLocal, toLocal, eased);
                yield return null;
            }
            Destroy(fxGo);
            isPlayingReward = false;
        }

        private void RefreshTexts()
        {
            if (service == null) return;
            var role = service.GetRoleStats();
            if (role == null) return;
            if (atkText != null) atkText.text = role.atk.ToString();
            if (defText != null) defText.text = role.def.ToString();
            if (hpText != null) hpText.text = role.maxHp.ToString();
            if (agilityText != null) agilityText.text = role.agility.ToString();
        }
    }
}

namespace PetDemo.UI.Farm
{
    public class TilePlantTipsPresenter : MonoBehaviour
    {
        private const string TipBackgroundResPath = "AirUI/FeiLiaoUI_1";
        private const float AutoCloseSeconds = 5f;
        private const float VerticalOffset = 130f;

        public static TilePlantTipsPresenter Instance { get; private set; }

        private IPlantingService service;
        private RectTransform canvasRect;
        private RectTransform blockerRt;
        private Button blockerButton;
        private RectTransform tipRt;
        private Image tipBg;
        private Text line1Text;
        private Text line2Text;
        private Coroutine autoCloseRoutine;

        public void Init(IPlantingService svc, RectTransform canvas)
        {
            service = svc;
            canvasRect = canvas;
            Instance = this;
            EnsureUi();
            HideTip();
        }

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool TryShowTip(string tileId)
        {
            if (!TryBuildTipData(tileId, out var line1, out var line2))
                return false;
            EnsureUi();
            if (blockerRt == null || tipRt == null)
                return false;

            Vector2 fromScreen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (FarmGridView.Instance != null)
                FarmGridView.Instance.TryGetTileScreenPosition(tileId, out fromScreen);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, fromScreen, null, out var fromLocal);

            tipRt.anchoredPosition = fromLocal + new Vector2(0f, VerticalOffset);
            line1Text.text = line1;
            line2Text.text = line2;
            line2Text.gameObject.SetActive(!string.IsNullOrEmpty(line2));

            blockerRt.gameObject.SetActive(true);
            tipRt.gameObject.SetActive(true);
            RestartAutoClose();
            return true;
        }

        private bool TryBuildTipData(string tileId, out string line1, out string line2)
        {
            line1 = string.Empty;
            line2 = string.Empty;
            if (service == null || string.IsNullOrEmpty(tileId))
                return false;

            var tile = FindTile(tileId);
            if (tile == null || string.IsNullOrEmpty(tile.plantInstanceId) || tile.harvest == HarvestFlag.AwaitingHarvest)
                return false;

            var plant = service.GetPlant(tile.plantInstanceId);
            if (plant == null)
                return false;

            if (plant.appearanceNode <= 2)
            {
                line1 = "未知";
                return true;
            }

            var cfg = service.GetPlantConfig(plant.plantConfigId);
            if (cfg == null)
                return false;

            line1 = cfg.displayName;
            line2 = BuildPlantTipLine2(cfg);
            return true;
        }

        private static string BuildPlantTipLine2(PlantConfig cfg)
        {
            if (cfg == null)
                return string.Empty;
            var line = $"每次收获 {cfg.harvestFruitCount} 个果实；关联 {StatLabel(cfg.harvestRewardStat)}";
            if (!string.IsNullOrWhiteSpace(cfg.eatBuffIconResource))
                line += "；吃下附带演示Buff（不参与战斗结算）";
            return line;
        }

        private static string StatLabel(RoleStatType statType)
        {
            switch (statType)
            {
                case RoleStatType.Def:
                    return "防御";
                case RoleStatType.MaxHp:
                    return "生命上限";
                case RoleStatType.Agility:
                    return "敏捷";
                default:
                    return "攻击";
            }
        }

        private CropTile FindTile(string tileId)
        {
            for (int i = 1; i <= service.FarmTileCount; i++)
            {
                var tile = service.GetTileByOrder(i);
                if (tile != null && tile.tileId == tileId)
                    return tile;
            }

            return null;
        }

        private void EnsureUi()
        {
            if (canvasRect == null)
                return;
            if (blockerRt != null && tipRt != null)
                return;

            var blockerGo = new GameObject("TilePlantTipBlocker", typeof(RectTransform), typeof(Image), typeof(Button));
            blockerRt = blockerGo.GetComponent<RectTransform>();
            blockerRt.SetParent(canvasRect, false);
            blockerRt.anchorMin = Vector2.zero;
            blockerRt.anchorMax = Vector2.one;
            blockerRt.offsetMin = Vector2.zero;
            blockerRt.offsetMax = Vector2.zero;
            blockerRt.pivot = new Vector2(0.5f, 0.5f);

            var blockerImage = blockerGo.GetComponent<Image>();
            blockerImage.color = new Color(0f, 0f, 0f, 0.001f);
            blockerImage.raycastTarget = true;

            blockerButton = blockerGo.GetComponent<Button>();
            blockerButton.transition = Selectable.Transition.None;
            blockerButton.targetGraphic = blockerImage;
            blockerButton.onClick.AddListener(HideTip);
            MainHudLayerRoot.ApplySortTier(blockerRt, MainUiSortTier.HudPopup);

            var tipGo = new GameObject("TilePlantTip", typeof(RectTransform), typeof(Image));
            tipRt = tipGo.GetComponent<RectTransform>();
            tipRt.SetParent(blockerRt, false);
            tipRt.anchorMin = new Vector2(0.5f, 0.5f);
            tipRt.anchorMax = new Vector2(0.5f, 0.5f);
            tipRt.pivot = new Vector2(0.5f, 0f);
            tipRt.sizeDelta = new Vector2(210f, 220f);

            tipBg = tipGo.GetComponent<Image>();
            tipBg.sprite = Resources.Load<Sprite>(TipBackgroundResPath);
            tipBg.color = tipBg.sprite == null ? new Color(0.18f, 0.15f, 0.12f, 0.92f) : Color.white;
            tipBg.preserveAspect = true;
            tipBg.raycastTarget = true;
            if (tipBg.sprite == null)
                UnityEngine.Debug.LogWarning("TilePlantTipsPresenter: 未找到 Tips 背景图 Resources/" + TipBackgroundResPath + "，使用纯色底板回退。");

            line1Text = CreateTipText(tipRt, "Line1", new Vector2(0f, -80f), 32, TextAnchor.MiddleCenter);
            line2Text = CreateTipText(tipRt, "Line2", new Vector2(0f, -140f), 24, TextAnchor.MiddleCenter);
        }

        private static Text CreateTipText(RectTransform parent, string name, Vector2 anchoredPos, int fontSize, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(180f, 48f);

            var text = go.GetComponent<Text>();
            text.font = FarmGridView.LoadBuiltinFont();
            text.fontSize = fontSize;
            text.color = new Color(0.15f, 0.11f, 0.08f, 1f);
            text.alignment = align;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private void RestartAutoClose()
        {
            if (autoCloseRoutine != null)
                StopCoroutine(autoCloseRoutine);
            autoCloseRoutine = StartCoroutine(AutoCloseAfterDelay());
        }

        private IEnumerator AutoCloseAfterDelay()
        {
            yield return new WaitForSeconds(AutoCloseSeconds);
            HideTip();
        }

        private void HideTip()
        {
            if (autoCloseRoutine != null)
            {
                StopCoroutine(autoCloseRoutine);
                autoCloseRoutine = null;
            }

            if (tipRt != null)
                tipRt.gameObject.SetActive(false);
            if (blockerRt != null)
                blockerRt.gameObject.SetActive(false);
        }
    }
}
