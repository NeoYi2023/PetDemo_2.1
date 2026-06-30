// SPEC §9.8.10：底部导航「商店」全屏背景层（仅底图，功能占位后续扩展）。
// 自 v3.123 起「公会」改由 GongHuiScreenView（§9.8.9 公会场景层）承载；GongHui 常量保留作历史兼容。
using System;
using UnityEngine;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class BottomNavSimpleBackgroundScreenView : MonoBehaviour
    {
        public const string GongHuiNavKey = "GongHui";
        public const string ShangDianNavKey = "ShangDian";
        public const string ResGongHuiBackground = "AirUI/Gonghui_0";
        public const string ResShangDianBackground = "AirUI/ShangDian_0";

        private RectTransform rootRt;
        private BottomNavBarView bottomNav;
        private string matchKey;

        /// <summary>
        /// 构建与 <see cref="BottomNavBar"/> 同级、叠在底栏之下的全屏面板；仅当 <c>OpenKey == navKey</c> 时显示。
        /// </summary>
        public static BottomNavSimpleBackgroundScreenView BuildInto(
            RectTransform canvasRect,
            BottomNavBarView barView,
            string panelObjectName,
            string navKey,
            string resourcesSpritePath)
        {
            if (canvasRect == null || barView == null || string.IsNullOrEmpty(navKey) ||
                string.IsNullOrEmpty(resourcesSpritePath))
                return null;

            var root = BottomNavAttachedScreenLayout.CreateRootBelowBottomNav(
                canvasRect, barView, panelObjectName);
            BottomNavAttachedScreenLayout.AddStretchedResourcesBackground(
                root, resourcesSpritePath, nameof(BottomNavSimpleBackgroundScreenView),
                "（navKey=" + navKey + "）");

            root.gameObject.SetActive(false);

            var view = root.gameObject.AddComponent<BottomNavSimpleBackgroundScreenView>();
            view.rootRt = root;
            view.bottomNav = barView;
            view.matchKey = navKey;
            barView.OnOpenChanged += view.OnBottomNavOpenChanged;
            view.OnBottomNavOpenChanged(barView.OpenIndex, barView.OpenKey);
            return view;
        }

        private void OnBottomNavOpenChanged(int index, string key)
        {
            bool show = !string.IsNullOrEmpty(key) &&
                         string.Equals(key, matchKey, StringComparison.Ordinal);
            if (rootRt != null)
                rootRt.gameObject.SetActive(show);
        }

        private void OnDestroy()
        {
            if (bottomNav != null)
                bottomNav.OnOpenChanged -= OnBottomNavOpenChanged;
        }

    }
}
