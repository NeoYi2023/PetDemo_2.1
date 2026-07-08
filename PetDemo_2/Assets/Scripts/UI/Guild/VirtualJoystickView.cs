// SPEC §9.8.9.4 / §9.8.9.6：透明虚拟摇杆 — 平时不可见；按下点显示半透明底盘+手柄，
// 拖动输出方向向量（模长 0..1，死区 0.12），松手归零并隐藏。
// 触控层置于场景层之下（sibling 最前、alpha=0 仍接收射线），视觉层置于最顶且不拦截射线，
// 保证建筑/NPC 名牌按钮可点（SPEC §9.8.9.6 ④）。
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class VirtualJoystickView : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform visualLayerRt;
        [SerializeField] private RectTransform baseRt;
        [SerializeField] private RectTransform knobRt;
        [SerializeField] private float maxRadius = 170f;
        [SerializeField] private float deadZone = 0.12f;

        private bool pressed;
        private Vector2 pressLocalPos;
        private bool inputEnabled = true;

        /// <summary>当前方向（模长 0..1）；未按下或死区内为 zero。</summary>
        public Vector2 Direction { get; private set; }

        public bool IsActive => pressed;

        /// <summary>SPEC §9.8.9.12：全景模式等场景下禁用触控输入。</summary>
        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (!inputEnabled)
                ResetState();
        }

        public void Configure(RectTransform visualLayer, RectTransform joyBase, RectTransform knob)
        {
            visualLayerRt = visualLayer;
            baseRt = joyBase;
            knobRt = knob;
        }

        /// <summary>
        /// 在 screenRoot 下构建摇杆双层结构：触控层（首子节点）+ 视觉层（末子节点）。
        /// 供预制体生成器与运行时回退共用。
        /// </summary>
        public static VirtualJoystickView BuildJoystickInto(RectTransform screenRoot)
        {
            var touchRt = CreateStretch(screenRoot, "JoystickTouchLayer");
            touchRt.SetAsFirstSibling();
            var touchImg = touchRt.gameObject.AddComponent<Image>();
            touchImg.color = new Color(1f, 1f, 1f, 0f);
            touchImg.raycastTarget = true;

            var visualRt = CreateStretch(screenRoot, "JoystickVisualLayer");
            visualRt.SetAsLastSibling();

            var baseRt = CreateCentered(visualRt, "JoystickBase", new Vector2(220f, 220f));
            var baseImg = baseRt.gameObject.AddComponent<Image>();
            baseImg.color = new Color(1f, 1f, 1f, 0.16f);
            baseImg.raycastTarget = false;

            var knobRt = CreateCentered(baseRt, "JoystickKnob", new Vector2(90f, 90f));
            var knobImg = knobRt.gameObject.AddComponent<Image>();
            knobImg.color = new Color(1f, 1f, 1f, 0.38f);
            knobImg.raycastTarget = false;

            baseRt.gameObject.SetActive(false);

            var view = touchRt.gameObject.AddComponent<VirtualJoystickView>();
            view.Configure(visualRt, baseRt, knobRt);
            return view;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!inputEnabled || visualLayerRt == null)
                return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    visualLayerRt, eventData.position, eventData.pressEventCamera, out pressLocalPos))
                return;

            pressed = true;
            Direction = Vector2.zero;
            if (baseRt != null)
            {
                baseRt.anchoredPosition = pressLocalPos;
                baseRt.gameObject.SetActive(true);
            }
            UpdateKnob(Vector2.zero);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!inputEnabled || !pressed || visualLayerRt == null)
                return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    visualLayerRt, eventData.position, eventData.pressEventCamera, out var localPos))
                return;

            var delta = Vector2.ClampMagnitude(localPos - pressLocalPos, maxRadius);
            UpdateKnob(delta);

            var dir = delta / Mathf.Max(1f, maxRadius);
            Direction = dir.magnitude < deadZone ? Vector2.zero : dir;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ResetState();
        }

        private void OnDisable()
        {
            ResetState();
        }

        private void ResetState()
        {
            pressed = false;
            Direction = Vector2.zero;
            if (baseRt != null)
                baseRt.gameObject.SetActive(false);
        }

        private void UpdateKnob(Vector2 offsetFromBase)
        {
            if (knobRt != null)
                knobRt.anchoredPosition = offsetFromBase;
        }

        private static RectTransform CreateStretch(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        private static RectTransform CreateCentered(RectTransform parent, string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            return rt;
        }
    }
}
