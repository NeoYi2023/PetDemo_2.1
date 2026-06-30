// SPEC §9.11（v3.64）：「灭虫」全屏小游戏界面。
using System.Collections;
using System.Collections.Generic;
using PetDemo.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Farm
{
    [DisallowMultipleComponent]
    public class PestControlScreenView : MonoBehaviour
    {
        private const int VictoryTurnThreshold = PestControlGameModel.VictoryTurnThreshold;

        private static readonly Vector2 OverlayButtonSize = new Vector2(280f, 110f);
        private static readonly Vector2 GiveUpButtonSize = new Vector2(200f, 80f);
        private static readonly Vector2 GiveUpButtonPos = new Vector2(24f, 24f);
        private static readonly Vector2 HudKillScoreTextPos = new Vector2(0f, 880f);
        private static readonly Vector2 HudTurnTextPos = new Vector2(0f, 820f);
        private static readonly Vector2 HudCountTextPos = new Vector2(0f, 760f);
        private static readonly Vector2 VictoryButtonPos = new Vector2(0f, -640f);
        private static readonly Vector2 DefeatLabelPos = new Vector2(0f, -480f);
        private static readonly Vector2 DefeatRetryButtonPos = new Vector2(-160f, -640f);
        private static readonly Vector2 DefeatCloseButtonPos = new Vector2(160f, -640f);
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.65f);

        public static PestControlScreenView Instance { get; private set; }

        private RectTransform modalRt;
        private Text hudKillScoreText;
        private Text hudTurnText;
        private Text hudCountText;
        private PestControlGridView gridView;
        private PestControlSwipeInput swipeInput;
        private RectTransform victoryOverlay;
        private Button victoryButton;
        private RectTransform defeatOverlay;
        private Button retryButton;
        private Button closeButton;
        private Button giveUpButton;

        private PestControlGameModel gameModel;
        private List<PestControlSpawnEntry> spawnConfig;
        private string currentTileId;
        private int sessionRequiredKillScore;
        private bool isAnimating;
        private Coroutine swipeAnimRoutine;

        public bool CanAcceptInput => !isAnimating && modalRt != null && modalRt.gameObject.activeSelf;

        public static PestControlScreenView BuildInto(RectTransform canvasRect, IPlantingService svc)
        {
            if (canvasRect == null)
                return null;

            var modalGo = new GameObject("PestControlModal", typeof(RectTransform));
            var modalRt = modalGo.GetComponent<RectTransform>();
            modalRt.SetParent(canvasRect, false);
            modalRt.anchorMin = Vector2.zero;
            modalRt.anchorMax = Vector2.one;
            modalRt.offsetMin = Vector2.zero;
            modalRt.offsetMax = Vector2.zero;
            modalGo.SetActive(false);

            var dimGo = new GameObject("DimOverlay", typeof(RectTransform));
            var dimRt = dimGo.GetComponent<RectTransform>();
            dimRt.SetParent(modalRt, false);
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = Vector2.zero;
            dimRt.offsetMax = Vector2.zero;
            var dimImage = dimGo.AddComponent<Image>();
            dimImage.color = DimColor;
            dimImage.raycastTarget = true;

            var view = modalGo.AddComponent<PestControlScreenView>();
            view.modalRt = modalRt;
            view.spawnConfig = PestControlConfigCatalog.LoadSpawnEntriesFromCsv();
            view.gameModel = new PestControlGameModel();

            view.hudKillScoreText = view.CreateHudText(modalRt, "HudKillScoreText", HudKillScoreTextPos, 42);
            view.hudTurnText = view.CreateHudText(modalRt, "HudTurnText", HudTurnTextPos, 30);
            view.hudCountText = view.CreateHudText(modalRt, "HudCountText", HudCountTextPos, 30);

            view.gridView = PestControlGridView.BuildInto(modalRt);

            float gridTotal = PestControlGridView.GridTotalSize;
            var swipeGo = new GameObject("SwipeInput", typeof(RectTransform));
            var swipeRt = swipeGo.GetComponent<RectTransform>();
            swipeRt.SetParent(modalRt, false);
            swipeRt.anchorMin = new Vector2(0.5f, 0.5f);
            swipeRt.anchorMax = new Vector2(0.5f, 0.5f);
            swipeRt.pivot = new Vector2(0.5f, 0.5f);
            swipeRt.sizeDelta = new Vector2(gridTotal, gridTotal);
            swipeRt.anchoredPosition = PestControlGridView.GridAnchoredPosition;
            var swipeImage = swipeGo.AddComponent<Image>();
            swipeImage.color = new Color(0f, 0f, 0f, 0.01f);
            swipeImage.raycastTarget = true;
            view.swipeInput = swipeGo.AddComponent<PestControlSwipeInput>();
            view.swipeInput.Init(view);

            view.giveUpButton = view.CreateCornerButton(modalRt, "GiveUpButton", "放弃", GiveUpButtonPos, GiveUpButtonSize,
                new Color(0.55f, 0.22f, 0.22f, 1f));
            view.giveUpButton.onClick.AddListener(view.OnGiveUpClicked);
            view.giveUpButton.gameObject.SetActive(false);

            view.victoryOverlay = view.CreateOverlay(modalRt, "VictoryOverlay", out view.victoryButton, "胜利", VictoryButtonPos);
            view.victoryButton.onClick.AddListener(view.OnVictoryClicked);

            view.defeatOverlay = view.CreateOverlay(modalRt, "DefeatOverlay", out view.retryButton, "重试", DefeatRetryButtonPos);
            view.retryButton.onClick.AddListener(view.OnRetryClicked);
            view.closeButton = view.CreateOverlayButton(view.defeatOverlay, "CloseButton", "关闭", DefeatCloseButtonPos);
            view.closeButton.onClick.AddListener(view.OnDefeatCloseClicked);
            view.CreateOverlayLabel(view.defeatOverlay, "狼人被吃掉了", DefeatLabelPos);
            view.defeatOverlay.gameObject.SetActive(false);
            view.victoryOverlay.gameObject.SetActive(false);

            view.RegisterAsInstance();
            return view;
        }

        private Text CreateHudText(RectTransform parent, string name, Vector2 pos, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(900f, 60f);
            var text = go.AddComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            text.font = FarmGridView.LoadBuiltinFont();
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private RectTransform CreateOverlay(RectTransform parent, string name, out Button primaryButton, string primaryLabel, Vector2 primaryPos)
        {
            var overlayGo = new GameObject(name, typeof(RectTransform));
            var overlayRt = overlayGo.GetComponent<RectTransform>();
            overlayRt.SetParent(parent, false);
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.offsetMin = Vector2.zero;
            overlayRt.offsetMax = Vector2.zero;

            var dim = overlayGo.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.35f);
            dim.raycastTarget = true;

            primaryButton = CreateOverlayButton(overlayRt, "PrimaryButton", primaryLabel, primaryPos);
            return overlayRt;
        }

        private Button CreateOverlayButton(RectTransform parent, string name, string label, Vector2 pos)
        {
            var btnGo = new GameObject(name, typeof(RectTransform));
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.SetParent(parent, false);
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = pos;
            btnRt.sizeDelta = OverlayButtonSize;

            var btnImage = btnGo.AddComponent<Image>();
            btnImage.color = new Color(0.20f, 0.75f, 0.20f, 1f);
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImage;

            var textGo = new GameObject("Label", typeof(RectTransform));
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.SetParent(btnRt, false);
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
            text.fontSize = 44;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.raycastTarget = false;
            return btn;
        }

        private Button CreateCornerButton(RectTransform parent, string name, string label, Vector2 pos, Vector2 size, Color bgColor)
        {
            var btnGo = new GameObject(name, typeof(RectTransform));
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.SetParent(parent, false);
            btnRt.anchorMin = Vector2.zero;
            btnRt.anchorMax = Vector2.zero;
            btnRt.pivot = Vector2.zero;
            btnRt.anchoredPosition = pos;
            btnRt.sizeDelta = size;

            var btnImage = btnGo.AddComponent<Image>();
            btnImage.color = bgColor;
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImage;

            var textGo = new GameObject("Label", typeof(RectTransform));
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.SetParent(btnRt, false);
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
            text.fontSize = 36;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.raycastTarget = false;
            return btn;
        }

        private void CreateOverlayLabel(RectTransform parent, string label, Vector2 pos)
        {
            var go = new GameObject("OverlayLabel", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(900f, 80f);
            var text = go.AddComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = FarmGridView.LoadBuiltinFont();
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 48;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        public static PestControlScreenView Resolve()
        {
            if (Instance != null)
                return Instance;
            return Object.FindObjectOfType<PestControlScreenView>(true);
        }

        public void RegisterAsInstance()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PestControlScreenView] 发现重复实例，销毁多余的。");
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void Open(string tileId)
        {
            if (modalRt == null)
            {
                Debug.LogError("[PestControlScreenView] Open 失败：modalRt 未初始化。");
                return;
            }

            currentTileId = tileId;
            sessionRequiredKillScore = ComputeRequiredKillScore();
            modalRt.gameObject.SetActive(true);
            modalRt.SetAsLastSibling();
            StartNewGame();
        }

        public void Close()
        {
            victoryOverlay?.gameObject.SetActive(false);
            defeatOverlay?.gameObject.SetActive(false);
            giveUpButton?.gameObject.SetActive(false);
            modalRt.gameObject.SetActive(false);
        }

        internal void HandleSwipe(PestControlSwipeDirection direction)
        {
            if (!CanAcceptInput || gameModel == null || gameModel.Result != PestControlGameResult.Playing)
                return;

            if (!gameModel.TryPrepareSwipe(direction, out var plan))
                return;

            if (swipeAnimRoutine != null)
                StopCoroutine(swipeAnimRoutine);
            isAnimating = true;
            swipeAnimRoutine = StartCoroutine(AnimateSwipeRoutine(plan));
        }

        private IEnumerator AnimateSwipeRoutine(PestControlSwipePlan plan)
        {
            if (gridView != null)
            {
                bool done = false;
                gridView.PlaySwipePlan(plan, () => done = true);
                while (!done)
                    yield return null;
            }

            gameModel.CommitSwipeAndSpawn(plan);
            RefreshPresentation();

            isAnimating = false;
            swipeAnimRoutine = null;

            RefreshGiveUpButtonVisibility();

            if (gameModel.Result == PestControlGameResult.Victory)
                victoryOverlay?.gameObject.SetActive(true);
            else if (gameModel.Result == PestControlGameResult.Defeat)
                defeatOverlay?.gameObject.SetActive(true);
        }

        private void StartNewGame()
        {
            if (swipeAnimRoutine != null)
            {
                StopCoroutine(swipeAnimRoutine);
                swipeAnimRoutine = null;
            }

            isAnimating = false;
            victoryOverlay?.gameObject.SetActive(false);
            defeatOverlay?.gameObject.SetActive(false);
            gameModel.Reset(spawnConfig, sessionRequiredKillScore);
            gridView?.SyncFromModel(gameModel);
            RefreshHudOnly();
            RefreshGiveUpButtonVisibility();
        }

        private void RefreshPresentation()
        {
            gridView?.SyncFromModel(gameModel);
            RefreshHudOnly();
        }

        private void RefreshHudOnly()
        {
            if (hudKillScoreText != null)
                hudKillScoreText.text = $"击杀分数: {gameModel.KillScore} / {gameModel.RequiredKillScore}";
            if (hudTurnText != null)
                hudTurnText.text = $"回合: {gameModel.Turn} / {VictoryTurnThreshold}";
            if (hudCountText != null)
            {
                hudCountText.text =
                    $"虫子: {gameModel.CountEntity(PestControlEntityType.Bug)}  狼人: {gameModel.CountEntity(PestControlEntityType.Werewolf)}";
            }
        }

        private int ComputeRequiredKillScore()
        {
            var svc = PlantingService.Instance;
            int pestCount = svc != null ? svc.CountAwaitingPestControlTiles() : 0;
            return pestCount * PestControlGameModel.VictoryKillScorePerPestIcon;
        }

        private void RefreshGiveUpButtonVisibility()
        {
            if (giveUpButton == null || gameModel == null)
                return;

            bool show = gameModel.Result == PestControlGameResult.Playing
                        && modalRt != null
                        && modalRt.gameObject.activeSelf;
            giveUpButton.gameObject.SetActive(show);
        }

        private void OnGiveUpClicked()
        {
            if (swipeAnimRoutine != null)
            {
                StopCoroutine(swipeAnimRoutine);
                swipeAnimRoutine = null;
            }

            isAnimating = false;
            victoryOverlay?.gameObject.SetActive(false);
            defeatOverlay?.gameObject.SetActive(false);
            Close();
        }

        private void OnVictoryClicked()
        {
            var svc = PlantingService.Instance;
            if (svc != null && !string.IsNullOrEmpty(currentTileId))
            {
                // SPEC §9.11/§4.1.6 (v3.80)：一次胜利清除农田内所有虫灾。
                int cleared = svc.CompleteAllPestControl();
                if (cleared <= 0)
                    Debug.LogWarning("[PestControlScreenView] CompleteAllPestControl 未清除任何田格，tileId=" + currentTileId);
                else
                {
                    Close();
                    WheelLotteryScreenView.Resolve()?.Open(currentTileId, grantMutationOnConfirm: false);
                    return;
                }
            }
            else
            {
                Debug.LogWarning("[PestControlScreenView] PlantingService.Instance 为空或 tileId 为空。");
            }

            Close();
        }

        private void OnRetryClicked()
        {
            StartNewGame();
        }

        private void OnDefeatCloseClicked()
        {
            Close();
        }

        private void Update()
        {
            if (!CanAcceptInput || gameModel == null || gameModel.Result != PestControlGameResult.Playing)
                return;

            if (Input.GetKeyDown(KeyCode.UpArrow))
                HandleSwipe(PestControlSwipeDirection.Up);
            else if (Input.GetKeyDown(KeyCode.DownArrow))
                HandleSwipe(PestControlSwipeDirection.Down);
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
                HandleSwipe(PestControlSwipeDirection.Left);
            else if (Input.GetKeyDown(KeyCode.RightArrow))
                HandleSwipe(PestControlSwipeDirection.Right);
        }

        private void Awake()
        {
            RegisterAsInstance();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
