// SPEC §9.8.9 (v3.123；背景分块 v3.133；3×3 v3.176；全景 v3.183)：公会场景层 — 预制体优先（Resources/Prefabs/Farm/GongHuiScreenPanel）
// + 运行时回退；大图世界 + 视口跟随（复用 JiaYuanViewportFollowController）、透明摇杆移动主角、
// 碰撞阻挡、建筑/NPC/响应区接近名牌、右下角全景切换；OpenKey == "GongHui" 时显示，否则隐藏。
using System;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class GongHuiScreenView : MonoBehaviour
    {
        public const string GongHuiNavKey = "GongHui";
        public const string PrefabResourcePath = "Prefabs/Farm/GongHuiScreenPanel";
        public const string ResGongHuiBackground = GongHuiBackgroundBuilder.ResGongHuiBackgroundLegacy;
        /// <summary>公会世界内容缩放（镜头拉近）；与预制体 GongHuiWorldContent.localScale 一致。</summary>
        public static readonly Vector3 WorldContentLocalScale = new Vector3(1.7f, 1.7f, 1f);

        private static readonly Vector2 MinWorldSize = new Vector2(1620f, 2880f);

        [SerializeField] private RectTransform viewportRt;
        [SerializeField] private RectTransform worldContentRt;
        [SerializeField] private RectTransform playerSpawnRt;
        [SerializeField] private RectTransform obstaclesRootRt;
        [SerializeField] private RectTransform buildingsRootRt;
        [SerializeField] private RectTransform npcsRootRt;
        [SerializeField] private RectTransform responseAreasRootRt;
        [SerializeField] private VirtualJoystickView joystick;
        [SerializeField] private Button panoramaButton;

        private BottomNavBarView bottomNav;
        private TopDingBarView topDingBar;
        private JiaYuanViewportFollowController followController;
        private GuildPlayerController playerController;
        private GuildProximityController proximityController;
        private GuildPanoramaController panoramaController;
        private GuildNpcFollowController npcFollowController;
        private RectTransform playerRt;
        private bool sceneSpawned;

        private void Awake()
        {
            EnsureTiledBackground();
            if (panoramaButton == null)
                BuildPanoramaButton((RectTransform)transform, this);
        }

        /// <summary>v3.133：预制体若仍挂旧单图 Background，Awake 时重拼切块。</summary>
        private void EnsureTiledBackground()
        {
            if (worldContentRt == null)
                return;

            var bgRt = worldContentRt.Find("Background") as RectTransform;
            if (bgRt == null)
                return;

            Vector2 worldSize;
            if (GongHuiBackgroundBuilder.TryBuild(bgRt, out worldSize)
                || GongHuiBackgroundBuilder.TryBuildSingleSpriteFallback(bgRt, out worldSize))
            {
                worldContentRt.sizeDelta = worldSize;
                return;
            }

            worldContentRt.sizeDelta = Vector2.Max(bgRt.sizeDelta, MinWorldSize);
        }

        /// <summary>SPEC §9.8.9.4：预制体优先 + 运行时回退；订阅底栏互斥显隐。</summary>
        public static GongHuiScreenView BuildInto(RectTransform canvasRect, BottomNavBarView barView)
        {
            if (canvasRect == null || barView == null)
                return null;

            GongHuiScreenView view = null;
            var prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab != null)
            {
                var go = Instantiate(prefab, canvasRect);
                go.name = "GongHuiScreen";
                view = go.GetComponent<GongHuiScreenView>();
                if (view == null)
                {
                    UnityEngine.Debug.LogWarning(
                        "[GongHuiScreenView] 预制体缺少 GongHuiScreenView 组件，改用运行时回退。");
                    Destroy(go);
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning(
                    "[GongHuiScreenView] 缺少预制体 Resources/" + PrefabResourcePath + "，使用运行时回退构建。");
            }

            if (view == null)
                view = BuildRuntimeFallback(canvasRect);

            var rootRt = (RectTransform)view.transform;
            BottomNavAttachedScreenLayout.StretchFull(rootRt);
            var barRt = barView.transform as RectTransform;
            if (barRt != null)
                rootRt.SetSiblingIndex(barRt.GetSiblingIndex());

            view.gameObject.SetActive(false);
            view.bottomNav = barView;
            barView.OnOpenChanged += view.OnBottomNavOpenChanged;
            view.OnBottomNavOpenChanged(barView.OpenIndex, barView.OpenKey);
            return view;
        }

        /// <summary>预制体生成器与运行时回退共用的场景引用装配。</summary>
        public void SetSceneRefs(
            RectTransform viewport,
            RectTransform worldContent,
            RectTransform playerSpawn,
            RectTransform obstaclesRoot,
            RectTransform buildingsRoot,
            RectTransform npcsRoot,
            VirtualJoystickView joystickView,
            RectTransform responseAreasRoot = null)
        {
            viewportRt = viewport;
            worldContentRt = worldContent;
            playerSpawnRt = playerSpawn;
            obstaclesRootRt = obstaclesRoot;
            buildingsRootRt = buildingsRoot;
            npcsRootRt = npcsRoot;
            responseAreasRootRt = responseAreasRoot;
            joystick = joystickView;
        }

        /// <summary>SPEC §9.8.15.1：注入 TopDingBar，在 EnsureSceneSpawned 后订阅跟随头像事件。</summary>
        public void BindTopDingBar(TopDingBarView view)
        {
            topDingBar = view;
            TryWireTopDingBar();
        }

        private void TryWireTopDingBar()
        {
            if (topDingBar == null || npcFollowController == null)
                return;
            topDingBar.BindGuildFollowController(npcFollowController);
        }

        private void OnBottomNavOpenChanged(int index, string key)
        {
            bool show = !string.IsNullOrEmpty(key) &&
                        string.Equals(key, GongHuiNavKey, StringComparison.Ordinal);

            if (show)
            {
                gameObject.SetActive(true);
                EnsureSceneSpawned();
                if (followController != null)
                    followController.SetFollowEnabled(true);
            }
            else
            {
                panoramaController?.ExitPanoramaIfActive();
                if (followController != null)
                    followController.SetFollowEnabled(false);
                gameObject.SetActive(false);
            }
        }

        // 首次显示时懒生成主角/NPC Spine 与各控制器（SPEC §9.8.9.4）。
        private void EnsureSceneSpawned()
        {
            if (sceneSpawned)
                return;
            if (viewportRt == null || worldContentRt == null)
            {
                UnityEngine.Debug.LogWarning("[GongHuiScreenView] 场景引用缺失，无法生成公会场景内容。");
                return;
            }
            sceneSpawned = true;

            followController = viewportRt.gameObject.AddComponent<JiaYuanViewportFollowController>();
            followController.Initialize(viewportRt, worldContentRt);

            // 主角：村民 Spine，出生在 PlayerSpawn（缺失则世界中心）。
            var spawnPos = playerSpawnRt != null
                ? GuildSceneGeometry.PointInContentSpace(playerSpawnRt, worldContentRt)
                : Vector2.zero;
            playerRt = GuildSpineCharacterBuilder.BuildVillager(
                worldContentRt, "GuildPlayer", spawnPos, out var playerSkeleton,
                GuildSpineCharacterBuilder.GuildPlayerLocalScale);

            var obstacles = obstaclesRootRt != null
                ? obstaclesRootRt.GetComponentsInChildren<GuildObstacleArea>(true)
                : Array.Empty<GuildObstacleArea>();
            playerController = gameObject.AddComponent<GuildPlayerController>();
            playerController.Initialize(playerRt, worldContentRt, joystick, playerSkeleton, obstacles);

            followController.SetFollowTarget(playerRt);
            playerController.BindViewportFollowSync(followController.ApplyPlayerContentDelta);

            // NPC：每个标记点位生成对应骨骼 Spine；互动按钮接跟随控制器（SPEC §9.8.9 v3.124 / §9.8.9.9 v3.156）。
            npcFollowController = gameObject.AddComponent<GuildNpcFollowController>();
            npcFollowController.Initialize(playerRt, worldContentRt);

            var work2Controller = gameObject.AddComponent<GuildNpcWork2InteractionController>();
            work2Controller.Initialize(playerController, npcFollowController);

            var npcs = npcsRootRt != null
                ? npcsRootRt.GetComponentsInChildren<GuildNpcMarker>(true)
                : Array.Empty<GuildNpcMarker>();
            for (int i = 0; i < npcs.Length; i++)
            {
                if (npcs[i] == null)
                    continue;
                var npcSpineRt = GuildSpineCharacterBuilder.BuildCharacter(
                    npcs[i].Rt, npcs[i].SkeletonKind, "NpcSpine", Vector2.zero, out var npcSkeleton);
                GuildSpineCharacterBuilder.PlayLoop(
                    npcSkeleton, "standby_1", "animation", "idle", "exclusive_2");
                npcs[i].AttachSpine(npcSpineRt, npcSkeleton);
                npcs[i].OnInteract = npcFollowController.StartFollow;
                if (npcs[i].ShowActionIcon)
                    npcs[i].OnActionIconClick = work2Controller.TryPlayWork2WithNpc;
            }

            var buildings = buildingsRootRt != null
                ? buildingsRootRt.GetComponentsInChildren<GuildBuildingMarker>(true)
                : Array.Empty<GuildBuildingMarker>();

            var responseAreas = responseAreasRootRt != null
                ? responseAreasRootRt.GetComponentsInChildren<GuildResponseAreaMarker>(true)
                : Array.Empty<GuildResponseAreaMarker>();
            for (int i = 0; i < responseAreas.Length; i++)
            {
                if (responseAreas[i] == null)
                    continue;
                responseAreas[i].Entered += HandleResponseAreaEntered;
            }

            proximityController = gameObject.AddComponent<GuildProximityController>();
            proximityController.Initialize(playerRt, worldContentRt, buildings, npcs, responseAreas);

            panoramaController = gameObject.AddComponent<GuildPanoramaController>();
            panoramaController.Initialize(
                viewportRt, worldContentRt, followController, joystick,
                proximityController, playerController);
            WirePanoramaButton();

            TryWireTopDingBar();
        }

        private void WirePanoramaButton()
        {
            if (panoramaButton == null)
                panoramaButton = transform.Find("PanoramaButtonLayer/PanoramaButton")
                    ?.GetComponent<Button>();
            if (panoramaButton == null || panoramaController == null)
                return;

            panoramaButton.onClick.RemoveListener(OnPanoramaButtonClicked);
            panoramaButton.onClick.AddListener(OnPanoramaButtonClicked);
        }

        private void OnPanoramaButtonClicked()
        {
            panoramaController?.TogglePanorama();
        }

        // SPEC §9.8.9.11：响应区域进入占位跳转；后续按 navTargetKey 对接底栏/全屏面板。
        private void HandleResponseAreaEntered(GuildResponseAreaMarker marker)
        {
            if (marker == null)
                return;
            UnityEngine.Debug.Log(
                "[GongHuiScreen] 响应区域进入（占位跳转）: id=" + marker.AreaId
                + ", name=" + marker.DisplayName
                + ", navKey=" + marker.NavTargetKey);
        }

        // SPEC §9.8.9.4：缺预制体时以代码搭建等价骨架（空 Obstacles/Buildings/Npcs），仅保 Play 不空跑。
        private static GongHuiScreenView BuildRuntimeFallback(RectTransform canvasRect)
        {
            var root = BottomNavAttachedScreenLayout.CreateChildRect(
                canvasRect, "GongHuiScreen",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(root);

            var view = root.gameObject.AddComponent<GongHuiScreenView>();
            BuildSceneSkeleton(root, view);
            return view;
        }

        /// <summary>
        /// 搭建场景骨架（Viewport/WorldContent/Background/三类根节点/PlayerSpawn/摇杆）并写入引用；
        /// 供运行时回退使用（预制体生成器在编辑器侧另行构建以挂示例标记）。
        /// </summary>
        public static void BuildSceneSkeleton(RectTransform root, GongHuiScreenView view)
        {
            var viewport = BottomNavAttachedScreenLayout.CreateChildRect(
                root, "GongHuiViewport",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            var worldContent = BottomNavAttachedScreenLayout.CreateChildRect(
                viewport, "GongHuiWorldContent",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

            var bgRt = BottomNavAttachedScreenLayout.CreateChildRect(
                worldContent, "Background",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

            Vector2 worldSize;
            if (!GongHuiBackgroundBuilder.TryBuild(bgRt, out worldSize)
                && !GongHuiBackgroundBuilder.TryBuildSingleSpriteFallback(bgRt, out worldSize))
            {
                worldSize = MinWorldSize;
                var bgImage = bgRt.gameObject.AddComponent<Image>();
                bgImage.color = new Color(0.10f, 0.12f, 0.18f, 1f);
                bgImage.raycastTarget = false;
                bgRt.sizeDelta = worldSize;
                UnityEngine.Debug.LogWarning(
                    "[GongHuiScreenView] 缺少公会背景切块（" + GongHuiBackgroundBuilder.TileResourcePrefix +
                    "_r*c*）及单图回退，已使用纯色回退。");
            }

            worldContent.sizeDelta = worldSize;
            worldContent.localScale = WorldContentLocalScale;

            var obstaclesRoot = CreateStretchedGroup(worldContent, "Obstacles");
            var buildingsRoot = CreateStretchedGroup(worldContent, "Buildings");
            var npcsRoot = CreateStretchedGroup(worldContent, "Npcs");
            var responseAreasRoot = CreateStretchedGroup(worldContent, "ResponseAreas");

            var playerSpawn = BottomNavAttachedScreenLayout.CreateChildRect(
                worldContent, "PlayerSpawn",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -200f), new Vector2(10f, 10f));

            var joystickView = VirtualJoystickView.BuildJoystickInto(root);

            view.SetSceneRefs(
                viewport, worldContent, playerSpawn,
                obstaclesRoot, buildingsRoot, npcsRoot, joystickView, responseAreasRoot);

            BuildPanoramaButton(root, view);
        }

        /// <summary>SPEC §9.8.9.12：右下角「全景」切换按钮（图片 ShouHuo_2）；预制体生成器与运行时回退共用。</summary>
        public const string ResPanoramaButtonSprite = "AirUI/ShouHuo_2";

        public static void BuildPanoramaButton(RectTransform screenRoot, GongHuiScreenView view)
        {
            if (screenRoot == null || view == null)
                return;

            var existing = screenRoot.Find("PanoramaButtonLayer/PanoramaButton");
            if (existing != null)
            {
                view.panoramaButton = existing.GetComponent<Button>();
                return;
            }

            var layerRt = BottomNavAttachedScreenLayout.CreateChildRect(
                screenRoot, "PanoramaButtonLayer",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(layerRt);
            layerRt.SetAsLastSibling();

            // y 需抬到底部导航栏（高 160）之上，否则会被底栏遮挡（SPEC §9.8.9.12）。
            var buttonRt = BottomNavAttachedScreenLayout.CreateChildRect(
                layerRt, "PanoramaButton",
                new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-24f, 184f), new Vector2(160f, 160f));
            buttonRt.pivot = new Vector2(1f, 0f);

            var bg = buttonRt.gameObject.AddComponent<Image>();
            bg.raycastTarget = true;
            var sprite = Resources.Load<Sprite>(ResPanoramaButtonSprite);
            if (sprite != null)
            {
                bg.sprite = sprite;
                bg.preserveAspect = true;
                bg.color = Color.white;
            }
            else
            {
                bg.color = new Color(0.05f, 0.08f, 0.14f, 0.78f);
                UnityEngine.Debug.LogWarning(
                    "[GongHuiScreenView] 缺少全景按钮图标 Resources/" + ResPanoramaButtonSprite + "。");
            }

            var btn = buttonRt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = bg;
            view.panoramaButton = btn;
        }

        public static RectTransform CreateStretchedGroup(RectTransform parent, string name)
        {
            var rt = BottomNavAttachedScreenLayout.CreateChildRect(
                parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(rt);
            return rt;
        }

        private void OnDestroy()
        {
            if (bottomNav != null)
                bottomNav.OnOpenChanged -= OnBottomNavOpenChanged;
        }
    }
}
