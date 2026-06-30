// 统一「操作」按钮视图（SPEC §9.2 + §9.2.1 + §10）：
// - 文字反映 PreviewNextAction()
// - 点击 → ExecuteUnifiedAction()
// - 20 田无事可做时灰显「暂无操作」
// - v3.23 起新增 AutoToggleButton 子按钮 + 自动浇水协程：
//   * autoMode=false → 底图 JiaoShui-1，主按钮一次性执行
//   * autoMode=true  → 底图 JiaoShui-2，主按钮切换 0.3s 周期协程
using System.Collections;
using PetDemo.Core;
using PetDemo.Farm;
using PetDemo.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Farm
{
    public class UnifiedActionButtonView : MonoBehaviour
    {
        public const string DefaultButtonPrefabResPath = "Prefabs/Farm/UnifiedActionButton";

        // SPEC §9.2.1：自动浇水协程固定周期 0.3s。
        private const float AutoWaterIntervalSeconds = 0.3f;

        private IPlantingService service;
        private InvasionService invasionService;
        private Button button;
        private Text label;
        private Image buttonImage;
        private Sprite spriteJiaoShui1;
        private Sprite spriteJiaoShui2;
        private Sprite spriteZhanDouKaiShi;
        private System.Action<PetDemo.Core.InvasionPhase> invasionPhaseHandler;

        // SPEC §9.2.1：Auto 子按钮与自动模式状态机。
        private RectTransform autoButtonRoot;
        private Button autoButton;
        private bool autoMode;
        private bool autoRunning;
        private Coroutine autoCoroutine;

        public static UnifiedActionButtonView BuildInto(
            RectTransform canvasRect,
            IPlantingService svc,
            RectTransform actionButtonPrefab = null,
            InvasionService invasionSvc = null)
        {
            var resolvedPrefab = actionButtonPrefab != null
                ? actionButtonPrefab
                : Resources.Load<RectTransform>(DefaultButtonPrefabResPath);

            RectTransform buttonRoot;
            if (resolvedPrefab != null)
            {
                buttonRoot = Object.Instantiate(resolvedPrefab, canvasRect, false);
                buttonRoot.name = "UnifiedActionButton";
            }
            else
            {
                buttonRoot = BuildFallbackButton(canvasRect);
                UnityEngine.Debug.LogWarning("UnifiedActionButtonView: 未找到按钮预制体，回退为运行时代码按钮。");
            }

            var btn = buttonRoot.GetComponent<Button>();
            if (btn == null)
                btn = buttonRoot.GetComponentInChildren<Button>(true);
            var view = buttonRoot.GetComponent<UnifiedActionButtonView>();
            var label = buttonRoot.GetComponentInChildren<Text>(true);

            if (btn == null)
            {
                UnityEngine.Debug.LogError("UnifiedActionButtonView: 预制体中缺少 Button 组件，无法绑定统一按钮。");
                return null;
            }

            EnsurePrefabClickability(buttonRoot, btn);
            if (label == null)
            {
                UnityEngine.Debug.LogError("UnifiedActionButtonView: 预制体中缺少 Text 组件，无法显示按钮文案。");
                return null;
            }
            if (view == null)
                view = buttonRoot.gameObject.AddComponent<UnifiedActionButtonView>();

            view.service = svc;
            view.invasionService = invasionSvc;
            view.button = btn;
            view.label = label;
            view.buttonImage = btn.targetGraphic as Image;
            view.spriteJiaoShui1 = LoadFirstAvailableSprite("AirUI/JiaoShui-1", "AirUI/JiaoShui");
            view.spriteJiaoShui2 = LoadFirstAvailableSprite("AirUI/JiaoShui-2");
            if (view.spriteJiaoShui2 == null)
            {
                UnityEngine.Debug.LogWarning(
                    "UnifiedActionButtonView: 未在 Resources/AirUI 找到 JiaoShui-2 图片，自动模式将沿用 JiaoShui-1 底图。");
            }
            view.spriteZhanDouKaiShi = LoadFirstAvailableSprite("AirUI/ZhanDouKaiShi", "AirUI/ZhanDou_1");
            if (view.spriteZhanDouKaiShi == null)
            {
                UnityEngine.Debug.LogWarning(
                    "UnifiedActionButtonView: 未在 Resources/AirUI 找到 ZhanDouKaiShi（或 ZhanDou_1）图片，入侵态将保留当前底图。");
            }
            view.BuildAutoToggleButton(buttonRoot);
            view.SubscribeEvents();
            view.RefreshPreview();
            btn.onClick.AddListener(view.OnClick);
            return view;
        }

        private static RectTransform BuildFallbackButton(RectTransform canvasRect)
        {
            var go = new GameObject("UnifiedActionButton");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(canvasRect, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -660f);
            rt.sizeDelta = new Vector2(282f, 193f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.45f, 0.22f, 0.92f);
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            btn.colors = colors;

            var labelGo = new GameObject("LabelText");
            var labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var label = labelGo.AddComponent<Text>();
            label.text = "暂无操作";
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.font = FarmGridView.LoadBuiltinFont();
            label.fontSize = 48;
            label.raycastTarget = false;
            return rt;
        }

        // 预制体分层后：装饰 Image 不得拦截射线；Button.targetGraphic 不得为空。
        private static void EnsurePrefabClickability(RectTransform buttonRoot, Button btn)
        {
            if (btn.targetGraphic == null)
            {
                var img = btn.GetComponent<Image>();
                if (img != null)
                    btn.targetGraphic = img;
            }

            for (int i = 0; i < buttonRoot.childCount; i++)
            {
                var child = buttonRoot.GetChild(i);
                if (child == btn.transform)
                    continue;
                if (child.GetComponent<Button>() != null)
                    continue;

                var image = child.GetComponent<Image>();
                if (image != null && image.raycastTarget)
                    image.raycastTarget = false;
            }
        }

        // SPEC §9.2.1：在 UnifiedActionButton 右下角构建 70x70 px 的 AutoToggleButton。
        private void BuildAutoToggleButton(RectTransform parentRoot)
        {
            if (parentRoot == null)
                return;

            var go = new GameObject("AutoToggleButton");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parentRoot, false);
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(-42f, 41f);
            rt.sizeDelta = new Vector2(70f, 70f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.45f, 0.22f, 0.92f);
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;

            var labelGo = new GameObject("LabelText");
            var labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var labelText = labelGo.AddComponent<Text>();
            labelText.text = "Auto";
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.font = FarmGridView.LoadBuiltinFont();
            labelText.fontSize = 28;
            labelText.raycastTarget = false;

            btn.onClick.AddListener(OnAutoToggleClick);

            autoButtonRoot = rt;
            autoButton = btn;
        }

        private System.Action<string> tileFlagsHandler;
        private System.Action<string, PetDemo.Core.PlantState> plantStateHandler;
        private System.Action<string, int> appearanceHandler;
        private System.Action seedBagHandler;
        private System.Action<string, ActionType> actionHandler;

        private void SubscribeEvents()
        {
            tileFlagsHandler = _ => RefreshPreview();
            plantStateHandler = (_, __) => RefreshPreview();
            appearanceHandler = (_, __) => RefreshPreview();
            seedBagHandler = RefreshPreview;
            actionHandler = (_, __) => RefreshPreview();

            service.OnTileFlagsChanged += tileFlagsHandler;
            service.OnPlantStateChanged += plantStateHandler;
            service.OnAppearanceNodeChanged += appearanceHandler;
            service.OnSeedBagChanged += seedBagHandler;
            service.OnUnifiedActionExecuted += actionHandler;

            if (invasionService != null)
            {
                invasionPhaseHandler = _ => RefreshPreview();
                invasionService.OnPhaseChanged += invasionPhaseHandler;
            }
        }

        private void OnDestroy()
        {
            StopAutoCoroutine();
            if (service == null)
                return;
            if (tileFlagsHandler != null) service.OnTileFlagsChanged -= tileFlagsHandler;
            if (plantStateHandler != null) service.OnPlantStateChanged -= plantStateHandler;
            if (appearanceHandler != null) service.OnAppearanceNodeChanged -= appearanceHandler;
            if (seedBagHandler != null) service.OnSeedBagChanged -= seedBagHandler;
            if (actionHandler != null) service.OnUnifiedActionExecuted -= actionHandler;
            if (invasionService != null && invasionPhaseHandler != null)
                invasionService.OnPhaseChanged -= invasionPhaseHandler;
        }

        private void OnClick()
        {
            if (IsInvading())
            {
                // 入侵态：强制停掉自动循环并直接打开战斗（v3.11）。
                StopAutoCoroutine();
                invasionService.OpenBattle();
                return;
            }

            // SPEC §9.2.1：autoMode=true 时主按钮变为「自动浇水开关」。
            if (autoMode)
            {
                if (autoRunning)
                    StopAutoCoroutine();
                else
                    StartAutoCoroutine();
                RefreshPreview();
                return;
            }

            TryExecuteAction();
            RefreshPreview();
        }

        // SPEC §9.1.2：收获时先摆动 PlantImage，再 TryHarvestTile；其它动作仍走 ExecuteUnifiedAction。
        private void TryExecuteAction()
        {
            if (service == null)
                return;

            if (TileSlotView.ActiveHarvestSwingCount > 0)
                return;

            var preview = service.PreviewNextAction();
            if (preview.HasValue
                && preview.Value.action == ActionType.Harvest
                && FarmGridView.Instance != null
                && FarmGridView.Instance.TryRequestHarvestWithSwing(preview.Value.tileId, service))
                return;

            service.ExecuteUnifiedAction();
        }

        // SPEC §9.2.1：Auto 子按钮翻转 autoMode。从自动模式切回手动时必须停掉协程。
        private void OnAutoToggleClick()
        {
            autoMode = !autoMode;
            if (!autoMode)
                StopAutoCoroutine();
            RefreshPreview();
        }

        private void StartAutoCoroutine()
        {
            if (autoRunning)
                return;
            autoRunning = true;
            autoCoroutine = StartCoroutine(AutoWaterLoop());
        }

        private void StopAutoCoroutine()
        {
            autoRunning = false;
            if (autoCoroutine != null)
            {
                StopCoroutine(autoCoroutine);
                autoCoroutine = null;
            }
        }

        private IEnumerator AutoWaterLoop()
        {
            var wait = new WaitForSeconds(AutoWaterIntervalSeconds);
            while (autoRunning)
            {
                if (service != null && !IsInvading())
                {
                    while (TileSlotView.ActiveHarvestSwingCount > 0)
                        yield return null;

                    // §9.1.2：收获走摆动+TryHarvestTile；其余仍走 ExecuteUnifiedAction。
                    TryExecuteAction();
                }
                yield return wait;
            }
            autoCoroutine = null;
        }

        // 主动 + 事件双驱：避免事件遗漏导致按钮文字与状态不同步。
        private void LateUpdate()
        {
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            if (service == null || button == null || label == null)
                return;

            if (IsInvading())
            {
                // 入侵态强制停掉自动循环，并隐藏 Auto 子按钮防止误触（SPEC §9.2.1）。
                if (autoRunning)
                    StopAutoCoroutine();
                if (autoButtonRoot != null && autoButtonRoot.gameObject.activeSelf)
                    autoButtonRoot.gameObject.SetActive(false);

                label.text = "战斗开始";
                button.interactable = true;
                if (buttonImage != null && spriteZhanDouKaiShi != null)
                    buttonImage.sprite = spriteZhanDouKaiShi;
                return;
            }

            if (autoButtonRoot != null && !autoButtonRoot.gameObject.activeSelf)
                autoButtonRoot.gameObject.SetActive(true);

            var preview = service.PreviewNextAction();
            if (preview.HasValue)
            {
                label.text = autoMode
                    ? (autoRunning ? "停止自动" : "自动浇水")
                    : ActionLabel(preview.Value.action);
                button.interactable = true;
            }
            else
            {
                label.text = autoMode
                    ? (autoRunning ? "停止自动" : "自动浇水")
                    : "暂无操作";
                // 自动模式下保留可点击，便于玩家随时停止协程；手动模式按 §9.2 既有规则置灰。
                button.interactable = autoMode;
            }

            if (buttonImage != null)
            {
                var sprite = autoMode && spriteJiaoShui2 != null
                    ? spriteJiaoShui2
                    : spriteJiaoShui1;
                if (sprite != null)
                    buttonImage.sprite = sprite;
            }
        }

        private bool IsInvading()
        {
            return invasionService != null
                && invasionService.GetPhase() == PetDemo.Core.InvasionPhase.Invading;
        }

        private static Sprite LoadFirstAvailableSprite(params string[] resourcePaths)
        {
            if (resourcePaths == null)
                return null;

            for (int i = 0; i < resourcePaths.Length; i++)
            {
                var path = resourcePaths[i];
                if (string.IsNullOrEmpty(path))
                    continue;
                var sprite = Resources.Load<Sprite>(path);
                if (sprite != null)
                    return sprite;
            }

            return null;
        }

        private static string ActionLabel(ActionType act)
        {
            switch (act)
            {
                case ActionType.Seed: return "播种";
                case ActionType.Water: return "浇水";
                case ActionType.Fertilize: return "施肥";
                case ActionType.PestControl: return "捉虫";
                case ActionType.Harvest: return "收获";
                default: return "暂无操作";
            }
        }
    }
}
