// SPEC §9.8.8.6 / §9.8.8.7：主线「选择关卡」全屏层（关卡节点 + 分页 + Go/返回）。
using System;
using System.Collections.Generic;
using PetDemo.Battle;
using PetDemo.Core;
using PetDemo.Farm;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class LevelSelectScreenPanelView : MonoBehaviour
    {
        public const string ResPrefabPath = "Prefabs/Farm/LevelSelectScreenPanel";
        public const string ResBackground = "AirUI/ZhuXian_2";
        public const string ResGoButton = "AirUI/ZhanDouKaiShi";
        public const string ResBtnLocked = "AirUI/ZhuXian_AnNiu_0";
        public const string ResBtnAvailable = "AirUI/ZhuXian_AnNiu_1";
        public const string ResBtnCleared = "AirUI/ZhuXian_AnNiu_2";
        public const string ResMob = "AirUI/ZhuXian_XiaoGuai";
        public const string ResBoss = "AirUI/ZhuXian_BOSS";
        public const string PanelObjectName = "LevelSelectScreenPanel";
        public const string LevelSlotsRootName = "LevelSlotsRoot";

        private const float GoBelowGap = 0f;

        private static readonly Vector2 GoButtonSize = new Vector2(120f, 120f);
        private static readonly Vector2 BackButtonSize = new Vector2(120f, 80f);

        private static LevelSelectScreenPanelView instance;

        [SerializeField] private Button goButton;
        [SerializeField] private Button backButton;
        [SerializeField] private RectTransform levelSlotsRoot;
        [SerializeField] private RectTransform goButtonRt;

        private RectTransform panelRt;
        private RectTransform canvasRectCache;
        private BottomNavBarView bottomNav;
        private MainStoryLineScreenView mainStoryView;

        private readonly List<LevelSelectLevelSlotView> slotViews = new List<LevelSelectLevelSlotView>(MainStoryLevelConfigCatalog.SlotsPerPage);
        private List<MainStoryLevelSlotLayout> slotLayouts;
        private List<MainStoryLevelConfig> levelConfigs;
        private int currentPageIndex;

        private Sprite spriteLocked;
        private Sprite spriteAvailable;
        private Sprite spriteCleared;
        private Sprite spriteMob;
        private Sprite spriteBoss;

        private bool wired;
        private bool navSubscribed;
        private bool battleEndedSubscribed;
        private bool slotsBuilt;

        public bool IsShown => gameObject != null && gameObject.activeSelf;

        public static LevelSelectScreenPanelView BuildInto(
            RectTransform canvasRect,
            BottomNavBarView barView,
            MainStoryLineScreenView mainStory)
        {
            var view = GetOrCreate(canvasRect);
            if (view != null)
            {
                view.bottomNav = barView;
                view.mainStoryView = mainStory;
                view.canvasRectCache = canvasRect;
                view.EnsureNavSubscription();
            }
            return view;
        }

        public static LevelSelectScreenPanelView GetOrCreate(RectTransform canvasRect)
        {
            if (canvasRect == null)
            {
                UnityEngine.Debug.LogWarning("[LevelSelectScreenPanelView] GetOrCreate: canvasRect 为空");
                return null;
            }

            if (instance != null && instance.panelRt != null)
            {
                instance.canvasRectCache = canvasRect;
                return instance;
            }

            var existing = canvasRect.Find(PanelObjectName);
            if (existing != null)
            {
                var existView = existing.GetComponent<LevelSelectScreenPanelView>();
                if (existView == null)
                    existView = existing.gameObject.AddComponent<LevelSelectScreenPanelView>();
                existView.panelRt = existing as RectTransform;
                existView.canvasRectCache = canvasRect;
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
                    "[LevelSelectScreenPanelView] 缺少预制体 Resources/" + ResPrefabPath +
                    "，使用运行时回退 UI；请在编辑器执行 Tools/PetDemo/Generate Level Select Screen Panel Prefab。");
                go = BuildRuntimeFallback(canvasRect);
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

            var view = go.GetComponent<LevelSelectScreenPanelView>();
            if (view == null)
                view = go.AddComponent<LevelSelectScreenPanelView>();
            view.panelRt = rt;
            view.canvasRectCache = canvasRect;
            instance = view;
            return view;
        }

        private void EnsureNavSubscription()
        {
            if (navSubscribed || bottomNav == null)
                return;
            bottomNav.OnOpenChanged += OnBottomNavOpenChanged;
            navSubscribed = true;
        }

        private void OnBottomNavOpenChanged(int index, string key)
        {
            bool isMainStory = !string.IsNullOrEmpty(key) &&
                               string.Equals(key, MainStoryLineScreenView.ZhuXianNavKey, StringComparison.Ordinal);
            if (!isMainStory && IsShown)
            {
                LevelSelectLevelInfoOverlayView.HideIfAny();
                Hide();
            }
        }

        public void Show()
        {
            EnsureFieldsFromHierarchy();
            EnsureConfigLoaded();
            EnsureSpritesLoaded();
            EnsureLevelSlotsBuilt();
            WireButtonsOnce();

            RefreshCurrentPage();
            EnsureBattleEndedSubscription();

            gameObject.SetActive(true);
            PlaceAboveBottomNav();
        }

        private void EnsureBattleEndedSubscription()
        {
            if (battleEndedSubscribed)
                return;
            var invasion = InvasionService.Instance;
            if (invasion == null)
                return;
            invasion.OnBattleEnded += OnBattleEnded;
            battleEndedSubscribed = true;
        }

        private void OnBattleEnded(bool playerWon)
        {
            if (playerWon && IsShown)
                RefreshCurrentPage();
        }

        public void Hide()
        {
            LevelSelectLevelInfoOverlayView.HideIfAny();
            gameObject.SetActive(false);
        }

        public static void HideIfAny()
        {
            if (instance != null && instance.IsShown)
                instance.Hide();
        }

        private void EnsureConfigLoaded()
        {
            if (slotLayouts == null || slotLayouts.Count == 0)
                slotLayouts = MainStoryLevelConfigCatalog.LoadSlotLayoutsFromCsv();
            if (levelConfigs == null || levelConfigs.Count == 0)
                levelConfigs = MainStoryLevelConfigCatalog.LoadLevelConfigsFromCsv();
        }

        private void EnsureSpritesLoaded()
        {
            if (spriteLocked == null)
                spriteLocked = Resources.Load<Sprite>(ResBtnLocked);
            if (spriteAvailable == null)
                spriteAvailable = Resources.Load<Sprite>(ResBtnAvailable);
            if (spriteCleared == null)
                spriteCleared = Resources.Load<Sprite>(ResBtnCleared);
            if (spriteMob == null)
                spriteMob = Resources.Load<Sprite>(ResMob);
            if (spriteBoss == null)
                spriteBoss = Resources.Load<Sprite>(ResBoss);
        }

        private void EnsureLevelSlotsRoot()
        {
            if (levelSlotsRoot != null)
                return;

            var t = transform.Find(LevelSlotsRootName);
            if (t != null)
            {
                levelSlotsRoot = t as RectTransform;
                return;
            }

            levelSlotsRoot = BottomNavAttachedScreenLayout.CreateChildRect(
                panelRt != null ? panelRt : transform as RectTransform,
                LevelSlotsRootName,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(levelSlotsRoot);
            levelSlotsRoot.SetSiblingIndex(1);
        }

        private void EnsureLevelSlotsBuilt()
        {
            EnsureLevelSlotsRoot();
            if (slotsBuilt && slotViews.Count >= MainStoryLevelConfigCatalog.SlotsPerPage)
                return;

            slotViews.Clear();
            EnsureConfigLoaded();
            EnsureSpritesLoaded();

            for (int i = 0; i < MainStoryLevelConfigCatalog.SlotsPerPage; i++)
            {
                var layout = MainStoryLevelConfigCatalog.FindSlotByIndex(slotLayouts, i)
                             ?? CreateFallbackLayout(i);
                var slotView = LevelSelectLevelSlotView.Create(
                    levelSlotsRoot, i, layout,
                    spriteLocked, spriteAvailable, spriteCleared,
                    OnLevelSlotClicked);
                slotViews.Add(slotView);
            }

            slotsBuilt = true;
        }

        private static MainStoryLevelSlotLayout CreateFallbackLayout(int slotIndex)
        {
            return new MainStoryLevelSlotLayout
            {
                slotIndex = slotIndex,
                posX = 0f,
                posY = 200f - slotIndex * 40f,
                width = 120f,
                height = 120f,
            };
        }

        private int GetHighestClearedLevel()
        {
            var planting = PlantingService.Instance;
            if (planting != null)
                return planting.GetMainStoryHighestClearedLevel();
            return 0;
        }

        private void RefreshCurrentPage()
        {
            EnsureLevelSlotsBuilt();
            int highestCleared = GetHighestClearedLevel();
            int challengeLevel = highestCleared + 1;
            int maxLevel = MainStoryLevelConfigCatalog.GetMaxLevelNumber(levelConfigs);

            if (challengeLevel > maxLevel && maxLevel > 0)
                challengeLevel = maxLevel;

            currentPageIndex = MainStoryLevelConfigCatalog.GetPageIndexForLevel(
                challengeLevel > 0 ? challengeLevel : 1);

            int maxPage = MainStoryLevelConfigCatalog.GetPageIndexForLevel(maxLevel);
            if (currentPageIndex > maxPage)
                currentPageIndex = maxPage;

            LevelSelectLevelSlotView availableSlot = null;

            for (int slotIndex = 0; slotIndex < MainStoryLevelConfigCatalog.SlotsPerPage; slotIndex++)
            {
                if (slotIndex >= slotViews.Count)
                    break;

                int levelNumber = MainStoryLevelConfigCatalog.LevelNumberForSlot(currentPageIndex, slotIndex);
                var levelConfig = MainStoryLevelConfigCatalog.FindLevelByNumber(levelConfigs, levelNumber);
                var slotView = slotViews[slotIndex];

                var layout = MainStoryLevelConfigCatalog.FindSlotByIndex(slotLayouts, slotIndex);
                if (layout != null)
                    slotView.ApplyLayout(layout);

                if (levelConfig == null)
                {
                    slotView.Refresh(null, MainStoryLevelState.Locked, null, false);
                    continue;
                }

                var state = MainStoryLevelConfigCatalog.ResolveState(levelNumber, highestCleared);
                ResolveOverlay(levelNumber, levelConfig, state, challengeLevel,
                    out Sprite overlaySprite, out bool overlayVisible);

                slotView.Refresh(levelConfig, state, overlaySprite, overlayVisible);

                if (state == MainStoryLevelState.Available)
                    availableSlot = slotView;
            }

            PositionGoButton(availableSlot);
        }

        private void ResolveOverlay(
            int levelNumber,
            MainStoryLevelConfig levelConfig,
            MainStoryLevelState state,
            int challengeLevel,
            out Sprite overlaySprite,
            out bool overlayVisible)
        {
            overlaySprite = null;
            overlayVisible = false;

            if (state == MainStoryLevelState.Cleared)
                return;

            if (state == MainStoryLevelState.Available && levelNumber == challengeLevel)
            {
                overlaySprite = levelConfig.isBoss ? spriteBoss : spriteMob;
                overlayVisible = overlaySprite != null;
                return;
            }

            if (levelConfig.isBoss)
            {
                overlaySprite = spriteBoss;
                overlayVisible = overlaySprite != null;
            }
        }

        private void PositionGoButton(LevelSelectLevelSlotView availableSlot)
        {
            if (goButtonRt == null && goButton != null)
                goButtonRt = goButton.transform as RectTransform;

            if (goButtonRt == null)
                return;

            if (availableSlot == null || !availableSlot.gameObject.activeSelf)
            {
                goButtonRt.gameObject.SetActive(false);
                return;
            }

            float slotH = availableSlot.SlotHeight;
            float goHalfH = GoButtonSize.y * 0.5f;
            float slotHalfH = slotH * 0.5f;
            var slotPos = availableSlot.SlotAnchoredPosition;
            goButtonRt.anchorMin = new Vector2(0.5f, 0.5f);
            goButtonRt.anchorMax = new Vector2(0.5f, 0.5f);
            goButtonRt.pivot = new Vector2(0.5f, 0.5f);
            goButtonRt.sizeDelta = GoButtonSize;
            goButtonRt.anchoredPosition = slotPos + new Vector2(0f, -(slotHalfH + GoBelowGap + goHalfH));
            goButtonRt.gameObject.SetActive(true);
        }

        private void OnLevelSlotClicked(MainStoryLevelConfig levelConfig)
        {
            if (canvasRectCache == null || levelConfig == null)
                return;
            var overlay = LevelSelectLevelInfoOverlayView.GetOrCreate(canvasRectCache);
            overlay?.Show(levelConfig);
        }

        private void PlaceAboveBottomNav()
        {
            if (bottomNav == null)
            {
                transform.SetAsLastSibling();
                return;
            }

            var barRt = bottomNav.transform as RectTransform;
            if (barRt != null && barRt.parent == transform.parent)
                transform.SetSiblingIndex(barRt.GetSiblingIndex() + 1);
            else
                transform.SetAsLastSibling();
        }

        private void WireButtonsOnce()
        {
            if (wired)
                return;

            if (goButton != null)
            {
                goButton.onClick.RemoveAllListeners();
                goButton.onClick.AddListener(OnGoClicked);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(OnBackClicked);
            }

            wired = true;
        }

        private void OnGoClicked()
        {
            if (mainStoryView == null)
            {
                UnityEngine.Debug.LogWarning("[LevelSelectScreenPanelView] mainStoryView 为空，无法打开饿肚子提示框。");
                return;
            }
            mainStoryView.OnLevelSelectGoClicked();
        }

        private void OnBackClicked()
        {
            mainStoryView?.HideHungryDialog();
            Hide();
        }

        private void EnsureFieldsFromHierarchy()
        {
            if (panelRt == null)
                panelRt = transform as RectTransform;
            if (goButton == null)
                goButton = FindDescendantButton("GoButton");
            if (goButtonRt == null && goButton != null)
                goButtonRt = goButton.transform as RectTransform;
            if (backButton == null)
                backButton = FindDescendantButton("BackButton");
            if (levelSlotsRoot == null)
            {
                var t = transform.Find(LevelSlotsRootName);
                if (t != null)
                    levelSlotsRoot = t as RectTransform;
            }
        }

        private Button FindDescendantButton(string nodeName)
        {
            var t = transform.Find(nodeName);
            if (t == null)
                t = FindDescendantByName(transform, nodeName);
            return t != null ? t.GetComponent<Button>() : null;
        }

        private static Transform FindDescendantByName(Transform root, string name)
        {
            if (root == null)
                return null;
            if (string.Equals(root.name, name, StringComparison.Ordinal))
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDescendantByName(root.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static GameObject BuildRuntimeFallback(RectTransform canvasRect)
        {
            var rootGo = new GameObject(PanelObjectName, typeof(RectTransform));
            var rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.SetParent(canvasRect, false);
            BottomNavAttachedScreenLayout.StretchFull(rootRt);

            var view = rootGo.AddComponent<LevelSelectScreenPanelView>();
            BottomNavAttachedScreenLayout.AddStretchedResourcesBackground(
                rootRt, ResBackground, nameof(LevelSelectScreenPanelView));

            view.levelSlotsRoot = BottomNavAttachedScreenLayout.CreateChildRect(
                rootRt, LevelSlotsRootName,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(view.levelSlotsRoot);

            view.goButton = CreateGoButton(rootRt, out view.goButtonRt);
            view.backButton = CreateRuntimeLabeledButton(rootRt, "BackButton", "返回",
                new Vector2(0f, 0f), new Vector2(20f, 20f), BackButtonSize,
                new Color(0.35f, 0.35f, 0.4f, 0.95f));

            return rootGo;
        }

        private static Button CreateGoButton(RectTransform parent, out RectTransform goRt)
        {
            goRt = BottomNavAttachedScreenLayout.CreateChildRect(
                parent, "GoButton",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, GoButtonSize);
            goRt.pivot = new Vector2(0.5f, 0.5f);
            goRt.gameObject.SetActive(false);

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
                UnityEngine.Debug.LogWarning("[LevelSelectScreenPanelView] 缺失精灵：" + ResGoButton);
                goImg.color = new Color(1f, 0.55f, 0.18f, 0.95f);
            }

            var goBtn = goRt.gameObject.AddComponent<Button>();
            goBtn.transition = Selectable.Transition.None;
            goBtn.targetGraphic = goImg;
            return goBtn;
        }

        private static Button CreateRuntimeLabeledButton(
            RectTransform parent,
            string name,
            string label,
            Vector2 anchorPivot,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color)
        {
            var btnRt = BottomNavAttachedScreenLayout.CreateChildRect(
                parent, name, anchorPivot, anchorPivot, anchoredPosition, size);
            btnRt.pivot = anchorPivot;

            var img = btnRt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            var btn = btnRt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;

            var labelRt = BottomNavAttachedScreenLayout.CreateChildRect(
                btnRt, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(labelRt);
            var txt = labelRt.gameObject.AddComponent<Text>();
            txt.text = label;
            txt.font = FarmGridView.LoadBuiltinFont();
            txt.fontSize = 36;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.raycastTarget = false;

            return btn;
        }

        private void OnDestroy()
        {
            if (bottomNav != null && navSubscribed)
                bottomNav.OnOpenChanged -= OnBottomNavOpenChanged;
            if (battleEndedSubscribed)
            {
                var invasion = InvasionService.Instance;
                if (invasion != null)
                    invasion.OnBattleEnded -= OnBattleEnded;
                battleEndedSubscribed = false;
            }
            if (instance == this)
                instance = null;
        }
    }
}
