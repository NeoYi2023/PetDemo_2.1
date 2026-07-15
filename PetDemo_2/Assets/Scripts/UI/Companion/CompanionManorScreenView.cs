// SPEC §9.8.19（v3.238；脚底枢轴 v3.254；Partner NPC + Waypoints v3.259；Obstacles v3.260；Scale 0.27 v3.261；建造弹图 v3.262）：
// 伴侣庄园全屏可走场景（公会屏下子层）。预制体优先 + 运行时骨架回退。
// 已结伴由 CompanionCottageView.TriggerEntry → Show(partnerFriendId)。
using System;
using System.Collections.Generic;
using PetDemo.Core;
using PetDemo.Farm;
using PetDemo.UI.Farm;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Companion
{
    [DisallowMultipleComponent]
    public sealed class CompanionManorScreenView : MonoBehaviour
    {
        public const string PrefabResourcePath = "Prefabs/Farm/CompanionManorScreenPanel";
        public const string HostRootName = "CompanionManorScreen";

        public const string ResMen1 = "AirUI/BLZY_men1";
        public const string ResMen2 = "AirUI/BLZY_men_2";
        public const string ResBuildOverlay = "AirUI/WDZY_ZS_UI_1";
        public const string ResJianZaoButton = "AirUI/ShouHuo_3";

        public const string Men1NodeName = "Men_1";
        public const string Men2NodeName = "Men_2";
        public const string PartnerNpcNodeName = "PartnerNpc";
        public const string DefaultPartnerNpcId = "friend-01";
        public const string BuildOverlayNodeName = "BuildOverlay";
        public const string JianZaoButtonNodeName = "Button-JianZao";
        public const string BaiFangButtonNodeName = "Button-BaiFang";

        private static readonly Vector2 MinWorldSize = new Vector2(1080f, 1920f);
        private static readonly Vector2 Men1DefaultPos = new Vector2(-280f, -80f);
        private static readonly Vector2 Men2DefaultPos = new Vector2(320f, -40f);
        private static readonly Vector2 PartnerNpcDefaultPos = new Vector2(180f, -160f);
        private static readonly Vector2 BackButtonPos = new Vector2(24f, -24f);
        private static readonly Vector2 BackButtonSize = new Vector2(160f, 80f);
        private static readonly Color BackButtonColor = new Color(0.14f, 0.16f, 0.22f, 0.92f);
        private static readonly Vector2 DefaultObstacleSize = new Vector2(300f, 200f);
        private static readonly Vector2 JianZaoButtonPos = new Vector2(-429f, -748f);
        private static readonly Vector2 BaiFangButtonPos = new Vector2(438f, -748f);
        private static readonly Vector2 ActionButtonSize = new Vector2(150f, 150f);
        private static readonly Vector2 BuildOverlayFallbackSize = new Vector2(1080f, 766f);

        private static readonly Vector2[] DefaultWaypointPositions =
        {
            new Vector2(-420f, -120f),
            new Vector2(200f, -80f),
            new Vector2(420f, 180f),
            new Vector2(-160f, 260f)
        };

        // SPEC §9.8.19（v3.260）：Obstacle_1..4 默认散落四角附近，供预制体再调。
        private static readonly Vector2[] DefaultObstaclePositions =
        {
            new Vector2(-700f, 600f),
            new Vector2(700f, 600f),
            new Vector2(-700f, -600f),
            new Vector2(700f, -600f)
        };

        [SerializeField] private RectTransform viewportRt;
        [SerializeField] private RectTransform worldContentRt;
        [SerializeField] private RectTransform playerSpawnRt;
        [SerializeField] private RectTransform obstaclesRootRt;
        [SerializeField] private RectTransform charactersRootRt;
        [SerializeField] private RectTransform npcsRootRt;
        [SerializeField] private RectTransform waypointsRootRt;
        [SerializeField] private VirtualJoystickView joystick;
        [SerializeField] private Button backButton;
        [SerializeField] private Button jianZaoButton;
        [SerializeField] private RectTransform buildOverlayRt;
        [SerializeField] private Button buildOverlayDimButton;
        [SerializeField] private Image buildOverlayImage;

        private JiaYuanViewportFollowController followController;
        private GuildPlayerController playerController;
        private GuildNpcFollowController npcFollowController;
        private GuildWorldDepthSorter depthSorter;
        private RectTransform playerRt;
        private RectTransform men1Rt;
        private RectTransform men2Rt;
        private bool sceneSpawned;
        private IPlantingService plantingService;
        private string pendingPartnerFriendId;

        /// <summary>
        /// SPEC §9.8.19：在公会屏根下装配庄园全屏子层（预制体优先，缺则运行时骨架）。
        /// </summary>
        public static CompanionManorScreenView BuildInto(RectTransform gongHuiRoot)
        {
            if (gongHuiRoot == null)
            {
                UnityEngine.Debug.LogWarning("[CompanionManorScreenView] gongHuiRoot 为空，跳过庄园构建。");
                return null;
            }

            var existing = gongHuiRoot.Find(HostRootName);
            if (existing != null)
            {
                var existingView = existing.GetComponent<CompanionManorScreenView>();
                if (existingView != null)
                {
                    existingView.WireBackButton();
                    existingView.WireActionButtons();
                    return existingView;
                }
            }

            CompanionManorScreenView view = null;
            var prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab != null)
            {
                var go = Instantiate(prefab, gongHuiRoot);
                go.name = HostRootName;
                view = go.GetComponent<CompanionManorScreenView>();
                if (view == null)
                {
                    UnityEngine.Debug.LogWarning(
                        "[CompanionManorScreenView] 预制体缺少 CompanionManorScreenView，改用运行时回退。");
                    Destroy(go);
                }
            }

            if (view == null)
                view = BuildRuntimeFallback(gongHuiRoot);

            var rootRt = (RectTransform)view.transform;
            BottomNavAttachedScreenLayout.StretchFull(rootRt);
            rootRt.SetAsLastSibling();
            view.gameObject.SetActive(false);
            view.WireBackButton();
            view.WireActionButtons();
            return view;
        }

        public void SetSceneRefs(
            RectTransform viewport,
            RectTransform worldContent,
            RectTransform playerSpawn,
            RectTransform obstaclesRoot,
            RectTransform charactersRoot,
            VirtualJoystickView joystickView,
            Button back)
        {
            SetSceneRefs(
                viewport, worldContent, playerSpawn,
                obstaclesRoot, charactersRoot, null, null, joystickView, back);
        }

        public void SetSceneRefs(
            RectTransform viewport,
            RectTransform worldContent,
            RectTransform playerSpawn,
            RectTransform obstaclesRoot,
            RectTransform charactersRoot,
            RectTransform npcsRoot,
            RectTransform waypointsRoot,
            VirtualJoystickView joystickView,
            Button back)
        {
            viewportRt = viewport;
            worldContentRt = worldContent;
            playerSpawnRt = playerSpawn;
            obstaclesRootRt = obstaclesRoot;
            charactersRootRt = charactersRoot;
            npcsRootRt = npcsRoot;
            waypointsRootRt = waypointsRoot;
            joystick = joystickView;
            backButton = back;
        }

        /// <summary>无伴侣 id 时等价空串（解析为 friend-01）。</summary>
        public void Show()
        {
            Show(null);
        }

        /// <summary>SPEC §9.8.19（v3.259）：显层并用会话伴侣 id 驱动 PartnerNpc。</summary>
        public void Show(string partnerFriendId)
        {
            pendingPartnerFriendId = partnerFriendId;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            EnsureSceneSpawned();
            if (joystick != null)
                joystick.SetInputEnabled(true);
            if (followController != null)
                followController.SetFollowEnabled(true);
        }

        public void Hide()
        {
            HideBuildOverlay();
            if (joystick != null)
                joystick.SetInputEnabled(false);
            if (followController != null)
                followController.SetFollowEnabled(false);
            gameObject.SetActive(false);
        }

        /// <summary>SPEC §9.8.19（v3.262）：底部原尺寸建造弹图。</summary>
        public void ShowBuildOverlay()
        {
            EnsureBuildOverlay();
            if (buildOverlayRt == null)
                return;
            buildOverlayRt.SetAsLastSibling();
            buildOverlayRt.gameObject.SetActive(true);
        }

        /// <summary>SPEC §9.8.19（v3.262）：关闭建造弹图。</summary>
        public void HideBuildOverlay()
        {
            if (buildOverlayRt != null)
                buildOverlayRt.gameObject.SetActive(false);
        }

        /// <summary>SPEC §9.8.19（v3.248）：注入种植服务，装扮装备后刷新 ManorPlayer。</summary>
        public void BindPlantingService(IPlantingService service)
        {
            if (plantingService != null)
                plantingService.OnPlayerAppearanceChanged -= OnPlayerAppearanceChanged;
            plantingService = service;
            if (plantingService != null)
                plantingService.OnPlayerAppearanceChanged += OnPlayerAppearanceChanged;

            if (sceneSpawned)
                RefreshManorPlayerAppearance();
        }

        private void Awake()
        {
            EnsureTiledBackground();
            WireBackButton();
            WireActionButtons();
            ResolveOptionalRoots();
        }

        private void ResolveOptionalRoots()
        {
            if (worldContentRt == null)
                return;
            if (npcsRootRt == null)
                npcsRootRt = worldContentRt.Find("Npcs") as RectTransform;
            if (waypointsRootRt == null)
                waypointsRootRt = worldContentRt.Find("Waypoints") as RectTransform;
        }

        private void EnsureTiledBackground()
        {
            if (worldContentRt == null)
                return;

            var bgRt = worldContentRt.Find("Background") as RectTransform;
            if (bgRt == null)
                return;

            Vector2 worldSize;
            if (CompanionManorBackgroundBuilder.TryBuild(bgRt, out worldSize))
            {
                worldContentRt.sizeDelta = worldSize;
                return;
            }

            worldContentRt.sizeDelta = Vector2.Max(bgRt.sizeDelta, MinWorldSize);
        }

        private void WireBackButton()
        {
            if (backButton == null)
                backButton = transform.Find("BackButton")?.GetComponent<Button>();
            if (backButton == null)
                return;

            backButton.onClick.RemoveListener(Hide);
            backButton.onClick.AddListener(Hide);
        }

        /// <summary>SPEC §9.8.19（v3.262）：幂等绑定建造按钮 → 底部弹图。</summary>
        private void WireActionButtons()
        {
            if (jianZaoButton == null)
            {
                var tf = transform.Find("Button/" + JianZaoButtonNodeName)
                    ?? transform.Find(JianZaoButtonNodeName);
                if (tf != null)
                    jianZaoButton = tf.GetComponent<Button>();
            }

            if (jianZaoButton == null)
                return;

            jianZaoButton.onClick.RemoveListener(ShowBuildOverlay);
            jianZaoButton.onClick.AddListener(ShowBuildOverlay);

            if (buildOverlayDimButton != null)
            {
                buildOverlayDimButton.onClick.RemoveListener(HideBuildOverlay);
                buildOverlayDimButton.onClick.AddListener(HideBuildOverlay);
            }
        }

        /// <summary>
        /// SPEC §9.8.19（v3.262）：懒建 BuildOverlay（Dim 空白关闭 + Image 底边原尺寸）。
        /// </summary>
        private void EnsureBuildOverlay()
        {
            if (buildOverlayRt != null && buildOverlayImage != null && buildOverlayDimButton != null)
            {
                ApplyBuildOverlaySprite(buildOverlayImage);
                buildOverlayDimButton.onClick.RemoveListener(HideBuildOverlay);
                buildOverlayDimButton.onClick.AddListener(HideBuildOverlay);
                return;
            }

            var rootRt = (RectTransform)transform;
            var existing = rootRt.Find(BuildOverlayNodeName) as RectTransform;
            if (existing != null)
                buildOverlayRt = existing;

            if (buildOverlayRt == null)
            {
                BuildBuildOverlaySkeleton(rootRt, out buildOverlayRt, out buildOverlayDimButton, out buildOverlayImage);
            }
            else
            {
                if (buildOverlayDimButton == null)
                {
                    var dimTf = buildOverlayRt.Find("Dim");
                    if (dimTf == null)
                    {
                        var dimRt = BottomNavAttachedScreenLayout.CreateChildRect(
                            buildOverlayRt, "Dim",
                            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                        BottomNavAttachedScreenLayout.StretchFull(dimRt);
                        dimRt.SetAsFirstSibling();
                        var dimImg = dimRt.gameObject.AddComponent<Image>();
                        dimImg.color = new Color(0f, 0f, 0f, 0f);
                        dimImg.raycastTarget = true;
                        buildOverlayDimButton = dimRt.gameObject.AddComponent<Button>();
                        buildOverlayDimButton.transition = Selectable.Transition.None;
                        buildOverlayDimButton.targetGraphic = dimImg;
                    }
                    else
                    {
                        buildOverlayDimButton = dimTf.GetComponent<Button>();
                        if (buildOverlayDimButton == null)
                        {
                            var dimImg = dimTf.GetComponent<Image>();
                            buildOverlayDimButton = dimTf.gameObject.AddComponent<Button>();
                            buildOverlayDimButton.transition = Selectable.Transition.None;
                            if (dimImg != null)
                                buildOverlayDimButton.targetGraphic = dimImg;
                        }
                    }
                }

                if (buildOverlayImage == null)
                {
                    var imageTf = buildOverlayRt.Find("Image");
                    if (imageTf == null)
                    {
                        var imageRt = BottomNavAttachedScreenLayout.CreateChildRect(
                            buildOverlayRt, "Image",
                            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            Vector2.zero, BuildOverlayFallbackSize);
                        imageRt.pivot = new Vector2(0.5f, 0f);
                        buildOverlayImage = imageRt.gameObject.AddComponent<Image>();
                        buildOverlayImage.raycastTarget = true;
                    }
                    else
                    {
                        buildOverlayImage = imageTf.GetComponent<Image>();
                        if (buildOverlayImage == null)
                            buildOverlayImage = imageTf.gameObject.AddComponent<Image>();
                    }
                }
            }

            if (buildOverlayImage != null)
                ApplyBuildOverlaySprite(buildOverlayImage);

            if (buildOverlayDimButton != null)
            {
                buildOverlayDimButton.onClick.RemoveListener(HideBuildOverlay);
                buildOverlayDimButton.onClick.AddListener(HideBuildOverlay);
            }

            if (buildOverlayRt != null)
                buildOverlayRt.gameObject.SetActive(false);
        }

        private static void ApplyBuildOverlaySprite(Image image)
        {
            if (image == null)
                return;

            var sprite = Resources.Load<Sprite>(ResBuildOverlay);
            var rt = image.rectTransform;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
                image.preserveAspect = true;
                rt.sizeDelta = sprite.rect.size;
            }
            else
            {
                image.sprite = null;
                image.color = new Color(0.2f, 0.22f, 0.28f, 0.96f);
                rt.sizeDelta = BuildOverlayFallbackSize;
                UnityEngine.Debug.LogWarning(
                    "[CompanionManorScreenView] 缺少建造弹图 Resources/" + ResBuildOverlay);
            }

            image.raycastTarget = true;
            // 底边居中，保持原生尺寸，不拉伸。
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
        }

        private static void BuildBuildOverlaySkeleton(
            RectTransform screenRoot,
            out RectTransform overlayRt,
            out Button dimButton,
            out Image panelImage)
        {
            overlayRt = BottomNavAttachedScreenLayout.CreateChildRect(
                screenRoot, BuildOverlayNodeName,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(overlayRt);
            overlayRt.SetAsLastSibling();

            var dimRt = BottomNavAttachedScreenLayout.CreateChildRect(
                overlayRt, "Dim",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(dimRt);
            var dimImg = dimRt.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0f);
            dimImg.raycastTarget = true;
            dimButton = dimRt.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.targetGraphic = dimImg;

            var imageRt = BottomNavAttachedScreenLayout.CreateChildRect(
                overlayRt, "Image",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                Vector2.zero, BuildOverlayFallbackSize);
            imageRt.pivot = new Vector2(0.5f, 0f);
            panelImage = imageRt.gameObject.AddComponent<Image>();
            panelImage.raycastTarget = true;
            ApplyBuildOverlaySprite(panelImage);

            overlayRt.gameObject.SetActive(false);
        }

        private void EnsureSceneSpawned()
        {
            if (sceneSpawned)
                return;
            if (viewportRt == null || worldContentRt == null)
            {
                UnityEngine.Debug.LogWarning("[CompanionManorScreenView] 场景引用缺失，无法生成庄园内容。");
                return;
            }
            sceneSpawned = true;
            ResolveOptionalRoots();

            followController = viewportRt.gameObject.AddComponent<JiaYuanViewportFollowController>();
            followController.Initialize(viewportRt, worldContentRt);

            var spawnPos = playerSpawnRt != null
                ? GuildSceneGeometry.PointInContentSpace(playerSpawnRt, worldContentRt)
                : Vector2.zero;

            var characterParentRt = charactersRootRt != null ? charactersRootRt : worldContentRt;
            EnsureDecorations(characterParentRt);

            var playerSkeletonData = ResolveManorPlayerSkeletonData();
            playerRt = GuildSpineCharacterBuilder.BuildVillager(
                characterParentRt, "ManorPlayer", spawnPos, out var playerSkeleton,
                GuildSpineCharacterBuilder.GuildPlayerLocalScale,
                playerSkeletonData);

            var obstacles = obstaclesRootRt != null
                ? obstaclesRootRt.GetComponentsInChildren<GuildObstacleArea>(true)
                : Array.Empty<GuildObstacleArea>();
            playerController = gameObject.AddComponent<GuildPlayerController>();
            playerController.Initialize(playerRt, worldContentRt, joystick, playerSkeleton, obstacles);

            followController.SetFollowTarget(playerRt);
            playerController.BindViewportFollowSync(followController.ApplyPlayerContentDelta);

            // SPEC §9.8.19（v3.259）：PartnerNpc + NamePlate / 跟随 / 游走（对齐公会 §9.8.9）。
            var npcs = SpawnPartnerNpcs(characterParentRt);
            npcFollowController = gameObject.AddComponent<GuildNpcFollowController>();
            npcFollowController.Initialize(playerRt, worldContentRt);

            var work2Controller = gameObject.AddComponent<GuildNpcWork2InteractionController>();
            work2Controller.Initialize(playerController, npcFollowController);

            for (int i = 0; i < npcs.Length; i++)
            {
                if (npcs[i] == null)
                    continue;
                npcs[i].OnInteract = npcFollowController.StartFollow;
                if (npcs[i].ShowActionIcon)
                    npcs[i].OnActionIconClick = work2Controller.TryPlayWork2WithNpc;
            }

            var proximityController = gameObject.AddComponent<GuildProximityController>();
            proximityController.Initialize(
                playerRt, worldContentRt,
                Array.Empty<GuildBuildingMarker>(),
                npcs,
                Array.Empty<GuildResponseAreaMarker>(),
                joystick);

            var waypointsRoot = waypointsRootRt != null
                ? waypointsRootRt
                : worldContentRt.Find("Waypoints") as RectTransform;
            var waypoints = waypointsRoot != null
                ? waypointsRoot.GetComponentsInChildren<GuildWaypointMarker>(true)
                : Array.Empty<GuildWaypointMarker>();
            var wanderController = gameObject.AddComponent<GuildNpcWanderController>();
            wanderController.Initialize(
                worldContentRt, npcs, obstacles, waypoints,
                playerController.MoveSpeed * 0.85f);

            var depthList = new List<RectTransform>(8);
            if (playerRt != null)
                depthList.Add(playerRt);
            if (men1Rt != null)
                depthList.Add(men1Rt);
            if (men2Rt != null)
                depthList.Add(men2Rt);
            for (int i = 0; i < npcs.Length; i++)
            {
                if (npcs[i] != null)
                    depthList.Add(npcs[i].Rt);
            }

            depthSorter = gameObject.AddComponent<GuildWorldDepthSorter>();
            depthSorter.Initialize(worldContentRt, characterParentRt);
            depthSorter.SetCharacters(depthList);
        }

        /// <summary>
        /// 解析伴侣 npcId、挂 Spine，并将 PartnerNpc reparent 到 Characters/ 以便深度排序。
        /// </summary>
        private GuildNpcMarker[] SpawnPartnerNpcs(RectTransform characterParentRt)
        {
            ResolveOptionalRoots();
            EnsurePartnerNpcSlot();
            EnsureDefaultWaypoints();

            var markers = npcsRootRt != null
                ? npcsRootRt.GetComponentsInChildren<GuildNpcMarker>(true)
                : Array.Empty<GuildNpcMarker>();
            if (markers.Length == 0 && characterParentRt != null)
                markers = characterParentRt.GetComponentsInChildren<GuildNpcMarker>(true);

            string resolvedId = ResolvePartnerNpcId(pendingPartnerFriendId);
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] == null)
                    continue;

                markers[i].SetNpcId(resolvedId);

                SkeletonDataAsset npcDataOverride = null;
                if (TopFriendCatalog.TryGetById(markers[i].NpcId, out var friendProfile)
                    && friendProfile != null
                    && !string.IsNullOrEmpty(friendProfile.spinePrefabPath))
                {
                    npcDataOverride = GuildSpineCharacterBuilder.ResolveSkeletonDataAssetFromPrefabPath(
                        friendProfile.spinePrefabPath);
                }

                var npcSpineRt = GuildSpineCharacterBuilder.BuildCharacter(
                    markers[i].Rt, markers[i].SkeletonKind, "NpcSpine", Vector2.zero, out var npcSkeleton,
                    localScale: GuildSpineCharacterBuilder.GuildPlayerLocalScale,
                    dataAssetOverride: npcDataOverride);
                GuildSpineCharacterBuilder.PlayLoop(
                    npcSkeleton, "standby_1", "animation", "idle", "exclusive_2");
                markers[i].AttachSpine(npcSpineRt, npcSkeleton);

                // 深度排序要求同父：保留 anchoredPosition，移入 Characters/。
                if (characterParentRt != null && markers[i].Rt.parent != characterParentRt)
                {
                    Vector2 pos = markers[i].Rt.anchoredPosition;
                    markers[i].Rt.SetParent(characterParentRt, false);
                    markers[i].Rt.anchoredPosition = pos;
                }
            }

            return markers;
        }

        private void EnsurePartnerNpcSlot()
        {
            if (worldContentRt == null)
                return;

            if (npcsRootRt == null)
                npcsRootRt = GongHuiScreenView.CreateStretchedGroup(worldContentRt, "Npcs");

            var existing = npcsRootRt.GetComponentsInChildren<GuildNpcMarker>(true);
            if (existing != null && existing.Length > 0)
                return;

            BuildPartnerNpcMarker(npcsRootRt, PartnerNpcDefaultPos);
        }

        private void EnsureDefaultWaypoints()
        {
            if (worldContentRt == null)
                return;

            if (waypointsRootRt == null)
                waypointsRootRt = GongHuiScreenView.CreateStretchedGroup(worldContentRt, "Waypoints");

            var existing = waypointsRootRt.GetComponentsInChildren<GuildWaypointMarker>(true);
            if (existing != null && existing.Length > 0)
                return;

            for (int i = 0; i < DefaultWaypointPositions.Length; i++)
                BuildWaypointMarker(waypointsRootRt, "Waypoint_" + (i + 1), DefaultWaypointPositions[i]);
        }

        /// <summary>SPEC §9.8.19（v3.259）：Catalog 无匹配或空 → friend-01。</summary>
        public static string ResolvePartnerNpcId(string partnerFriendId)
        {
            if (!string.IsNullOrEmpty(partnerFriendId)
                && TopFriendCatalog.TryGetById(partnerFriendId, out var profile)
                && profile != null)
            {
                return partnerFriendId;
            }

            return DefaultPartnerNpcId;
        }

        private void OnDestroy()
        {
            if (plantingService != null)
                plantingService.OnPlayerAppearanceChanged -= OnPlayerAppearanceChanged;
        }

        private void OnPlayerAppearanceChanged()
        {
            RefreshManorPlayerAppearance();
        }

        private SkeletonDataAsset ResolveManorPlayerSkeletonData()
        {
            string equipped = plantingService != null
                ? plantingService.GetEquippedPlayerSpineResource()
                : null;
            return PlayerSpineAppearanceResolver.Resolve(equipped);
        }

        /// <summary>SPEC §9.8.19（v3.248）：按会话装备路径刷新庄园主角 Spine。</summary>
        private void RefreshManorPlayerAppearance()
        {
            if (!sceneSpawned || playerRt == null)
                return;

            var dataAsset = ResolveManorPlayerSkeletonData();
            if (dataAsset == null)
                return;

            float facingSign = playerRt.localScale.x;
            if (!GuildSpineCharacterBuilder.TryReplaceSkeletonData(playerRt, dataAsset, out var sg))
            {
                UnityEngine.Debug.LogWarning("[CompanionManorScreenView] 刷新 ManorPlayer Spine 失败。");
                return;
            }

            if (facingSign < 0f && playerRt.localScale.x > 0f)
                GuildSpineCharacterBuilder.SetFacing(playerRt, faceRight: true);
            else if (facingSign > 0f && playerRt.localScale.x < 0f)
                GuildSpineCharacterBuilder.SetFacing(playerRt, faceRight: false);

            if (playerController != null)
                playerController.RebindSkeleton(sg);
            else
                GuildSpineCharacterBuilder.PlayLoop(sg, "exclusive_2", "standby_1", "animation", "idle");
        }

        private void EnsureDecorations(RectTransform characterParent)
        {
            if (characterParent == null)
                return;

            men1Rt = EnsureDecoration(characterParent, Men1NodeName, ResMen1, Men1DefaultPos);
            men2Rt = EnsureDecoration(characterParent, Men2NodeName, ResMen2, Men2DefaultPos);
        }

        private static RectTransform EnsureDecoration(
            RectTransform parent, string nodeName, string spritePath, Vector2 defaultPos)
        {
            var existing = parent.Find(nodeName) as RectTransform;
            if (existing != null)
            {
                EnsureDecorationImage(existing, spritePath);
                // SPEC §9.8.19.2.1（v3.254）：深度排序读 Rect 枢轴，须落到脚底。
                EnsureFeetPivot(existing);
                return existing;
            }

            var sprite = Resources.Load<Sprite>(spritePath);
            var size = sprite != null ? sprite.rect.size : new Vector2(200f, 280f);
            var rt = BottomNavAttachedScreenLayout.CreateChildRect(
                parent, nodeName,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                defaultPos, size);
            EnsureDecorationImage(rt, spritePath);
            EnsureFeetPivot(rt);
            return rt;
        }

        /// <summary>
        /// SPEC §9.8.19.2.1：把门装饰 RectTransform 枢轴落到脚底 (0.5,0)，
        /// 使 GuildWorldDepthSorter 的 sortY 与可见脚底一致；补偿 anchoredPosition 保持画面不跳。
        /// </summary>
        private static void EnsureFeetPivot(RectTransform rt)
        {
            if (rt == null)
                return;

            var feetPivot = new Vector2(0.5f, 0f);
            if (Mathf.Approximately(rt.pivot.x, feetPivot.x) &&
                Mathf.Approximately(rt.pivot.y, feetPivot.y))
                return;

            Vector2 size = rt.rect.size;
            Vector2 deltaPivot = feetPivot - rt.pivot;
            Vector3 scale = rt.localScale;
            rt.pivot = feetPivot;
            rt.anchoredPosition += new Vector2(
                deltaPivot.x * size.x * scale.x,
                deltaPivot.y * size.y * scale.y);
        }

        private static void EnsureDecorationImage(RectTransform rt, string spritePath)
        {
            if (rt == null)
                return;
            var image = rt.GetComponent<Image>();
            if (image == null)
                image = rt.gameObject.AddComponent<Image>();
            var sprite = Resources.Load<Sprite>(spritePath);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.preserveAspect = true;
                image.color = Color.white;
                if (rt.sizeDelta.sqrMagnitude < 1f)
                    rt.sizeDelta = sprite.rect.size;
            }
            else
            {
                image.color = new Color(0.55f, 0.4f, 0.3f, 0.9f);
                UnityEngine.Debug.LogWarning(
                    "[CompanionManorScreenView] 缺少装饰 Resources/" + spritePath);
            }
            image.raycastTarget = false;
        }

        private static CompanionManorScreenView BuildRuntimeFallback(RectTransform parent)
        {
            var root = BottomNavAttachedScreenLayout.CreateChildRect(
                parent, HostRootName,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(root);

            var view = root.gameObject.AddComponent<CompanionManorScreenView>();
            BuildSceneSkeleton(root, view);
            return view;
        }

        /// <summary>搭建场景骨架；预制体生成器与运行时回退共用。</summary>
        public static void BuildSceneSkeleton(RectTransform root, CompanionManorScreenView view)
        {
            var viewport = BottomNavAttachedScreenLayout.CreateChildRect(
                root, "ManorViewport",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            var worldContent = BottomNavAttachedScreenLayout.CreateChildRect(
                viewport, "ManorWorldContent",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

            var bgRt = BottomNavAttachedScreenLayout.CreateChildRect(
                worldContent, "Background",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

            Vector2 worldSize;
            if (!CompanionManorBackgroundBuilder.TryBuild(bgRt, out worldSize))
            {
                worldSize = MinWorldSize;
                var bgImage = bgRt.gameObject.AddComponent<Image>();
                bgImage.color = new Color(0.18f, 0.22f, 0.16f, 1f);
                bgImage.raycastTarget = false;
                bgRt.sizeDelta = worldSize;
                UnityEngine.Debug.LogWarning(
                    "[CompanionManorScreenView] 缺少庄园背景切块（" +
                    CompanionManorBackgroundBuilder.TileResourcePrefix +
                    "_r*c*），已使用纯色回退。");
            }

            worldContent.sizeDelta = worldSize;
            worldContent.localScale = Vector3.one;

            var obstaclesRoot = GongHuiScreenView.CreateStretchedGroup(worldContent, "Obstacles");
            var charactersRoot = GongHuiScreenView.CreateStretchedGroup(worldContent, "Characters");
            var npcsRoot = GongHuiScreenView.CreateStretchedGroup(worldContent, "Npcs");
            var waypointsRoot = GongHuiScreenView.CreateStretchedGroup(worldContent, "Waypoints");

            // SPEC §9.8.19（v3.260）：默认 Obstacles 占位，位置/尺寸由预制体再调。
            for (int i = 0; i < DefaultObstaclePositions.Length; i++)
            {
                BuildObstacleMarker(
                    obstaclesRoot, "Obstacle_" + (i + 1),
                    DefaultObstaclePositions[i], DefaultObstacleSize);
            }

            // 预制体内预置门装饰，便于 Inspector 调坐标。
            EnsureDecoration(charactersRoot, Men1NodeName, ResMen1, Men1DefaultPos);
            EnsureDecoration(charactersRoot, Men2NodeName, ResMen2, Men2DefaultPos);

            // SPEC §9.8.19（v3.259）：PartnerNpc + 默认 Waypoints（出生点/路径由预制体再调）。
            BuildPartnerNpcMarker(npcsRoot, PartnerNpcDefaultPos);
            for (int i = 0; i < DefaultWaypointPositions.Length; i++)
                BuildWaypointMarker(waypointsRoot, "Waypoint_" + (i + 1), DefaultWaypointPositions[i]);

            var playerSpawn = BottomNavAttachedScreenLayout.CreateChildRect(
                worldContent, "PlayerSpawn",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -200f), new Vector2(10f, 10f));

            var joystickView = VirtualJoystickView.BuildJoystickInto(root);
            var back = BuildBackButton(root);

            // SPEC §9.8.19（v3.262）：动作按钮 + 建造弹层骨架（预制体生成器复用）。
            var jianZao = EnsureActionButtons(root);
            RectTransform overlayRt;
            Button overlayDim;
            Image overlayImage;
            BuildBuildOverlaySkeleton(root, out overlayRt, out overlayDim, out overlayImage);

            view.SetSceneRefs(
                viewport, worldContent, playerSpawn,
                obstaclesRoot, charactersRoot, npcsRoot, waypointsRoot, joystickView, back);
            view.jianZaoButton = jianZao;
            view.buildOverlayRt = overlayRt;
            view.buildOverlayDimButton = overlayDim;
            view.buildOverlayImage = overlayImage;
        }

        /// <summary>
        /// SPEC §9.8.19（v3.262）：确保 Button/Button-JianZao（及占位 BaiFang）存在。
        /// </summary>
        private static Button EnsureActionButtons(RectTransform screenRoot)
        {
            if (screenRoot == null)
                return null;

            var buttonRoot = screenRoot.Find("Button") as RectTransform;
            if (buttonRoot == null)
            {
                buttonRoot = BottomNavAttachedScreenLayout.CreateChildRect(
                    screenRoot, "Button",
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                BottomNavAttachedScreenLayout.StretchFull(buttonRoot);
            }

            EnsureActionButton(
                buttonRoot, BaiFangButtonNodeName, BaiFangButtonPos, ActionButtonSize, null, "拜访");
            return EnsureActionButton(
                buttonRoot, JianZaoButtonNodeName, JianZaoButtonPos, ActionButtonSize,
                ResJianZaoButton, "建造");
        }

        private static Button EnsureActionButton(
            RectTransform parent, string name, Vector2 anchoredPos, Vector2 size,
            string spritePath, string labelText)
        {
            if (parent == null)
                return null;

            var existing = parent.Find(name);
            if (existing != null)
            {
                var existingBtn = existing.GetComponent<Button>();
                if (existingBtn != null)
                    return existingBtn;
            }

            var rt = BottomNavAttachedScreenLayout.CreateChildRect(
                parent, name,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                anchoredPos, size);

            var bg = rt.gameObject.AddComponent<Image>();
            bg.raycastTarget = true;
            if (!string.IsNullOrEmpty(spritePath))
            {
                var sprite = Resources.Load<Sprite>(spritePath);
                if (sprite != null)
                {
                    bg.sprite = sprite;
                    bg.color = Color.white;
                    bg.preserveAspect = true;
                }
                else
                {
                    bg.color = new Color(0.85f, 0.45f, 0.22f, 1f);
                }
            }
            else
            {
                bg.color = new Color(0.35f, 0.55f, 0.75f, 1f);
            }

            var labelRt = BottomNavAttachedScreenLayout.CreateChildRect(
                rt, "Text",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(labelRt);
            var label = labelRt.gameObject.AddComponent<Text>();
            label.text = labelText ?? string.Empty;
            label.font = FarmGridView.LoadBuiltinFont();
            label.fontSize = 28;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = bg;
            return btn;
        }

        private static void BuildObstacleMarker(
            RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size)
        {
            if (parent == null)
                return;
            if (parent.Find(name) != null)
                return;

            var rt = BottomNavAttachedScreenLayout.CreateChildRect(
                parent, name,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                anchoredPosition, size);
            rt.gameObject.AddComponent<GuildObstacleArea>();
        }

        private static void BuildPartnerNpcMarker(RectTransform parent, Vector2 anchoredPosition)
        {
            if (parent == null)
                return;

            var existing = parent.Find(PartnerNpcNodeName) as RectTransform;
            if (existing != null && existing.GetComponent<GuildNpcMarker>() != null)
                return;

            var rt = BottomNavAttachedScreenLayout.CreateChildRect(
                parent, PartnerNpcNodeName,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                anchoredPosition, new Vector2(10f, 10f));
            var marker = rt.gameObject.AddComponent<GuildNpcMarker>();
            marker.SetNpcId(DefaultPartnerNpcId);
            marker.SetSkeletonKind(GuildNpcSkeletonKind.LangMeiRen);
            marker.SetShowActionIcon(true);
            BakePartnerNamePlate(rt, DefaultPartnerNpcId);
        }

        private static void BuildWaypointMarker(RectTransform parent, string name, Vector2 anchoredPosition)
        {
            if (parent == null)
                return;
            if (parent.Find(name) != null)
                return;

            var rt = BottomNavAttachedScreenLayout.CreateChildRect(
                parent, name,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                anchoredPosition, new Vector2(10f, 10f));
            rt.gameObject.AddComponent<GuildWaypointMarker>();
        }

        private static void BakePartnerNamePlate(RectTransform npcRt, string npcId)
        {
            if (npcRt == null || npcRt.Find("NamePlate") != null)
                return;

            string displayName = npcId;
            Sprite avatarSprite = null;
            if (TopFriendCatalog.TryGetById(npcId, out var profile) && profile != null)
            {
                if (!string.IsNullOrEmpty(profile.displayName))
                    displayName = profile.displayName;
                if (!string.IsNullOrEmpty(profile.avatarResource))
                    avatarSprite = Resources.Load<Sprite>(profile.avatarResource);
            }

            var plateRt = GuildSceneUiFactory.BuildNpcNamePlate(
                npcRt, displayName, avatarSprite,
                new Color(0.45f, 0.55f, 0.75f, 1f), 330f);
            plateRt.gameObject.SetActive(false);
        }

        public static Button BuildBackButton(RectTransform screenRoot)
        {
            if (screenRoot == null)
                return null;

            var existing = screenRoot.Find("BackButton");
            if (existing != null)
                return existing.GetComponent<Button>();

            var buttonRt = BottomNavAttachedScreenLayout.CreateChildRect(
                screenRoot, "BackButton",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                BackButtonPos, BackButtonSize);
            buttonRt.pivot = new Vector2(0f, 1f);
            buttonRt.SetAsLastSibling();

            var bg = buttonRt.gameObject.AddComponent<Image>();
            bg.color = BackButtonColor;
            bg.raycastTarget = true;

            var labelRt = BottomNavAttachedScreenLayout.CreateChildRect(
                buttonRt, "Label",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(labelRt);
            var label = labelRt.gameObject.AddComponent<Text>();
            label.text = "返回";
            label.font = FarmGridView.LoadBuiltinFont();
            label.fontSize = 40;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;

            var btn = buttonRt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = bg;
            return btn;
        }
    }
}
