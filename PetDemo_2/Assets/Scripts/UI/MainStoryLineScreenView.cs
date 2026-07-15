// SPEC §9.8.8 (v3.40)：底部导航「主线 / ZhuXian」打开时显示的全屏关卡选择层。
// 旧 LevelSlot_1/2/3 已下线；本期改为「章节标记点 + 前往按钮 + 饿肚子提示框」三段式。
// SPEC §9.8.8 (v3.47)：体力 HUD；每次进入主线或统一仓库 Hide 后刷新。
// SPEC §9.8.8 (v3.240)：体力 HUD 改右上；左上 BackButton（common_bg_9）返回 GongHui。
using System;
using PetDemo.Battle;
using PetDemo.Core;
using PetDemo.Farm;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public class MainStoryLineScreenView : MonoBehaviour
    {
        public const string ResMainStoryBackground = "AirUI/ZhuXian_1";
        public const string ResChapterPinUnselected = "AirUI/ZhuXian_1_0";
        public const string ResChapterPinSelected = "AirUI/ZhuXian_1_1";
        public const string ResGoButton = "AirUI/ZhanDouKaiShi";
        public const string ResBackButton = "AirUI/common_bg_9";
        public const string ZhuXianNavKey = "ZhuXian";

        // SPEC §9.8.8.1：ChapterPin Demo 默认坐标与尺寸。
        private static readonly Vector2 PinSize = new Vector2(220f, 220f);
        private static readonly Vector2 PinAnchoredPosition = new Vector2(25f, -332f);

        // SPEC §9.8.8.3：前往按钮在 pin 下方 200px，尺寸 120×120（视觉 ×0.5）。
        private static readonly Vector2 GoButtonSize = new Vector2(120f, 120f);
        private const float GoButtonOffsetY = -200f;

        // SPEC §9.8.8 (v3.240)：左上角返回公会。
        private static readonly Vector2 BackButtonSize = new Vector2(120f, 120f);
        private static readonly Vector2 BackButtonPos = new Vector2(20f, -20f);

        // SPEC §9.8.8.4：饿肚子提示框文案与样式。
        private const string HungryDialogText = "阿狼还饿着肚子，需要吃饱了才能上路！";

        // SPEC §9.8.8.4 (v3.105)：与 §12.9 单场扣费、§9.8.13.5「开始」显示阈值一致。
        private const int HungryDialogStaminaThreshold = InvasionService.BattleStaminaCostPerEncounter;

        // SPEC §9.8.8 (v3.47 / v3.240)：右上角体力 HUD（与 §9.8.12.4 StaminaBarView 复用；槽高与 §9.8.13 仓库 275×116 一致）。
        private static readonly Vector2 MainStoryStaminaHudSize = new Vector2(300f, 168f);
        private static readonly Vector2 MainStoryStaminaBarSlotSize = new Vector2(275f, 116f);
        private static readonly Vector2 MainStoryStaminaHudAnchoredPos = new Vector2(-20f, -20f);

        private RectTransform rootRt;
        private BottomNavBarView bottomNav;
        private IPlantingService plantingService;

        private RectTransform canvasRectCache;
        private RectTransform staminaBarSlotRt;
        private Text staminaValueText;
        private StaminaBarView mainStoryStaminaBar;
        private bool warehouseHiddenSubscribed;
        private WarehouseHubPanelView subscribedWarehouseHub;
        private Image chapterPinImage;
        private Sprite chapterPinUnselectedSprite;
        private Sprite chapterPinSelectedSprite;
        private bool chapterPinSelected;

        private RectTransform goButtonRt;
        private RectTransform hungryDialogRt;
        private FoodWarehouseModalView foodWarehouseModal;
        private LevelSelectScreenPanelView levelSelectPanel;

        private static MainStoryLineScreenView instance;

        public static MainStoryLineScreenView BuildInto(
            RectTransform canvasRect,
            BottomNavBarView barView,
            IPlantingService plantingService = null)
        {
            if (canvasRect == null || barView == null)
                return null;

            var root = BottomNavAttachedScreenLayout.CreateRootBelowBottomNav(
                canvasRect, barView, "MainStoryLineScreen");
            BottomNavAttachedScreenLayout.AddStretchedResourcesBackground(
                root, ResMainStoryBackground, nameof(MainStoryLineScreenView));

            var view = root.gameObject.AddComponent<MainStoryLineScreenView>();
            view.rootRt = root;
            view.bottomNav = barView;
            view.plantingService = plantingService;
            view.canvasRectCache = canvasRect;

            // SPEC §9.8.8：Title「第1章」固定上方。
            var titleRt = BottomNavAttachedScreenLayout.CreateChildRect(root, "Title",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -30f), new Vector2(720f, 88f));
            titleRt.pivot = new Vector2(0.5f, 1f);
            var titleText = titleRt.gameObject.AddComponent<Text>();
            titleText.text = "第1章";
            titleText.font = FarmGridView.LoadBuiltinFont();
            titleText.fontSize = 52;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.98f, 0.95f, 0.88f, 1f);
            titleText.raycastTarget = false;

            // SPEC §9.8.8.2：透明 EmptyAreaCloseButton（位于背景之上、ChapterPin/GoButton 之下）。
            var emptyRt = BottomNavAttachedScreenLayout.CreateChildRect(root, "EmptyAreaCloseButton",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(emptyRt);
            var emptyImg = emptyRt.gameObject.AddComponent<Image>();
            emptyImg.color = new Color(0f, 0f, 0f, 0f);
            emptyImg.raycastTarget = true;
            var emptyBtn = emptyRt.gameObject.AddComponent<Button>();
            emptyBtn.transition = Selectable.Transition.None;
            emptyBtn.targetGraphic = emptyImg;
            emptyBtn.onClick.AddListener(view.OnEmptyAreaClicked);

            // SPEC §9.8.8 (v3.240)：左上角返回公会（须在 EmptyAreaCloseButton 之上）。
            var backRt = BottomNavAttachedScreenLayout.CreateChildRect(root, "BackButton",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                BackButtonPos, BackButtonSize);
            backRt.pivot = new Vector2(0f, 1f);
            var backImg = backRt.gameObject.AddComponent<Image>();
            backImg.preserveAspect = true;
            var backSprite = Resources.Load<Sprite>(ResBackButton);
            if (backSprite != null)
            {
                backImg.sprite = backSprite;
                backImg.color = Color.white;
            }
            else
            {
                UnityEngine.Debug.LogWarning("[MainStoryLineScreenView] 缺失精灵：" + ResBackButton);
                backImg.color = new Color(0.14f, 0.16f, 0.22f, 0.92f);
            }
            var backBtn = backRt.gameObject.AddComponent<Button>();
            backBtn.transition = Selectable.Transition.None;
            backBtn.targetGraphic = backImg;
            backBtn.onClick.AddListener(view.OnBackToGongHuiClicked);

            // SPEC §9.8.8 (v3.47 / v3.240)：右上角体力 HUD（须在 EmptyAreaCloseButton 之上，避免被透明层遮挡）。
            var staminaHudRt = BottomNavAttachedScreenLayout.CreateChildRect(root, "MainStoryStaminaHud",
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                MainStoryStaminaHudAnchoredPos, MainStoryStaminaHudSize);
            staminaHudRt.pivot = new Vector2(1f, 1f);
            view.staminaBarSlotRt = BottomNavAttachedScreenLayout.CreateChildRect(staminaHudRt, "MainStoryStaminaBarSlot",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                Vector2.zero, MainStoryStaminaBarSlotSize);
            view.staminaBarSlotRt.pivot = new Vector2(0f, 1f);
            var staminaTextRt = BottomNavAttachedScreenLayout.CreateChildRect(staminaHudRt, "MainStoryStaminaText",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -124f), new Vector2(280f, 36f));
            staminaTextRt.pivot = new Vector2(0f, 1f);
            var staminaTxt = staminaTextRt.gameObject.AddComponent<Text>();
            staminaTxt.text = "-- / --";
            staminaTxt.font = FarmGridView.LoadBuiltinFont();
            staminaTxt.fontSize = 28;
            staminaTxt.alignment = TextAnchor.MiddleLeft;
            staminaTxt.color = Color.white;
            staminaTxt.raycastTarget = false;
            view.staminaValueText = staminaTxt;

            // SPEC §9.8.8.1：单 ChapterPin。
            view.chapterPinUnselectedSprite = Resources.Load<Sprite>(ResChapterPinUnselected);
            view.chapterPinSelectedSprite = Resources.Load<Sprite>(ResChapterPinSelected);
            if (view.chapterPinUnselectedSprite == null)
                UnityEngine.Debug.LogWarning("[MainStoryLineScreenView] 缺失精灵：" + ResChapterPinUnselected);
            if (view.chapterPinSelectedSprite == null)
                UnityEngine.Debug.LogWarning("[MainStoryLineScreenView] 缺失精灵：" + ResChapterPinSelected);

            var pinRt = BottomNavAttachedScreenLayout.CreateChildRect(root, "ChapterPin_1",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                PinAnchoredPosition, PinSize);
            pinRt.pivot = new Vector2(0.5f, 0.5f);
            var pinImg = pinRt.gameObject.AddComponent<Image>();
            pinImg.raycastTarget = true;
            pinImg.preserveAspect = true;
            view.chapterPinImage = pinImg;
            var pinBtn = pinRt.gameObject.AddComponent<Button>();
            pinBtn.transition = Selectable.Transition.None;
            pinBtn.targetGraphic = pinImg;
            pinBtn.onClick.AddListener(view.OnChapterPinClicked);

            // SPEC §9.8.8.3：GoButton（默认隐藏；OnEnable / 选中切换时控制）。
            var goRt = BottomNavAttachedScreenLayout.CreateChildRect(root, "GoButton",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(PinAnchoredPosition.x, PinAnchoredPosition.y + GoButtonOffsetY),
                GoButtonSize);
            goRt.pivot = new Vector2(0.5f, 0.5f);
            var goImg = goRt.gameObject.AddComponent<Image>();
            goImg.preserveAspect = true;
            var goSprite = Resources.Load<Sprite>(ResGoButton);
            if (goSprite != null)
            {
                goImg.sprite = goSprite;
                goImg.color = Color.white;
            }
            else
            {
                UnityEngine.Debug.LogWarning("[MainStoryLineScreenView] 缺失精灵：" + ResGoButton);
                goImg.color = new Color(1f, 0.55f, 0.18f, 0.95f);
            }
            var goBtn = goRt.gameObject.AddComponent<Button>();
            goBtn.transition = Selectable.Transition.None;
            goBtn.targetGraphic = goImg;
            goBtn.onClick.AddListener(view.OnGoButtonClicked);
            view.goButtonRt = goRt;
            goRt.gameObject.SetActive(false);

            // 默认进入显示态时 = 已选中（OnEnable 时再确保一次）。
            view.SetChapterPinSelected(true);

            // SPEC §9.8.16：竞技场入口（升级窗首次展示后解锁）。
            MainStoryArenaEntryView.BuildInto(root, canvasRect);

            root.gameObject.SetActive(false);
            barView.OnOpenChanged += view.OnBottomNavOpenChanged;
            view.OnBottomNavOpenChanged(barView.OpenIndex, barView.OpenKey);
            instance = view;
            return view;
        }

        /// <summary>
        /// SPEC §12.11.10 (v3.181)：嵌入 BOSS 战胜利后，从 <see cref="InvasionBattleModal2View"/> 返回关卡选择层。
        /// </summary>
        public static void ShowLevelSelectPanel()
        {
            if (instance == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[MainStoryLineScreenView] ShowLevelSelectPanel: 主线层未初始化，无法打开关卡选择。");
                return;
            }
            instance.EnsureLevelSelectPanelShown();
        }

        /// <summary>
        /// SPEC §12.11.10 (v3.227)：嵌入 BOSS 战胜利后，从 <see cref="InvasionBattleModal2View"/> 返回主线界面。
        /// 选中底栏「主线」Tab（<see cref="ZhuXianNavKey"/>）并关闭关卡选择层。
        /// </summary>
        public static void ShowMainStoryScreen()
        {
            if (instance == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[MainStoryLineScreenView] ShowMainStoryScreen: 主线层未初始化，无法返回主线界面。");
                return;
            }
            instance.EnsureMainStoryScreenShown();
        }

        private void EnsureMainStoryScreenShown()
        {
            // 关闭关卡选择层，回到主线界面本体。
            LevelSelectScreenPanelView.HideIfAny();

            if (bottomNav != null)
            {
                // 切换底栏至「主线」Tab；若已在主线 Tab，则直接确保根节点可见。
                if (!string.Equals(bottomNav.OpenKey, ZhuXianNavKey, StringComparison.Ordinal))
                {
                    bottomNav.SetOpenKey(ZhuXianNavKey);
                    return;
                }
            }

            if (rootRt != null)
                rootRt.gameObject.SetActive(true);
            EnsureBottomNavBarHidden();
            SetChapterPinSelected(true);
            RefreshMainStoryStamina();
            RefreshArenaEntryVisibility();
        }

        private void EnsureLevelSelectPanelShown()
        {
            if (canvasRectCache == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[MainStoryLineScreenView] canvasRectCache 为空，无法打开关卡选择层。");
                return;
            }
            if (levelSelectPanel == null)
                levelSelectPanel = LevelSelectScreenPanelView.BuildInto(canvasRectCache, bottomNav, this);
            levelSelectPanel?.Show();
        }

        private void OnBottomNavOpenChanged(int index, string key)
        {
            bool show = !string.IsNullOrEmpty(key) &&
                         string.Equals(key, ZhuXianNavKey, StringComparison.Ordinal);
            if (rootRt != null)
                rootRt.gameObject.SetActive(show);
            if (show)
            {
                // SPEC §9.8.8（v3.239）：主线界面显示时强制隐藏 BottomNavBar。
                EnsureBottomNavBarHidden();
                // SPEC §9.8.8.1：每次显示时默认选中。
                SetChapterPinSelected(true);
                RefreshMainStoryStamina();
                RefreshArenaEntryVisibility();
            }
            else
            {
                // 强制关闭关联弹窗，避免切到其它底栏页时残留遮挡。
                LevelSelectScreenPanelView.HideIfAny();
                HideHungryDialog();
                if (foodWarehouseModal != null && foodWarehouseModal.IsShown)
                    foodWarehouseModal.Hide();
            }
        }

        /// <summary>SPEC §9.8.8（v3.239）：主线层可见时兜底隐藏底栏（对齐 §9.8 v3.237 永久隐藏）。</summary>
        private void EnsureBottomNavBarHidden()
        {
            if (bottomNav != null && bottomNav.gameObject.activeSelf)
                bottomNav.gameObject.SetActive(false);
        }

        /// <summary>SPEC §9.8.8（v3.240）：返回公会 EnterHomeHud / GongHuiScreen。</summary>
        private void OnBackToGongHuiClicked()
        {
            if (bottomNav == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[MainStoryLineScreenView] OnBackToGongHuiClicked: bottomNav 为空，无法返回公会。");
                return;
            }
            bottomNav.SetOpenKey(GongHuiScreenView.GongHuiNavKey);
        }

        private void OnEmptyAreaClicked()
        {
            SetChapterPinSelected(false);
        }

        private void OnChapterPinClicked()
        {
            // 单选互斥：再次点击当前 pin 切换选中态。
            SetChapterPinSelected(!chapterPinSelected);
        }

        private void SetChapterPinSelected(bool selected)
        {
            chapterPinSelected = selected;
            if (chapterPinImage != null)
            {
                var sp = selected ? chapterPinSelectedSprite : chapterPinUnselectedSprite;
                if (sp != null)
                    chapterPinImage.sprite = sp;
            }
            if (goButtonRt != null)
                goButtonRt.gameObject.SetActive(selected);
        }

        private void OnGoButtonClicked()
        {
            // SPEC §9.8.8 (v3.167)：主线「前往」→ 直接打开新战斗界面 InvasionBattleModal_2（§12.11）。
            // v3.48 的「打开选择关卡全屏层」已停用（LevelSelectScreenPanel 保留代码与预制体，不再从此打开）。
            if (canvasRectCache == null)
            {
                UnityEngine.Debug.LogWarning("[MainStoryLineScreenView] canvasRectCache 为空，无法打开 InvasionBattleModal_2。");
                return;
            }
            var modal2 = PetDemo.UI.Battle.InvasionBattleModal2View.GetOrCreate(canvasRectCache);
            modal2?.Show(plantingService);
        }

        /// <summary>
        /// SPEC §9.8.8.4 (v3.48 / v3.105)：由 <see cref="LevelSelectScreenPanelView"/> 的 GoButton 调用。
        /// </summary>
        public void OnLevelSelectGoClicked()
        {
            if (GetRoleStamina() > HungryDialogStaminaThreshold)
            {
                TryOpenBattleFromLevelSelect();
                return;
            }
            ShowHungryDialog();
        }

        // ============================================================
        // 饿肚子提示框（挂 Main Canvas，叠在选择关卡层之上）
        // ============================================================
        public void ShowHungryDialog()
        {
            if (GetRoleStamina() > HungryDialogStaminaThreshold)
                return;
            if (canvasRectCache == null)
                return;
            if (hungryDialogRt == null)
                BuildHungryDialog();
            if (hungryDialogRt != null)
            {
                hungryDialogRt.gameObject.SetActive(true);
                hungryDialogRt.SetAsLastSibling();
            }
        }

        private void TryOpenBattleFromLevelSelect()
        {
            HideHungryDialog();
            var invasion = InvasionService.Instance;
            if (invasion == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[MainStoryLineScreenView] OnLevelSelectGoClicked: InvasionService.Instance 为空，无法开战。");
                return;
            }
            invasion.RequestMainStoryLevelSelectBattle();
            invasion.OpenBattleFromWarehouseHub();
        }

        private int GetRoleStamina()
        {
            if (plantingService == null)
                return 0;
            var role = plantingService.GetRoleStats();
            return role != null ? role.stamina : 0;
        }

        public void HideHungryDialog()
        {
            if (hungryDialogRt != null)
                hungryDialogRt.gameObject.SetActive(false);
        }

        private void BuildHungryDialog()
        {
            if (canvasRectCache == null)
                return;
            var dialogRt = BottomNavAttachedScreenLayout.CreateChildRect(canvasRectCache, "HungryDialog",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(dialogRt);

            // 半透明遮罩（点击不关闭）
            var dimImg = dialogRt.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.55f);
            dimImg.raycastTarget = true;

            // 中央 Panel
            var panelRt = BottomNavAttachedScreenLayout.CreateChildRect(dialogRt, "DialogPanel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(880f, 520f));
            var panelImg = panelRt.gameObject.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.10f, 0.16f, 0.96f);

            var msgRt = BottomNavAttachedScreenLayout.CreateChildRect(panelRt, "MessageText",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 60f), new Vector2(780f, 200f));
            var msgText = msgRt.gameObject.AddComponent<Text>();
            msgText.text = HungryDialogText;
            msgText.font = FarmGridView.LoadBuiltinFont();
            msgText.fontSize = 44;
            msgText.color = Color.white;
            msgText.alignment = TextAnchor.MiddleCenter;
            msgText.raycastTarget = false;

            // OK button
            var okRt = BottomNavAttachedScreenLayout.CreateChildRect(panelRt, "OkButton",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(150f, -140f), new Vector2(220f, 100f));
            var okImg = okRt.gameObject.AddComponent<Image>();
            okImg.color = new Color(0.31f, 0.64f, 1f, 1f);
            var okBtn = okRt.gameObject.AddComponent<Button>();
            okBtn.transition = Selectable.Transition.None;
            okBtn.targetGraphic = okImg;
            okBtn.onClick.AddListener(OnHungryDialogOk);
            var okLabelRt = BottomNavAttachedScreenLayout.CreateChildRect(okRt, "Label",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(okLabelRt);
            var okLabel = okLabelRt.gameObject.AddComponent<Text>();
            okLabel.text = "确定";
            okLabel.font = FarmGridView.LoadBuiltinFont();
            okLabel.fontSize = 40;
            okLabel.alignment = TextAnchor.MiddleCenter;
            okLabel.color = Color.white;
            okLabel.raycastTarget = false;

            // Cancel button
            var cancelRt = BottomNavAttachedScreenLayout.CreateChildRect(panelRt, "CancelButton",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-150f, -140f), new Vector2(220f, 100f));
            var cancelImg = cancelRt.gameObject.AddComponent<Image>();
            cancelImg.color = new Color(0.53f, 0.53f, 0.53f, 1f);
            var cancelBtn = cancelRt.gameObject.AddComponent<Button>();
            cancelBtn.transition = Selectable.Transition.None;
            cancelBtn.targetGraphic = cancelImg;
            cancelBtn.onClick.AddListener(OnHungryDialogCancel);
            var cancelLabelRt = BottomNavAttachedScreenLayout.CreateChildRect(cancelRt, "Label",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(cancelLabelRt);
            var cancelLabel = cancelLabelRt.gameObject.AddComponent<Text>();
            cancelLabel.text = "取消";
            cancelLabel.font = FarmGridView.LoadBuiltinFont();
            cancelLabel.fontSize = 40;
            cancelLabel.alignment = TextAnchor.MiddleCenter;
            cancelLabel.color = Color.white;
            cancelLabel.raycastTarget = false;

            dialogRt.gameObject.SetActive(false);
            hungryDialogRt = dialogRt;
        }

        private void OnHungryDialogOk()
        {
            HideHungryDialog();
            if (plantingService == null)
            {
                UnityEngine.Debug.LogWarning("[MainStoryLineScreenView] plantingService 为空，无法打开食物仓库。");
                return;
            }
            if (canvasRectCache == null)
            {
                UnityEngine.Debug.LogWarning("[MainStoryLineScreenView] canvasRectCache 为空，无法打开食物仓库。");
                return;
            }
            if (foodWarehouseModal == null)
                foodWarehouseModal = FoodWarehouseModalView.GetOrCreate(canvasRectCache);
            if (foodWarehouseModal != null)
            {
                EnsureWarehouseHubHiddenSubscription();
                foodWarehouseModal.Show(plantingService);
            }
        }

        private void OnHungryDialogCancel()
        {
            HideHungryDialog();
        }

        /// <summary>
        /// SPEC §9.8.8 (v3.47)：同步左上角体力条与数值（进入主线、从统一仓库返回时调用）。
        /// </summary>
        private void RefreshMainStoryStamina()
        {
            if (staminaBarSlotRt == null)
                return;

            if (plantingService == null)
            {
                if (staminaValueText != null)
                    staminaValueText.text = "-- / --";
                if (mainStoryStaminaBar != null)
                    mainStoryStaminaBar.Refresh();
                EnsureWarehouseHubHiddenSubscription();
                return;
            }

            RoleStats role = plantingService.GetRoleStats();
            if (mainStoryStaminaBar == null)
                mainStoryStaminaBar = StaminaBarView.BuildInto(staminaBarSlotRt, role, plantingService);
            else
            {
                mainStoryStaminaBar.Bind(role);
                mainStoryStaminaBar.SubscribeService(plantingService);
            }

            mainStoryStaminaBar?.Refresh();

            if (staminaValueText != null)
            {
                if (role != null)
                    staminaValueText.text = role.stamina + " / " + role.staminaMax;
                else
                    staminaValueText.text = "0 / 100";
            }

            EnsureWarehouseHubHiddenSubscription();
        }

        /// <summary>
        /// 统一仓库可能由家园先创建；不依赖主线「确定」也能订阅 <see cref="WarehouseHubPanelView.Hidden"/>。
        /// </summary>
        private void EnsureWarehouseHubHiddenSubscription()
        {
            if (warehouseHiddenSubscribed || canvasRectCache == null)
                return;

            WarehouseHubPanelView hub = null;
            if (foodWarehouseModal != null)
                hub = foodWarehouseModal.GetComponent<WarehouseHubPanelView>();
            if (hub == null)
            {
                var t = canvasRectCache.Find(WarehouseHubPanelView.PanelObjectName);
                if (t != null)
                    hub = t.GetComponent<WarehouseHubPanelView>();
            }

            if (hub == null)
                return;
            hub.Hidden += OnWarehouseHubHidden;
            subscribedWarehouseHub = hub;
            warehouseHiddenSubscribed = true;
        }

        private void OnWarehouseHubHidden()
        {
            if (rootRt != null && rootRt.gameObject.activeInHierarchy)
                RefreshMainStoryStamina();
        }

        private void RefreshArenaEntryVisibility()
        {
            if (rootRt == null)
                return;
            var entry = rootRt.Find("MainStoryArenaEntry");
            if (entry == null)
                return;
            var entryView = entry.GetComponent<MainStoryArenaEntryView>();
            entryView?.NotifyMainStoryVisibilityChanged();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
            if (bottomNav != null)
                bottomNav.OnOpenChanged -= OnBottomNavOpenChanged;
            if (subscribedWarehouseHub != null)
            {
                subscribedWarehouseHub.Hidden -= OnWarehouseHubHidden;
                subscribedWarehouseHub = null;
            }
        }
    }
}
