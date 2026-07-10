// SPEC §9.14.12：训练页签面板（预制体 TrainingPanel）。
using System;
using System.Collections;
using System.Collections.Generic;
using PetDemo.Core;
using PetDemo.Farm;
using PetDemo.UI.Farm;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class TrainingPanelView : MonoBehaviour
    {
        public const string ResPrefabPath = "Prefabs/Farm/TrainingPanel";
        public const string PanelObjectName = "TrainingPanel";

        private const string RoleResourcesPrefabPath = "Prefabs/Air/Hero_Role_cunmin";
        private const string SkeletonGraphicShaderName = "Spine/SkeletonGraphic";
        private const float RoleSpineDisplayScale = 0.75f;
        private static readonly Vector2 RoleDisplaySize = new Vector2(420f, 580f);
        private static readonly string[] RoleIdleAnimationFallbacks =
        {
            "exclusive_2", "standby_1", "animation", "idle",
        };

        private static readonly Color FilterActiveColor = Color.white;
        private static readonly Color FilterDimColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        private static readonly Color CourseLockedTint = new Color(0.45f, 0.45f, 0.45f, 1f);
        private static readonly Color AttrGainColor = new Color(0.55f, 0.92f, 0.62f, 1f);
        private static readonly Color DurationTextColor = new Color(0.85f, 0.88f, 0.92f, 1f);
        private const float TipsDurationSec = 2.2f;

        private static TrainingPanelView instance;

        [SerializeField] private RectTransform roleMount;
        [SerializeField] private Text idleHintText;
        [SerializeField] private GameObject activeCourseRoot;
        [SerializeField] private Image activeCourseIcon;
        [SerializeField] private Text activeCourseName;
        [SerializeField] private Text countdownText;
        [SerializeField] private Button completeButton;
        [SerializeField] private RectTransform courseContent;
        [SerializeField] private GameObject courseCellTemplate;
        [SerializeField] private GameObject tipsToast;
        [SerializeField] private Text tipsText;

        private readonly Image[] filterIcons = new Image[TrainingPanelLayout.FilterAttrCount];
        private readonly GameObject[] filterBadges = new GameObject[TrainingPanelLayout.FilterAttrCount];
        private readonly Button[] filterButtons = new Button[TrainingPanelLayout.FilterAttrCount];
        private readonly List<CourseCell> courseCells = new List<CourseCell>();

        private RectTransform panelRt;
        private IPlantingService service;
        private SkeletonGraphic roleSkeletonGraphic;
        private bool wired;
        private bool roleBuilt;
        private Coroutine tipsRoutine;
        private string lastCountdownKey = string.Empty;

        public bool IsShown => gameObject != null && gameObject.activeSelf;

        private sealed class CourseCell
        {
            public GameObject root;
            public Button button;
            public Image background;
            public Image icon;
            public Text name;
            public Text duration;
            public Text attrGains;
            public GameObject lockIcon;
            public string courseId;
            public bool unlocked;
            public string unlockTip;
        }

        public static TrainingPanelView GetOrCreate(RectTransform parentRect)
        {
            if (parentRect == null)
            {
                UnityEngine.Debug.LogWarning("[TrainingPanelView] GetOrCreate: parentRect 为空");
                return null;
            }

            if (instance != null && instance.panelRt != null)
                return instance;

            var existing = parentRect.Find(PanelObjectName);
            if (existing != null)
            {
                var existView = existing.GetComponent<TrainingPanelView>();
                if (existView == null)
                    existView = existing.gameObject.AddComponent<TrainingPanelView>();
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
                    "[TrainingPanelView] 缺少预制体 Resources/" + ResPrefabPath +
                    "，使用运行时回退 UI；请在编辑器执行 Tools/PetDemo/Generate Training Panel Prefab。");
                go = TrainingPanelLayout.BuildRuntime(parentRect);
            }

            go.name = PanelObjectName;
            var rt = go.GetComponent<RectTransform>();
            if (rt == null)
                rt = go.AddComponent<RectTransform>();

            var view = go.GetComponent<TrainingPanelView>();
            if (view == null)
                view = go.AddComponent<TrainingPanelView>();
            view.panelRt = rt;
            instance = view;
            return view;
        }

        public void Bind(IPlantingService plantingService)
        {
            if (service != null)
                service.OnRoleStatsChanged -= OnRoleStatsChanged;
            service = plantingService;
            if (service != null)
                service.OnRoleStatsChanged += OnRoleStatsChanged;
        }

        public void Show()
        {
            EnsureFieldsFromHierarchy();
            WireOnce();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            EnsureRoleSpine();
            RebuildCourseCells();
            RefreshAll();
        }

        public void Hide()
        {
            HideTipsImmediate();
            gameObject.SetActive(false);
        }

        public void RefreshAll()
        {
            RefreshFilterVisuals();
            RefreshActiveTrainingSlot();
            RefreshCourseVisibility();
        }

        private void Update()
        {
            if (!IsShown)
                return;
            RefreshActiveTrainingSlot();
        }

        private void OnDestroy()
        {
            HideTipsImmediate();
            if (service != null)
                service.OnRoleStatsChanged -= OnRoleStatsChanged;
            if (instance == this)
                instance = null;
        }

        private void OnRoleStatsChanged()
        {
            // 属性结算后训练 UI 本身不展示六属性数值；保留钩子供扩展。
        }

        private void WireOnce()
        {
            if (wired)
                return;
            wired = true;

            for (int i = 0; i < TrainingPanelLayout.FilterAttrCount; i++)
            {
                int attrIndex = i;
                if (filterButtons[i] != null)
                {
                    filterButtons[i].onClick.RemoveAllListeners();
                    filterButtons[i].onClick.AddListener(() => OnFilterClicked(attrIndex));
                }
            }

            if (completeButton != null)
            {
                completeButton.onClick.RemoveAllListeners();
                completeButton.onClick.AddListener(OnCompleteClicked);
            }
        }

        private void OnFilterClicked(int attrIndex0)
        {
            if (service == null || attrIndex0 < 0 || attrIndex0 >= 6)
                return;
            var ts = service.GetTrainingSession();
            int mask = ts != null ? ts.activeFilterMask : 0;
            int bit = 1 << attrIndex0;
            if ((mask & bit) != 0)
                mask &= ~bit;
            else
                mask |= bit;
            service.SetTrainingFilterMask(mask);
            RefreshAll();
        }

        private void OnCompleteClicked()
        {
            if (service == null)
                return;
            if (!service.TryCompleteTraining(out var gains))
                return;

            PlayGainFlyFx(gains);
            RefreshAll();
        }

        private void OnCourseClicked(CourseCell cell)
        {
            if (cell == null || service == null)
                return;

            if (!cell.unlocked)
            {
                ShowTips(string.IsNullOrEmpty(cell.unlockTip) ? "尚未解锁" : cell.unlockTip);
                return;
            }

            var ts = service.GetTrainingSession();
            if (ts != null && ts.HasActiveCourse)
            {
                ShowTips("已有训练进行中");
                return;
            }

            if (!service.TryStartTraining(cell.courseId))
            {
                ShowTips("无法开始训练");
                return;
            }

            RefreshAll();
        }

        private void RefreshFilterVisuals()
        {
            int mask = 0;
            if (service != null)
            {
                var ts = service.GetTrainingSession();
                if (ts != null)
                    mask = ts.activeFilterMask;
            }

            for (int i = 0; i < TrainingPanelLayout.FilterAttrCount; i++)
            {
                bool selected = (mask & (1 << i)) != 0;
                if (filterIcons[i] != null)
                    filterIcons[i].color = selected ? FilterActiveColor : FilterDimColor;
                if (filterBadges[i] != null)
                    filterBadges[i].SetActive(selected);
            }
        }

        private void RefreshActiveTrainingSlot()
        {
            var ts = service != null ? service.GetTrainingSession() : null;
            bool hasCourse = ts != null && ts.HasActiveCourse;

            if (idleHintText != null)
                idleHintText.gameObject.SetActive(!hasCourse);
            if (activeCourseRoot != null)
                activeCourseRoot.SetActive(hasCourse);

            if (!hasCourse)
            {
                lastCountdownKey = string.Empty;
                return;
            }

            if (!RoleTrainingCourseCatalog.TryGet(ts.courseId, out var course) || course == null)
            {
                if (activeCourseName != null)
                    activeCourseName.text = ts.courseId;
                return;
            }

            if (activeCourseName != null)
                activeCourseName.text = course.name ?? string.Empty;
            if (activeCourseIcon != null)
            {
                var spr = string.IsNullOrEmpty(course.icon) ? null : Resources.Load<Sprite>(course.icon);
                activeCourseIcon.sprite = spr;
                activeCourseIcon.enabled = spr != null;
                activeCourseIcon.preserveAspect = true;
            }

            bool ready = service.IsTrainingReadyToComplete();
            if (completeButton != null)
                completeButton.gameObject.SetActive(ready);
            if (countdownText != null)
            {
                countdownText.gameObject.SetActive(!ready);
                if (!ready)
                {
                    long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    long remainMs = Math.Max(0L, ts.endUnixMs - nowMs);
                    int totalSec = (int)(remainMs / 1000L);
                    string key = totalSec.ToString();
                    if (key != lastCountdownKey)
                    {
                        lastCountdownKey = key;
                        int mm = totalSec / 60;
                        int ss = totalSec % 60;
                        countdownText.text = $"{mm:00}:{ss:00}";
                    }
                }
            }
        }

        private void RebuildCourseCells()
        {
            EnsureFieldsFromHierarchy();
            if (courseContent == null || courseCellTemplate == null)
                return;

            TrainingPanelLayout.EnsureCourseCellAttrGains(courseCellTemplate.transform as RectTransform);

            for (int i = courseCells.Count - 1; i >= 0; i--)
            {
                if (courseCells[i] != null && courseCells[i].root != null)
                    Destroy(courseCells[i].root);
            }
            courseCells.Clear();

            var all = RoleTrainingCourseCatalog.GetAll();
            for (int i = 0; i < all.Count; i++)
            {
                var cfg = all[i];
                if (cfg == null)
                    continue;
                var go = Instantiate(courseCellTemplate, courseContent);
                go.name = "CourseCell_" + cfg.id;
                go.SetActive(true);
                var cell = WireCourseCell(go, cfg);
                courseCells.Add(cell);
            }
        }

        private CourseCell WireCourseCell(GameObject go, RoleTrainingCourseConfig cfg)
        {
            var cell = new CourseCell
            {
                root = go,
                courseId = cfg.id,
                unlocked = cfg.unlockedByDefault,
                unlockTip = cfg.unlockTip ?? string.Empty,
                button = go.GetComponent<Button>(),
                background = go.GetComponent<Image>(),
            };

            var iconT = FindChild(go.transform, "Icon");
            cell.icon = iconT != null ? iconT.GetComponent<Image>() : null;
            var nameT = FindChild(go.transform, "Name");
            cell.name = nameT != null ? nameT.GetComponent<Text>() : null;
            var durT = FindChild(go.transform, "Duration");
            cell.duration = durT != null ? durT.GetComponent<Text>() : null;
            var gainsT = FindChild(go.transform, "AttrGains");
            cell.attrGains = gainsT != null ? gainsT.GetComponent<Text>() : null;
            var lockT = FindChild(go.transform, "LockIcon");
            cell.lockIcon = lockT != null ? lockT.gameObject : null;

            if (cell.name != null)
                cell.name.text = cfg.name ?? string.Empty;
            if (cell.duration != null)
                cell.duration.text = FormatDuration(cfg.durationSec);
            if (cell.attrGains != null)
            {
                cell.attrGains.text = RoleTrainingCourseCatalog.FormatAttrGainsDisplay(cfg.attrGains);
                cell.attrGains.color = AttrGainColor;
            }
            if (cell.icon != null)
            {
                var spr = string.IsNullOrEmpty(cfg.icon) ? null : Resources.Load<Sprite>(cfg.icon);
                cell.icon.sprite = spr;
                cell.icon.enabled = spr != null;
                cell.icon.preserveAspect = true;
            }

            ApplyLockedVisual(cell, !cfg.unlockedByDefault);

            if (cell.button != null)
            {
                cell.button.onClick.RemoveAllListeners();
                var captured = cell;
                cell.button.onClick.AddListener(() => OnCourseClicked(captured));
            }

            return cell;
        }

        private void RefreshCourseVisibility()
        {
            int mask = 0;
            if (service != null)
            {
                var ts = service.GetTrainingSession();
                if (ts != null)
                    mask = ts.activeFilterMask;
            }

            for (int i = 0; i < courseCells.Count; i++)
            {
                var cell = courseCells[i];
                if (cell == null || cell.root == null)
                    continue;
                if (!RoleTrainingCourseCatalog.TryGet(cell.courseId, out var cfg) || cfg == null)
                {
                    cell.root.SetActive(false);
                    continue;
                }
                cell.root.SetActive(RoleTrainingCourseCatalog.MatchesFilter(cfg, mask));
            }
        }

        private static void ApplyLockedVisual(CourseCell cell, bool locked)
        {
            if (cell == null)
                return;
            if (cell.lockIcon != null)
                cell.lockIcon.SetActive(locked);
            if (cell.icon != null)
                cell.icon.color = locked ? CourseLockedTint : Color.white;
            if (cell.name != null)
                cell.name.color = locked ? CourseLockedTint : Color.white;
            if (cell.duration != null)
                cell.duration.color = locked ? CourseLockedTint : DurationTextColor;
            if (cell.attrGains != null)
                cell.attrGains.color = locked ? CourseLockedTint : AttrGainColor;
            if (cell.background != null)
                cell.background.color = locked
                    ? new Color(0.22f, 0.22f, 0.26f, 0.85f)
                    : new Color(0.28f, 0.3f, 0.38f, 1f);
        }

        private void PlayGainFlyFx(AttrDeltaEntry[] gains)
        {
            if (gains == null || gains.Length == 0 || roleMount == null)
                return;

            var canvas = GetComponentInParent<Canvas>();
            var canvasRt = canvas != null ? canvas.transform as RectTransform : panelRt;
            if (canvasRt == null)
                return;

            Vector2 toScreen = RectTransformUtility.WorldToScreenPoint(
                canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null,
                roleMount.position);

            Vector2 fromScreen = toScreen;
            if (completeButton != null)
            {
                fromScreen = RectTransformUtility.WorldToScreenPoint(
                    canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null,
                    completeButton.transform.position);
            }

            float stagger = 0f;
            for (int i = 0; i < gains.Length; i++)
            {
                var g = gains[i];
                if (g == null || g.delta == 0)
                    continue;
                string iconPath = RoleTrainingCourseCatalog.GetGrowthAttrIconPath(g.attrId);
                if (string.IsNullOrEmpty(iconPath))
                    continue;
                float delay = stagger;
                stagger += 0.12f;
                StartCoroutine(PlayFlyDelayed(canvasRt, fromScreen, toScreen, iconPath, delay));
            }
        }

        private static IEnumerator PlayFlyDelayed(
            RectTransform canvasRt, Vector2 from, Vector2 to, string icon, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            RewardFlyFx.Play(canvasRt, from, to, icon);
        }

        private void ShowTips(string message)
        {
            EnsureFieldsFromHierarchy();
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

        private static string FormatDuration(int sec)
        {
            sec = Mathf.Max(0, sec);
            int mm = sec / 60;
            int ss = sec % 60;
            return mm > 0 ? $"{mm}分{ss:00}秒" : $"{ss}秒";
        }

        private void EnsureFieldsFromHierarchy()
        {
            if (panelRt == null)
                panelRt = transform as RectTransform;

            if (roleMount == null)
                roleMount = FindRect("RoleMount");
            if (idleHintText == null)
                idleHintText = FindText("IdleHintText");
            if (activeCourseRoot == null)
            {
                var t = FindDescendant("ActiveCourseRoot");
                if (t != null)
                    activeCourseRoot = t.gameObject;
            }
            if (activeCourseIcon == null)
            {
                var t = FindDescendant("CourseIcon");
                if (t != null)
                    activeCourseIcon = t.GetComponent<Image>();
            }
            if (activeCourseName == null)
                activeCourseName = FindText("CourseName");
            if (countdownText == null)
                countdownText = FindText("CountdownText");
            if (completeButton == null)
            {
                var t = FindDescendant("CompleteButton");
                if (t != null)
                    completeButton = t.GetComponent<Button>();
            }
            if (courseContent == null)
                courseContent = FindRect("CourseContent");
            if (courseCellTemplate == null)
            {
                var t = FindDescendant("CourseCellTemplate");
                if (t != null)
                    courseCellTemplate = t.gameObject;
            }
            if (tipsToast == null)
            {
                var t = FindDescendant("TipsToast");
                if (t != null)
                    tipsToast = t.gameObject;
            }
            if (tipsText == null)
                tipsText = FindText("TipsText");

            for (int i = 0; i < TrainingPanelLayout.FilterAttrCount; i++)
            {
                if (filterIcons[i] != null)
                    continue;
                var item = FindDescendant("FilterAttr_" + i);
                if (item == null)
                    continue;
                filterIcons[i] = item.GetComponent<Image>();
                filterButtons[i] = item.GetComponent<Button>();
                var badge = item.Find("SelectedBadge");
                filterBadges[i] = badge != null ? badge.gameObject : null;
            }
        }

        private void EnsureRoleSpine()
        {
            if (roleBuilt || roleMount == null)
                return;
            roleBuilt = true;

            for (int i = roleMount.childCount - 1; i >= 0; i--)
                Destroy(roleMount.GetChild(i).gameObject);

            if (TryBuildRoleSkeletonGraphic(roleMount))
                return;

            var go = new GameObject("RolePlaceholder", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(roleMount, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = RoleDisplaySize;
            go.GetComponent<Image>().color = new Color(0.3f, 0.45f, 0.7f, 1f);
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
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
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
            PlayIdleLoop();
            return true;
        }

        private SkeletonDataAsset cachedRoleDataAsset;
        private bool roleDataAssetResolved;

        private SkeletonDataAsset ResolveRoleSkeletonDataAsset()
        {
            if (roleDataAssetResolved)
                return cachedRoleDataAsset;
            roleDataAssetResolved = true;

            var prefab = Resources.Load<GameObject>(RoleResourcesPrefabPath);
            if (prefab != null)
            {
                var sg = prefab.GetComponentInChildren<SkeletonGraphic>(true);
                if (sg != null && sg.skeletonDataAsset != null)
                {
                    cachedRoleDataAsset = sg.skeletonDataAsset;
                    return cachedRoleDataAsset;
                }
                var anim = prefab.GetComponentInChildren<SkeletonAnimation>(true);
                if (anim != null && anim.skeletonDataAsset != null)
                {
                    cachedRoleDataAsset = anim.skeletonDataAsset;
                    return cachedRoleDataAsset;
                }
            }

#if UNITY_EDITOR
            var editorPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Scenes/Air/Role/Hero_Role_cunmin.prefab");
            if (editorPrefab != null)
            {
                var sg = editorPrefab.GetComponentInChildren<SkeletonGraphic>(true);
                if (sg != null && sg.skeletonDataAsset != null)
                    cachedRoleDataAsset = sg.skeletonDataAsset;
                else
                {
                    var anim = editorPrefab.GetComponentInChildren<SkeletonAnimation>(true);
                    if (anim != null)
                        cachedRoleDataAsset = anim.skeletonDataAsset;
                }
            }
#endif
            return cachedRoleDataAsset;
        }

        private void PlayIdleLoop()
        {
            if (roleSkeletonGraphic == null || roleSkeletonGraphic.AnimationState == null)
                return;
            var data = roleSkeletonGraphic.Skeleton != null ? roleSkeletonGraphic.Skeleton.Data : null;
            if (data == null)
                return;
            for (int i = 0; i < RoleIdleAnimationFallbacks.Length; i++)
            {
                string animName = RoleIdleAnimationFallbacks[i];
                if (data.FindAnimation(animName) == null)
                    continue;
                roleSkeletonGraphic.AnimationState.SetAnimation(0, animName, true);
                return;
            }
            if (data.Animations.Count > 0)
                roleSkeletonGraphic.AnimationState.SetAnimation(0, data.Animations.Items[0].Name, true);
        }

        private Text FindText(string name)
        {
            var t = FindDescendant(name);
            return t != null ? t.GetComponent<Text>() : null;
        }

        private RectTransform FindRect(string name)
        {
            return FindDescendant(name) as RectTransform;
        }

        private Transform FindDescendant(string name)
        {
            return FindChild(transform, name);
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null)
                return null;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (string.Equals(child.name, name, StringComparison.Ordinal))
                    return child;
                var found = FindChild(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
