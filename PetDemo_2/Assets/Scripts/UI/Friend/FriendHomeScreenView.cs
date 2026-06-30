// SPEC §13.3 / §13.4：好友家园场景层 — 视觉复刻玩家家园（背景 + FarmGridRoot 克隆；村民 Spine 可开关），
// 纯展示不读写 PlantingService 存档；随机植物随机状态；随机有植物田上「驱赶」按钮（HYXieZhu_3）。
// 点击驱赶 → InvasionService.OpenBattleFromFriendHome()；开战成功后隐藏本层，战毕（OnBattleEnded）再恢复并播放 Xing_2 飞行。
using System.Collections.Generic;
using PetDemo.Battle;
using PetDemo.Core;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Friend
{
    [DisallowMultipleComponent]
    public sealed class FriendHomeScreenView : MonoBehaviour
    {
        public const string ResBackground = "AirUI/JianYuan_2";
        public const string ResTitleBackground = "AirUI/HaoYouList_2";
        public const string ResDriveAwayIcon = "AirUI/HYXieZhu_3";
        public const string ResHarvestReadyIcon = "AirUI/ShouHuo-0";
        public const string ResFarmGridRootPrefab = "Prefabs/Farm/FarmGridRoot";
        public const string ResTileSlotPrefab = "Prefabs/Farm/TileSlot";

        /// <summary>SPEC §13.3：暂时关闭好友家园主角村民；恢复展示时改为 true。</summary>
        private const bool ShowFriendHomeVillager = false;

        private const float PlantSpawnChance = 0.6f;
        private const float DriveAwayButtonSize = 150f;
        private const float DriveAwayOffsetY = 110f;
        private static readonly Vector2 VillagerOffsetFromFarm = new Vector2(0f, -620f);
        private static readonly Color WiltedTint = new Color(0.55f, 0.55f, 0.55f, 1f);
        private static readonly Color FallbackBgColor = new Color(0.10f, 0.12f, 0.18f, 1f);
        private static readonly Color TitleFallbackColor = new Color(0.16f, 0.18f, 0.24f, 0.95f);

        private static List<PlantConfig> sPlantConfigs;

        private InvasionService invasionService;
        private RectTransform rootRt;
        private RectTransform worldContentRt;
        private RectTransform villagerRt;
        private RectTransform driveAwayButtonRt;
        private Text titleText;
        private bool battleRequested;
        /// <summary>SPEC §13.4：驱赶开战成功后隐藏本层，战后 OnBattleEnded 再恢复（用户主动 Hide 时清除）。</summary>
        private bool suspendedForDriveAwayBattle;

        public bool IsShown => rootRt != null && rootRt.gameObject.activeSelf;

        /// <summary>SPEC §13.3：在 HUD 根下构建好友家园层（默认隐藏；HudModal，低于战斗 HudOverlay）。</summary>
        public static FriendHomeScreenView BuildInto(RectTransform hudRoot, InvasionService invasion)
        {
            if (hudRoot == null)
                return null;

            var rootGo = new GameObject("FriendHomeScreen", typeof(RectTransform));
            var root = rootGo.GetComponent<RectTransform>();
            root.SetParent(hudRoot, false);
            StretchFull(root);
            MainHudLayerRoot.ApplySortTier(root, MainUiSortTier.HudModal);

            var view = rootGo.AddComponent<FriendHomeScreenView>();
            view.rootRt = root;
            view.invasionService = invasion;
            view.BuildStaticHierarchy(root);

            if (invasion != null)
                invasion.OnBattleEnded += view.OnBattleEnded;

            rootGo.SetActive(false);
            return view;
        }

        /// <summary>SPEC §13.2「去Ta家」：打开指定好友的家园（每次打开随机重建）。</summary>
        public void ShowFor(FriendProfile friend)
        {
            if (rootRt == null)
                return;

            if (titleText != null)
                titleText.text = (friend != null ? friend.displayName : "好友") + "的家园";

            RebuildWorld();
            battleRequested = false;
            suspendedForDriveAwayBattle = false;
            rootRt.SetAsLastSibling();
            rootRt.gameObject.SetActive(true);
        }

        public void Hide()
        {
            suspendedForDriveAwayBattle = false;
            if (rootRt != null)
                rootRt.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (invasionService != null)
                invasionService.OnBattleEnded -= OnBattleEnded;
        }

        // ---- 构建 ----

        private void BuildStaticHierarchy(RectTransform root)
        {
            // 全屏射线阻挡层（背景缺图时也保证遮挡其下 UI）。
            var blocker = CreateChild(root, "Blocker", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(blocker);
            var blockerImg = blocker.gameObject.AddComponent<Image>();
            blockerImg.color = FallbackBgColor;
            blockerImg.raycastTarget = true;

            // 视口 + 世界内容（静态，无跟随/拖动）。
            var viewport = CreateChild(root, "FriendHomeViewport", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            worldContentRt = CreateChild(viewport, "FriendHomeWorldContent",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            // 标题（FriendHomeViewport 内，顶部居中；HaoYouList_2 底图 + 子节点 Text，避免同 GO 多 Graphic）。
            var titleBarRt = CreateChild(viewport, "TitleBar",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(800f, 80f));
            var titleBg = titleBarRt.gameObject.AddComponent<Image>();
            var titleSprite = Resources.Load<Sprite>(ResTitleBackground);
            if (titleSprite != null)
            {
                titleBg.sprite = titleSprite;
                titleBg.color = Color.white;
            }
            else
            {
                titleBg.color = TitleFallbackColor;
            }
            titleBg.raycastTarget = false;

            var titleLabelRt = CreateChild(titleBarRt, "TitleText", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(titleLabelRt);
            titleText = titleLabelRt.gameObject.AddComponent<Text>();
            titleText.font = FarmGridView.LoadBuiltinFont();
            titleText.text = "好友的家园";
            titleText.fontSize = 48;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
            titleText.raycastTarget = false;
            titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            titleText.verticalOverflow = VerticalWrapMode.Overflow;

            // 右上角返回按钮（§9.14.9 关闭范式）。
            var backRt = CreateChild(root, "BackButton", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(72f, 72f));
            var backImg = backRt.gameObject.AddComponent<Image>();
            backImg.color = new Color(0.25f, 0.25f, 0.3f, 0.9f);
            backImg.raycastTarget = true;
            var backBtn = backRt.gameObject.AddComponent<Button>();
            backBtn.transition = Selectable.Transition.ColorTint;
            backBtn.targetGraphic = backImg;
            backBtn.onClick.AddListener(Hide);
            var backLabel = CreateText(backRt, "Label", "×",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 72f), 48, TextAnchor.MiddleCenter);
            backLabel.raycastTarget = false;
        }

        private void RebuildWorld()
        {
            if (worldContentRt == null)
                return;

            for (int i = worldContentRt.childCount - 1; i >= 0; i--)
                Destroy(worldContentRt.GetChild(i).gameObject);
            villagerRt = null;
            driveAwayButtonRt = null;

            // 背景（同 §9.8.14 JianYuan_2）。
            var bgRt = CreateChild(worldContentRt, "Background",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var bgImage = bgRt.gameObject.AddComponent<Image>();
            var bgSprite = Resources.Load<Sprite>(ResBackground);
            if (bgSprite != null)
            {
                var size = bgSprite.rect.size;
                worldContentRt.sizeDelta = size;
                bgRt.sizeDelta = size;
                bgImage.sprite = bgSprite;
                bgImage.preserveAspect = false;
                bgImage.color = Color.white;
            }
            else
            {
                worldContentRt.sizeDelta = new Vector2(1080f, 1920f);
                bgRt.sizeDelta = worldContentRt.sizeDelta;
                bgImage.color = FallbackBgColor;
            }
            bgImage.raycastTarget = false;

            // 农田克隆 + 随机植物。
            var farmRoot = BuildFarmClone(worldContentRt, out var plantedSlots);

            // 世界内容平移：使农田居中于屏幕（SPEC §13.3）。
            if (farmRoot != null)
                worldContentRt.anchoredPosition = -farmRoot.anchoredPosition;

            // 主角村民（§9.8.9.6 构建路径，exclusive_2 待机）；ShowFriendHomeVillager=false 时跳过。
            if (ShowFriendHomeVillager)
            {
                var farmPos = farmRoot != null ? farmRoot.anchoredPosition : Vector2.zero;
                villagerRt = GuildSpineCharacterBuilder.BuildVillager(
                    worldContentRt, "FriendHomeVillager", farmPos + VillagerOffsetFromFarm, out var villagerSkeleton);
                GuildSpineCharacterBuilder.PlayLoop(villagerSkeleton, "exclusive_2", "idle");
            }

            // 随机 1 个有植物田上方放「驱赶」按钮（挂农田根 overlay，同 §13.5 HomeAssistEventController）。
            if (plantedSlots.Count > 0 && farmRoot != null)
            {
                var slot = plantedSlots[Random.Range(0, plantedSlots.Count)];
                BuildDriveAwayButton(farmRoot, slot);
            }
            else
            {
                UnityEngine.Debug.LogWarning("[FriendHomeScreenView] 本次随机无植物田，不显示「驱赶」按钮。");
            }
        }

        /// <summary>
        /// 实例化 FarmGridRoot 预制体并剥离 FarmGridView / TileSlotView 行为，
        /// 每格仅保留 SoilImage + PlantImage；为约 60% 的田格随机刷植物（随机配置 + 随机状态）。
        /// </summary>
        private RectTransform BuildFarmClone(RectTransform parent, out List<RectTransform> plantedSlots)
        {
            plantedSlots = new List<RectTransform>();

            var gridPrefab = Resources.Load<RectTransform>(ResFarmGridRootPrefab);
            RectTransform farmRoot;
            List<RectTransform> slotRects;
            if (gridPrefab != null)
            {
                farmRoot = Instantiate(gridPrefab, parent, false);
                farmRoot.name = "FriendFarmGridRoot";
                slotRects = StripFarmBehaviours(farmRoot);
            }
            else
            {
                UnityEngine.Debug.LogWarning("[FriendHomeScreenView] 缺少预制体 Resources/" + ResFarmGridRootPrefab +
                                             "，使用代码回退布局。");
                farmRoot = BuildFallbackFarmRoot(parent, out slotRects);
            }

            DisableFarmDecorRaycasts(farmRoot);

            var configs = LoadPlantConfigs();
            for (int i = 0; i < slotRects.Count; i++)
            {
                if (Random.value > PlantSpawnChance)
                    continue;
                if (ApplyRandomPlantVisual(slotRects[i], configs))
                    plantedSlots.Add(slotRects[i]);
            }

            return farmRoot;
        }

        /// <summary>
        /// FarmGridRoot 预制体含 4096×4096 装饰底图 Image（raycastTarget 默认开启），
        /// 会吞掉田格上方 overlay 按钮的点击；好友家园纯展示层须关闭。
        /// </summary>
        private static void DisableFarmDecorRaycasts(RectTransform farmRoot)
        {
            if (farmRoot == null)
                return;

            var decor = farmRoot.Find("Image");
            if (decor == null)
                return;

            var img = decor.GetComponent<Image>();
            if (img != null)
                img.raycastTarget = false;
        }

        private static List<RectTransform> StripFarmBehaviours(RectTransform farmRoot)
        {
            var slotRects = new List<RectTransform>(20);

            var gridView = farmRoot.GetComponent<FarmGridView>();
            if (gridView != null)
                Destroy(gridView);

            var slotViews = farmRoot.GetComponentsInChildren<TileSlotView>(true);
            for (int i = 0; i < slotViews.Length; i++)
            {
                var slotRt = slotViews[i].transform as RectTransform;
                if (slotRt == null)
                    continue;
                slotRects.Add(slotRt);
                Destroy(slotViews[i]);

                // 仅保留泥土与植物图层，徽标/序号/焦点环等一律隐藏（SPEC §13.3）。
                for (int c = 0; c < slotRt.childCount; c++)
                {
                    var child = slotRt.GetChild(c);
                    bool keep = child.name == "SoilImage" || child.name == "PlantImage";
                    child.gameObject.SetActive(keep);
                }
            }

            return slotRects;
        }

        private static RectTransform BuildFallbackFarmRoot(RectTransform parent, out List<RectTransform> slotRects)
        {
            // 与 FarmGridView.BuildAutoSlots 同款 5×4 公式回退布局。
            const int cols = FarmGridView.Cols;
            const int rows = FarmGridView.Rows;
            const float tileW = 220f;
            const float tileH = 160f;
            const float colGap = 24f;
            const float rowGap = 12f;

            slotRects = new List<RectTransform>(cols * rows);

            var rootGo = new GameObject("FriendFarmGridRoot", typeof(RectTransform));
            var rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.SetParent(parent, false);
            rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.anchoredPosition = new Vector2(0f, 60f);
            rootRt.sizeDelta = new Vector2(952f, 1020f);

            float totalW = cols * tileW + (cols - 1) * colGap;
            float totalH = rows * tileH + (rows - 1) * rowGap;
            float startX = -totalW * 0.5f + tileW * 0.5f;
            float startY = totalH * 0.5f - tileH * 0.5f;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    var slotRt = CreateChild(rootRt, "TileSlot_" + (r * cols + c + 1).ToString("D2"),
                        new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(startX + c * (tileW + colGap), startY - r * (tileH + rowGap)),
                        new Vector2(tileW, tileH));

                    var soil = CreateChild(slotRt, "SoilImage", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(tileW, tileH));
                    var soilImg = soil.gameObject.AddComponent<Image>();
                    soilImg.color = new Color(0xA0 / 255f, 0x76 / 255f, 0x3A / 255f, 1f);
                    soilImg.raycastTarget = false;

                    var plant = CreateChild(slotRt, "PlantImage", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 140f));
                    var plantImg = plant.gameObject.AddComponent<Image>();
                    plantImg.preserveAspect = true;
                    plantImg.raycastTarget = false;
                    plantImg.enabled = false;

                    slotRects.Add(slotRt);
                }
            }

            return rootRt;
        }

        /// <summary>随机植物配置 + 随机状态（Growing 节点 1..4 / AwaitingHarvest 末节点+徽标 / Wilted 末节点灰化）。</summary>
        private static bool ApplyRandomPlantVisual(RectTransform slotRt, List<PlantConfig> configs)
        {
            if (slotRt == null || configs == null || configs.Count == 0)
                return false;

            var plantImage = FindChildImage(slotRt, "PlantImage");
            if (plantImage == null)
                return false;

            var cfg = configs[Random.Range(0, configs.Count)];
            if (cfg?.appearanceSpriteIds == null || cfg.appearanceSpriteIds.Count == 0)
                return false;

            int lastNode = cfg.appearanceSpriteIds.Count;
            int stateRoll = Random.Range(0, 3); // 0=Growing 1=AwaitingHarvest 2=Wilted
            int node = stateRoll == 0 ? Random.Range(1, Mathf.Max(2, lastNode)) : lastNode;

            string spritePath = cfg.appearanceSpriteIds[Mathf.Clamp(node - 1, 0, lastNode - 1)];
            var sprite = !string.IsNullOrWhiteSpace(spritePath)
                ? Resources.Load<Sprite>(spritePath.Trim())
                : null;
            if (sprite == null)
                return false;

            plantImage.gameObject.SetActive(true);
            plantImage.sprite = sprite;
            plantImage.enabled = true;
            plantImage.preserveAspect = true;
            plantImage.color = stateRoll == 2 ? WiltedTint : Color.white;

            if (stateRoll == 1)
            {
                // AwaitingHarvest：田格中心叠 ShouHuo-0 徽标（同 §9.1 SetHarvestBadge 观感）。
                var badge = CreateChild(slotRt, "FriendHarvestBadge",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(120f, 120f));
                var badgeImg = badge.gameObject.AddComponent<Image>();
                badgeImg.sprite = Resources.Load<Sprite>(ResHarvestReadyIcon);
                badgeImg.preserveAspect = true;
                badgeImg.raycastTarget = false;
                badgeImg.enabled = badgeImg.sprite != null;
            }

            return true;
        }

        private void BuildDriveAwayButton(RectTransform farmRoot, RectTransform slotRt)
        {
            var rt = CreateChild(farmRoot, "DriveAwayButton",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(DriveAwayButtonSize, DriveAwayButtonSize));
            rt.SetAsLastSibling();
            var local = (Vector2)farmRoot.InverseTransformPoint(slotRt.position);
            rt.anchoredPosition = local + new Vector2(0f, DriveAwayOffsetY);
            driveAwayButtonRt = rt;

            var img = rt.gameObject.AddComponent<Image>();
            var sprite = Resources.Load<Sprite>(ResDriveAwayIcon);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.preserveAspect = true;
                img.color = Color.white;
            }
            else
            {
                img.color = new Color(0.85f, 0.35f, 0.25f, 0.95f);
                var label = CreateText(rt, "Label", "驱赶",
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(DriveAwayButtonSize, 60f), 36, TextAnchor.MiddleCenter);
                label.raycastTarget = false;
                UnityEngine.Debug.LogWarning("[FriendHomeScreenView] 缺少图标 Resources/" + ResDriveAwayIcon +
                                             "，驱赶按钮使用文字回退。");
            }
            img.raycastTarget = true;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;
            btn.onClick.AddListener(OnDriveAwayClicked);
        }

        // ---- 驱赶战斗（SPEC §13.4） ----

        private void OnDriveAwayClicked()
        {
            if (battleRequested)
                return;
            if (invasionService == null)
            {
                UnityEngine.Debug.LogWarning("[FriendHomeScreenView] InvasionService 不可用，无法开战。");
                return;
            }

            battleRequested = true;
            invasionService.OpenBattleFromFriendHome();
            if (invasionService.GetPhase() != InvasionPhase.InBattle)
            {
                battleRequested = false;
                return;
            }

            // SPEC §13.4：开战成功后隐藏好友家园层，避免叠在战斗 HudOverlay 之上。
            suspendedForDriveAwayBattle = true;
            if (rootRt != null)
                rootRt.gameObject.SetActive(false);
        }

        private void OnBattleEnded(bool playerWon)
        {
            battleRequested = false;
            if (invasionService == null || !invasionService.EnteredBattleViaFriendHome || !suspendedForDriveAwayBattle)
                return;

            suspendedForDriveAwayBattle = false;
            if (rootRt != null)
            {
                rootRt.SetAsLastSibling();
                rootRt.gameObject.SetActive(true);
            }

            // 每次进入好友家园至多驱赶一次（SPEC §13.4）。
            if (driveAwayButtonRt != null)
            {
                Destroy(driveAwayButtonRt.gameObject);
                driveAwayButtonRt = null;
            }

            PlayStarFlyFromVillager();
        }

        private void PlayStarFlyFromVillager()
        {
            var canvas = rootRt != null ? rootRt.GetComponentInParent<Canvas>() : null;
            var canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            if (canvasRect == null)
                return;

            Vector2 fromScreen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (villagerRt != null)
            {
                var cam = StarFlyFx.ResolveCanvasCamera(canvasRect);
                fromScreen = RectTransformUtility.WorldToScreenPoint(cam, villagerRt.position);
            }

            StarFlyFx.Play(canvasRect, fromScreen);
        }

        // ---- 构建工具 ----

        private static Image FindChildImage(RectTransform parent, string childName)
        {
            var t = parent.Find(childName);
            return t != null ? t.GetComponent<Image>() : null;
        }

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
            txt.color = Color.white;
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

        private static List<PlantConfig> LoadPlantConfigs()
        {
            if (sPlantConfigs == null || sPlantConfigs.Count == 0)
                sPlantConfigs = PlantConfigCatalog.LoadPlantConfigsFromCsv();
            return sPlantConfigs;
        }
    }
}
