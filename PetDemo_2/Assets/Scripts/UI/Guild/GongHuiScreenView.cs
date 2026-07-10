// SPEC §9.8.9 (v3.123；背景分块 v3.133；3×3 v3.176；全景 v3.183；跳转 v3.195；建筑跳转 v3.196；右上玩法按钮 v3.198)：公会场景层
using System;
using System.Collections;
using System.Collections.Generic;
using PetDemo.UI.Friend;
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

        /// <summary>SPEC §9.8.9.11（v3.195）：响应区跳转创角底栏页签。</summary>
        public const string NavIntimacyTab = "IntimacyTab";
        public const string NavDressUpTab = "DressUpButton";
        public const string NavTrainingTab = "RoleAddFavorButton";
        public const string NavHomeTab = "HomeTabButton";
        /// <summary>SPEC §9.8.9.13（v3.196）：建筑跳转主线界面。</summary>
        public const string NavMainStoryLine = "MainStoryLine";
        /// <summary>SPEC §9.8.9.13（v3.196）：建筑跳转好友列表弹窗。</summary>
        public const string NavFriendListPanel = "FriendListPanel";
        /// <summary>SPEC §9.8.9.13（v3.196）：建筑跳转家园世界层。</summary>
        public const string NavJiaYuanWorld = JiaYuanHomeFeatureEntriesView.JiaYuanNavKey;

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
        [SerializeField] private Button wfXuanShangButton;
        [SerializeField] private Button wfZuDuiButton;
        [SerializeField] private Button wfJjcButton;
        [SerializeField] private Button wfZhuangYuanButton;

        private GameObject tipsToast;
        private Text tipsText;
        private Coroutine tipsRoutine;
        private const float TipsDurationSec = 2.2f;

        private BottomNavBarView bottomNav;
        private TopDingBarView topDingBar;
        private CharacterCreationScreenView characterCreationHost;
        private Action onRequestOpenCharacterCreation;
        private FriendListPanelView friendListPanel;
        private JiaYuanViewportFollowController followController;
        private GuildPlayerController playerController;
        private GuildProximityController proximityController;
        private GuildPanoramaController panoramaController;
        private GuildNpcFollowController npcFollowController;
        private RectTransform playerRt;
        private bool sceneSpawned;
        // SPEC §9.14.8（v3.203）：「去小镇寻找」一次性 NPC 就近摆放。
        private bool townSearchNpcBootstrapped;
        private const string TownSearchInteractButtonLabel = "打招呼";
        private static readonly string[] TownSearchNpcNames = { "Npc_1", "Npc_2", "Npc_3" };

        // SPEC §9.14.10（v3.184）：创角界面内嵌公会（保留创角 BottomTabBar）。
        private bool embeddedInCharacterCreation;
        private RectTransform embedOriginalParent;
        private int embedOriginalSiblingIndex;
        private Vector2 embedOriginalAnchorMin;
        private Vector2 embedOriginalAnchorMax;
        private Vector2 embedOriginalOffsetMin;
        private Vector2 embedOriginalOffsetMax;

        public bool IsEmbeddedInCharacterCreation => embeddedInCharacterCreation;

        private void Awake()
        {
            EnsureTiledBackground();
            var screenRoot = (RectTransform)transform;
            if (panoramaButton == null)
                BuildPanoramaButton(screenRoot, this);
            GongHuiScreenLayout.EnsureTopRightWorkflowActions(screenRoot);
            GongHuiScreenLayout.EnsureTipsToast(screenRoot);
            WireTopRightWorkflowButtons();
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

        /// <summary>SPEC §9.8.9.11（v3.195）：注入创角界面，供响应区/建筑跳转。</summary>
        public void BindCharacterCreationHost(CharacterCreationScreenView host)
        {
            characterCreationHost = host;
        }

        /// <summary>SPEC §9.8.9.11（v3.195）：主 HUD 公会模式下，跳转创角页签前先打开创角界面。</summary>
        public void BindOpenCharacterCreationRequest(Action openCharacterCreation)
        {
            onRequestOpenCharacterCreation = openCharacterCreation;
        }

        /// <summary>SPEC §9.8.9.13（v3.196）：注入好友列表弹窗，供 Building_2 跳转。</summary>
        public void BindFriendListPanel(FriendListPanelView panel)
        {
            friendListPanel = panel;
        }

        private void TryWireTopDingBar()
        {
            if (topDingBar == null || npcFollowController == null)
                return;
            topDingBar.BindGuildFollowController(npcFollowController);
        }

        private void OnBottomNavOpenChanged(int index, string key)
        {
            if (embeddedInCharacterCreation)
                return;

            bool show = !string.IsNullOrEmpty(key) &&
                        string.Equals(key, GongHuiNavKey, StringComparison.Ordinal);

            if (show)
                ShowGuildScreen();
            else
                HideGuildScreen();
        }

        /// <summary>SPEC §9.8（v3.207）：EnterHomeHud 下为底栏留出底部 inset。</summary>
        public void ApplyEnterHomeHudBottomInset(float bottomInset)
        {
            var rt = (RectTransform)transform;
            rt.offsetMin = new Vector2(rt.offsetMin.x, bottomInset);
        }

        /// <summary>SPEC §9.8（v3.207）：离开 EnterHomeHud 时清除底栏 inset。</summary>
        public void ClearEnterHomeHudBottomInset()
        {
            var rt = (RectTransform)transform;
            rt.offsetMin = new Vector2(rt.offsetMin.x, 0f);
        }

        /// <summary>SPEC §9.8（v3.207）：EnterHomeHud 强制显示公会（避免 SetOpenKey 同 key 不触发）。</summary>
        public void ForceShowForEnterHomeHud()
        {
            ShowGuildScreen();
        }

        /// <summary>SPEC §9.14.10（v3.184）：在创角界面内嵌展示公会场景（v3.207 主路径已废弃，保留 API）。</summary>
        public void EnterCharacterCreationEmbed(RectTransform mount, float bottomInset)
        {
            if (mount == null)
                return;

            if (embeddedInCharacterCreation)
            {
                ShowGuildScreen();
                return;
            }

            var rt = (RectTransform)transform;
            embedOriginalParent = rt.parent as RectTransform;
            embedOriginalSiblingIndex = rt.GetSiblingIndex();
            embedOriginalAnchorMin = rt.anchorMin;
            embedOriginalAnchorMax = rt.anchorMax;
            embedOriginalOffsetMin = rt.offsetMin;
            embedOriginalOffsetMax = rt.offsetMax;

            rt.SetParent(mount, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            embeddedInCharacterCreation = true;
            ShowGuildScreen();
        }

        /// <summary>SPEC §9.14.10（v3.184）：退出创角内嵌并还原至主 HUD 层级。</summary>
        public void ExitCharacterCreationEmbed()
        {
            if (!embeddedInCharacterCreation)
                return;

            HideGuildScreen();

            var rt = (RectTransform)transform;
            if (embedOriginalParent != null)
            {
                rt.SetParent(embedOriginalParent, false);
                rt.SetSiblingIndex(embedOriginalSiblingIndex);
                rt.anchorMin = embedOriginalAnchorMin;
                rt.anchorMax = embedOriginalAnchorMax;
                rt.offsetMin = embedOriginalOffsetMin;
                rt.offsetMax = embedOriginalOffsetMax;
            }

            embeddedInCharacterCreation = false;
        }

        private void ShowGuildScreen()
        {
            gameObject.SetActive(true);
            EnsureSceneSpawned();
            if (followController != null)
                followController.SetFollowEnabled(true);
        }

        private void HideGuildScreen()
        {
            panoramaController?.ExitPanoramaIfActive();
            if (followController != null)
                followController.SetFollowEnabled(false);
            gameObject.SetActive(false);
        }

        /// <summary>SPEC §9.14.8（v3.203）：「去小镇寻找」— Npc_1~3 移至主角附近并改互动文案。</summary>
        public void ApplyTownSearchNpcBootstrap()
        {
            if (townSearchNpcBootstrapped)
                return;

            EnsureSceneSpawned();
            if (playerRt == null || npcsRootRt == null)
            {
                UnityEngine.Debug.LogWarning("[GongHuiScreenView] 无法执行小镇寻找 NPC 摆放：主角或 Npcs 根节点缺失。");
                return;
            }

            townSearchNpcBootstrapped = true;
            var playerPos = playerRt.anchoredPosition;
            var placed = new List<Vector2>(TownSearchNpcNames.Length);

            for (int i = 0; i < TownSearchNpcNames.Length; i++)
            {
                var npcTr = npcsRootRt.Find(TownSearchNpcNames[i]) as RectTransform;
                if (npcTr == null)
                    continue;

                var pos = SampleNpcPositionNearPlayer(playerPos, placed);
                npcTr.anchoredPosition = pos;
                placed.Add(pos);

                var plateRt = npcTr.Find("NamePlate") as RectTransform;
                if (plateRt != null)
                    GuildSceneUiFactory.SetNpcInteractButtonLabel(plateRt, TownSearchInteractButtonLabel);
            }
        }

        private static Vector2 SampleNpcPositionNearPlayer(
            Vector2 playerPos, List<Vector2> placed,
            float minRadius = 150f, float maxRadius = 350f, float minSeparation = 80f)
        {
            const int maxAttempts = 24;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float radius = UnityEngine.Random.Range(minRadius, maxRadius);
                var candidate = playerPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                bool tooClose = false;
                for (int i = 0; i < placed.Count; i++)
                {
                    if (Vector2.Distance(candidate, placed[i]) < minSeparation)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                    return candidate;
            }

            float fallbackAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float fallbackRadius = UnityEngine.Random.Range(minRadius, maxRadius);
            return playerPos + new Vector2(Mathf.Cos(fallbackAngle), Mathf.Sin(fallbackAngle)) * fallbackRadius;
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
            for (int i = 0; i < buildings.Length; i++)
            {
                if (buildings[i] == null)
                    continue;
                buildings[i].WireActionButton();
                buildings[i].ActionClicked += HandleBuildingActionClicked;
            }

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
            proximityController.Initialize(playerRt, worldContentRt, buildings, npcs, responseAreas, joystick);

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

        // SPEC §9.8.9.14（v3.198）：右上玩法入口按钮。
        private void WireTopRightWorkflowButtons()
        {
            EnsureWorkflowButtonRefs();

            if (wfXuanShangButton != null)
            {
                wfXuanShangButton.onClick.RemoveListener(OnWfXuanShangClicked);
                wfXuanShangButton.onClick.AddListener(OnWfXuanShangClicked);
            }

            if (wfZuDuiButton != null)
            {
                wfZuDuiButton.onClick.RemoveListener(OnWfZuDuiClicked);
                wfZuDuiButton.onClick.AddListener(OnWfZuDuiClicked);
            }

            if (wfJjcButton != null)
            {
                wfJjcButton.onClick.RemoveListener(OnWfJjcClicked);
                wfJjcButton.onClick.AddListener(OnWfJjcClicked);
            }

            if (wfZhuangYuanButton != null)
            {
                wfZhuangYuanButton.onClick.RemoveListener(OnWfZhuangYuanClicked);
                wfZhuangYuanButton.onClick.AddListener(OnWfZhuangYuanClicked);
            }
        }

        private void EnsureWorkflowButtonRefs()
        {
            var actions = transform.Find(
                GongHuiScreenLayout.LayerName + "/" + GongHuiScreenLayout.ActionsName);
            if (wfXuanShangButton == null)
                wfXuanShangButton = actions?.Find(GongHuiScreenLayout.WfXuanShangButtonName)?.GetComponent<Button>();
            if (wfZuDuiButton == null)
                wfZuDuiButton = actions?.Find(GongHuiScreenLayout.WfZuDuiButtonName)?.GetComponent<Button>();
            if (wfJjcButton == null)
                wfJjcButton = actions?.Find(GongHuiScreenLayout.WfJjcButtonName)?.GetComponent<Button>();
            if (wfZhuangYuanButton == null)
                wfZhuangYuanButton = actions?.Find(GongHuiScreenLayout.WfZhuangYuanButtonName)?.GetComponent<Button>();

            if (tipsToast == null)
            {
                var tips = transform.Find("TipsToast");
                if (tips != null)
                    tipsToast = tips.gameObject;
            }

            if (tipsText == null)
                tipsText = transform.Find("TipsToast/TipsText")?.GetComponent<Text>();
        }

        private void OnWfXuanShangClicked() => NavigateByKey(NavMainStoryLine);

        private void OnWfZuDuiClicked() => NavigateByKey(NavFriendListPanel);

        private void OnWfJjcClicked() => ShowTips("敬请期待");

        private void OnWfZhuangYuanClicked() => NavigateByKey(NavJiaYuanWorld);

        private void ShowTips(string message)
        {
            EnsureWorkflowButtonRefs();
            if (tipsToast == null || tipsText == null)
                return;

            tipsText.text = message ?? string.Empty;
            tipsToast.SetActive(true);
            tipsToast.transform.SetAsLastSibling();
            if (tipsRoutine != null)
                StopCoroutine(tipsRoutine);
            tipsRoutine = StartCoroutine(TipsRoutine());
        }

        private IEnumerator TipsRoutine()
        {
            yield return new WaitForSeconds(TipsDurationSec);
            HideTipsImmediate();
        }

        private void HideTipsImmediate()
        {
            if (tipsRoutine != null)
            {
                StopCoroutine(tipsRoutine);
                tipsRoutine = null;
            }

            if (tipsToast != null)
                tipsToast.SetActive(false);
        }

        // SPEC §9.8.9.11（v3.197）：响应区域区域内静止 2s 后按 navTargetKey 跳转。
        private void HandleResponseAreaEntered(GuildResponseAreaMarker marker)
        {
            if (marker == null)
                return;
            NavigateByKey(marker.NavTargetKey);
        }

        private void HandleBuildingActionClicked(GuildBuildingMarker marker)
        {
            if (marker == null)
                return;
            NavigateByKey(marker.NavTargetKey);
        }

        private void NavigateByKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (string.Equals(key, NavIntimacyTab, StringComparison.Ordinal)
                || string.Equals(key, NavDressUpTab, StringComparison.Ordinal)
                || string.Equals(key, NavTrainingTab, StringComparison.Ordinal)
                || string.Equals(key, NavHomeTab, StringComparison.Ordinal))
            {
                NavigateToCharacterCreationTab(key);
                return;
            }

            if (string.Equals(key, NavMainStoryLine, StringComparison.Ordinal))
            {
                OpenMainStoryLineFromGuild();
                return;
            }

            if (string.Equals(key, NavFriendListPanel, StringComparison.Ordinal))
            {
                OpenFriendListFromGuild();
                return;
            }

            if (string.Equals(key, NavJiaYuanWorld, StringComparison.Ordinal))
            {
                OpenJiaYuanWorldFromGuild();
            }
        }

        private void NavigateToCharacterCreationTab(string tabKey)
        {
            if (characterCreationHost == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[GongHuiScreenView] 未注入 CharacterCreationScreenView，无法跳转页签：" + tabKey);
                return;
            }

            if (!characterCreationHost.IsShown)
            {
                onRequestOpenCharacterCreation?.Invoke();
                if (!characterCreationHost.IsShown)
                {
                    UnityEngine.Debug.LogWarning(
                        "[GongHuiScreenView] 创角界面未打开，无法跳转页签：" + tabKey);
                    return;
                }
            }

            characterCreationHost.NavigateFromGuild(tabKey);
        }

        private void OpenMainStoryLineFromGuild()
        {
            SwitchToBottomNav(MainStoryLineScreenView.ZhuXianNavKey);
        }

        private void OpenFriendListFromGuild()
        {
            if (friendListPanel == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[GongHuiScreenView] 未注入 FriendListPanelView，无法打开好友列表。");
                return;
            }

            friendListPanel.Show();
        }

        private void OpenJiaYuanWorldFromGuild()
        {
            SwitchToBottomNav(NavJiaYuanWorld);
        }

        private void SwitchToBottomNav(string navKey)
        {
            if (string.IsNullOrEmpty(navKey))
                return;

            // SPEC §9.8（v3.207）：EnterHomeHud / 主 HUD 均直接切 BottomNavBar（可处于 inactive）。
            if (bottomNav != null)
            {
                if (!bottomNav.gameObject.activeSelf)
                    bottomNav.gameObject.SetActive(true);
                bottomNav.SetOpenKey(navKey);
                return;
            }

            if (characterCreationHost != null)
                characterCreationHost.RequestExitToBottomNav(navKey);
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
            GongHuiScreenLayout.EnsureTopRightWorkflowActions(root);
            GongHuiScreenLayout.EnsureTipsToast(root);
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
            if (embeddedInCharacterCreation)
                embeddedInCharacterCreation = false;
            if (bottomNav != null)
                bottomNav.OnOpenChanged -= OnBottomNavOpenChanged;
        }
    }
}
