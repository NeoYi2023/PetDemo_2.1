// SPEC §9.4.6：仓库内播种触发按钮。
// 挂在 Canvas 根（不进入 modal 子树）以保证 IPointerDown 关闭 modal 后仍能接收 IDrag/IPointerUp。
// v3.95：优先实例化 SowActionButton.prefab，样式由预制体编辑。
using PetDemo.Farm;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PetDemo.UI.Farm
{
    public class SowActionButtonView : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public const string DefaultPrefabResPath = "Prefabs/Farm/SowActionButton";

        private static readonly Color BackgroundNormal = new Color(0.85f, 0.55f, 0.18f, 0.95f);
        private static readonly Color BackgroundPressed = new Color(0.70f, 0.42f, 0.12f, 0.95f);

        [SerializeField] private Image background;
        [SerializeField] private CanvasGroup canvasGroup;

        private IPlantingService service;
        private SowGestureController controller;
        private System.Action seedBagHandler;

        public static SowActionButtonView BuildInto(
            RectTransform canvasRect,
            IPlantingService svc,
            SowGestureController ctrl,
            RectTransform prefabOverride = null)
        {
            if (canvasRect == null || svc == null || ctrl == null)
                return null;

            var resolvedPrefab = prefabOverride != null
                ? prefabOverride
                : Resources.Load<RectTransform>(DefaultPrefabResPath);

            RectTransform buttonRoot;
            SowActionButtonView view;
            if (resolvedPrefab != null)
            {
                buttonRoot = Object.Instantiate(resolvedPrefab, canvasRect, false);
                buttonRoot.name = "SowActionButton";
                view = buttonRoot.GetComponent<SowActionButtonView>();
                if (view == null)
                    view = buttonRoot.gameObject.AddComponent<SowActionButtonView>();
                view.EnsureComponents(buttonRoot);
            }
            else
            {
                UnityEngine.Debug.LogWarning(
                    "SowActionButtonView: 未找到播种按钮预制体 Resources/" + DefaultPrefabResPath +
                    "，回退为运行时代码按钮。请执行 Tools/PetDemo/Generate Sow Action Button Prefab。");
                buttonRoot = BuildFallbackButton(canvasRect, out view);
            }

            view.service = svc;
            view.controller = ctrl;
            view.SubscribeEvents();
            ctrl.RegisterButton(view);
            view.RefreshVisibility();
            return view;
        }

        private void EnsureComponents(RectTransform buttonRoot)
        {
            if (background == null)
                background = buttonRoot.GetComponent<Image>();
            if (canvasGroup == null)
                canvasGroup = buttonRoot.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = buttonRoot.gameObject.AddComponent<CanvasGroup>();
        }

        private static RectTransform BuildFallbackButton(RectTransform canvasRect, out SowActionButtonView view)
        {
            var go = new GameObject(
                "SowActionButton",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Image),
                typeof(SowActionButtonView));

            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvasRect, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -680f);
            rt.sizeDelta = new Vector2(360f, 120f);

            var img = go.GetComponent<Image>();
            img.color = BackgroundNormal;
            img.raycastTarget = true;

            var labelGo = new GameObject("LabelText", typeof(RectTransform), typeof(Text));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var text = labelGo.GetComponent<Text>();
            text.text = "播种";
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = FarmGridView.LoadBuiltinFont();
            text.fontSize = 48;
            text.raycastTarget = false;

            view = go.GetComponent<SowActionButtonView>();
            view.background = img;
            view.canvasGroup = go.GetComponent<CanvasGroup>();
            return rt;
        }

        private void SubscribeEvents()
        {
            if (seedBagHandler != null && service != null)
                service.OnSeedBagChanged -= seedBagHandler;

            seedBagHandler = RefreshVisibility;
            if (service != null)
                service.OnSeedBagChanged += seedBagHandler;
        }

        private void OnDestroy()
        {
            if (service != null && seedBagHandler != null)
                service.OnSeedBagChanged -= seedBagHandler;
        }

        public void RefreshVisibility()
        {
            if (controller == null)
                return;
            if (controller.CurrentMode != SowGestureController.Mode.Idle)
                return;
            ApplyVisibility(controller.ShouldButtonBeVisible);
        }

        public void ApplyVisibility(bool visible)
        {
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
            if (visible)
                transform.SetAsLastSibling();
        }

        public void SetVisualPressed(bool pressed)
        {
            if (background != null)
                background.color = pressed ? BackgroundPressed : BackgroundNormal;
        }

        public void SetButtonHidden(bool hidden)
        {
            if (canvasGroup == null)
                return;
            canvasGroup.alpha = hidden ? 0f : 1f;
            canvasGroup.blocksRaycasts = !hidden;
            canvasGroup.interactable = !hidden;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (controller != null)
                controller.OnButtonPointerDown(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (controller != null)
                controller.OnButtonPointerUp(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (controller != null)
                controller.OnButtonBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (controller != null)
                controller.OnButtonDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (controller != null)
                controller.OnButtonEndDrag(eventData);
        }
    }
}
