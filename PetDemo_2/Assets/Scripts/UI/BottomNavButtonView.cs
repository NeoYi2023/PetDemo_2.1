// SPEC §9.8 / §9.8.3 / §9.8.4：主界面底部一级导航切换栏的单按钮 View。
// 每个按钮持有 OpenState / ClosedState 两组子节点 + 一个 HitArea Button，
// ApplyState 由父级 BottomNavBarView 调用：宽度由父级根据当前打开索引推导，
// X 坐标由父级按左对齐前缀和写入；自身只负责宽度切换与子节点显隐。
using System;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public class BottomNavButtonView : MonoBehaviour
    {
        [SerializeField] private string key;
        [SerializeField] private RectTransform selfRt;
        [SerializeField] private RectTransform openState;
        [SerializeField] private RectTransform closedState;
        [SerializeField] private Button hitButton;

        public string Key => key;
        public RectTransform SelfRectTransform
        {
            get
            {
                EnsureSelfRt();
                return selfRt;
            }
        }

        public event Action<BottomNavButtonView> OnClicked;

        private bool wired;

        private void Awake()
        {
            EnsureSelfRt();
            WireButton();
        }

        private void OnEnable()
        {
            EnsureSelfRt();
            WireButton();
        }

        // SPEC §9.8.5：纯代码回退路径下 AddComponent 与字段注入存在时序差；Start 再次补连接。
        private void Start()
        {
            EnsureSelfRt();
            WireButton();
        }

        private void OnDestroy()
        {
            if (hitButton != null && wired)
            {
                hitButton.onClick.RemoveListener(HandleClick);
                wired = false;
            }
        }

        private void EnsureSelfRt()
        {
            if (selfRt == null)
                selfRt = transform as RectTransform;
        }

        private void WireButton()
        {
            if (wired || hitButton == null)
                return;
            hitButton.onClick.AddListener(HandleClick);
            wired = true;
        }

        private void HandleClick()
        {
            OnClicked?.Invoke(this);
        }

        public void ApplyState(bool isOpen, float width, float x)
        {
            EnsureSelfRt();
            if (selfRt != null)
            {
                var size = selfRt.sizeDelta;
                size.x = width;
                selfRt.sizeDelta = size;

                var pos = selfRt.anchoredPosition;
                pos.x = x;
                pos.y = 0f;
                selfRt.anchoredPosition = pos;
            }
            if (openState != null)
                openState.gameObject.SetActive(isOpen);
            if (closedState != null)
                closedState.gameObject.SetActive(!isOpen);
        }
    }
}
