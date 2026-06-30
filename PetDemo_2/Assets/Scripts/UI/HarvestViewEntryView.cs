// SPEC §9.7.1：收获视角入口 — 三态（Entry/FreePan/HarvestLock）、图标切换、ZhongTian 镜头锁与视口滑动平移。
using System;
using PetDemo.Core;
using PetDemo.Farm;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class HarvestViewEntryView : MonoBehaviour
    {
        public enum HarvestViewUiState
        {
            Entry,
            FreePan,
            HarvestLock
        }

        public const string ResEntryIcon = "AirUI/ShouHuo_0";
        public const string ResCloseIcon = "AirUI/ShouHuo_1";
        public const string ResFreePanIcon = "AirUI/ShouHuo_2";
        public const string JiaYuanNavKey = "JiaYuan";

        private const float EntrySize = 150f;
        private const float EntryPosX = 450f;
        private const float EntryPosY = 438f;
        private const int CountFontSize = 42;
        private static readonly Color DimEntryColor = new Color(0.55f, 0.55f, 0.55f, 1f);

        private IPlantingService service;
        private JiaYuanViewportFollowController viewportFollow;
        private Func<RectTransform> resolveZhongTianAnchor;
        private Func<bool> resolveBlockPan;
        private BottomNavBarView bottomNav;

        private RectTransform rootRt;
        private RectTransform entryRt;
        private RectTransform closeRt;
        private Image entryImage;
        private Image closeImage;
        private Text countText;
        private Sprite entrySprite;
        private Sprite closeSprite;
        private Sprite freePanSprite;
        private HarvestViewUiState uiState = HarvestViewUiState.Entry;

        public HarvestViewUiState CurrentState => uiState;

        public static HarvestViewEntryView BuildInto(
            RectTransform canvasRect,
            IPlantingService plantingService,
            JiaYuanViewportFollowController follow,
            RectTransform viewportRt,
            Func<RectTransform> resolveZhongTian,
            Sprite entryIconSprite,
            Sprite closeIconSprite,
            Sprite freePanIconSprite,
            BottomNavBarView barView = null,
            Func<bool> blockPan = null)
        {
            if (canvasRect == null || plantingService == null)
                return null;

            entryIconSprite ??= Resources.Load<Sprite>(ResEntryIcon);
            closeIconSprite ??= Resources.Load<Sprite>(ResCloseIcon);
            freePanIconSprite ??= Resources.Load<Sprite>(ResFreePanIcon);
            if (entryIconSprite == null)
                return null;

            var rootGo = new GameObject("HarvestViewEntryLayer", typeof(RectTransform));
            var root = rootGo.GetComponent<RectTransform>();
            root.SetParent(canvasRect, false);
            StretchFull(root);

            var entryRt = CreateChildRect(root, "HarvestViewEntryButton",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(EntryPosX, EntryPosY), new Vector2(EntrySize, EntrySize));
            var entryImage = entryRt.gameObject.AddComponent<Image>();
            entryImage.sprite = entryIconSprite;
            entryImage.preserveAspect = true;
            entryImage.raycastTarget = true;
            var entryBtn = entryRt.gameObject.AddComponent<Button>();
            entryBtn.transition = Selectable.Transition.None;
            entryBtn.targetGraphic = entryImage;

            var countLabelRt = CreateChildRect(entryRt, "CountLabel",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 12f), new Vector2(EntrySize, 56f));
            countLabelRt.pivot = new Vector2(0.5f, 0f);
            var countText = countLabelRt.gameObject.AddComponent<Text>();
            countText.font = FarmGridView.LoadBuiltinFont();
            countText.fontSize = CountFontSize;
            countText.alignment = TextAnchor.LowerCenter;
            countText.color = Color.white;
            countText.raycastTarget = false;

            var closeRt = CreateChildRect(root, "HarvestViewCloseButton",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(EntryPosX, EntryPosY), new Vector2(EntrySize, EntrySize));
            var closeImage = closeRt.gameObject.AddComponent<Image>();
            closeImage.sprite = closeIconSprite != null ? closeIconSprite : entryIconSprite;
            closeImage.preserveAspect = true;
            closeImage.raycastTarget = true;
            var closeBtn = closeRt.gameObject.AddComponent<Button>();
            closeBtn.transition = Selectable.Transition.None;
            closeBtn.targetGraphic = closeImage;

            closeRt.gameObject.SetActive(false);

            var view = rootGo.AddComponent<HarvestViewEntryView>();
            view.rootRt = root;
            view.entryRt = entryRt;
            view.closeRt = closeRt;
            view.entryImage = entryImage;
            view.closeImage = closeImage;
            view.countText = countText;
            view.service = plantingService;
            view.viewportFollow = follow;
            view.resolveZhongTianAnchor = resolveZhongTian;
            view.resolveBlockPan = blockPan;
            view.bottomNav = barView;
            view.entrySprite = entryIconSprite;
            view.closeSprite = closeIconSprite != null ? closeIconSprite : entryIconSprite;
            view.freePanSprite = freePanIconSprite != null ? freePanIconSprite : entryIconSprite;

            entryBtn.onClick.AddListener(view.OnHarvestEntryClicked);
            closeBtn.onClick.AddListener(view.ExitHarvestView);

            if (follow != null)
                HarvestViewPanInput.Attach(view, follow);

            view.SubscribeServiceEvents();
            view.ApplyUiState(HarvestViewUiState.Entry);
            if (barView != null)
            {
                barView.OnOpenChanged += view.OnBottomNavOpenChanged;
                view.OnBottomNavOpenChanged(barView.OpenIndex, barView.OpenKey);
            }
            else
            {
                root.gameObject.SetActive(true);
                view.RefreshCount();
            }

            return view;
        }

        public bool IsPointerOverHarvestButtons(GameObject go)
        {
            if (go == null)
                return false;
            if (entryRt != null && entryRt.gameObject.activeInHierarchy && go.transform.IsChildOf(entryRt))
                return true;
            if (closeRt != null && closeRt.gameObject.activeInHierarchy && go.transform.IsChildOf(closeRt))
                return true;
            return false;
        }

        public bool ShouldAcceptPanInput()
        {
            if (uiState == HarvestViewUiState.HarvestLock)
                return false;
            if (rootRt == null || !rootRt.gameObject.activeSelf)
                return false;
            if (resolveBlockPan != null && resolveBlockPan())
                return false;
            var sow = SowGestureController.Instance;
            if (sow != null && sow.CurrentMode != SowGestureController.Mode.Idle)
                return false;
            return uiState == HarvestViewUiState.Entry || uiState == HarvestViewUiState.FreePan;
        }

        public void EnterFreePan()
        {
            if (uiState != HarvestViewUiState.Entry || viewportFollow == null)
                return;

            viewportFollow.EnterFreePanMode();
            ApplyUiState(HarvestViewUiState.FreePan);
        }

        private void OnHarvestEntryClicked()
        {
            switch (uiState)
            {
                case HarvestViewUiState.Entry:
                    EnterHarvestView();
                    break;
                case HarvestViewUiState.FreePan:
                    ExitFreePan();
                    break;
            }
        }

        private void EnterHarvestView()
        {
            if (viewportFollow == null)
                return;

            var anchor = resolveZhongTianAnchor != null ? resolveZhongTianAnchor() : null;
            if (anchor == null)
            {
                Debug.LogWarning(
                    "[HarvestViewEntryView] ZhongTian 锚点不可用，收获视角镜头继续跟随村民。");
                return;
            }

            viewportFollow.ExitFreePanMode();
            viewportFollow.SetSowAnchorLock(true, anchor);
            ApplyUiState(HarvestViewUiState.HarvestLock);
        }

        private void ExitHarvestView()
        {
            viewportFollow?.ExitAnchorViewLock();
            ApplyUiState(HarvestViewUiState.Entry);
            viewportFollow?.SnapOnce();
        }

        private void ExitFreePan()
        {
            viewportFollow?.ExitFreePanMode();
            ApplyUiState(HarvestViewUiState.Entry);
            viewportFollow?.SnapOnce();
        }

        private void ApplyUiState(HarvestViewUiState state)
        {
            uiState = state;

            switch (state)
            {
                case HarvestViewUiState.Entry:
                    if (entryRt != null)
                    {
                        entryRt.gameObject.SetActive(true);
                        if (entryImage != null)
                        {
                            entryImage.sprite = entrySprite;
                            entryImage.color = Color.white;
                        }
                    }
                    if (closeRt != null)
                        closeRt.gameObject.SetActive(false);
                    if (countText != null)
                        countText.gameObject.SetActive(true);
                    RefreshCount();
                    break;

                case HarvestViewUiState.FreePan:
                    if (entryRt != null)
                    {
                        entryRt.gameObject.SetActive(true);
                        if (entryImage != null)
                        {
                            entryImage.sprite = freePanSprite;
                            entryImage.color = Color.white;
                        }
                    }
                    if (closeRt != null)
                        closeRt.gameObject.SetActive(false);
                    if (countText != null)
                        countText.gameObject.SetActive(false);
                    break;

                case HarvestViewUiState.HarvestLock:
                    if (entryRt != null)
                        entryRt.gameObject.SetActive(false);
                    if (closeRt != null)
                    {
                        closeRt.gameObject.SetActive(true);
                        if (closeImage != null)
                        {
                            closeImage.sprite = closeSprite;
                            closeImage.color = Color.white;
                        }
                    }
                    if (countText != null)
                        countText.gameObject.SetActive(false);
                    break;
            }

        }

        private void RefreshCount()
        {
            if (service == null || countText == null || entryImage == null)
                return;
            if (uiState != HarvestViewUiState.Entry)
                return;

            int count = service.GetHarvestablePlantCount();
            countText.text = count.ToString();
            entryImage.color = count > 0 ? Color.white : DimEntryColor;
        }

        private void SubscribeServiceEvents()
        {
            if (service == null)
                return;
            service.OnTileFlagsChanged += OnTileFlagsChanged;
            service.OnPlantStateChanged += OnPlantStateChanged;
            service.OnMutationCreated += OnMutationCreated;
            service.OnMutationHarvested += OnMutationHarvested;
            RefreshCount();
        }

        private void UnsubscribeServiceEvents()
        {
            if (service == null)
                return;
            service.OnTileFlagsChanged -= OnTileFlagsChanged;
            service.OnPlantStateChanged -= OnPlantStateChanged;
            service.OnMutationCreated -= OnMutationCreated;
            service.OnMutationHarvested -= OnMutationHarvested;
        }

        private void OnTileFlagsChanged(string _) => RefreshCount();

        private void OnPlantStateChanged(string _, PlantState __) => RefreshCount();

        private void OnMutationCreated(string _) => RefreshCount();

        private void OnMutationHarvested(string _, MutationKind __, string ___) => RefreshCount();

        private void OnBottomNavOpenChanged(int index, string key)
        {
            bool jiaYuan = !string.IsNullOrEmpty(key) &&
                           string.Equals(key, JiaYuanNavKey, StringComparison.Ordinal);
            if (rootRt != null)
                rootRt.gameObject.SetActive(jiaYuan);

            if (!jiaYuan)
                ExitHarvestView();
            else
                RefreshCount();

        }

        private void OnDestroy()
        {
            UnsubscribeServiceEvents();
            if (bottomNav != null)
                bottomNav.OnOpenChanged -= OnBottomNavOpenChanged;
            if (uiState == HarvestViewUiState.HarvestLock || uiState == HarvestViewUiState.FreePan)
                viewportFollow?.ExitAnchorViewLock();
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
}
