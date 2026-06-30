// 单块农田 UI 组件；每格挂一个 TileSlotView，由 FarmGridView 在创建时填充并订阅事件刷新。
// SPEC §9.1：SoilImage → PlantImage → NeedWaterIcon → PendingWaterIcon → StatusBadges → FocusRing。
// SPEC §9.4.6：实现 IPointerClickHandler 以在 SowGestureController.ClickMode 期间消费一次性点击播种。
// SPEC §9.7（自 v2.10 起）：未处于播种 ClickMode 时，若 PlayerFertilizerBag.activeId != null 且
//   本田 tile.fertilizer == AwaitingFertilizer，则点击触发 IPlantingService.ApplyFertilizerToTile。
// SPEC §9.1.2 (v3.92)：可收获时先播放 PlantImage 摆动动画，再 TryHarvestTile。
// SPEC §9.1.4 (v3.111)：田格可见 UI 参与家园世界 Y 轴深度排序。
using System.Collections;
using System.Collections.Generic;
using PetDemo.Core;
using PetDemo.UI;
using PetDemo.Farm;
using Spine.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PetDemo.UI.Farm
{
    public class TileSlotView : MonoBehaviour, IPointerClickHandler
    {
        private const string HarvestReadyIconResPath = "AirUI/ShouHuo-0";
        private const string NeedWaterIconResPath = "AirUI/QueShui_1";
        private static readonly string[] PendingWaterIconResPaths =
        {
            "AirUI/JiaoShi_Dai_1",
            "AirUI/JiaoShi_Dai_2",
            "AirUI/JiaoShi_Dai_3",
        };
        private const float NeedWaterIconSize = 150f;
        private const float PlantSpineHostScaleXY = 0.75f;
        private const string PestEventIconResPath = "AirUI/WH_Chong";
        private const string MoleTheftEventIconResPath = "AirUI/WH_Tou";

        [SerializeField] private Image soilImage;
        [SerializeField] private Image plantImage;
        private RectTransform plantSpineHost;
        private SkeletonGraphic plantSpineGraphic;
        private string activeSpineResourcePath;
        private bool plantSpineActive;
        private int lastAppearanceNode;
        private bool windWork2Active;
        private readonly PlantSpineAnimationPlayer spineAnimPlayer = new PlantSpineAnimationPlayer();
        [SerializeField] private Image waterBadge;
        [SerializeField] private Image fertilizerBadge;
        [SerializeField] private Image pestBadge;
        [SerializeField] private Image harvestBadge;
        [SerializeField] private Image needWaterIcon;
        [SerializeField] private Image pendingWaterIcon;
        [SerializeField] private Image focusRing;
        [SerializeField] private Text focusArrow;
        [SerializeField] private Text orderLabel;

        [SerializeField] private Image pestEventIcon;
        [SerializeField] private Image moleTheftEventIcon;

        private string tileId;
        private static Sprite sHarvestReadySprite;
        private static Sprite sNeedWaterSprite;
        private static readonly Sprite[] sPendingWaterSprites = new Sprite[3];
        private static Sprite sPestEventSprite;
        private static Sprite sMoleTheftEventSprite;
        private Vector2 harvestBadgeDefaultPos;
        private Vector2 harvestBadgeDefaultSize;
        private bool harvestBadgeLayoutCached;
        private Coroutine pestBlinkRoutine;
        private Coroutine moleTheftBlinkRoutine;
        private Coroutine harvestSwingRoutine;
        private readonly List<RectTransform> depthSortRegistered = new List<RectTransform>();

        public static int ActiveHarvestSwingCount => PlantHarvestSwingAnimator.ActiveSwingCount;

        public string TileId => tileId;

        public bool IsHarvestSwingPlaying => harvestSwingRoutine != null;

        private void Awake()
        {
            EnsureBindings();
        }

        private void OnEnable()
        {
            // SPEC §9.11.1 / §9.12.1：JiaYuanWorldScreen 未显示时 BuildInto 会 Refresh 但无法启协程，激活后补启。
            if (pestEventIcon != null && pestEventIcon.enabled)
                TryStartPestBlinkRoutine();
            if (moleTheftEventIcon != null && moleTheftEventIcon.enabled)
                TryStartMoleTheftBlinkRoutine();
        }

        private void Reset()
        {
            EnsureBindings();
        }

        public void Init(
            string tileId,
            Image soil,
            Image plant,
            Image water,
            Image fert,
            Image pest,
            Image harvest,
            Image focus,
            Text order)
        {
            this.tileId = tileId;
            soilImage = soil;
            plantImage = plant;
            waterBadge = water;
            fertilizerBadge = fert;
            pestBadge = pest;
            harvestBadge = harvest;
            focusRing = focus;
            orderLabel = order;
        }

        public void SetTileId(string id)
        {
            tileId = id;
        }

        public void SetOrderLabel(int orderIndex)
        {
            EnsureBindings();
            if (orderLabel != null)
                orderLabel.text = orderIndex.ToString();
        }

        public void SetFocus(bool on)
        {
            if (focusRing != null)
            {
                focusRing.enabled = on;
                // 焦点环仅作高亮，不参与射线检测，避免挡住 PestEventIcon 等叠层点击。
                focusRing.raycastTarget = false;
            }
            if (focusArrow != null)
                focusArrow.gameObject.SetActive(on);

            RefreshDepthSort();
        }

        public void Refresh(IPlantingService service)
        {
            EnsureBindings();
            if (service == null)
                return;
            // 找到 tile + plant
            CropTile tile = null;
            for (int i = 1; i <= service.FarmTileCount; i++)
            {
                var t = service.GetTileByOrder(i);
                if (t != null && t.tileId == tileId)
                {
                    tile = t;
                    break;
                }
            }
            if (tile == null)
                return;

            PlantInstance plant = null;
            PlantConfig cfg = null;
            if (!string.IsNullOrEmpty(tile.plantInstanceId))
            {
                plant = service.GetPlant(tile.plantInstanceId);
                if (plant != null)
                    cfg = service.GetPlantConfig(plant.plantConfigId);
            }

            // SPEC §4.1.10.3：被变异锁定时，仅保留 SoilImage，其它子层级一律隐藏。
            bool lockedByMutation = !string.IsNullOrEmpty(tile.lockedByMutationId);
            if (lockedByMutation)
            {
                HidePlantVisuals();
                SetBadge(waterBadge, null);
                SetBadge(fertilizerBadge, null);
                SetBadge(pestBadge, null);
                SetHarvestBadge(HarvestFlag.None);
                SetNeedWaterIconVisible(false);
                SetPendingWaterIconVisible(false);
                RefreshDepthSort();
                return;
            }

            // PlantImage / PlantSpineHost：依 appearanceNode 双轨切换（§9.1 v3.93；摆动中不重置旋转）
            RefreshPlantVisual(plant, cfg);

            // 状态徽标
            SetBadge(waterBadge, GetWaterColor(tile.water));
            SetBadge(fertilizerBadge, GetFertilizerColor(tile.fertilizer));
            SetBadge(pestBadge, GetPestColor(tile.pest));
            // SPEC §4.1.6.1 (v3.52)：地鼠偷窃期间不显示可收获图标。
            var harvestForBadge = tile.moleTheft == MoleTheftFlag.AwaitingMoleTheft
                ? HarvestFlag.None
                : tile.harvest;
            SetHarvestBadge(harvestForBadge);

            // SPEC §9.1 (v3.70)：统一按钮浇水 pending 叠层（先于 NeedWater，pending 时后者隐藏）。
            RefreshPendingWaterOverlay(tile, service);

            // SPEC §9.1：缺水暂停时叠放 QueShui_1；有水阶或可继续生长时隐藏。
            RefreshNeedWaterOverlay(tile, plant, service);

            // SPEC §9.11.1 (v3.50)：虫灾时叠放 WH_Chong 闪烁图标。
            RefreshPestEventOverlay(tile);

            // SPEC §9.12.1 (v3.52)：地鼠偷窃时叠放 WH_Tou 闪烁图标。
            RefreshMoleTheftEventOverlay(tile);

            RefreshDepthSort();
        }

        private void RefreshDepthSort()
        {
            var sorter = JiaYuanWorldDepthSorter.Instance;
            if (sorter == null)
                return;

            ClearDepthSortRegistration(sorter);

            var tileRt = transform as RectTransform;
            if (tileRt == null)
                return;

            float sortY = sorter.ResolveSortYFromWorldPoint(tileRt.position);

            RegisterTileDepthSort(sorter, soilImage, sortY, JiaYuanWorldDepthLayer.Soil);

            if (plantSpineActive && plantSpineHost != null && plantSpineHost.gameObject.activeInHierarchy)
                RegisterTileDepthSort(sorter, plantSpineHost, sortY, JiaYuanWorldDepthLayer.Plant);
            else
                RegisterTileDepthSort(sorter, plantImage, sortY, JiaYuanWorldDepthLayer.Plant);

            RegisterTileDepthSort(sorter, needWaterIcon, sortY, JiaYuanWorldDepthLayer.NeedWater);
            RegisterTileDepthSort(sorter, pendingWaterIcon, sortY, JiaYuanWorldDepthLayer.PendingWater);
            RegisterTileDepthSort(sorter, waterBadge, sortY, JiaYuanWorldDepthLayer.WaterBadge);
            RegisterTileDepthSort(sorter, fertilizerBadge, sortY, JiaYuanWorldDepthLayer.StatusBadges);
            RegisterTileDepthSort(sorter, pestBadge, sortY, JiaYuanWorldDepthLayer.StatusBadges);
            RegisterTileDepthSort(sorter, harvestBadge, sortY, JiaYuanWorldDepthLayer.StatusBadges);
            RegisterTileDepthSort(sorter, pestEventIcon, sortY, JiaYuanWorldDepthLayer.PestMoleIcon);
            RegisterTileDepthSort(sorter, moleTheftEventIcon, sortY, JiaYuanWorldDepthLayer.PestMoleIcon);
            RegisterTileDepthSort(sorter, focusRing, sortY, JiaYuanWorldDepthLayer.FocusRing);

            if (focusArrow != null && focusArrow.gameObject.activeInHierarchy)
                RegisterTileDepthSort(sorter, focusArrow.rectTransform, sortY, JiaYuanWorldDepthLayer.FocusRing);

            sorter.MarkDirty();
        }

        private void ClearDepthSortRegistration(JiaYuanWorldDepthSorter sorter)
        {
            for (int i = 0; i < depthSortRegistered.Count; i++)
                sorter.Unregister(depthSortRegistered[i]);
            depthSortRegistered.Clear();
        }

        private void RegisterTileDepthSort(
            JiaYuanWorldDepthSorter sorter, Graphic graphic, float sortY, int layerOffset)
        {
            if (graphic == null || !graphic.enabled || !graphic.gameObject.activeInHierarchy)
                return;
            RegisterTileDepthSort(sorter, graphic.rectTransform, sortY, layerOffset);
        }

        private void RegisterTileDepthSort(
            JiaYuanWorldDepthSorter sorter, RectTransform visual, float sortY, int layerOffset)
        {
            if (visual == null || !visual.gameObject.activeInHierarchy)
                return;

            sorter.RegisterOrUpdate(new DepthSortEntry
            {
                Visual = visual,
                SortY = sortY,
                LayerOffset = layerOffset,
                Active = true,
                UseVisualCenterY = false,
            });
            depthSortRegistered.Add(visual);
        }

        private void RefreshPendingWaterOverlay(CropTile tile, IPlantingService service)
        {
            if (pendingWaterIcon == null)
                return;

            int tier = service != null ? service.GetPendingWaterDisplayTier(tile.tileId) : 0;
            if (tier < 1 || tier > 3)
            {
                SetPendingWaterIconVisible(false);
                return;
            }

            int idx = tier - 1;
            if (sPendingWaterSprites[idx] == null)
                sPendingWaterSprites[idx] = Resources.Load<Sprite>(PendingWaterIconResPaths[idx]);

            pendingWaterIcon.sprite = sPendingWaterSprites[idx];
            pendingWaterIcon.color = Color.white;
            pendingWaterIcon.preserveAspect = true;
            SetPendingWaterIconVisible(sPendingWaterSprites[idx] != null);
        }

        private void RefreshNeedWaterOverlay(CropTile tile, PlantInstance plant, IPlantingService service)
        {
            if (needWaterIcon == null)
                return;

            if (service != null && service.GetPendingWaterDisplayTier(tile.tileId) > 0)
            {
                SetNeedWaterIconVisible(false);
                return;
            }

            bool needWaterPause =
                plant != null
                && tile.water == WaterStage.Empty
                && (plant.state == PlantState.Growing || plant.state == PlantState.Paused);

            if (!needWaterPause)
            {
                SetNeedWaterIconVisible(false);
                return;
            }

            if (sNeedWaterSprite == null)
                sNeedWaterSprite = Resources.Load<Sprite>(NeedWaterIconResPath);

            needWaterIcon.sprite = sNeedWaterSprite;
            needWaterIcon.color = Color.white;
            needWaterIcon.preserveAspect = true;
            SetNeedWaterIconVisible(sNeedWaterSprite != null);
        }

        private void SetNeedWaterIconVisible(bool on)
        {
            if (needWaterIcon == null)
                return;
            needWaterIcon.enabled = on;
        }

        private void SetPendingWaterIconVisible(bool on)
        {
            if (pendingWaterIcon == null)
                return;
            pendingWaterIcon.enabled = on;
        }

        // SPEC §9.11.1 (v3.50)：虫灾叠层刷新
        private void RefreshPestEventOverlay(CropTile tile)
        {
            EnsurePestEventIcon();
            if (pestEventIcon == null) return;

            bool hasPest = tile != null && tile.pest == PestFlag.AwaitingPestControl;

            // 隐藏原色块徽标以避免重复提示
            if (hasPest)
                SetBadge(pestBadge, null);

            if (hasPest)
            {
                if (sPestEventSprite == null)
                    sPestEventSprite = Resources.Load<Sprite>(PestEventIconResPath);

                pestEventIcon.sprite = sPestEventSprite;
                if (sPestEventSprite == null)
                    pestEventIcon.color = new Color(0.90f, 0.30f, 0.10f, 1f);
                else
                    pestEventIcon.color = Color.white;
                pestEventIcon.preserveAspect = true;
                pestEventIcon.enabled = true;
                // 置于田格子树最前，避免被 FocusRing / 徽标挡住点击。
                pestEventIcon.transform.SetAsLastSibling();
                BindPestEventIconClick(pestEventIcon);

                TryStartPestBlinkRoutine();
            }
            else
            {
                if (pestBlinkRoutine != null)
                {
                    StopCoroutine(pestBlinkRoutine);
                    pestBlinkRoutine = null;
                }
                pestEventIcon.enabled = false;
            }
        }

        private void TryStartPestBlinkRoutine()
        {
            if (pestBlinkRoutine != null)
                return;
            if (!gameObject.activeInHierarchy)
                return;
            pestBlinkRoutine = StartCoroutine(PestBlinkRoutine());
        }

        private void TryStartMoleTheftBlinkRoutine()
        {
            if (moleTheftBlinkRoutine != null)
                return;
            if (!gameObject.activeInHierarchy)
                return;
            moleTheftBlinkRoutine = StartCoroutine(MoleTheftBlinkRoutine());
        }

        private System.Collections.IEnumerator PestBlinkRoutine()
        {
            const float blinkSpeed = 5f;
            const float minAlpha = 0.35f;
            const float maxAlpha = 1.0f;
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime;
                float wave = (Mathf.Sin(t * blinkSpeed) + 1f) * 0.5f;
                float alpha = Mathf.Lerp(minAlpha, maxAlpha, wave);
                if (pestEventIcon != null)
                {
                    var c = pestEventIcon.color;
                    c.a = alpha;
                    pestEventIcon.color = c;
                }
                yield return null;
            }
        }

        private void EnsurePestEventIcon()
        {
            if (pestEventIcon != null)
            {
                BindPestEventIconClick(pestEventIcon);
                return;
            }
            pestEventIcon = FindImage("PestEventIcon");
            if (pestEventIcon != null)
            {
                BindPestEventIconClick(pestEventIcon);
                return;
            }

            // 运行时创建，插在 FocusRing 之前
            var go = new GameObject("PestEventIcon");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(72f, 72f);

            var img = go.AddComponent<Image>();
            img.raycastTarget = true; // 允许点击
            img.enabled = false;

            if (focusRing != null)
                rt.SetSiblingIndex(focusRing.transform.GetSiblingIndex());

            pestEventIcon = img;
            BindPestEventIconClick(pestEventIcon);
        }

        // SPEC §9.12.1 (v3.52)：地鼠偷窃叠层刷新
        private void RefreshMoleTheftEventOverlay(CropTile tile)
        {
            EnsureMoleTheftEventIcon();
            if (moleTheftEventIcon == null) return;

            bool hasMoleTheft = tile != null && tile.moleTheft == MoleTheftFlag.AwaitingMoleTheft;

            if (hasMoleTheft)
            {
                if (sMoleTheftEventSprite == null)
                    sMoleTheftEventSprite = Resources.Load<Sprite>(MoleTheftEventIconResPath);

                moleTheftEventIcon.sprite = sMoleTheftEventSprite;
                if (sMoleTheftEventSprite == null)
                    moleTheftEventIcon.color = new Color(0.85f, 0.55f, 0.15f, 1f);
                else
                    moleTheftEventIcon.color = Color.white;
                moleTheftEventIcon.preserveAspect = true;
                moleTheftEventIcon.enabled = true;

                TryStartMoleTheftBlinkRoutine();
            }
            else
            {
                if (moleTheftBlinkRoutine != null)
                {
                    StopCoroutine(moleTheftBlinkRoutine);
                    moleTheftBlinkRoutine = null;
                }
                moleTheftEventIcon.enabled = false;
            }
        }

        private System.Collections.IEnumerator MoleTheftBlinkRoutine()
        {
            const float blinkSpeed = 5f;
            const float minAlpha = 0.35f;
            const float maxAlpha = 1.0f;
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime;
                float wave = (Mathf.Sin(t * blinkSpeed) + 1f) * 0.5f;
                float alpha = Mathf.Lerp(minAlpha, maxAlpha, wave);
                if (moleTheftEventIcon != null)
                {
                    var c = moleTheftEventIcon.color;
                    c.a = alpha;
                    moleTheftEventIcon.color = c;
                }
                yield return null;
            }
        }

        private void EnsureMoleTheftEventIcon()
        {
            if (moleTheftEventIcon != null)
            {
                BindOverlayClickRelay(moleTheftEventIcon);
                return;
            }
            moleTheftEventIcon = FindImage("MoleTheftEventIcon");
            if (moleTheftEventIcon != null)
            {
                BindOverlayClickRelay(moleTheftEventIcon);
                return;
            }

            var go = new GameObject("MoleTheftEventIcon");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(72f, 72f);

            var img = go.AddComponent<Image>();
            img.raycastTarget = true;
            img.enabled = false;

            if (focusRing != null)
                rt.SetSiblingIndex(focusRing.transform.GetSiblingIndex());

            moleTheftEventIcon = img;
            BindOverlayClickRelay(moleTheftEventIcon);
        }

        private static void BindOverlayClickRelay(Image icon)
        {
            if (icon == null)
                return;
            var relay = icon.GetComponent<TileSlotOverlayClickRelay>();
            if (relay == null)
                relay = icon.gameObject.AddComponent<TileSlotOverlayClickRelay>();
            var slot = icon.GetComponentInParent<TileSlotView>();
            if (slot != null)
                relay.Bind(slot);
        }

        private void BindPestEventIconClick(Image icon)
        {
            if (icon == null)
                return;
            icon.raycastTarget = true;
            BindOverlayClickRelay(icon);

            var btn = icon.GetComponent<Button>();
            if (btn == null)
            {
                btn = icon.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
            }
            btn.targetGraphic = icon;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(TryOpenPestControlScreen);
        }

        private void TryOpenPestControlScreen()
        {
            if (string.IsNullOrEmpty(tileId))
                return;

            var view = PestControlScreenView.Resolve();
            if (view == null)
            {
                UnityEngine.Debug.LogWarning("[TileSlotView] 无法打开打虫子界面：未找到 PestControlScreenView。");
                return;
            }
            view.Open(tileId);
        }

        private void EnsureBindings()
        {
            if (soilImage == null) soilImage = FindImage("SoilImage");
            if (plantImage == null) plantImage = FindImage("PlantImage");
            if (waterBadge == null) waterBadge = FindImage("WaterBadge");
            if (fertilizerBadge == null) fertilizerBadge = FindImage("FertilizerBadge");
            if (pestBadge == null) pestBadge = FindImage("PestBadge");
            if (pestEventIcon == null) pestEventIcon = FindImage("PestEventIcon");
            if (moleTheftEventIcon == null) moleTheftEventIcon = FindImage("MoleTheftEventIcon");
            if (harvestBadge == null) harvestBadge = FindImage("HarvestBadge");
            if (focusRing == null) focusRing = FindImage("FocusRing");
            if (needWaterIcon == null) needWaterIcon = FindImage("NeedWaterIcon");
            if (needWaterIcon == null) needWaterIcon = CreateNeedWaterIconIfMissing();
            ApplyNeedWaterIconLayout();
            if (pendingWaterIcon == null) pendingWaterIcon = FindImage("PendingWaterIcon");
            if (pendingWaterIcon == null) pendingWaterIcon = CreatePendingWaterIconIfMissing();
            ApplyPendingWaterIconLayout();
            if (focusArrow == null) focusArrow = FindText("FocusArrow");
            if (orderLabel == null) orderLabel = FindText("OrderLabel");

            // SPEC §9.4.6：保证至少一个子 Image 接收 raycast，
            // 否则 EventSystem 无法把 IPointerClick / IPointerEnter 派发到本 TileSlotView。
            // 现有 TileSlot 预制体默认所有 Image.m_RaycastTarget=0，因此在运行时强制开启 SoilImage 的 raycastTarget。
            if (soilImage != null && !soilImage.raycastTarget)
                soilImage.raycastTarget = true;
            if (focusRing != null)
                focusRing.raycastTarget = false;
        }

        /// <summary>
        /// SPEC §9.1（v3.24）：预制体缺少 `NeedWaterIcon` 时运行时创建，并插在 FocusRing 之前以便叠放在植物之上、焦点环之下。
        /// </summary>
        private Image CreateNeedWaterIconIfMissing()
        {
            var go = new GameObject("NeedWaterIcon");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(NeedWaterIconSize, NeedWaterIconSize);

            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.enabled = false;

            if (focusRing != null)
                rt.SetSiblingIndex(focusRing.transform.GetSiblingIndex());
            return img;
        }

        /// <summary>
        /// SPEC §9.1（v3.70）：预制体缺少 `PendingWaterIcon` 时运行时创建，叠于 NeedWaterIcon 之上、FocusRing 之前。
        /// </summary>
        private Image CreatePendingWaterIconIfMissing()
        {
            var go = new GameObject("PendingWaterIcon");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(NeedWaterIconSize, NeedWaterIconSize);

            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.enabled = false;

            if (needWaterIcon != null)
                rt.SetSiblingIndex(needWaterIcon.transform.GetSiblingIndex() + 1);
            else if (focusRing != null)
                rt.SetSiblingIndex(focusRing.transform.GetSiblingIndex());
            return img;
        }

        private void ApplyPendingWaterIconLayout()
        {
            if (pendingWaterIcon == null)
                return;
            var rt = pendingWaterIcon.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(NeedWaterIconSize, NeedWaterIconSize);
            if (needWaterIcon != null
                && pendingWaterIcon.transform.GetSiblingIndex() <= needWaterIcon.transform.GetSiblingIndex())
                pendingWaterIcon.transform.SetSiblingIndex(needWaterIcon.transform.GetSiblingIndex() + 1);
        }

        private void ApplyNeedWaterIconLayout()
        {
            if (needWaterIcon == null)
                return;
            var rt = needWaterIcon.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(NeedWaterIconSize, NeedWaterIconSize);
        }

        /// <summary>
        /// 子叠层图标（PestEventIcon 等）经 <see cref="TileSlotOverlayClickRelay"/> 转发至此。
        /// </summary>
        internal void ForwardPointerClick(PointerEventData eventData)
        {
            OnPointerClick(eventData);
        }

        // SPEC §9.4.6：仓库播种按钮进入 ClickMode 后，下一次点击农田由本回调消费（优先级最高）。
        // SPEC §9.7 / §9.1.1：若玩家在仓库选中肥料且该田仍可施肥（Seeded+AwaitingFertilizer），先于 Tips 执行
        //   ApplyFertilizerToTile，避免「Tips 分支无条件 return」导致生长阶段永远也走不到施肥 API。
        public void OnPointerClick(PointerEventData eventData)
        {
            var service = PlantingService.Instance;
            // SPEC §4.1.10.3：被变异锁定时，所有点击交互短路返回；
            // 4 格植物的收获入口由 MutationOverlayView 上的独立图标承担。
            if (service != null && !string.IsNullOrEmpty(tileId))
            {
                var lockedTile = GetTileById(service, tileId);
                if (lockedTile != null && !string.IsNullOrEmpty(lockedTile.lockedByMutationId))
                    return;
            }

            var ctrl = SowGestureController.Instance;
            if (ctrl != null && ctrl.IsClickMode)
            {
                ctrl.RequestClickModeSow(tileId);
                return;
            }

            if (service == null || string.IsNullOrEmpty(tileId))
                return;

            var actionable = service.GetActionableActionOf(tileId);
            if (actionable.HasValue && actionable.Value == ActionType.Harvest)
            {
                RequestHarvestWithSwing(service);
                return;
            }

            var tile = GetTileById(service, tileId);

            // SPEC §9.11.2 (v3.50)：虫灾状态下点击田格（或虫灾图标）打开打虫子界面。
            if (tile != null && tile.pest == PestFlag.AwaitingPestControl)
            {
                TryOpenPestControlScreen();
                return;
            }

            // SPEC §9.12.2 (v3.82)：地鼠偷窃状态下点击田格（或地鼠图标）打开「附魔」转盘玩法界面。
            if (tile != null && tile.moleTheft == MoleTheftFlag.AwaitingMoleTheft)
            {
                EnchantScreenView.Resolve()?.Open(tileId);
                return;
            }

            // §9.7：选中肥料 + 本田待施肥 → 优先施肥（成功则短路），否则会落入 Tips。
            if (tile != null
                && !string.IsNullOrEmpty(service.GetActiveFertilizer())
                && tile.planting == PlantingFlag.Seeded
                && tile.fertilizer == FertilizerFlag.AwaitingFertilizer)
            {
                if (service.ApplyFertilizerToTile(tileId))
                    return;
            }

            if (tile != null
                && !string.IsNullOrEmpty(tile.plantInstanceId)
                && tile.harvest != HarvestFlag.AwaitingHarvest)
            {
                var presenter = TilePlantTipsPresenter.Instance;
                if (presenter != null)
                    presenter.TryShowTip(tileId);
                return;
            }
        }

        /// <summary>SPEC §9.1.2：可收获时播放摆动后再收获；已在播放或不可收获时返回 false。</summary>
        public bool RequestHarvestWithSwing(IPlantingService service)
        {
            if (service == null || string.IsNullOrEmpty(tileId))
                return false;
            if (IsHarvestSwingPlaying || ActiveHarvestSwingCount > 0)
                return false;

            var actionable = service.GetActionableActionOf(tileId);
            if (!actionable.HasValue || actionable.Value != ActionType.Harvest)
                return false;

            EnsureBindings();
            var activeRt = GetActivePlantVisualRectTransform();
            if (activeRt == null || !IsPlantVisualActive())
                return false;

            if (harvestSwingRoutine != null)
                StopCoroutine(harvestSwingRoutine);
            harvestSwingRoutine = StartCoroutine(HarvestSwingThenHarvestRoutine(service));
            return true;
        }

        private IEnumerator HarvestSwingThenHarvestRoutine(IPlantingService service)
        {
            PlantHarvestSwingAnimator.BeginSwing();
            var plantRt = GetActivePlantVisualRectTransform();
            bool harvestBadgeWasEnabled = harvestBadge != null && harvestBadge.enabled;

            try
            {
                if (harvestBadge != null)
                    harvestBadge.enabled = false;

                yield return PlantHarvestSwingAnimator.RunSwingSequence(plantRt);

                if (service != null && !string.IsNullOrEmpty(tileId))
                    service.TryHarvestTile(tileId);
            }
            finally
            {
                PlantHarvestSwingAnimator.EndSwing();

                if (harvestBadge != null && harvestBadgeWasEnabled)
                    harvestBadge.enabled = true;

                harvestSwingRoutine = null;
                if (service != null)
                    Refresh(service);
            }
        }

        private void OnDisable()
        {
            if (harvestSwingRoutine == null)
                return;

            StopCoroutine(harvestSwingRoutine);
            harvestSwingRoutine = null;

            var activeRt = GetActivePlantVisualRectTransform();
            if (activeRt != null)
                activeRt.localEulerAngles = Vector3.zero;
        }

        private static string ResolveSpritePathForNode(PlantConfig cfg, int appearanceNode)
        {
            if (cfg?.appearanceSpriteIds == null || cfg.appearanceSpriteIds.Count == 0)
                return null;
            int idx = Mathf.Clamp(appearanceNode - 1, 0, cfg.appearanceSpriteIds.Count - 1);
            var s = cfg.appearanceSpriteIds[idx];
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        private void RefreshPlantVisual(PlantInstance plant, PlantConfig cfg)
        {
            if (plant == null || cfg == null)
            {
                HidePlantVisuals();
                return;
            }

            var appearance = cfg.ResolveFarmAppearance(plant.appearanceNode);
            if (appearance.kind == PlantAppearanceKind.Spine)
            {
                if (TryShowPlantSpine(plant, appearance.resourcePath))
                {
                    if (!IsHarvestSwingPlaying)
                        ResetPlantVisualRotationIfIdle();
                    return;
                }

                UnityEngine.Debug.LogWarning(
                    $"[TileSlotView] Spine 加载失败，回退 Sprite：tile={tileId}, path={appearance.resourcePath}");
            }

            string spritePath = ResolveSpritePathForNode(cfg, plant.appearanceNode);
            ShowPlantSprite(spritePath);
            if (!IsHarvestSwingPlaying)
                ResetPlantVisualRotationIfIdle();
        }

        public void NotifyPlantTileInteracted()
        {
            if (!plantSpineActive || plantSpineGraphic == null || !plantSpineGraphic.enabled)
                return;
            spineAnimPlayer.PlayWork1Once(plantSpineGraphic, ResumeAfterOneShot);
        }

        public void SetWindWork2Active(bool on)
        {
            if (windWork2Active == on)
                return;
            windWork2Active = on;
            if (!plantSpineActive || plantSpineGraphic == null || !plantSpineGraphic.enabled)
                return;
            if (spineAnimPlayer.IsOneShotPlaying)
                return;
            if (windWork2Active)
                spineAnimPlayer.PlayWork2Loop(plantSpineGraphic);
            else
                spineAnimPlayer.PlayIdle(plantSpineGraphic);
        }

        private bool TryShowPlantSpine(PlantInstance plant, string skeletonDataPath)
        {
            if (string.IsNullOrWhiteSpace(skeletonDataPath) || plant == null)
                return false;

            EnsureBindings();
            var host = EnsurePlantSpineHost();
            if (host == null)
                return false;

            bool nodeChanged = plant.appearanceNode != lastAppearanceNode;
            string path = skeletonDataPath.Trim();
            bool assetChanged = plantSpineGraphic == null
                || !string.Equals(activeSpineResourcePath, path, System.StringComparison.Ordinal);
            if (assetChanged)
            {
                if (!PlantSpineGraphicBuilder.TryApply(host, path, out plantSpineGraphic))
                    return false;
                activeSpineResourcePath = path;
            }

            if (plantImage != null)
                plantImage.enabled = false;

            ApplyPlantSpineHostLayout(host);
            host.gameObject.SetActive(true);
            if (plantSpineGraphic != null)
                plantSpineGraphic.enabled = true;

            plantSpineActive = true;

            if (nodeChanged)
            {
                lastAppearanceNode = plant.appearanceNode;
                spineAnimPlayer.PlayGrowOnce(plantSpineGraphic, ResumeAfterOneShot);
            }
            else if (assetChanged)
            {
                lastAppearanceNode = plant.appearanceNode;
                ResumeAmbientSpineAnimation();
            }

            return true;
        }

        private void ResumeAfterOneShot()
        {
            if (!plantSpineActive || plantSpineGraphic == null || !plantSpineGraphic.enabled)
                return;
            ResumeAmbientSpineAnimation();
        }

        private void ResumeAmbientSpineAnimation()
        {
            if (windWork2Active)
                spineAnimPlayer.PlayWork2Loop(plantSpineGraphic);
            else
                spineAnimPlayer.PlayIdle(plantSpineGraphic);
        }

        private void ShowPlantSprite(string spritePath)
        {
            EnsureBindings();
            HidePlantSpineVisual();

            if (plantImage == null)
                return;

            Sprite sprite = null;
            if (!string.IsNullOrWhiteSpace(spritePath))
                sprite = Resources.Load<Sprite>(spritePath.Trim());

            plantImage.sprite = sprite;
            plantImage.enabled = sprite != null;
            plantImage.preserveAspect = true;
            plantSpineActive = false;
        }

        private void HidePlantSpineVisual()
        {
            if (plantSpineGraphic != null)
            {
                spineAnimPlayer.Unbind(plantSpineGraphic);
                PlantSpineGraphicBuilder.Hide(plantSpineGraphic);
            }
            if (plantSpineHost != null)
                plantSpineHost.gameObject.SetActive(false);
            plantSpineActive = false;
            activeSpineResourcePath = null;
            lastAppearanceNode = 0;
        }

        private void HidePlantVisuals()
        {
            if (plantImage != null)
                plantImage.enabled = false;
            HidePlantSpineVisual();
        }

        private RectTransform EnsurePlantSpineHost()
        {
            if (plantSpineHost != null)
                return plantSpineHost;

            if (plantImage == null)
                plantImage = FindImage("PlantImage");
            if (plantImage == null)
                return null;

            var go = new GameObject("PlantSpineHost");
            plantSpineHost = go.AddComponent<RectTransform>();
            plantSpineHost.SetParent(plantImage.transform.parent, false);

            var plantRt = plantImage.rectTransform;
            plantSpineHost.anchorMin = plantRt.anchorMin;
            plantSpineHost.anchorMax = plantRt.anchorMax;
            plantSpineHost.pivot = plantRt.pivot;
            plantSpineHost.sizeDelta = plantRt.sizeDelta;
            ApplyPlantSpineHostLayout(plantSpineHost);
            plantSpineHost.SetSiblingIndex(plantRt.GetSiblingIndex() + 1);
            plantSpineHost.gameObject.SetActive(false);
            return plantSpineHost;
        }

        private void ApplyPlantSpineHostLayout(RectTransform host)
        {
            if (host == null)
                return;
            float z = plantImage != null ? plantImage.rectTransform.localScale.z : 1f;
            host.localScale = new Vector3(PlantSpineHostScaleXY, PlantSpineHostScaleXY, z);
            float posX = plantImage != null ? plantImage.rectTransform.anchoredPosition.x : 0f;
            host.anchoredPosition = new Vector2(posX, 0f);
        }

        private RectTransform GetActivePlantVisualRectTransform()
        {
            if (plantSpineActive && plantSpineHost != null && plantSpineHost.gameObject.activeInHierarchy)
                return plantSpineHost;
            return plantImage != null ? plantImage.rectTransform : null;
        }

        private bool IsPlantVisualActive()
        {
            if (plantSpineActive && plantSpineGraphic != null && plantSpineGraphic.enabled)
                return true;
            return plantImage != null && plantImage.enabled;
        }

        private void ResetPlantVisualRotationIfIdle()
        {
            var rt = GetActivePlantVisualRectTransform();
            if (rt == null)
                return;
            var euler = rt.localEulerAngles;
            if (euler.z != 0f)
                rt.localEulerAngles = Vector3.zero;
        }

        private void OnDestroy()
        {
            var sorter = JiaYuanWorldDepthSorter.Instance;
            if (sorter != null)
                ClearDepthSortRegistration(sorter);

            if (plantSpineGraphic != null)
                spineAnimPlayer.Unbind(plantSpineGraphic);
            if (plantSpineHost != null)
                PlantSpineGraphicBuilder.DestroyVisual(plantSpineHost);
        }

        private static CropTile GetTileById(IPlantingService service, string tileId)
        {
            if (service == null || string.IsNullOrEmpty(tileId))
                return null;
            for (int i = 1; i <= service.FarmTileCount; i++)
            {
                var tile = service.GetTileByOrder(i);
                if (tile != null && tile.tileId == tileId)
                    return tile;
            }

            return null;
        }

        private void SetHarvestBadge(HarvestFlag flag)
        {
            if (harvestBadge == null)
                return;
            CacheHarvestBadgeLayoutIfNeeded();
            if (flag == HarvestFlag.AwaitingHarvest)
            {
                if (sHarvestReadySprite == null)
                    sHarvestReadySprite = Resources.Load<Sprite>(HarvestReadyIconResPath);
                harvestBadge.sprite = sHarvestReadySprite;
                harvestBadge.color = Color.white;
                harvestBadge.preserveAspect = true;
                var rt = harvestBadge.rectTransform;
                if (rt != null)
                {
                    rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = new Vector2(120f, 120f);
                }
                harvestBadge.enabled = sHarvestReadySprite != null;
                return;
            }
            RestoreHarvestBadgeLayout();
            harvestBadge.sprite = null;
            SetBadge(harvestBadge, GetHarvestColor(flag));
        }

        private void CacheHarvestBadgeLayoutIfNeeded()
        {
            if (harvestBadgeLayoutCached || harvestBadge == null)
                return;
            var rt = harvestBadge.rectTransform;
            if (rt == null)
                return;
            harvestBadgeDefaultPos = rt.anchoredPosition;
            harvestBadgeDefaultSize = rt.sizeDelta;
            harvestBadgeLayoutCached = true;
        }

        private void RestoreHarvestBadgeLayout()
        {
            if (harvestBadge == null || !harvestBadgeLayoutCached)
                return;
            var rt = harvestBadge.rectTransform;
            if (rt == null)
                return;
            rt.anchoredPosition = harvestBadgeDefaultPos;
            rt.sizeDelta = harvestBadgeDefaultSize;
            harvestBadge.preserveAspect = false;
        }

        private Image FindImage(string childName)
        {
            var child = transform.Find(childName);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private Text FindText(string childName)
        {
            var child = transform.Find(childName);
            return child != null ? child.GetComponent<Text>() : null;
        }

        private static void SetBadge(Image img, Color? c)
        {
            if (img == null)
                return;
            if (c.HasValue)
            {
                img.color = c.Value;
                img.enabled = true;
            }
            else
            {
                img.enabled = false;
            }
        }

        private static Color? GetWaterColor(WaterStage w)
        {
            // SPEC §9.1：W1/W2/W3 使用指定 Hex，Alpha=80（0–255）。
            const byte a = 80;
            switch (w)
            {
                case WaterStage.W1: return new Color32(0x4E, 0x8A, 0xA1, a);
                case WaterStage.W2: return new Color32(0x34, 0x62, 0x74, a);
                case WaterStage.W3: return new Color32(0x1E, 0x44, 0x52, a);
                default: return null;
            }
        }

        private static Color? GetFertilizerColor(FertilizerFlag f)
        {
            switch (f)
            {
                case FertilizerFlag.AwaitingFertilizer: return new Color(1.00f, 0.85f, 0.45f, 1f);
                case FertilizerFlag.Fertilized: return new Color(0.65f, 0.45f, 0.20f, 1f);
                default: return null;
            }
        }

        private static Color? GetPestColor(PestFlag p)
        {
            switch (p)
            {
                case PestFlag.AwaitingPestControl: return new Color(0.90f, 0.30f, 0.30f, 1f);
                case PestFlag.PestControlled: return new Color(0.55f, 0.85f, 0.45f, 1f);
                default: return null;
            }
        }

        private static Color? GetHarvestColor(HarvestFlag h)
        {
            switch (h)
            {
                case HarvestFlag.AwaitingHarvest: return new Color(1.00f, 0.55f, 0.20f, 1f);
                case HarvestFlag.Harvested: return new Color(0.60f, 0.60f, 0.60f, 1f);
                default: return null;
            }
        }
    }
}
