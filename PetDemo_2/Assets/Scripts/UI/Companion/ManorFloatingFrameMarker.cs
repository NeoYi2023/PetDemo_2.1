// SPEC §9.8.19.2.2 / §9.8.19.6（v3.263；VisibilityChanged v3.264）：庄园场景悬浮框标记。
using System;
using UnityEngine;

namespace PetDemo.UI.Companion
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ManorFloatingFrameMarker : MonoBehaviour
    {
        public const float DefaultShowRadius = 300f;

        [SerializeField, Min(0f)] private float showRadius = DefaultShowRadius;
        [SerializeField] private RectTransform panelRt;

        private bool panelVisible;

        public RectTransform Rt => (RectTransform)transform;
        public float ShowRadius => showRadius;
        public bool IsPanelVisible => panelVisible;

        /// <summary>Panel 显隐真变化时触发（含 InitializeHidden / SetVisible）。</summary>
        public event Action<bool> VisibilityChanged;

        private void Awake()
        {
            ResolvePanel();
            SetVisible(false);
        }

        public void InitializeHidden()
        {
            ResolvePanel();
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            ResolvePanel();
            if (panelRt != null && panelRt.gameObject.activeSelf != visible)
                panelRt.gameObject.SetActive(visible);

            bool resolvedVisible = panelRt != null && panelRt.gameObject.activeSelf;
            if (panelVisible == resolvedVisible)
                return;

            panelVisible = resolvedVisible;
            VisibilityChanged?.Invoke(panelVisible);
        }

        public void SetPanel(RectTransform panel)
        {
            panelRt = panel;
        }

        private void ResolvePanel()
        {
            if (panelRt == null)
                panelRt = transform.Find("Panel") as RectTransform;
        }
    }
}
