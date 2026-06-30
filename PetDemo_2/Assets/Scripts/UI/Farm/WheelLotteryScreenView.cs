// SPEC §9.13（v3.59）：转盘抽奖全屏层；在打虫子 / 打地鼠胜利后打开。
// 第 1 次点击「选择摇奖」→ JL_ZhuanPan_3 旋转 1520~2830° / 3s；第 2 次点击「确定」→ 若 Open(..., grantMutationOnConfirm:true) 则 TriggerSingleTileMutation（捉虫流为 false，无变异奖励）。
using System.Collections;
using PetDemo.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Farm
{
    [DisallowMultipleComponent]
    public class WheelLotteryScreenView : MonoBehaviour
    {
        private const string ResWheel1 = "AirUI/JL_ZhuanPan_1";
        private const string ResWheel2 = "AirUI/JL_ZhuanPan_2";
        private const string ResWheel3 = "AirUI/JL_ZhuanPan_3";
        private const float SpinDurationSec = 3f;
        private const float WheelSize = 600f;

        private static readonly Vector2 ActionButtonPos = new Vector2(0f, -520f);
        private static readonly Vector2 ActionButtonSize = new Vector2(280f, 110f);

        public static WheelLotteryScreenView Instance { get; private set; }

        private RectTransform modalRt;
        private RectTransform wheelLayer3Rt;
        private Button actionButton;
        private Text actionButtonLabel;
        private string currentTileId;
        /// <summary>为 false 时（§9.11 捉虫入口）点击「确定」仅关窗，不调用 TriggerSingleTileMutation。</summary>
        private bool grantMutationOnConfirm = true;
        private bool spinning;
        private bool spinCompleted;
        private Coroutine spinRoutine;

        public static WheelLotteryScreenView BuildInto(RectTransform canvasRect, IPlantingService svc)
        {
            if (canvasRect == null)
                return null;

            var modalGo = new GameObject("WheelLotteryModal", typeof(RectTransform));
            var modalRt = modalGo.GetComponent<RectTransform>();
            modalRt.SetParent(canvasRect, false);
            modalRt.anchorMin = Vector2.zero;
            modalRt.anchorMax = Vector2.one;
            modalRt.offsetMin = Vector2.zero;
            modalRt.offsetMax = Vector2.zero;
            modalGo.SetActive(false);

            var dimGo = new GameObject("DimBackground", typeof(RectTransform));
            var dimRt = dimGo.GetComponent<RectTransform>();
            dimRt.SetParent(modalRt, false);
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = Vector2.zero;
            dimRt.offsetMax = Vector2.zero;
            var dimImg = dimGo.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.55f);
            dimImg.raycastTarget = true;

            RectTransform layer3 = CreateWheelLayer(modalRt, "WheelLayer3", ResWheel3);
            RectTransform layer2 = CreateWheelLayer(modalRt, "WheelLayer2", ResWheel2);
            RectTransform layer1 = CreateWheelLayer(modalRt, "WheelLayer1", ResWheel1);

            var btnGo = new GameObject("ActionButton", typeof(RectTransform));
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.SetParent(modalRt, false);
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = ActionButtonPos;
            btnRt.sizeDelta = ActionButtonSize;

            var btnImage = btnGo.AddComponent<Image>();
            btnImage.color = new Color(0.25f, 0.55f, 0.95f, 1f);
            var btn = btnGo.AddComponent<Button>();
            var cs = btn.colors;
            cs.highlightedColor = new Color(0.35f, 0.65f, 1f, 1f);
            cs.pressedColor = new Color(0.15f, 0.40f, 0.75f, 1f);
            btn.colors = cs;
            btn.targetGraphic = btnImage;

            var textGo = new GameObject("Label", typeof(RectTransform));
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.SetParent(btnRt, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<Text>();
            text.text = "选择摇奖";
            text.alignment = TextAnchor.MiddleCenter;
            text.font = FarmGridView.LoadBuiltinFont();
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 36;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.raycastTarget = false;
            text.supportRichText = false;

            var view = modalGo.AddComponent<WheelLotteryScreenView>();
            view.modalRt = modalRt;
            view.wheelLayer3Rt = layer3;
            view.actionButton = btn;
            view.actionButtonLabel = text;
            btn.onClick.AddListener(view.OnActionButtonClicked);

            view.RegisterAsInstance();
            return view;
        }

        private static RectTransform CreateWheelLayer(RectTransform parent, string name, string resPath)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(WheelSize, WheelSize);

            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            var sp = Resources.Load<Sprite>(resPath);
            if (sp != null)
            {
                img.sprite = sp;
                img.preserveAspect = true;
            }
            else
            {
                img.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
                UnityEngine.Debug.LogWarning("[WheelLotteryScreenView] 缺少转盘素材 Resources/" + resPath);
            }

            return rt;
        }

        public static WheelLotteryScreenView Resolve()
        {
            if (Instance != null)
                return Instance;
            return Object.FindObjectOfType<WheelLotteryScreenView>(true);
        }

        public void RegisterAsInstance()
        {
            if (Instance != null && Instance != this)
            {
                UnityEngine.Debug.LogWarning("[WheelLotteryScreenView] 发现重复实例，销毁多余的。");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <param name="tileId">关联农田格</param>
        /// <param name="grantMutationOnConfirm">false：仅保留转盘表现，确定后不写入单格变异（§9.11 捉虫产品配置）</param>
        public void Open(string tileId, bool grantMutationOnConfirm = true)
        {
            if (modalRt == null)
            {
                UnityEngine.Debug.LogError("[WheelLotteryScreenView] Open 失败：modalRt 未初始化。");
                return;
            }

            currentTileId = tileId;
            this.grantMutationOnConfirm = grantMutationOnConfirm;
            spinning = false;
            spinCompleted = false;

            if (wheelLayer3Rt != null)
                wheelLayer3Rt.localRotation = Quaternion.identity;

            if (actionButtonLabel != null)
                actionButtonLabel.text = "选择摇奖";
            if (actionButton != null)
                actionButton.interactable = true;

            if (spinRoutine != null)
            {
                StopCoroutine(spinRoutine);
                spinRoutine = null;
            }

            modalRt.gameObject.SetActive(true);
            modalRt.SetAsLastSibling();
        }

        public void Close()
        {
            if (spinRoutine != null)
            {
                StopCoroutine(spinRoutine);
                spinRoutine = null;
            }
            spinning = false;
            spinCompleted = false;
            if (modalRt != null)
                modalRt.gameObject.SetActive(false);
        }

        private void OnActionButtonClicked()
        {
            if (spinning)
                return;

            if (!spinCompleted)
            {
                float targetDeg = Random.Range(1520f, 2830f);
                if (spinRoutine != null)
                    StopCoroutine(spinRoutine);
                spinRoutine = StartCoroutine(SpinRoutine(targetDeg));
                return;
            }

            if (grantMutationOnConfirm)
            {
                var svc = PlantingService.Instance;
                if (svc != null && !string.IsNullOrEmpty(currentTileId))
                {
                    bool ok = svc.TriggerSingleTileMutation(currentTileId);
                    if (!ok)
                        UnityEngine.Debug.LogWarning("[WheelLotteryScreenView] TriggerSingleTileMutation 失败，tileId=" + currentTileId);
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[WheelLotteryScreenView] PlantingService.Instance 为空或 tileId 为空。");
                }
            }

            Close();
        }

        private IEnumerator SpinRoutine(float targetDeg)
        {
            spinning = true;
            if (actionButton != null)
                actionButton.interactable = false;

            float elapsed = 0f;
            float zEnd = -targetDeg;
            while (elapsed < SpinDurationSec)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / SpinDurationSec);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                float z = Mathf.Lerp(0f, zEnd, eased);
                if (wheelLayer3Rt != null)
                    wheelLayer3Rt.localRotation = Quaternion.Euler(0f, 0f, z);
                yield return null;
            }

            if (wheelLayer3Rt != null)
                wheelLayer3Rt.localEulerAngles = new Vector3(0f, 0f, zEnd);

            spinning = false;
            spinCompleted = true;
            if (actionButton != null)
                actionButton.interactable = true;
            if (actionButtonLabel != null)
                actionButtonLabel.text = "确定";

            spinRoutine = null;
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
