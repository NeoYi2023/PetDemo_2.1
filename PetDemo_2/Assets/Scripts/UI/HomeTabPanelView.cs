// SPEC §9.14.11：家园页签面板（预制体 HomeTabPanel）。
using System;
using System.Collections;
using System.Collections.Generic;
using PetDemo.Core;
using PetDemo.Farm;
using PetDemo.UI.Farm;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class HomeTabPanelView : MonoBehaviour
    {
        public const string ResPrefabPath = "Prefabs/Farm/HomeTabPanel";
        public const string PanelObjectName = "HomeTabPanel";

        private const string RoleResourcesPrefabPath = "Prefabs/Air/Hero_Role_cunmin";
        private const string SkeletonGraphicShaderName = "Spine/SkeletonGraphic";
        private const float RoleSpineDisplayScale = 0.75f;
        private static readonly Vector2 RoleDisplaySize = new Vector2(720f, 1000f);
        private static readonly string[] RoleIdleAnimationFallbacks =
        {
            "exclusive_2", "standby_1", "animation", "idle",
        };
        private static readonly string[] DeathAnimationFallbacks = { "death", "Dead" };
        private const string Work2ClipName = "work_2";
        private const string StandbyClipName = "standby_1";
        private const string OpeningRescueBubbleText = "好饿哦~~~好饿哦~~~";

        private static readonly Color TabActiveColor = new Color(0.26f, 0.55f, 0.85f, 1f);
        private static readonly Color TabNormalColor = new Color(0.3f, 0.28f, 0.24f, 1f);

        private static HomeTabPanelView instance;

        [SerializeField] private RectTransform roleMount;
        [SerializeField] private Text levelText;
        [SerializeField] private RectTransform expFillRect;
        [SerializeField] private Image expFillImage;
        [SerializeField] private Text expText;
        [SerializeField] private Button hexAttrsTab;
        [SerializeField] private Button currentTab;
        [SerializeField] private GameObject hexAttrsPage;
        [SerializeField] private GameObject currentPlaceholderPage;
        [SerializeField] private Image[] attrIcons = new Image[HomeTabPanelLayout.GrowthAttrCount];
        [SerializeField] private Text[] attrValues = new Text[HomeTabPanelLayout.GrowthAttrCount];
        [SerializeField] private Text[] currentPlaceholderAttrValues = new Text[HomeTabPanelLayout.GrowthAttrCount];
        [SerializeField] private RectTransform speechBubble;
        [SerializeField] private Text speechBubbleText;
        [SerializeField] private Button speechBubbleButton;
        [SerializeField] private Button rankingButton;
        [SerializeField] private Button dailyTaskButton;
        [SerializeField] private Button screenCloseButton;
        [SerializeField] private RectTransform staminaBarSlot;
        [SerializeField] private StaminaBarView staminaBar;
        [SerializeField] private Button addExpButton;

        private RectTransform panelRt;
        private IPlantingService service;
        private SkeletonGraphic roleSkeletonGraphic;
        private bool wired;
        private bool roleBuilt;
        private int activeInfoTabIndex;
        private float expTrackWidth;

        private List<HomeTabBubbleConfig> bubbleQueue;
        private int bubbleQueueIndex = -1;
        private int bubbleAnimRemaining;
        private TrackEntry bubbleAnimTrack;
        private Button roleClickButton;
        private Coroutine rescueRoutine;
        private bool rescueAnimPlaying;

        /// <summary>SPEC §9.14.11 v3.190：每日任务按钮请求打开创角加好感 / ZhuanQianPopup。</summary>
        public event Action OnDailyTaskRequested;

        /// <summary>SPEC §9.14.11 v3.193：关闭按钮请求离开创角回 APP PageHome。</summary>
        public event Action OnCloseRequested;

        public bool IsShown => gameObject != null && gameObject.activeSelf;

        public static HomeTabPanelView GetOrCreate(RectTransform parentRect)
        {
            if (parentRect == null)
            {
                UnityEngine.Debug.LogWarning("[HomeTabPanelView] GetOrCreate: parentRect 为空");
                return null;
            }

            if (instance != null && instance.panelRt != null)
                return instance;

            var existing = parentRect.Find(PanelObjectName);
            if (existing != null)
            {
                var existView = existing.GetComponent<HomeTabPanelView>();
                if (existView == null)
                    existView = existing.gameObject.AddComponent<HomeTabPanelView>();
                existView.panelRt = existing as RectTransform;
                instance = existView;
                return existView;
            }

            var prefab = Resources.Load<GameObject>(ResPrefabPath);
            GameObject go;
            if (prefab != null)
                go = Instantiate(prefab, parentRect, false);
            else
            {
                UnityEngine.Debug.LogWarning(
                    "[HomeTabPanelView] 缺少预制体 Resources/" + ResPrefabPath +
                    "，使用运行时回退 UI；请在编辑器执行 Tools/PetDemo/Generate Home Tab Panel Prefab。");
                go = HomeTabPanelLayout.BuildRuntime(parentRect);
            }

            go.name = PanelObjectName;
            var rt = go.GetComponent<RectTransform>();
            if (rt == null)
                rt = go.AddComponent<RectTransform>();

            var view = go.GetComponent<HomeTabPanelView>();
            if (view == null)
                view = go.AddComponent<HomeTabPanelView>();
            view.panelRt = rt;
            instance = view;
            return view;
        }

        public void Bind(IPlantingService plantingService)
        {
            if (service != null)
            {
                service.OnRoleStatsChanged -= OnRoleStatsChanged;
                service.OnStaminaChanged -= OnStaminaChanged;
            }
            service = plantingService;
            if (service != null)
            {
                service.OnRoleStatsChanged += OnRoleStatsChanged;
                service.OnStaminaChanged += OnStaminaChanged;
            }
        }

        public void Show()
        {
            EnsureFieldsFromHierarchy();
            WireOnce();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            EnsureRoleSpine();
            RefreshAll();
            SelectInfoTab(0);
            BeginSpeechBubbleQueue();
        }

        public void Hide()
        {
            ClearSpeechBubbleState(restoreIdle: true);
            gameObject.SetActive(false);
        }

        public void RefreshAll()
        {
            RefreshLevelExp();
            RefreshHexAttrs();
            EnsureStaminaBar();
        }

        private void OnDestroy()
        {
            if (rescueRoutine != null)
            {
                StopCoroutine(rescueRoutine);
                rescueRoutine = null;
            }
            rescueAnimPlaying = false;
            ClearSpeechBubbleState(restoreIdle: false);
            if (service != null)
            {
                service.OnRoleStatsChanged -= OnRoleStatsChanged;
                service.OnStaminaChanged -= OnStaminaChanged;
            }
            if (instance == this)
                instance = null;
        }

        private void OnRoleStatsChanged()
        {
            if (!IsShown)
                return;
            RefreshAll();
        }

        private void OnStaminaChanged(int newValue, int max)
        {
            if (!IsShown)
                return;
            if (staminaBar != null)
                staminaBar.Refresh();
            else
                EnsureStaminaBar();
        }

        private void WireOnce()
        {
            if (wired)
                return;
            wired = true;

            if (hexAttrsTab != null)
            {
                hexAttrsTab.onClick.RemoveAllListeners();
                hexAttrsTab.onClick.AddListener(() => SelectInfoTab(0));
            }
            if (currentTab != null)
            {
                currentTab.onClick.RemoveAllListeners();
                currentTab.onClick.AddListener(() => SelectInfoTab(1));
            }

            if (speechBubbleButton != null)
            {
                speechBubbleButton.onClick.RemoveAllListeners();
                speechBubbleButton.onClick.AddListener(OnSpeechBubbleClicked);
            }

            // 排行榜：本期仅 ColorTint 可点反馈，不打开界面。
            if (rankingButton != null)
                rankingButton.onClick.RemoveAllListeners();

            if (dailyTaskButton != null)
            {
                dailyTaskButton.onClick.RemoveAllListeners();
                dailyTaskButton.onClick.AddListener(OnDailyTaskClicked);
            }

            if (screenCloseButton != null)
            {
                screenCloseButton.onClick.RemoveAllListeners();
                screenCloseButton.onClick.AddListener(OnScreenCloseClicked);
            }

            EnsureAddExpButtonRef();
            if (addExpButton != null)
            {
                addExpButton.onClick.RemoveAllListeners();
                addExpButton.onClick.AddListener(OnAddExpClicked);
            }
        }

        private void OnDailyTaskClicked()
        {
            OnDailyTaskRequested?.Invoke();
        }

        private void OnScreenCloseClicked()
        {
            OnCloseRequested?.Invoke();
        }

        /// <summary>SPEC §9.14.11 v3.208：增加本级单级需求 40% 经验（最小 1），升级则弹出 RoleLevelUpPanel。</summary>
        private void OnAddExpClicked()
        {
            if (service == null)
                return;

            var role = service.GetRoleStats();
            int expToNext = role != null && role.expToNextLevel > 0 ? role.expToNextLevel : 100;
            int grant = Mathf.Max(1, Mathf.FloorToInt(expToNext * 0.4f));

            if (!service.TryAddRoleExp(grant, out var leveled) || leveled == null || leveled.Count == 0)
                return;

            // 挂到创角根（与 HomeTabPanel 同级），保证 SetAsLastSibling 盖过 BottomTabBar。
            var parent = panelRt != null && panelRt.parent is RectTransform pr
                ? pr
                : transform.parent as RectTransform;
            if (parent == null)
                parent = panelRt;

            var levelUp = RoleLevelUpPanelView.GetOrCreate(parent);
            if (levelUp != null)
            {
                levelUp.transform.SetAsLastSibling();
                levelUp.EnqueueLevels(service.GetRoleStats(), leveled);
            }
        }

        private void SelectInfoTab(int index)
        {
            activeInfoTabIndex = index;
            SetActiveSafe(hexAttrsPage, index == 0);
            SetActiveSafe(currentPlaceholderPage, index == 1);
            SetTabVisual(hexAttrsTab, index == 0);
            SetTabVisual(currentTab, index == 1);
        }

        private static void SetTabVisual(Button button, bool active)
        {
            if (button == null)
                return;
            var img = button.GetComponent<Image>();
            if (img != null)
                img.color = active ? TabActiveColor : TabNormalColor;
        }

        private void RefreshLevelExp()
        {
            var role = service != null ? service.GetRoleStats() : null;
            int level = role != null ? Mathf.Max(1, role.level) : 1;
            int currentExp = role != null ? Mathf.Max(0, role.currentExp) : 0;
            int expToNext = role != null && role.expToNextLevel > 0 ? role.expToNextLevel : 100;

            if (levelText != null)
                levelText.text = level.ToString();
            if (expText != null)
                expText.text = $"{currentExp}/{expToNext}";

            EnsureExpTrackWidth();
            if (expFillRect != null)
            {
                float ratio = expToNext <= 0 ? 0f : Mathf.Clamp01((float)currentExp / expToNext);
                float width = expTrackWidth * ratio;
                var sd = expFillRect.sizeDelta;
                sd.x = width;
                expFillRect.sizeDelta = sd;
            }
            if (expFillImage != null)
                expFillImage.gameObject.SetActive(currentExp > 0);
        }

        private void EnsureExpTrackWidth()
        {
            if (expFillRect == null)
                return;
            // SPEC §9.14.11 v3.209：有有效轨道宽时每次刷新，避免布局未完成时永久缓存回退值。
            var parent = expFillRect.parent as RectTransform;
            if (parent != null && parent.rect.width > 0f)
                expTrackWidth = parent.rect.width;
            else if (expTrackWidth <= 0f)
                expTrackWidth = 800f;
        }

        private void RefreshHexAttrs()
        {
            var role = service != null ? service.GetRoleStats() : null;
            var values = GetGrowthAttrValues(role);
            EnsureAttrIconsLoaded();

            if (attrValues == null)
                return;
            for (int i = 0; i < attrValues.Length && i < values.Length; i++)
            {
                string valueText = values[i].ToString();
                if (attrValues[i] != null)
                    attrValues[i].text = valueText;
                if (currentPlaceholderAttrValues != null
                    && i < currentPlaceholderAttrValues.Length
                    && currentPlaceholderAttrValues[i] != null)
                    currentPlaceholderAttrValues[i].text = valueText;
            }
        }

        private static int[] GetGrowthAttrValues(RoleStats role)
        {
            var values = new int[HomeTabPanelLayout.GrowthAttrCount];
            if (role == null)
                return values;
            values[0] = role.intelligence;
            values[1] = role.memory;
            values[2] = role.imagination;
            values[3] = role.physique;
            values[4] = role.charm;
            values[5] = role.emotionalIntelligence;
            return values;
        }

        private void EnsureAttrIconsLoaded()
        {
            if (attrIcons == null)
                return;
            for (int i = 0; i < attrIcons.Length; i++)
            {
                if (attrIcons[i] == null)
                    continue;
                if (attrIcons[i].sprite != null)
                    continue;
                if (i >= RoleLevelConfigCatalog.GrowthAttrIconPaths.Length)
                    continue;
                var sprite = Resources.Load<Sprite>(RoleLevelConfigCatalog.GrowthAttrIconPaths[i]);
                if (sprite != null)
                {
                    attrIcons[i].sprite = sprite;
                    attrIcons[i].color = Color.white;
                    attrIcons[i].preserveAspect = true;
                }
            }
        }

        private void EnsureRoleSpine()
        {
            if (roleMount == null)
                return;

            if (!roleBuilt)
            {
                for (int i = roleMount.childCount - 1; i >= 0; i--)
                    Destroy(roleMount.GetChild(i).gameObject);

                if (!TryBuildRoleSkeletonGraphic(roleMount))
                    BuildRolePlaceholder(roleMount);

                roleBuilt = true;
            }

            RefreshRoleAnimForRescueState();
            EnsureRoleClickHitbox();
        }

        private bool TryBuildRoleSkeletonGraphic(RectTransform mount)
        {
            var dataAsset = ResolveRoleSkeletonDataAsset();
            if (dataAsset == null)
                return false;

            var shader = Shader.Find(SkeletonGraphicShaderName);
            if (shader == null)
                return false;

            var uiMaterial = SkeletonGraphicUiMaterialFactory.CreateForPmaVertexColors(shader);
            var roleGo = new GameObject("RoleSpine", typeof(RectTransform));
            var rt = roleGo.GetComponent<RectTransform>();
            rt.SetParent(mount, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = RoleDisplaySize;
            rt.localScale = new Vector3(RoleSpineDisplayScale, RoleSpineDisplayScale, 1f);

            var sg = SkeletonGraphic.AddSkeletonGraphicComponent(roleGo, dataAsset, uiMaterial);
            if (sg == null || !sg.IsValid)
            {
                Destroy(roleGo);
                return false;
            }

            sg.raycastTarget = false;
            roleSkeletonGraphic = sg;
            return true;
        }

        private SkeletonDataAsset cachedRoleDataAsset;
        private bool roleDataAssetResolved;

        private SkeletonDataAsset ResolveRoleSkeletonDataAsset()
        {
            if (roleDataAssetResolved)
                return cachedRoleDataAsset;
            roleDataAssetResolved = true;

            var prefab = ResolveRolePrefab();
            if (prefab == null)
                return null;

            var probe = Instantiate(prefab);
            probe.SetActive(false);
            var srcAnim = probe.GetComponent<SkeletonAnimation>()
                ?? probe.GetComponentInChildren<SkeletonAnimation>(true);
            cachedRoleDataAsset = srcAnim != null ? srcAnim.skeletonDataAsset : null;
            Destroy(probe);
            return cachedRoleDataAsset;
        }

        private static void PlaySpineIdleLoop(SkeletonGraphic skeletonGraphic)
        {
            if (skeletonGraphic == null)
                return;

            var idleClip = ResolveRoleIdleClip(skeletonGraphic);
            if (string.IsNullOrEmpty(idleClip))
            {
                UnityEngine.Debug.LogWarning("[HomeTabPanelView] 未找到可播放的待机动画。");
                return;
            }

            try
            {
                skeletonGraphic.AnimationState.SetAnimation(0, idleClip, true);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[HomeTabPanelView] 播放待机动画失败: " + e.Message);
            }
        }

        private bool IsOpeningRescuePending() =>
            service != null && service.IsOpeningRescuePending();

        private void RefreshRoleAnimForRescueState()
        {
            if (roleSkeletonGraphic == null || rescueAnimPlaying)
                return;

            if (IsOpeningRescuePending())
                ApplyDeathLastFrame(roleSkeletonGraphic);
            else
                PlaySpineIdleLoop(roleSkeletonGraphic);
        }

        private static void ApplyDeathLastFrame(SkeletonGraphic skeletonGraphic)
        {
            if (skeletonGraphic == null)
                return;

            var clip = ResolveClipFromFallbacks(skeletonGraphic, DeathAnimationFallbacks);
            if (string.IsNullOrEmpty(clip))
            {
                UnityEngine.Debug.LogWarning("[HomeTabPanelView] 未找到 death 动画，回退待机。");
                PlaySpineIdleLoop(skeletonGraphic);
                return;
            }

            try
            {
                var entry = skeletonGraphic.AnimationState.SetAnimation(0, clip, false);
                if (entry == null)
                    return;

                entry.TrackTime = entry.AnimationEnd;
                skeletonGraphic.Update(0f);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[HomeTabPanelView] 冻结 death 末帧失败: " + e.Message);
            }
        }

        private static string ResolveClipFromFallbacks(SkeletonGraphic skeletonGraphic, string[] fallbacks)
        {
            if (skeletonGraphic?.Skeleton?.Data == null || fallbacks == null)
                return null;

            for (int i = 0; i < fallbacks.Length; i++)
            {
                var clip = ResolveNamedClip(skeletonGraphic, fallbacks[i]);
                if (!string.IsNullOrEmpty(clip))
                    return clip;
            }

            return null;
        }

        private void EnsureRoleClickHitbox()
        {
            if (roleMount == null)
                return;

            var hitT = roleMount.Find("RoleClickHitbox");
            if (hitT == null)
            {
                var hitGo = new GameObject("RoleClickHitbox", typeof(RectTransform), typeof(Image), typeof(Button));
                var hitRt = hitGo.GetComponent<RectTransform>();
                hitRt.SetParent(roleMount, false);
                hitRt.anchorMin = Vector2.zero;
                hitRt.anchorMax = Vector2.one;
                hitRt.offsetMin = Vector2.zero;
                hitRt.offsetMax = Vector2.zero;
                hitRt.SetAsLastSibling();

                var img = hitGo.GetComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0f);
                img.raycastTarget = true;

                roleClickButton = hitGo.GetComponent<Button>();
                roleClickButton.transition = Selectable.Transition.None;
            }
            else
            {
                roleClickButton = hitT.GetComponent<Button>();
            }

            if (roleClickButton == null)
                return;

            roleClickButton.onClick.RemoveAllListeners();
            roleClickButton.onClick.AddListener(OnRoleClickHitboxClicked);
            roleClickButton.interactable = IsOpeningRescuePending();
        }

        private void OnRoleClickHitboxClicked()
        {
            if (!IsOpeningRescuePending() || service == null || rescueAnimPlaying)
                return;

            service.CompleteOpeningRescue();
            if (roleClickButton != null)
                roleClickButton.interactable = false;

            if (rescueRoutine != null)
                StopCoroutine(rescueRoutine);
            rescueRoutine = StartCoroutine(PlayOpeningRescueAnimRoutine());
        }

        private IEnumerator PlayOpeningRescueAnimRoutine()
        {
            rescueAnimPlaying = true;
            DetachBubbleAnimListener();

            if (roleSkeletonGraphic != null)
            {
                var workClip = ResolveNamedClip(roleSkeletonGraphic, Work2ClipName);
                if (!string.IsNullOrEmpty(workClip))
                {
                    TrackEntry workEntry = null;
                    try
                    {
                        workEntry = roleSkeletonGraphic.AnimationState.SetAnimation(0, workClip, false);
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogWarning("[HomeTabPanelView] 播放 work_2 失败: " + e.Message);
                    }

                    if (workEntry != null)
                    {
                        float timeout = 8f;
                        float elapsed = 0f;
                        while (!workEntry.IsComplete && elapsed < timeout)
                        {
                            elapsed += Time.deltaTime;
                            yield return null;
                        }
                    }
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[HomeTabPanelView] 未找到 work_2 动画。");
                }

                var standbyClip = ResolveNamedClip(roleSkeletonGraphic, StandbyClipName);
                if (!string.IsNullOrEmpty(standbyClip))
                {
                    try
                    {
                        roleSkeletonGraphic.AnimationState.SetAnimation(0, standbyClip, true);
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogWarning("[HomeTabPanelView] 播放 standby_1 失败: " + e.Message);
                        PlaySpineIdleLoop(roleSkeletonGraphic);
                    }
                }
                else
                {
                    PlaySpineIdleLoop(roleSkeletonGraphic);
                }
            }

            rescueAnimPlaying = false;
            rescueRoutine = null;
            BeginSpeechBubbleQueue();
        }

        private static string ResolveRoleIdleClip(SkeletonGraphic skeletonGraphic)
        {
            if (skeletonGraphic?.Skeleton?.Data == null)
                return null;

            var data = skeletonGraphic.Skeleton.Data;
            for (int i = 0; i < RoleIdleAnimationFallbacks.Length; i++)
            {
                var name = RoleIdleAnimationFallbacks[i];
                if (!string.IsNullOrEmpty(name) && data.FindAnimation(name) != null)
                    return name;
            }

            var anims = data.Animations;
            if (anims != null && anims.Count > 0 && anims.Items[0] != null)
                return anims.Items[0].Name;
            return null;
        }

        private static GameObject ResolveRolePrefab()
        {
            var fromResources = Resources.Load<GameObject>(RoleResourcesPrefabPath);
            if (fromResources != null)
                return fromResources;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Scenes/Air/Role/Hero_Role_cunmin.prefab");
#else
            return null;
#endif
        }

        private static void BuildRolePlaceholder(RectTransform mount)
        {
            var go = new GameObject("RolePlaceholder", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(mount, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = RoleDisplaySize;
            go.GetComponent<Image>().color = new Color(0.3f, 0.45f, 0.7f, 1f);
        }

        private void EnsureFieldsFromHierarchy()
        {
            if (panelRt == null)
                panelRt = transform as RectTransform;

            if (roleMount == null)
            {
                var t = transform.Find("CharacterZone/RoleMount");
                if (t != null)
                    roleMount = t as RectTransform;
            }

            if (levelText == null)
            {
                var t = transform.Find("LevelExpRow/LevelBadge/LevelText");
                if (t != null)
                    levelText = t.GetComponent<Text>();
            }

            if (expFillRect == null)
            {
                var t = transform.Find("LevelExpRow/ExpBarRoot/ExpFill");
                if (t != null)
                    expFillRect = t as RectTransform;
            }

            if (expFillImage == null && expFillRect != null)
                expFillImage = expFillRect.GetComponent<Image>();

            if (expText == null)
            {
                var t = transform.Find("LevelExpRow/ExpBarRoot/ExpText");
                if (t != null)
                    expText = t.GetComponent<Text>();
            }

            if (hexAttrsTab == null)
            {
                var t = transform.Find("InfoSection/InfoTabBar/HexAttrsTab");
                if (t != null)
                    hexAttrsTab = t.GetComponent<Button>();
            }

            if (currentTab == null)
            {
                var t = transform.Find("InfoSection/InfoTabBar/CurrentTab");
                if (t != null)
                    currentTab = t.GetComponent<Button>();
            }

            if (hexAttrsPage == null)
            {
                var t = transform.Find("InfoSection/InfoContent/HexAttrsPage");
                if (t != null)
                    hexAttrsPage = t.gameObject;
            }

            if (currentPlaceholderPage == null)
            {
                var t = transform.Find("InfoSection/InfoContent/CurrentPlaceholderPage");
                if (t != null)
                    currentPlaceholderPage = t.gameObject;
            }

            EnsureAttrGridRefs();
            EnsureSpeechBubbleRefs();
            EnsureTopRightActionRefs();
            EnsureScreenCloseButtonRef();
            EnsureTopLeftStaminaHudRef();

            if (staminaBarSlot == null)
            {
                var t = transform.Find("TopLeftStaminaHud/StaminaBarSlot");
                if (t != null)
                    staminaBarSlot = t as RectTransform;
            }

            if (staminaBar == null && staminaBarSlot != null)
                staminaBar = staminaBarSlot.GetComponentInChildren<StaminaBarView>(true);
        }

        private void EnsureTopLeftStaminaHudRef()
        {
            if (panelRt == null)
                panelRt = transform as RectTransform;
            if (panelRt == null)
                return;

            if (staminaBarSlot == null)
                staminaBarSlot = HomeTabPanelLayout.BuildTopLeftStaminaHud(panelRt);
            else
            {
                var hud = transform.Find("TopLeftStaminaHud") as RectTransform;
                if (hud != null)
                    HomeTabPanelLayout.EnsureAddExpButton(hud);
            }

            EnsureAddExpButtonRef();
        }

        private void EnsureAddExpButtonRef()
        {
            if (addExpButton != null)
                return;
            var t = transform.Find("TopLeftStaminaHud/AddExpButton");
            if (t != null)
                addExpButton = t.GetComponent<Button>();
        }

        private void EnsureStaminaBar()
        {
            if (service == null)
                return;

            EnsureTopLeftStaminaHudRef();
            if (staminaBarSlot == null)
                return;

            var role = service.GetRoleStats();
            staminaBar = StaminaBarView.GetOrCreateIn(staminaBarSlot, role, service);
        }

        private void EnsureScreenCloseButtonRef()
        {
            if (panelRt == null)
                panelRt = transform as RectTransform;
            if (panelRt == null)
                return;

            var closeRt = HomeTabPanelLayout.BuildScreenCloseButton(panelRt);
            if (screenCloseButton == null && closeRt != null)
                screenCloseButton = closeRt.GetComponent<Button>();
        }

        private void EnsureTopRightActionRefs()
        {
            if (panelRt == null)
                panelRt = transform as RectTransform;

            var actions = panelRt != null ? panelRt.Find("TopRightActions") as RectTransform : null;
            if (actions == null && panelRt != null)
                actions = HomeTabPanelLayout.BuildTopRightActions(panelRt);

            if (rankingButton == null && actions != null)
            {
                var t = actions.Find("RankingButton");
                if (t != null)
                    rankingButton = t.GetComponent<Button>();
            }

            if (dailyTaskButton == null && actions != null)
            {
                var t = actions.Find("DailyTaskButton");
                if (t != null)
                    dailyTaskButton = t.GetComponent<Button>();
            }
        }

        private void EnsureAttrGridRefs()
        {
            if (attrIcons == null || attrIcons.Length != HomeTabPanelLayout.GrowthAttrCount)
                attrIcons = new Image[HomeTabPanelLayout.GrowthAttrCount];
            if (attrValues == null || attrValues.Length != HomeTabPanelLayout.GrowthAttrCount)
                attrValues = new Text[HomeTabPanelLayout.GrowthAttrCount];
            if (currentPlaceholderAttrValues == null
                || currentPlaceholderAttrValues.Length != HomeTabPanelLayout.GrowthAttrCount)
                currentPlaceholderAttrValues = new Text[HomeTabPanelLayout.GrowthAttrCount];

            WireAttrGridValueRefs(hexAttrsPage, attrIcons, attrValues, ensureGrid: true);
            WireAttrGridValueRefs(currentPlaceholderPage, null, currentPlaceholderAttrValues, ensureGrid: false);
        }

        private static void WireAttrGridValueRefs(
            GameObject page,
            Image[] icons,
            Text[] values,
            bool ensureGrid)
        {
            if (page == null)
                return;

            var pageRt = page.transform as RectTransform;
            var grid = pageRt != null ? pageRt.Find("AttrGrid") as RectTransform : null;
            if (grid == null && ensureGrid && pageRt != null)
                grid = HomeTabPanelLayout.BuildAttrGrid(pageRt);
            if (grid == null)
                return;

            for (int i = 0; i < HomeTabPanelLayout.GrowthAttrCount; i++)
            {
                var item = grid.Find("AttrItem_" + i);
                if (item == null)
                    continue;
                if (icons != null && icons[i] == null)
                {
                    var iconT = item.Find("Icon");
                    if (iconT != null)
                        icons[i] = iconT.GetComponent<Image>();
                }
                if (values != null && values[i] == null)
                {
                    var valueT = item.Find("Value");
                    if (valueT != null)
                        values[i] = valueT.GetComponent<Text>();
                }
            }
        }

        private void EnsureSpeechBubbleRefs()
        {
            if (speechBubble == null)
            {
                var t = transform.Find("CharacterZone/SpeechBubble");
                if (t == null)
                {
                    var zone = transform.Find("CharacterZone") as RectTransform;
                    if (zone != null)
                        t = HomeTabPanelLayout.BuildSpeechBubble(zone);
                }

                if (t != null)
                    speechBubble = t as RectTransform;
            }

            if (speechBubble == null)
                return;

            if (speechBubbleText == null)
            {
                var textT = speechBubble.Find("BubbleText");
                if (textT != null)
                    speechBubbleText = textT.GetComponent<Text>();
            }

            if (speechBubbleButton == null)
                speechBubbleButton = speechBubble.GetComponent<Button>();

            if (speechBubbleButton != null)
            {
                speechBubbleButton.onClick.RemoveAllListeners();
                speechBubbleButton.onClick.AddListener(OnSpeechBubbleClicked);
            }
        }

        private void BeginSpeechBubbleQueue()
        {
            ClearSpeechBubbleState(restoreIdle: false);
            if (IsOpeningRescuePending())
            {
                ShowOpeningRescueBubble();
                return;
            }

            bubbleQueue = HomeTabBubbleCatalog.GetEligible();
            bubbleQueueIndex = -1;
            if (bubbleQueue == null || bubbleQueue.Count == 0)
            {
                SetSpeechBubbleVisible(false);
                return;
            }

            ShowSpeechBubbleAt(0);
        }

        private void OnSpeechBubbleClicked()
        {
            if (IsOpeningRescuePending())
                return;

            if (bubbleQueue == null || bubbleQueueIndex < 0)
            {
                SetSpeechBubbleVisible(false);
                return;
            }

            int next = bubbleQueueIndex + 1;
            if (next < bubbleQueue.Count)
            {
                ShowSpeechBubbleAt(next);
                return;
            }

            ClearSpeechBubbleState(restoreIdle: true);
        }

        private void ShowOpeningRescueBubble()
        {
            bubbleQueue = null;
            bubbleQueueIndex = -1;
            if (speechBubbleText != null)
                speechBubbleText.text = OpeningRescueBubbleText;
            SetSpeechBubbleVisible(true);
        }

        private void ShowSpeechBubbleAt(int index)
        {
            if (bubbleQueue == null || index < 0 || index >= bubbleQueue.Count)
            {
                SetSpeechBubbleVisible(false);
                return;
            }

            DetachBubbleAnimListener();
            bubbleQueueIndex = index;
            var cfg = bubbleQueue[index];
            if (speechBubbleText != null)
                speechBubbleText.text = cfg != null ? (cfg.bubbleText ?? string.Empty) : string.Empty;
            SetSpeechBubbleVisible(true);
            PlayBubbleRoleAnim(cfg);
        }

        private void SetSpeechBubbleVisible(bool visible)
        {
            if (speechBubble != null)
                speechBubble.gameObject.SetActive(visible);
        }

        private void ClearSpeechBubbleState(bool restoreIdle)
        {
            DetachBubbleAnimListener();
            bubbleAnimRemaining = 0;
            bubbleQueue = null;
            bubbleQueueIndex = -1;
            SetSpeechBubbleVisible(false);
            if (restoreIdle)
                RefreshRoleAnimForRescueState();
        }

        private void PlayBubbleRoleAnim(HomeTabBubbleConfig cfg)
        {
            if (IsOpeningRescuePending())
                return;

            if (roleSkeletonGraphic == null || cfg == null)
                return;

            if (string.IsNullOrEmpty(cfg.roleAnim))
            {
                PlaySpineIdleLoop(roleSkeletonGraphic);
                return;
            }

            var clip = ResolveNamedClip(roleSkeletonGraphic, cfg.roleAnim);
            if (string.IsNullOrEmpty(clip))
            {
                UnityEngine.Debug.LogWarning(
                    "[HomeTabPanelView] 气泡动作未找到: " + cfg.roleAnim + "（entryId=" + cfg.entryId + "）");
                PlaySpineIdleLoop(roleSkeletonGraphic);
                return;
            }

            try
            {
                if (cfg.animPlayCount <= 0)
                {
                    roleSkeletonGraphic.AnimationState.SetAnimation(0, clip, true);
                    return;
                }

                bubbleAnimRemaining = cfg.animPlayCount;
                var entry = roleSkeletonGraphic.AnimationState.SetAnimation(0, clip, false);
                AttachBubbleAnimListener(entry);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[HomeTabPanelView] 播放气泡动作失败: " + e.Message);
                PlaySpineIdleLoop(roleSkeletonGraphic);
            }
        }

        private void AttachBubbleAnimListener(TrackEntry entry)
        {
            DetachBubbleAnimListener();
            if (entry == null)
            {
                PlaySpineIdleLoop(roleSkeletonGraphic);
                return;
            }

            bubbleAnimTrack = entry;
            bubbleAnimTrack.Complete += OnBubbleAnimTrackComplete;
        }

        private void DetachBubbleAnimListener()
        {
            if (bubbleAnimTrack != null)
            {
                bubbleAnimTrack.Complete -= OnBubbleAnimTrackComplete;
                bubbleAnimTrack = null;
            }
        }

        private void OnBubbleAnimTrackComplete(TrackEntry entry)
        {
            if (entry != null)
                entry.Complete -= OnBubbleAnimTrackComplete;
            if (bubbleAnimTrack == entry)
                bubbleAnimTrack = null;

            bubbleAnimRemaining--;
            if (bubbleAnimRemaining <= 0)
            {
                PlaySpineIdleLoop(roleSkeletonGraphic);
                return;
            }

            if (roleSkeletonGraphic == null || entry == null || string.IsNullOrEmpty(entry.Animation?.Name))
            {
                PlaySpineIdleLoop(roleSkeletonGraphic);
                return;
            }

            try
            {
                var next = roleSkeletonGraphic.AnimationState.SetAnimation(0, entry.Animation.Name, false);
                AttachBubbleAnimListener(next);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[HomeTabPanelView] 续播气泡动作失败: " + e.Message);
                PlaySpineIdleLoop(roleSkeletonGraphic);
            }
        }

        private static string ResolveNamedClip(SkeletonGraphic skeletonGraphic, string clipName)
        {
            if (skeletonGraphic?.Skeleton?.Data == null || string.IsNullOrEmpty(clipName))
                return null;

            var data = skeletonGraphic.Skeleton.Data;
            var exact = data.FindAnimation(clipName);
            if (exact != null)
                return exact.Name;

            var anims = data.Animations;
            if (anims == null)
                return null;

            for (int i = 0; i < anims.Count; i++)
            {
                var anim = anims.Items[i];
                if (anim != null && string.Equals(anim.Name, clipName, StringComparison.OrdinalIgnoreCase))
                    return anim.Name;
            }

            return null;
        }

        private static void SetActiveSafe(GameObject go, bool active)
        {
            if (go != null)
                go.SetActive(active);
        }
    }
}
