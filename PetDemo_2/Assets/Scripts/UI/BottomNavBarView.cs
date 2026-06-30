// SPEC §9.8：主界面底部一级导航切换栏 / Bottom Primary Navigation Switch Bar。
// 5 个按钮（公会/角色/家园/主线/商店）左对齐并联排布，整体 1080×160。
// 互斥语义：任一时刻必有且仅有 1 个按钮处于 Open（364 宽），其余 4 个 Closed（179 宽）。
// 因 364 + 179×4 = 1080，5 个按钮在切换栏内恰好填满整行，无空隙无溢出（§9.8.1）。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public class BottomNavBarView : MonoBehaviour
    {
        public const float BarWidth = 1080f;
        public const float BarHeight = 160f;
        public const float OpenButtonWidth = 364f;
        public const float ClosedButtonWidth = 179f;
        public const int ExpectedButtonCount = 5;

        [SerializeField] private int defaultOpenIndex = 2;
        [SerializeField] private List<BottomNavButtonView> buttons = new List<BottomNavButtonView>(ExpectedButtonCount);

        public event Action<int, string> OnOpenChanged;

        public int OpenIndex { get; private set; } = -1;

        public string OpenKey
        {
            get
            {
                if (buttons == null) return null;
                if (OpenIndex < 0 || OpenIndex >= buttons.Count) return null;
                var btn = buttons[OpenIndex];
                return btn != null ? btn.Key : null;
            }
        }

        public int ButtonCount => buttons != null ? buttons.Count : 0;

        private bool subscribed;
        private bool started;

        private void Start()
        {
            SubscribeButtons();
            // SPEC §9.8：创角覆盖层期间 HUD 隐藏，Start 会延迟到 RestoreFromOverlay 首次 SetVisible(true) 之后。
            // 若此前已通过 SetOpenKey 写入目标 Tab，须保留 OpenIndex，不可再强制回落 defaultOpenIndex（JiaYuan）。
            int idx = OpenIndex >= 0
                ? OpenIndex
                : Mathf.Clamp(defaultOpenIndex, 0, Mathf.Max(0, ButtonCount - 1));
            ApplyOpenIndex(idx, fireEvent: true, forceApply: true);
            started = true;
        }

        private void OnEnable()
        {
            if (!started)
                return;
            SubscribeButtons();
            ApplyOpenIndex(OpenIndex >= 0 ? OpenIndex : Mathf.Clamp(defaultOpenIndex, 0, Mathf.Max(0, ButtonCount - 1)),
                fireEvent: false, forceApply: true);
        }

        private void OnDisable()
        {
            UnsubscribeButtons();
        }

        private void OnDestroy()
        {
            UnsubscribeButtons();
        }

        public void SetOpenIndex(int index)
        {
            ApplyOpenIndex(index, fireEvent: true, forceApply: false);
        }

        public void SetOpenKey(string key)
        {
            if (string.IsNullOrEmpty(key) || buttons == null)
                return;
            for (int i = 0; i < buttons.Count; i++)
            {
                var btn = buttons[i];
                if (btn != null && string.Equals(btn.Key, key, StringComparison.Ordinal))
                {
                    ApplyOpenIndex(i, fireEvent: true, forceApply: false);
                    return;
                }
            }
            UnityEngine.Debug.LogWarning("BottomNavBarView: SetOpenKey 未匹配到 key=" + key);
        }

        private void ApplyOpenIndex(int index, bool fireEvent, bool forceApply)
        {
            if (buttons == null || buttons.Count == 0)
                return;
            int clamped = Mathf.Clamp(index, 0, buttons.Count - 1);
            int prev = OpenIndex;
            if (!forceApply && prev == clamped)
                return;

            OpenIndex = clamped;

            float runningX = 0f;
            for (int i = 0; i < buttons.Count; i++)
            {
                var btn = buttons[i];
                bool isOpen = (i == clamped);
                float width = isOpen ? OpenButtonWidth : ClosedButtonWidth;
                if (btn != null)
                    btn.ApplyState(isOpen, width, runningX);
                runningX += width;
            }

            if (fireEvent && prev != clamped)
            {
                var key = OpenKey;
                OnOpenChanged?.Invoke(clamped, key);
            }
        }

        private void SubscribeButtons()
        {
            if (subscribed || buttons == null)
                return;
            for (int i = 0; i < buttons.Count; i++)
            {
                var btn = buttons[i];
                if (btn == null)
                    continue;
                btn.OnClicked += HandleButtonClicked;
            }
            subscribed = true;
        }

        private void UnsubscribeButtons()
        {
            if (!subscribed || buttons == null)
                return;
            for (int i = 0; i < buttons.Count; i++)
            {
                var btn = buttons[i];
                if (btn == null)
                    continue;
                btn.OnClicked -= HandleButtonClicked;
            }
            subscribed = false;
        }

        private void HandleButtonClicked(BottomNavButtonView btn)
        {
            if (btn == null || buttons == null)
                return;
            int idx = buttons.IndexOf(btn);
            if (idx < 0)
                return;
            if (idx == OpenIndex)
                return;
            ApplyOpenIndex(idx, fireEvent: true, forceApply: false);
        }
    }
}
