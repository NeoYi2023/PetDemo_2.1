// SPEC §9.12（v3.82）：「附魔」转盘玩法全屏界面（正式版，取代 v3.52「打地鼠」演示）。
// 触发：TileSlotView 检测到 tile.moleTheft == AwaitingMoleTheft 后调用 Instance.Open(tileId)。
// 结构（预制件 EnchantScreen.prefab）：Background(Game_2_1_0) + PlantImage（中上部激活植物）+
//   Wheel{ Indicator(Game_2_1_3，低于底座) / WheelBase(Game_2_1_2，点击区) / Pointer(Game_2_1_1) } + ResultPanel{ Retry / Abandon }。
// 规则：4 回合操作指针，累计 3 次命中目标即胜（满 3 胜提前结束）；指针 360°/s，点底座立即停，
//   指针与目标角环形差 ≤ 30° 判本回合胜；目标角自 {0,45,...,315} 随机取一，指示灯置于半径 264px 处。
// 胜利：CompleteMoleTheft + TriggerSingleTileMutation（变异待收获）→ 停留 1s 自动 TryHarvestMutation 弹收获弹窗 → Close。
// 失败：显示 ResultPanel；「重新挑战」重置重开；「放弃」AbandonMoleTheftPlant 删除植物 → Close。
using System;
using System.Collections;
using PetDemo.Core;
using PetDemo.Farm;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PetDemo.UI.Farm
{
    [DisallowMultipleComponent]
    public class EnchantScreenView : MonoBehaviour
    {
        private const string ResBackground = "AirUI/Game_2_1_0";
        private const string ResPointer = "AirUI/Game_2_1_1";
        private const string ResWheelBase = "AirUI/Game_2_1_2";
        private const string ResIndicator = "AirUI/Game_2_1_3";
        private const string DefaultPrefabResPath = "Prefabs/Farm/EnchantScreen";
        // SPEC §4.1.10.4 / §5.2：变异「待收获」外形（与 MutationOverlayView 图标一致）。
        private const string ResMutationPetIcon = "AirUI/ShiWu_2";
        private const string ResMutationSkillIcon = "AirUI/DaShouHuo_2";

        private static readonly float[] TargetAngles = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };

        // ---- 可在 Inspector 调整的玩法参数 ----
        [Header("玩法参数（可调）")]
        [SerializeField] private float rotateDegPerSec = 360f;   // 指针旋转速度：1 秒 360°
        [SerializeField] private float indicatorRadius = 230f;   // 指示灯中心距底座中心的半径（像素）
        [SerializeField] private float winToleranceDeg = 30f;    // 判胜的环形角差阈值
        [SerializeField] private int requiredWins = 3;           // 命中达到该数即胜
        [SerializeField] private int totalRounds = 4;            // 总回合数
        [SerializeField] private float autoHarvestDelaySec = 1f; // 胜利后停留再自动收获的时间

        // ---- 预制件引用（生成器自动绑定；运行时缺失则按名兜底） ----
        [Header("节点引用")]
        [SerializeField] private Image background;
        [SerializeField] private Image plantImage;
        [SerializeField] private RectTransform wheelRoot;
        [SerializeField] private Image wheelBase;
        [SerializeField] private RectTransform pointer;
        [SerializeField] private Image indicator;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button abandonButton;

        public static EnchantScreenView Instance { get; private set; }

        private IPlantingService service;
        private RectTransform modalRt;
        private string currentTileId;

        private int wins;
        private int losses;
        private float currentTargetAngle;
        private bool roundActive;
        private bool pointerStopRequested;
        private Coroutine gameRoutine;
        private Coroutine plantFeedbackRoutine;

        private string capturedMutationId;
        private bool capturingMutation;
        private Vector2 plantBasePos;

        public static EnchantScreenView BuildInto(RectTransform canvasRect, IPlantingService svc, GameObject prefab)
        {
            if (canvasRect == null)
                return null;

            GameObject instance = null;
            if (prefab != null)
            {
                instance = Instantiate(prefab, canvasRect, false);
            }
            else
            {
                var resPrefab = Resources.Load<GameObject>(DefaultPrefabResPath);
                if (resPrefab != null)
                    instance = Instantiate(resPrefab, canvasRect, false);
            }

            EnchantScreenView view;
            if (instance != null)
            {
                instance.name = "EnchantScreen";
                view = instance.GetComponent<EnchantScreenView>();
                if (view == null)
                    view = instance.AddComponent<EnchantScreenView>();
            }
            else
            {
                UnityEngine.Debug.LogWarning("[EnchantScreenView] 缺少预制件，回退为代码构建的简版界面。");
                view = BuildFallback(canvasRect);
            }

            view.service = svc;
            view.InitAfterBuild(canvasRect);
            return view;
        }

        private void InitAfterBuild(RectTransform canvasRect)
        {
            modalRt = transform as RectTransform;
            if (modalRt != null)
            {
                modalRt.anchorMin = Vector2.zero;
                modalRt.anchorMax = Vector2.one;
                modalRt.offsetMin = Vector2.zero;
                modalRt.offsetMax = Vector2.zero;
            }

            EnsureBindings();
            BindWheelBaseClick();
            BindResultButtons();

            if (resultPanel != null)
                resultPanel.SetActive(false);

            RegisterAsInstance();
            gameObject.SetActive(false);
        }

        private void BindWheelBaseClick()
        {
            if (wheelBase == null)
                return;
            wheelBase.raycastTarget = true;
            var relay = wheelBase.GetComponent<EnchantWheelBaseClick>();
            if (relay == null)
                relay = wheelBase.gameObject.AddComponent<EnchantWheelBaseClick>();
            relay.Bind(this);
        }

        private void BindResultButtons()
        {
            if (retryButton != null)
            {
                retryButton.onClick.RemoveListener(OnRetryClicked);
                retryButton.onClick.AddListener(OnRetryClicked);
            }
            if (abandonButton != null)
            {
                abandonButton.onClick.RemoveListener(OnAbandonClicked);
                abandonButton.onClick.AddListener(OnAbandonClicked);
            }
        }

        public static EnchantScreenView Resolve()
        {
            if (Instance != null)
                return Instance;
            return FindObjectOfType<EnchantScreenView>(true);
        }

        public void RegisterAsInstance()
        {
            if (Instance != null && Instance != this)
            {
                UnityEngine.Debug.LogWarning("[EnchantScreenView] 发现重复实例，销毁多余的。");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Open(string tileId)
        {
            if (modalRt == null)
                modalRt = transform as RectTransform;

            if (service == null)
                service = PlantingService.Instance;

            currentTileId = tileId;

            gameObject.SetActive(true);
            if (modalRt != null)
                modalRt.SetAsLastSibling();

            ShowActivatedPlant(tileId);

            if (resultPanel != null)
                resultPanel.SetActive(false);

            StartGame();
        }

        public void Close()
        {
            StopAllGameRoutines();
            if (resultPanel != null)
                resultPanel.SetActive(false);
            gameObject.SetActive(false);
        }

        private void StopAllGameRoutines()
        {
            if (gameRoutine != null)
            {
                StopCoroutine(gameRoutine);
                gameRoutine = null;
            }
            if (plantFeedbackRoutine != null)
            {
                StopCoroutine(plantFeedbackRoutine);
                plantFeedbackRoutine = null;
            }
            roundActive = false;
            pointerStopRequested = false;
        }

        private void StartGame()
        {
            StopAllGameRoutines();
            wins = 0;
            losses = 0;
            if (resultPanel != null)
                resultPanel.SetActive(false);
            gameRoutine = StartCoroutine(GameRoutine());
        }

        private IEnumerator GameRoutine()
        {
            for (int round = 0; round < totalRounds; round++)
            {
                yield return PlayOneRound();

                // 提前结束判定：满足 requiredWins 即胜；剩余回合已无法凑满即败。
                if (wins >= requiredWins)
                {
                    yield return VictorySequence();
                    yield break;
                }
                int remaining = totalRounds - (round + 1);
                if (wins + remaining < requiredWins)
                    break;
            }

            // 满 totalRounds（或提前确定无法达成）仍未达标 → 失败。
            ShowResultPanel();
            gameRoutine = null;
        }

        private IEnumerator PlayOneRound()
        {
            // 1) 随机目标角并放置指示灯。
            currentTargetAngle = TargetAngles[UnityEngine.Random.Range(0, TargetAngles.Length)];
            PlaceIndicator(currentTargetAngle);

            // 2) 指针从 0° 起匀速顺时针旋转，等待玩家点击底座停下。
            if (pointer != null)
                pointer.localEulerAngles = Vector3.zero;

            pointerStopRequested = false;
            roundActive = true;

            float z = 0f;
            while (!pointerStopRequested)
            {
                z -= rotateDegPerSec * Time.deltaTime; // 负向 = 顺时针（UI 中 +Z 为逆时针）
                z = Mathf.Repeat(z, 360f) - 360f;       // 保持在 (-360,0] 范围避免溢出
                if (pointer != null)
                    pointer.localEulerAngles = new Vector3(0f, 0f, z);
                yield return null;
            }

            roundActive = false;

            // 3) 判定：指针「自上方顺时针」的角度与目标角的环形差。
            float pointerClockwise = NormalizeAngle(-z);
            float diff = Mathf.Abs(Mathf.DeltaAngle(pointerClockwise, currentTargetAngle));
            bool hit = diff <= winToleranceDeg;

            if (hit)
            {
                wins++;
                SpawnFlyingIndicatorToPlant();
                PlayPlantFeedback();
                yield return new WaitForSeconds(0.45f);
            }
            else
            {
                losses++;
                yield return new WaitForSeconds(0.25f);
            }

            HideIndicator();
        }

        private IEnumerator VictorySequence()
        {
            if (indicator != null)
                indicator.enabled = false;

            if (service != null && !string.IsNullOrEmpty(currentTileId))
            {
                service.CompleteMoleTheft(currentTileId);

                capturedMutationId = null;
                capturingMutation = true;
                service.OnMutationCreated += HandleMutationCreated;
                bool ok = service.TriggerSingleTileMutation(currentTileId);
                service.OnMutationCreated -= HandleMutationCreated;
                capturingMutation = false;

                if (!ok)
                    UnityEngine.Debug.LogWarning("[EnchantScreenView] TriggerSingleTileMutation 失败，tileId=" + currentTileId);

                // 植物切换为「变异待收获」外形，持续 autoHarvestDelaySec 秒后再弹奖励。
                ShowAwaitingHarvestAppearance(capturedMutationId);

                yield return new WaitForSeconds(autoHarvestDelaySec);

                if (!string.IsNullOrEmpty(capturedMutationId))
                {
                    var mutation = service.GetMutation(capturedMutationId);
                    if (mutation != null && mutation.state == PlantState.AwaitingHarvest)
                        service.TryHarvestMutation(capturedMutationId);
                }
            }
            else
            {
                yield return new WaitForSeconds(autoHarvestDelaySec);
            }

            gameRoutine = null;
            Close();
        }

        private void HandleMutationCreated(string mutationId)
        {
            if (capturingMutation)
                capturedMutationId = mutationId;
        }

        private void ShowResultPanel()
        {
            roundActive = false;
            if (resultPanel != null)
                resultPanel.SetActive(true);
        }

        private void OnRetryClicked()
        {
            if (resultPanel != null)
                resultPanel.SetActive(false);
            StartGame();
        }

        private void OnAbandonClicked()
        {
            if (service != null && !string.IsNullOrEmpty(currentTileId))
            {
                bool ok = service.AbandonMoleTheftPlant(currentTileId);
                if (!ok)
                    UnityEngine.Debug.LogWarning("[EnchantScreenView] AbandonMoleTheftPlant 失败，tileId=" + currentTileId);
            }
            Close();
        }

        // ---- 几何与展示 ----

        private void PlaceIndicator(float clockwiseAngleDeg)
        {
            if (indicator == null)
                return;
            var indRt = indicator.rectTransform;
            float rad = clockwiseAngleDeg * Mathf.Deg2Rad;
            // 自上方（+Y）顺时针：x = r·sinθ, y = r·cosθ。
            Vector2 offset = new Vector2(indicatorRadius * Mathf.Sin(rad), indicatorRadius * Mathf.Cos(rad));
            Vector2 baseCenter = (wheelBase != null && indRt.parent == wheelBase.transform.parent)
                ? wheelBase.rectTransform.anchoredPosition
                : Vector2.zero;
            indRt.anchoredPosition = baseCenter + offset;
            indicator.enabled = true;
        }

        private void HideIndicator()
        {
            if (indicator != null)
                indicator.enabled = false;
        }

        private void SpawnFlyingIndicatorToPlant()
        {
            if (indicator == null || plantImage == null)
                return;

            var copyGo = Instantiate(indicator.gameObject, indicator.transform.parent);
            copyGo.name = "IndicatorFlyCopy";
            var copyRelay = copyGo.GetComponent<EnchantWheelBaseClick>();
            if (copyRelay != null)
                Destroy(copyRelay);
            var copyImg = copyGo.GetComponent<Image>();
            if (copyImg != null)
            {
                copyImg.enabled = true;
                copyImg.raycastTarget = false;
            }
            var copyRt = copyGo.transform as RectTransform;
            if (copyRt != null)
                StartCoroutine(FlyToPlantRoutine(copyRt));
        }

        private IEnumerator FlyToPlantRoutine(RectTransform flyRt)
        {
            Vector3 startWorld = flyRt.position;
            Vector3 endWorld = plantImage.rectTransform.position;
            const float dur = 0.45f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                flyRt.position = Vector3.Lerp(startWorld, endWorld, k);
                flyRt.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.4f, k);
                yield return null;
            }
            Destroy(flyRt.gameObject);
        }

        private void PlayPlantFeedback()
        {
            if (plantImage == null)
                return;
            if (plantFeedbackRoutine != null)
                StopCoroutine(plantFeedbackRoutine);
            plantFeedbackRoutine = StartCoroutine(PlantFeedbackRoutine());
        }

        private IEnumerator PlantFeedbackRoutine()
        {
            var rt = plantImage.rectTransform;
            Vector3 baseScale = Vector3.one;
            Vector2 basePos = plantBasePos;
            const float dur = 0.5f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                // 放大缩小：先放大到 1.25 再回落。
                float scale = 1f + 0.25f * Mathf.Sin(k * Mathf.PI);
                rt.localScale = baseScale * scale;
                // 震动：随时间衰减的横向抖动。
                float shakeMag = 18f * (1f - k);
                float shake = Mathf.Sin(k * Mathf.PI * 12f) * shakeMag;
                rt.anchoredPosition = basePos + new Vector2(shake, 0f);
                yield return null;
            }
            rt.localScale = baseScale;
            rt.anchoredPosition = basePos;
            plantFeedbackRoutine = null;
        }

        private void ShowActivatedPlant(string tileId)
        {
            if (plantImage == null)
                return;

            Sprite sprite = ResolvePlantSprite(tileId);
            if (sprite != null)
            {
                plantImage.sprite = sprite;
                plantImage.color = Color.white;
                plantImage.preserveAspect = true;
                plantImage.enabled = true;
            }
            else
            {
                plantImage.enabled = false;
            }
            plantImage.rectTransform.localScale = Vector3.one;
            plantBasePos = plantImage.rectTransform.anchoredPosition;
        }

        // 胜利后把中上部植物切换为「变异待收获」外形（与 MutationOverlayView 图标一致）。
        private void ShowAwaitingHarvestAppearance(string mutationId)
        {
            if (plantImage == null)
                return;

            if (plantFeedbackRoutine != null)
            {
                StopCoroutine(plantFeedbackRoutine);
                plantFeedbackRoutine = null;
            }

            string resPath = ResMutationPetIcon;
            if (service != null && !string.IsNullOrEmpty(mutationId))
            {
                var mutation = service.GetMutation(mutationId);
                if (mutation != null && mutation.kind == MutationKind.Skill)
                    resPath = ResMutationSkillIcon;
            }

            var sprite = Resources.Load<Sprite>(resPath);
            if (sprite != null)
            {
                plantImage.sprite = sprite;
                plantImage.color = Color.white;
                plantImage.preserveAspect = true;
                plantImage.enabled = true;
            }
            plantImage.rectTransform.localScale = Vector3.one;
            plantImage.rectTransform.anchoredPosition = plantBasePos;
        }

        private Sprite ResolvePlantSprite(string tileId)
        {
            if (service == null || string.IsNullOrEmpty(tileId))
                return null;
            var tile = service.GetTileById(tileId);
            if (tile == null || string.IsNullOrEmpty(tile.plantInstanceId))
                return null;
            var plant = service.GetPlant(tile.plantInstanceId);
            if (plant == null)
                return null;
            var cfg = service.GetPlantConfig(plant.plantConfigId);
            if (cfg == null || cfg.appearanceSpriteIds == null || cfg.appearanceSpriteIds.Count == 0)
                return null;
            int idx = Mathf.Clamp(plant.appearanceNode - 1, 0, cfg.appearanceSpriteIds.Count - 1);
            return Resources.Load<Sprite>(cfg.appearanceSpriteIds[idx]);
        }

        private static float NormalizeAngle(float deg)
        {
            float a = Mathf.Repeat(deg, 360f);
            return a;
        }

        // 点击底座的回调（由 EnchantWheelBaseClick 转发）。
        internal void OnWheelBaseClicked()
        {
            if (roundActive)
                pointerStopRequested = true;
        }

        // ---- 绑定兜底：按子节点名查找 ----

        private void EnsureBindings()
        {
            if (background == null) background = FindImage("Background");
            if (plantImage == null) plantImage = FindImage("PlantImage");
            if (wheelRoot == null) wheelRoot = FindRect("Wheel");
            if (wheelBase == null) wheelBase = FindImage("WheelBase");
            if (pointer == null) pointer = FindRect("Pointer");
            if (indicator == null) indicator = FindImage("Indicator");
            if (resultPanel == null)
            {
                var rt = FindRect("ResultPanel");
                if (rt != null) resultPanel = rt.gameObject;
            }
            if (retryButton == null) retryButton = FindButton("RetryButton");
            if (abandonButton == null) abandonButton = FindButton("AbandonButton");
        }

        private Image FindImage(string childName)
        {
            var t = FindDeepChild(transform, childName);
            return t != null ? t.GetComponent<Image>() : null;
        }

        private RectTransform FindRect(string childName)
        {
            var t = FindDeepChild(transform, childName);
            return t as RectTransform;
        }

        private Button FindButton(string childName)
        {
            var t = FindDeepChild(transform, childName);
            return t != null ? t.GetComponent<Button>() : null;
        }

        private static Transform FindDeepChild(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == childName)
                    return c;
                var found = FindDeepChild(c, childName);
                if (found != null)
                    return found;
            }
            return null;
        }

        private void Awake()
        {
            if (modalRt == null)
                modalRt = transform as RectTransform;
            RegisterAsInstance();
        }

        private void OnDestroy()
        {
            if (service != null)
                service.OnMutationCreated -= HandleMutationCreated;
            if (Instance == this)
                Instance = null;
        }

        // ---- 预制件缺失时的简版回退构建 ----

        private static EnchantScreenView BuildFallback(RectTransform canvasRect)
        {
            var rootGo = new GameObject("EnchantScreen", typeof(RectTransform));
            var rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.SetParent(canvasRect, false);
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var view = rootGo.AddComponent<EnchantScreenView>();

            view.background = CreateStretchImage(rootRt, "Background", ResBackground);
            view.background.raycastTarget = true;

            view.plantImage = CreateCenteredImage(rootRt, "PlantImage", null, new Vector2(220f, 220f), new Vector2(0f, 520f));
            view.plantImage.enabled = false;

            var wheelGo = new GameObject("Wheel", typeof(RectTransform));
            view.wheelRoot = wheelGo.GetComponent<RectTransform>();
            view.wheelRoot.SetParent(rootRt, false);
            view.wheelRoot.anchorMin = new Vector2(0.5f, 0.5f);
            view.wheelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            view.wheelRoot.pivot = new Vector2(0.5f, 0.5f);
            view.wheelRoot.anchoredPosition = new Vector2(0f, -360f);
            view.wheelRoot.sizeDelta = new Vector2(640f, 640f);

            view.indicator = CreateCenteredImage(view.wheelRoot, "Indicator", ResIndicator, new Vector2(96f, 96f), Vector2.zero);
            view.indicator.raycastTarget = false;
            view.indicator.enabled = false;

            view.wheelBase = CreateCenteredImage(view.wheelRoot, "WheelBase", ResWheelBase, new Vector2(560f, 560f), Vector2.zero);
            view.wheelBase.raycastTarget = true;

            var pointerImg = CreateCenteredImage(view.wheelRoot, "Pointer", ResPointer, new Vector2(80f, 360f), Vector2.zero);
            pointerImg.raycastTarget = false;
            view.pointer = pointerImg.rectTransform;

            // 失败面板
            var panelGo = new GameObject("ResultPanel", typeof(RectTransform));
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.SetParent(rootRt, false);
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(720f, 420f);
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.8f);
            panelImg.raycastTarget = true;
            view.resultPanel = panelGo;

            view.retryButton = CreateButton(panelRt, "RetryButton", "重新挑战", new Vector2(0f, 60f), new Color(0.25f, 0.6f, 0.95f, 1f));
            view.abandonButton = CreateButton(panelRt, "AbandonButton", "放弃", new Vector2(0f, -90f), new Color(0.8f, 0.3f, 0.2f, 1f));

            return view;
        }

        private static Image CreateStretchImage(RectTransform parent, string name, string resPath)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            var sp = string.IsNullOrEmpty(resPath) ? null : Resources.Load<Sprite>(resPath);
            if (sp != null) { img.sprite = sp; img.preserveAspect = false; }
            else img.color = new Color(0.1f, 0.08f, 0.05f, 1f);
            return img;
        }

        private static Image CreateCenteredImage(RectTransform parent, string name, string resPath, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            var sp = string.IsNullOrEmpty(resPath) ? null : Resources.Load<Sprite>(resPath);
            if (sp != null) { img.sprite = sp; img.preserveAspect = true; }
            else img.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            return img;
        }

        private static Button CreateButton(RectTransform parent, string name, string label, Vector2 pos, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(320f, 110f);
            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var textGo = new GameObject("Label", typeof(RectTransform));
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.SetParent(rt, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = FarmGridView.LoadBuiltinFont();
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 40;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.raycastTarget = false;
            text.supportRichText = false;
            return btn;
        }
    }

    /// <summary>转盘底座点击转发：点击立即停指针。</summary>
    public class EnchantWheelBaseClick : MonoBehaviour, IPointerClickHandler
    {
        private EnchantScreenView owner;

        public void Bind(EnchantScreenView view)
        {
            owner = view;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (owner != null)
                owner.OnWheelBaseClicked();
        }
    }
}
