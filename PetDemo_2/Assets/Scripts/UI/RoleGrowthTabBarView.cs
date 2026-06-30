// SPEC §9.10：「角色」界面顶部页签栏；3 槽位互斥、等宽 TabSlotWidth，复用 BottomNavButtonView 两态子树（仅切显隐，槽宽不变）。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public class RoleGrowthTabBarView : MonoBehaviour
    {
        public const float BarWidth = 956f;
        public const float BarHeight = 119;
        public const int ExpectedTabCount = 4;
        public const float TabSlotWidth = BarWidth / ExpectedTabCount;

        [SerializeField] private int defaultOpenIndex;
        [SerializeField] private List<BottomNavButtonView> tabButtons = new List<BottomNavButtonView>(ExpectedTabCount);

        public event Action<int, string> OnTabChanged;

        public int OpenTabIndex { get; private set; } = -1;

        public string OpenTabKey
        {
            get
            {
                if (tabButtons == null) return null;
                if (OpenTabIndex < 0 || OpenTabIndex >= tabButtons.Count) return null;
                var btn = tabButtons[OpenTabIndex];
                return btn != null ? btn.Key : null;
            }
        }

        public int TabCount => tabButtons != null ? tabButtons.Count : 0;

        private bool subscribed;
        private bool started;

        // 与 BottomNavBarView 一致：Start 订阅，避免实例化后立刻隐藏层时 OnDisable 取消 Awake 订阅。
        private void Start()
        {
            SubscribeTabs();
            int idx = OpenTabIndex >= 0
                ? OpenTabIndex
                : Mathf.Clamp(defaultOpenIndex, 0, Mathf.Max(0, TabCount - 1));
            ApplyOpenIndex(idx, fireEvent: true, forceApply: true);
            started = true;
        }

        private void OnEnable()
        {
            if (!started)
                return;
            SubscribeTabs();
            ApplyOpenIndex(OpenTabIndex >= 0 ? OpenTabIndex : Mathf.Clamp(defaultOpenIndex, 0, Mathf.Max(0, TabCount - 1)),
                fireEvent: false, forceApply: true);
        }

        private void OnDisable()
        {
            UnsubscribeTabs();
        }

        private void OnDestroy()
        {
            UnsubscribeTabs();
        }

        public void SetOpenTabIndex(int index)
        {
            ApplyOpenIndex(index, fireEvent: true, forceApply: false);
        }

        /// <summary>切换到底栏「角色」时重置为属性页；即使目标索引未变也重算布局并派发事件。</summary>
        public void ForceSetOpenTabIndex(int index)
        {
            ApplyOpenIndex(index, fireEvent: true, forceApply: true);
        }

        private void ApplyOpenIndex(int index, bool fireEvent, bool forceApply)
        {
            if (tabButtons == null || tabButtons.Count == 0)
                return;
            int clamped = Mathf.Clamp(index, 0, tabButtons.Count - 1);
            int prev = OpenTabIndex;
            if (!forceApply && prev == clamped)
                return;

            OpenTabIndex = clamped;

            for (int i = 0; i < tabButtons.Count; i++)
            {
                var btn = tabButtons[i];
                bool isOpen = (i == clamped);
                if (btn != null)
                {
                    EnsureTabSlotLayout(btn.SelfRectTransform);
                    btn.ApplyState(isOpen, TabSlotWidth, i * TabSlotWidth);
                }
            }

            if (fireEvent && (forceApply || prev != clamped))
            {
                var key = OpenTabKey;
                OnTabChanged?.Invoke(clamped, key);
            }
        }

        private void SubscribeTabs()
        {
            if (subscribed || tabButtons == null)
                return;
            for (int i = 0; i < tabButtons.Count; i++)
            {
                var btn = tabButtons[i];
                if (btn == null)
                    continue;
                btn.OnClicked += HandleTabClicked;
            }
            subscribed = true;
        }

        private void UnsubscribeTabs()
        {
            if (!subscribed || tabButtons == null)
                return;
            for (int i = 0; i < tabButtons.Count; i++)
            {
                var btn = tabButtons[i];
                if (btn == null)
                    continue;
                btn.OnClicked -= HandleTabClicked;
            }
            subscribed = false;
        }

        private static void EnsureTabSlotLayout(RectTransform rt)
        {
            if (rt == null)
                return;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
        }

        private void HandleTabClicked(BottomNavButtonView btn)
        {
            if (btn == null || tabButtons == null)
                return;
            int idx = tabButtons.IndexOf(btn);
            if (idx < 0)
                return;
            if (idx == OpenTabIndex)
                return;
            ApplyOpenIndex(idx, fireEvent: true, forceApply: false);
        }
    }
}
