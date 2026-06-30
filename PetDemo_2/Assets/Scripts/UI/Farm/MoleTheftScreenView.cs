// SPEC §9.12（v3.52）：「打地鼠」全屏演示界面。
// 触发：TileSlotView 检测到 tile.moleTheft == AwaitingMoleTheft 后调用 Instance.Open(tileId)。
// 结构：WH_Game_Tou 铺满背景 + 2s 延迟显示「胜利」按钮（中下部）。
// 关闭：点「胜利」→ CompleteMoleTheft(tileId) → SetActive(false) → WheelLotteryScreenView.Open，不切换底栏 Tab。
using System.Collections;
using PetDemo.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Farm
{
    [DisallowMultipleComponent]
    public class MoleTheftScreenView : MonoBehaviour
    {
        private const string ResGameBackground = "AirUI/WH_Game_Tou";
        private const float VictoryDelaySec = 2f;

        private static readonly Vector2 VictoryButtonPos = new Vector2(0f, -520f);
        private static readonly Vector2 VictoryButtonSize = new Vector2(280f, 110f);

        public static MoleTheftScreenView Instance { get; private set; }

        private RectTransform modalRt;
        private Button victoryButton;
        private string currentTileId;
        private Coroutine victoryDelayRoutine;

        public static MoleTheftScreenView BuildInto(RectTransform canvasRect, IPlantingService svc)
        {
            if (canvasRect == null)
                return null;

            var modalGo = new GameObject("MoleTheftModal", typeof(RectTransform));
            var modalRt = modalGo.GetComponent<RectTransform>();
            modalRt.SetParent(canvasRect, false);
            modalRt.anchorMin = Vector2.zero;
            modalRt.anchorMax = Vector2.one;
            modalRt.offsetMin = Vector2.zero;
            modalRt.offsetMax = Vector2.zero;
            modalGo.SetActive(false);

            var bgGo = new GameObject("MoleGameBackground", typeof(RectTransform));
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.SetParent(modalRt, false);
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.raycastTarget = true;

            var bgSprite = Resources.Load<Sprite>(ResGameBackground);
            if (bgSprite != null)
            {
                bgImage.sprite = bgSprite;
                bgImage.preserveAspect = false;
            }
            else
            {
                bgImage.color = new Color(0.20f, 0.12f, 0.05f, 1f);
                UnityEngine.Debug.LogWarning("[MoleTheftScreenView] 缺少背景图 Resources/" + ResGameBackground + "，回退为深色底。");
            }

            var view = modalGo.AddComponent<MoleTheftScreenView>();
            view.modalRt = modalRt;

            var btnGo = new GameObject("VictoryButton", typeof(RectTransform));
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.SetParent(modalRt, false);
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = VictoryButtonPos;
            btnRt.sizeDelta = VictoryButtonSize;

            var btnImage = btnGo.AddComponent<Image>();
            btnImage.color = new Color(0.20f, 0.75f, 0.20f, 1f);
            var btn = btnGo.AddComponent<Button>();
            var cs = btn.colors;
            cs.highlightedColor = new Color(0.30f, 0.90f, 0.30f, 1f);
            cs.pressedColor = new Color(0.15f, 0.55f, 0.15f, 1f);
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
            text.text = "胜利";
            text.alignment = TextAnchor.MiddleCenter;
            text.font = FarmGridView.LoadBuiltinFont();
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 48;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.raycastTarget = false;
            text.supportRichText = false;

            btnGo.SetActive(false);
            view.victoryButton = btn;
            btn.onClick.AddListener(view.OnVictoryClicked);

            view.RegisterAsInstance();
            return view;
        }

        public static MoleTheftScreenView Resolve()
        {
            if (Instance != null)
                return Instance;
            return Object.FindObjectOfType<MoleTheftScreenView>(true);
        }

        public void RegisterAsInstance()
        {
            if (Instance != null && Instance != this)
            {
                UnityEngine.Debug.LogWarning("[MoleTheftScreenView] 发现重复实例，销毁多余的。");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Open(string tileId)
        {
            if (modalRt == null)
            {
                UnityEngine.Debug.LogError("[MoleTheftScreenView] Open 失败：modalRt 未初始化。");
                return;
            }

            currentTileId = tileId;

            modalRt.gameObject.SetActive(true);
            modalRt.SetAsLastSibling();

            victoryButton?.gameObject.SetActive(false);

            if (victoryDelayRoutine != null)
                StopCoroutine(victoryDelayRoutine);
            victoryDelayRoutine = StartCoroutine(ShowVictoryAfterDelay());
        }

        public void Close()
        {
            if (victoryDelayRoutine != null)
            {
                StopCoroutine(victoryDelayRoutine);
                victoryDelayRoutine = null;
            }
            victoryButton?.gameObject.SetActive(false);
            modalRt.gameObject.SetActive(false);
        }

        private IEnumerator ShowVictoryAfterDelay()
        {
            yield return new WaitForSeconds(VictoryDelaySec);
            victoryButton?.gameObject.SetActive(true);
            victoryDelayRoutine = null;
        }

        private void OnVictoryClicked()
        {
            var svc = PlantingService.Instance;
            if (svc != null && !string.IsNullOrEmpty(currentTileId))
            {
                bool ok = svc.CompleteMoleTheft(currentTileId);
                if (!ok)
                    UnityEngine.Debug.LogWarning("[MoleTheftScreenView] CompleteMoleTheft 失败，tileId=" + currentTileId);
                else
                {
                    Close();
                    WheelLotteryScreenView.Resolve()?.Open(currentTileId);
                    return;
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("[MoleTheftScreenView] PlantingService.Instance 为空或 tileId 为空。");
            }
            Close();
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
