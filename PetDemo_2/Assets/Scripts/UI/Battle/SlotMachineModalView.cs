// SPEC §12.12 / §B.19：老虎机抽奖界面 SlotMachineModal（三轴 slot3 / 五轴 slot5）。
// 职责：
//   1) 预制体优先 / 代码回退地构建全屏 modal：纯黑底 + 轴背景(Zhou_x_2) + 各轴中心属性图标层 + 老虎机样式图(Zhou_x_1) + 「摇奖」按钮 + 关闭；
//      层级由下至上：黑底 < 轴背景 < 图标层 < 样式图 < 按钮（图标层介于轴背景与样式图之间）。
//   2) Show(reelCount, catalog, onComplete)：在属性增强表随机不重复选 reelCount-1 项作候选，每轴等概率；
//      点「摇奖」每轴独立按概率定格 → 按出现次数取 value{count} 汇总 → onComplete 回调。
// 三轴/五轴机制与产出一致，仅轴数(3/5)、候选数(2/4)与素材(Zhou_3_*/Zhou_5_*)不同；分别对应两套预制体。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using PetDemo.Battle;
using PetDemo.UI;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Battle
{
    /// <summary>SPEC §12.12.5：单个属性项的抽奖产出（属性项 + 出现次数 + 固定增加值）。</summary>
    public sealed class SlotMachineResultItem
    {
        public AttrEnhanceConfig cfg;
        public int count;
        public int gain;
    }

    /// <summary>SPEC §12.12.7：摇奖结果汇总文案（与 EventScroll EventCard 一致）。</summary>
    public static class SlotMachineResultText
    {
        public const string EmptyFallback = "摇奖结束，未获得可用属性。";

        public static string FormatResultSummary(IReadOnlyList<SlotMachineResultItem> results)
        {
            if (results == null || results.Count == 0)
                return EmptyFallback;

            var sb = new StringBuilder();
            sb.Append("摇奖结果：");
            bool any = false;
            for (int i = 0; i < results.Count; i++)
            {
                var item = results[i];
                if (item == null || item.cfg == null || item.gain == 0)
                    continue;
                if (!AttrEnhanceConfigCatalog.TryNormalizeAttrId(item.cfg.attrId, out _))
                    continue;
                if (any)
                    sb.Append("，");
                sb.Append(item.cfg.attrName).Append(" +").Append(item.gain);
                any = true;
            }

            if (!any)
                return EmptyFallback;
            sb.Append("。");
            return sb.ToString();
        }
    }

    [DisallowMultipleComponent]
    public sealed class SlotMachineModalView : MonoBehaviour
    {
        public const string ResPrefab3 = "Prefabs/Battle/SlotMachineModal_3";
        public const string ResPrefab5 = "Prefabs/Battle/SlotMachineModal_5";
        public const string ResReelBg3 = "AirUI/Zhou_3_2";
        public const string ResFrame3 = "AirUI/Zhou_3_1";
        public const string ResReelBg5 = "AirUI/Zhou_5_2";
        public const string ResFrame5 = "AirUI/Zhou_5_1";

        public const string PanelObjectName3 = "SlotMachineModal_3";
        public const string PanelObjectName5 = "SlotMachineModal_5";
        public const string BackgroundName = "Background";
        public const string ReelBgName = "ReelBackground";
        public const string IconLayerName = "IconLayer";
        public const string ResultSummaryName = "ResultSummary";
        public const string FrameName = "Frame";
        public const string ShakeButtonName = "ShakeButton";
        public const string CloseButtonName = "CloseButton";

        // 轴中心布局（1080×1920 基准，Inspector 可调）
        public static readonly Vector2 ReelIconSize3 = new Vector2(200f, 200f);
        public static readonly Vector2 ReelIconSize5 = new Vector2(150f, 150f);
        public const float ReelSpacing3 = 250f;
        public const float ReelSpacing5 = 175f;
        public const float ReelCenterY = 40f;
        public const float ReelLabelTop = 78f;
        public const float ReelLabelBottom = -78f;

        public const string ResResultFrame = "AirUI/ShiJian_1";
        public static readonly Vector2 ResultSummarySize = new Vector2(900f, 120f);
        public const float ResultSummaryY = -220f;

        public static readonly Vector2 ShakeSize = new Vector2(360f, 130f);
        public static readonly Vector2 ShakePos = new Vector2(0f, -740f);
        public static readonly Vector2 CloseSize = new Vector2(96f, 96f);
        public static readonly Vector2 ClosePos = new Vector2(-70f, -70f);

        private const float SpinDuration = 1.0f;
        private const float SpinTick = 0.06f;
        private const float ReelStopStaggerSec = 0.3f;

        private static SlotMachineModalView instance;

        [SerializeField] private Image background;
        [SerializeField] private Image reelBackground;
        [SerializeField] private RectTransform iconLayer;
        [SerializeField] private RectTransform resultSummaryRoot;
        [SerializeField] private Text resultSummaryText;
        [SerializeField] private Image frame;
        [SerializeField] private Image[] reelIcons;
        [SerializeField] private Text[] reelLabels;
        [SerializeField] private Button shakeButton;
        [SerializeField] private Text shakeLabel;
        [SerializeField] private Button closeButton;
        [SerializeField] private int reelCount = 3;

        private RectTransform panelRt;
        private RectTransform canvasRectCache;
        private RectTransform attrFlyTarget;

        private List<AttrEnhanceConfig> candidates;
        private Action<List<SlotMachineResultItem>> onComplete;
        private AttrEnhanceConfig[] reelResults;
        private Coroutine spinRoutine;
        private bool wired;
        private bool finalized;

        private enum SlotState { Ready, Spinning, Settled }
        private SlotState state = SlotState.Ready;

        public int ReelCount => reelCount;
        public bool IsShown => gameObject != null && gameObject.activeSelf;

        /// <summary>运行时回退构建时注入引用（编辑器生成器亦复用同一 Builder 直接赋值序列化字段）。</summary>
        internal void AssignRuntimeRefs(int builtReelCount, Image bg, Image reelBg, RectTransform icons,
            RectTransform resultSummary, Text resultSummaryTxt, Image slotFrame, Image[] icoImgs, Text[] icoLabels,
            Button shake, Text shakeTxt, Button close)
        {
            reelCount = builtReelCount;
            background = bg;
            reelBackground = reelBg;
            iconLayer = icons;
            resultSummaryRoot = resultSummary;
            resultSummaryText = resultSummaryTxt;
            frame = slotFrame;
            reelIcons = icoImgs;
            reelLabels = icoLabels;
            shakeButton = shake;
            shakeLabel = shakeTxt;
            closeButton = close;
        }

        // ============================================================
        // 单例创建 / 加载预制体（§12.12.1）
        // ============================================================
        public static SlotMachineModalView GetOrCreate(RectTransform canvasRect, int reelCount)
        {
            if (canvasRect == null)
            {
                UnityEngine.Debug.LogWarning("[SlotMachineModalView] GetOrCreate: canvasRect 为空");
                return null;
            }

            int rc = reelCount == 5 ? 5 : 3;
            string panelName = rc == 5 ? PanelObjectName5 : PanelObjectName3;
            string prefabPath = rc == 5 ? ResPrefab5 : ResPrefab3;

            if (instance != null && instance.panelRt != null && instance.reelCount == rc)
            {
                instance.canvasRectCache = canvasRect;
                return instance;
            }

            var existing = canvasRect.Find(panelName);
            if (existing != null)
            {
                var existView = existing.GetComponent<SlotMachineModalView>();
                if (existView == null)
                    existView = existing.gameObject.AddComponent<SlotMachineModalView>();
                existView.panelRt = existing as RectTransform;
                existView.canvasRectCache = canvasRect;
                instance = existView;
                return existView;
            }

            var prefab = Resources.Load<GameObject>(prefabPath);
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, canvasRect, false);
            }
            else
            {
                UnityEngine.Debug.LogWarning(
                    "[SlotMachineModalView] 缺少预制体 Resources/" + prefabPath +
                    "，使用运行时回退 UI；请在编辑器执行 Tools/PetDemo/Generate Slot Machine Modal Prefabs。");
                go = BuildRuntimeFallback(canvasRect, rc);
                if (go == null)
                    return null;
            }

            go.name = panelName;
            go.SetActive(false);
            var rt = go.transform as RectTransform;
            if (rt != null)
                BottomNavAttachedScreenLayout.StretchFull(rt);

            var view = go.GetComponent<SlotMachineModalView>();
            if (view == null)
                view = go.AddComponent<SlotMachineModalView>();
            view.panelRt = rt;
            view.canvasRectCache = canvasRect;
            view.reelCount = rc;
            instance = view;
            return view;
        }

        // ============================================================
        // 玩法（§12.12.2）
        // ============================================================
        public void Show(int reelCountParam, List<AttrEnhanceConfig> catalog,
            Action<List<SlotMachineResultItem>> completeCallback,
            RectTransform flyTarget = null)
        {
            EnsureFieldsFromHierarchy();
            WireOnce();

            onComplete = completeCallback;
            attrFlyTarget = flyTarget;
            finalized = false;

            // 候选数 = 轴数 - 1（三轴选 2 / 五轴选 4）。以预制体烘焙的 reelCount 为准。
            int pickCount = Mathf.Max(1, reelCount - 1);
            candidates = AttrEnhanceConfigCatalog.PickDistinct(catalog, pickCount);
            if (candidates == null || candidates.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[SlotMachineModalView] 属性增强表候选为空，直接完成空产出。");
                Finalize(new List<SlotMachineResultItem>());
                return;
            }

            reelResults = new AttrEnhanceConfig[reelCount];
            ResetReelVisuals();
            SetState(SlotState.Ready);

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            if (spinRoutine != null)
            {
                StopCoroutine(spinRoutine);
                spinRoutine = null;
            }
            gameObject.SetActive(false);
        }

        public static void HideIfAny()
        {
            if (instance != null && instance.IsShown)
                instance.Hide();
        }

        private void WireOnce()
        {
            if (wired)
                return;
            if (shakeButton != null)
            {
                shakeButton.onClick.RemoveAllListeners();
                shakeButton.onClick.AddListener(OnShakeClicked);
            }
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(OnCloseClicked);
            }
            wired = true;
        }

        private void OnShakeClicked()
        {
            if (state == SlotState.Ready)
            {
                if (spinRoutine != null)
                    StopCoroutine(spinRoutine);
                spinRoutine = StartCoroutine(SpinRoutine());
            }
            else if (state == SlotState.Settled)
            {
                Finalize(BuildResults());
            }
        }

        private void OnCloseClicked()
        {
            if (state == SlotState.Settled)
                Finalize(BuildResults());
            else if (state == SlotState.Ready)
                Finalize(new List<SlotMachineResultItem>());
            // Spinning 态忽略关闭，避免中途打断产出
        }

        private IEnumerator SpinRoutine()
        {
            SetState(SlotState.Spinning);
            HideResultSummary();

            var predetermined = new AttrEnhanceConfig[reelCount];
            for (int i = 0; i < reelCount; i++)
                predetermined[i] = candidates[UnityEngine.Random.Range(0, candidates.Count)];

            float elapsed = 0f;
            while (elapsed < SpinDuration)
            {
                SpinUnsettledReels(0);
                yield return new WaitForSeconds(SpinTick);
                elapsed += SpinTick;
            }

            var reelStopped = new bool[reelCount];
            for (int i = 0; i < reelCount; i++)
            {
                reelResults[i] = predetermined[i];
                ApplyReelVisual(i, predetermined[i]);
                reelStopped[i] = true;

                if (i < reelCount - 1)
                {
                    float wait = 0f;
                    while (wait < ReelStopStaggerSec)
                    {
                        SpinUnsettledReels(i + 1, reelStopped);
                        yield return new WaitForSeconds(SpinTick);
                        wait += SpinTick;
                    }
                }
            }

            ShowResultSummary(BuildResults());
            spinRoutine = null;
            SetState(SlotState.Settled);
        }

        private void SpinUnsettledReels(int firstUnsettledIndex, bool[] reelStopped = null)
        {
            for (int j = firstUnsettledIndex; j < reelCount; j++)
            {
                if (reelStopped != null && reelStopped[j])
                    continue;
                var rnd = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                ApplyReelVisual(j, rnd);
            }
        }

        // ============================================================
        // 产出汇总（§12.12.2 第四步）
        // ============================================================
        private List<SlotMachineResultItem> BuildResults()
        {
            var byId = new Dictionary<string, SlotMachineResultItem>();
            var ordered = new List<SlotMachineResultItem>();
            if (reelResults == null)
                return ordered;

            for (int i = 0; i < reelResults.Length; i++)
            {
                var cfg = reelResults[i];
                if (cfg == null)
                    continue;
                if (!byId.TryGetValue(cfg.attrId, out var item))
                {
                    item = new SlotMachineResultItem { cfg = cfg, count = 0, gain = 0 };
                    byId[cfg.attrId] = item;
                    ordered.Add(item);
                }
                item.count += 1;
            }

            for (int i = 0; i < ordered.Count; i++)
                ordered[i].gain = ordered[i].cfg.GetGain(ordered[i].count);
            return ordered;
        }

        private void Finalize(List<SlotMachineResultItem> results)
        {
            if (finalized)
                return;
            finalized = true;

            List<RectTransform> flyIcons = null;
            if (state == SlotState.Settled && canvasRectCache != null && reelIcons != null)
                flyIcons = SlotAttrFlyFx.CreateIconsAtReels(canvasRectCache, reelIcons);

            var cb = onComplete;
            var flyTarget = attrFlyTarget;
            onComplete = null;
            attrFlyTarget = null;
            Hide();

            if (flyIcons != null && flyIcons.Count > 0)
                SlotAttrFlyFx.Launch(canvasRectCache, flyIcons, flyTarget);

            cb?.Invoke(results ?? new List<SlotMachineResultItem>());
        }

        // ============================================================
        // 视觉
        // ============================================================
        private void SetState(SlotState next)
        {
            state = next;
            if (shakeButton != null)
                shakeButton.interactable = next != SlotState.Spinning;
            if (shakeLabel != null)
                shakeLabel.text = next == SlotState.Settled ? "关闭" : "摇奖";
        }

        private void ResetReelVisuals()
        {
            HideResultSummary();
            if (reelIcons == null)
                return;
            for (int i = 0; i < reelIcons.Length; i++)
            {
                if (reelIcons[i] != null)
                {
                    reelIcons[i].sprite = null;
                    reelIcons[i].color = new Color(1f, 1f, 1f, 0.15f);
                }
                if (reelLabels != null && i < reelLabels.Length && reelLabels[i] != null)
                    reelLabels[i].text = "?";
            }
        }

        private void ApplyReelVisual(int reelIndex, AttrEnhanceConfig cfg)
        {
            if (reelIcons == null || reelIndex < 0 || reelIndex >= reelIcons.Length)
                return;
            var img = reelIcons[reelIndex];
            if (img != null)
            {
                string iconPath = cfg != null ? cfg.icon : null;
                Sprite sprite = LoadAttrEnhanceIcon(iconPath);
                img.sprite = sprite;
                img.preserveAspect = true;
                img.color = sprite != null
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0.15f);
                if (sprite == null && !string.IsNullOrEmpty(iconPath))
                {
                    UnityEngine.Debug.LogWarning(
                        "[SlotMachineModalView] 缺少图标 Resources/" + iconPath +
                        "（已尝试 AirUI/ 前缀），Reel" + reelIndex);
                }
            }
            if (reelLabels != null && reelIndex < reelLabels.Length && reelLabels[reelIndex] != null)
                reelLabels[reelIndex].text = cfg != null ? cfg.attrName : "";
        }

        private void ShowResultSummary(List<SlotMachineResultItem> results)
        {
            EnsureResultSummaryPanel();
            if (resultSummaryText == null)
                return;
            resultSummaryText.text = SlotMachineResultText.FormatResultSummary(results);
            if (resultSummaryRoot != null)
                resultSummaryRoot.gameObject.SetActive(true);
        }

        private void HideResultSummary()
        {
            if (resultSummaryRoot != null)
                resultSummaryRoot.gameObject.SetActive(false);
        }

        private void EnsureResultSummaryPanel()
        {
            if (resultSummaryRoot == null)
                resultSummaryRoot = FindDescendantRect(ResultSummaryName);
            if (resultSummaryText == null && resultSummaryRoot != null)
            {
                var t = FindDescendantByName(resultSummaryRoot, "Text");
                resultSummaryText = t != null ? t.GetComponent<Text>() : null;
            }
            if (resultSummaryRoot != null && resultSummaryText != null)
                return;

            var root = transform as RectTransform;
            if (root == null)
                return;
            SlotMachineModalBuilder.BuildResultSummary(root, out var summaryRt, out var summaryTxt);
            resultSummaryRoot = summaryRt;
            resultSummaryText = summaryTxt;
            HideResultSummary();
        }

        private static Sprite LoadAttrEnhanceIcon(string iconPath)
        {
            if (string.IsNullOrEmpty(iconPath))
                return null;
            var sprite = Resources.Load<Sprite>(iconPath);
            if (sprite != null)
                return sprite;
            if (iconPath.IndexOf('/') < 0)
                return Resources.Load<Sprite>("AirUI/" + iconPath);
            return null;
        }

        // ============================================================
        // 字段回填（预制体路径按名查找）
        // ============================================================
        private void EnsureFieldsFromHierarchy()
        {
            if (panelRt == null)
                panelRt = transform as RectTransform;
            if (background == null)
                background = FindDescendantImage(BackgroundName);
            if (reelBackground == null)
                reelBackground = FindDescendantImage(ReelBgName);
            if (iconLayer == null)
                iconLayer = FindDescendantRect(IconLayerName);
            if (resultSummaryRoot == null)
                resultSummaryRoot = FindDescendantRect(ResultSummaryName);
            if (resultSummaryText == null && resultSummaryRoot != null)
            {
                var t = FindDescendantByName(resultSummaryRoot, "Text");
                resultSummaryText = t != null ? t.GetComponent<Text>() : null;
            }
            if (frame == null)
                frame = FindDescendantImage(FrameName);
            if (shakeButton == null)
                shakeButton = FindDescendantButton(ShakeButtonName);
            if (shakeLabel == null && shakeButton != null)
            {
                var t = FindDescendantByName(shakeButton.transform, "Label");
                shakeLabel = t != null ? t.GetComponent<Text>() : null;
            }
            if (closeButton == null)
                closeButton = FindDescendantButton(CloseButtonName);

            if (reelIcons == null || reelIcons.Length != reelCount)
                reelIcons = new Image[reelCount];
            if (reelLabels == null || reelLabels.Length != reelCount)
                reelLabels = new Text[reelCount];
            for (int i = 0; i < reelCount; i++)
            {
                var reelT = FindDescendantByName(transform, "Reel" + i);
                if (reelT == null)
                    continue;
                if (reelIcons[i] == null)
                    reelIcons[i] = reelT.GetComponent<Image>();
                if (reelLabels[i] == null)
                {
                    var t = FindDescendantByName(reelT, "Label");
                    reelLabels[i] = t != null ? t.GetComponent<Text>() : null;
                }
            }
        }

        private RectTransform FindDescendantRect(string nodeName)
        {
            return FindDescendantByName(transform, nodeName) as RectTransform;
        }

        private Image FindDescendantImage(string nodeName)
        {
            var t = FindDescendantByName(transform, nodeName);
            return t != null ? t.GetComponent<Image>() : null;
        }

        private Button FindDescendantButton(string nodeName)
        {
            var t = FindDescendantByName(transform, nodeName);
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

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        // ============================================================
        // 运行时代码回退（缺预制体时，与生成器布局对齐）
        // ============================================================
        private static GameObject BuildRuntimeFallback(RectTransform canvasRect, int reelCount)
        {
            var panelName = reelCount == 5 ? PanelObjectName5 : PanelObjectName3;
            var rootGo = new GameObject(panelName, typeof(RectTransform));
            var rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.SetParent(canvasRect, false);
            BottomNavAttachedScreenLayout.StretchFull(rootRt);

            var view = rootGo.AddComponent<SlotMachineModalView>();
            SlotMachineModalBuilder.Build(rootRt, view, reelCount);
            return rootGo;
        }
    }

    /// <summary>
    /// SPEC §12.12：老虎机界面结构构建（运行时回退与编辑器生成器共用同一布局）。
    /// 层级由下至上：黑底 &lt; 轴背景(Zhou_x_2) &lt; 图标层 &lt; 样式图(Zhou_x_1) &lt; 「摇奖」/关闭按钮。
    /// </summary>
    public static class SlotMachineModalBuilder
    {
        public static void Build(RectTransform root, SlotMachineModalView view, int reelCount)
        {
            int rc = reelCount == 5 ? 5 : 3;
            string reelBgPath = rc == 5 ? SlotMachineModalView.ResReelBg5 : SlotMachineModalView.ResReelBg3;
            string framePath = rc == 5 ? SlotMachineModalView.ResFrame5 : SlotMachineModalView.ResFrame3;
            Vector2 iconSize = rc == 5 ? SlotMachineModalView.ReelIconSize5 : SlotMachineModalView.ReelIconSize3;
            float spacing = rc == 5 ? SlotMachineModalView.ReelSpacing5 : SlotMachineModalView.ReelSpacing3;

            var font = FarmGridView.LoadBuiltinFont();

            // 1) 纯黑底（兼作点击拦截）
            var bgRt = BottomNavAttachedScreenLayout.CreateChildRect(
                root, SlotMachineModalView.BackgroundName, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(bgRt);
            var background = bgRt.gameObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 1f);
            background.raycastTarget = true;

            // 2) 轴背景 Zhou_x_2（全屏、保持比例居中）
            var reelBg = AddFullscreenSprite(root, SlotMachineModalView.ReelBgName, reelBgPath,
                new Color(0.12f, 0.12f, 0.16f, 1f));

            // 3) 图标层（介于轴背景与样式图之间）
            var iconLayer = BottomNavAttachedScreenLayout.CreateChildRect(
                root, SlotMachineModalView.IconLayerName, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(iconLayer);
            var iconLayerImg = iconLayer.gameObject.AddComponent<Image>();
            iconLayerImg.color = new Color(0f, 0f, 0f, 0f);
            iconLayerImg.raycastTarget = false;

            var reelIcons = new Image[rc];
            var reelLabels = new Text[rc];
            float startX = -(rc - 1) * 0.5f * spacing;
            for (int i = 0; i < rc; i++)
            {
                float x = startX + i * spacing;
                BuildReel(iconLayer, i, new Vector2(x, SlotMachineModalView.ReelCenterY), iconSize, font,
                    out reelIcons[i], out reelLabels[i]);
            }

            // 4) 结果汇总条（IconLayer 下方，§12.12.7）
            BuildResultSummary(root, out var resultSummaryRt, out var resultSummaryTxt);
            resultSummaryRt.gameObject.SetActive(false);

            // 5) 老虎机样式图 Zhou_x_1（叠在轴背景之上）
            var frame = AddFullscreenSprite(root, SlotMachineModalView.FrameName, framePath,
                new Color(1f, 1f, 1f, 0f));
            frame.raycastTarget = false;

            // 6) 「摇奖」按钮 + 关闭按钮（最上层）
            var shakeButton = BuildTextButton(root, SlotMachineModalView.ShakeButtonName, "摇奖",
                SlotMachineModalView.ShakePos, SlotMachineModalView.ShakeSize, font, out var shakeLabel);

            var closeButton = BuildTextButton(root, SlotMachineModalView.CloseButtonName, "X",
                SlotMachineModalView.ClosePos, SlotMachineModalView.CloseSize, font, out _);
            var closeRt = closeButton.transform as RectTransform;
            if (closeRt != null)
            {
                closeRt.anchorMin = new Vector2(1f, 1f);
                closeRt.anchorMax = new Vector2(1f, 1f);
                closeRt.anchoredPosition = SlotMachineModalView.ClosePos;
            }

            view.AssignRuntimeRefs(rc, background, reelBg, iconLayer, resultSummaryRt, resultSummaryTxt, frame,
                reelIcons, reelLabels, shakeButton, shakeLabel, closeButton);
        }

        public static void BuildResultSummary(RectTransform root, out RectTransform summaryRt, out Text summaryText)
        {
            var font = FarmGridView.LoadBuiltinFont();

            summaryRt = BottomNavAttachedScreenLayout.CreateChildRect(
                root, SlotMachineModalView.ResultSummaryName,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, SlotMachineModalView.ResultSummaryY),
                SlotMachineModalView.ResultSummarySize);

            var frameImg = summaryRt.gameObject.AddComponent<Image>();
            var frameSprite = Resources.Load<Sprite>(SlotMachineModalView.ResResultFrame);
            if (frameSprite != null)
            {
                frameImg.sprite = frameSprite;
                frameImg.type = Image.Type.Sliced;
                frameImg.fillCenter = true;
                frameImg.color = Color.white;
            }
            else
            {
                frameImg.color = new Color(0f, 0f, 0f, 0.35f);
            }
            frameImg.raycastTarget = false;

            var vlg = summaryRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(48, 48, 30, 30);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = summaryRt.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textGo = new GameObject("Text");
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.SetParent(summaryRt, false);
            summaryText = textGo.AddComponent<Text>();
            summaryText.font = font;
            summaryText.fontSize = 34;
            summaryText.color = Color.black;
            summaryText.alignment = TextAnchor.MiddleLeft;
            summaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            summaryText.verticalOverflow = VerticalWrapMode.Overflow;
            summaryText.supportRichText = true;
            summaryText.raycastTarget = false;
            summaryText.text = "";
        }

        private static Image AddFullscreenSprite(RectTransform parent, string name, string resPath, Color fallback)
        {
            var rt = BottomNavAttachedScreenLayout.CreateChildRect(
                parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(rt);
            var img = rt.gameObject.AddComponent<Image>();
            var sprite = Resources.Load<Sprite>(resPath);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.preserveAspect = true;
                img.color = Color.white;
            }
            else
            {
                img.color = fallback;
                UnityEngine.Debug.LogWarning("[SlotMachineModal] 缺少素材 Resources/" + resPath + "，已使用回退色。");
            }
            img.raycastTarget = false;
            return img;
        }

        private static void BuildReel(RectTransform parent, int index, Vector2 pos, Vector2 size, Font font,
            out Image icon, out Text label)
        {
            var reelRt = BottomNavAttachedScreenLayout.CreateChildRect(
                parent, "Reel" + index,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            icon = reelRt.gameObject.AddComponent<Image>();
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var labelRt = BottomNavAttachedScreenLayout.CreateChildRect(
                reelRt, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(labelRt);
            labelRt.offsetMax = new Vector2(labelRt.offsetMax.x, -SlotMachineModalView.ReelLabelTop);
            labelRt.offsetMin = new Vector2(labelRt.offsetMin.x, SlotMachineModalView.ReelLabelBottom);
            label = labelRt.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = 34;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.color = Color.white;
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            label.text = "?";
        }

        private static Button BuildTextButton(RectTransform parent, string name, string text,
            Vector2 pos, Vector2 size, Font font, out Text label)
        {
            var rt = BottomNavAttachedScreenLayout.CreateChildRect(
                parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.85f, 0.2f, 0.2f, 1f);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;

            var labelRt = BottomNavAttachedScreenLayout.CreateChildRect(
                rt, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(labelRt);
            label = labelRt.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = 44;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            label.text = text;
            return button;
        }
    }
}
