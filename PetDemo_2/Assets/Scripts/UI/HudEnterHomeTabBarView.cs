// SPEC §9.8（v3.207）：EnterHomeHud 态下挂在 MainHudLayerRoot 的 BottomTabBar（与 §9.14.10 同款）。
using System;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class HudEnterHomeTabBarView : MonoBehaviour
    {
        public const string ObjectName = "BottomTabBar";

        public const int TabIndexIntimacy = 0;
        public const int TabIndexDressUp = 1;
        public const int TabIndexHome = 2;
        public const int TabIndexEnterHome = 3;
        public const int TabIndexTraining = 4;

        private Button intimacyTab;
        private Button dressUpButton;
        private Button homeTabButton;
        private Button enterHomeButton;
        private Button roleAddFavorButton;
        private int activeTabIndex = -1;
        private bool wired;

        /// <summary>页签点击（index）；EnterHome 已激活时不触发。</summary>
        public event Action<int> OnTabClicked;

        public int ActiveTabIndex => activeTabIndex;

        public static HudEnterHomeTabBarView BuildInto(RectTransform hudRoot)
        {
            if (hudRoot == null)
                return null;

            var barRt = CharacterCreationScreenLayout.BuildHudBottomTabBar(hudRoot);
            if (barRt == null)
                return null;

            var view = barRt.GetComponent<HudEnterHomeTabBarView>();
            if (view == null)
                view = barRt.gameObject.AddComponent<HudEnterHomeTabBarView>();
            view.EnsureFields();
            view.WireOnce();
            view.gameObject.SetActive(false);
            return view;
        }

        /// <summary>进入 EnterHomeHud：显示底栏并高亮 EnterHome。</summary>
        public void ShowEnterHomeMode()
        {
            EnsureFields();
            WireOnce();
            gameObject.SetActive(true);
            SetActiveTab(TabIndexEnterHome);
            transform.SetAsLastSibling();
        }

        public void HideBar()
        {
            gameObject.SetActive(false);
        }

        public void SetActiveTab(int index)
        {
            activeTabIndex = index;
            SetTabHighlight(intimacyTab, index == TabIndexIntimacy);
            SetTabHighlight(dressUpButton, index == TabIndexDressUp);
            SetTabHighlight(homeTabButton, index == TabIndexHome);
            SetTabHighlight(enterHomeButton, index == TabIndexEnterHome);
            SetTabHighlight(roleAddFavorButton, index == TabIndexTraining);
        }

        private void EnsureFields()
        {
            if (intimacyTab != null)
                return;
            intimacyTab = FindButton("IntimacyTab");
            dressUpButton = FindButton("DressUpButton");
            homeTabButton = FindButton("HomeTabButton");
            enterHomeButton = FindButton("EnterHomeButton");
            roleAddFavorButton = FindButton("RoleAddFavorButton");
        }

        private void WireOnce()
        {
            if (wired)
                return;
            EnsureFields();
            WireTab(intimacyTab, TabIndexIntimacy);
            WireTab(dressUpButton, TabIndexDressUp);
            WireTab(homeTabButton, TabIndexHome);
            WireTab(enterHomeButton, TabIndexEnterHome);
            WireTab(roleAddFavorButton, TabIndexTraining);
            wired = true;
        }

        private void WireTab(Button button, int index)
        {
            if (button == null)
                return;
            EnsureBottomTabButton(button);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => HandleTabClicked(index));
        }

        private void HandleTabClicked(int index)
        {
            // SPEC §9.14.10（v3.192）/ §9.8（v3.207）：已 IconOpen 再点无变化。
            if (activeTabIndex == index)
                return;
            OnTabClicked?.Invoke(index);
        }

        private Button FindButton(string name)
        {
            var t = transform.Find(name);
            return t != null ? t.GetComponent<Button>() : null;
        }

        private static void SetTabHighlight(Button button, bool active)
        {
            if (button == null)
                return;
            var iconOpen = FindDescendantByName(button.transform, "IconOpen");
            var iconClosed = FindDescendantByName(button.transform, "IconClosed");
            if (iconOpen != null)
                iconOpen.gameObject.SetActive(active);
            if (iconClosed != null)
                iconClosed.gameObject.SetActive(!active);
        }

        private static void EnsureBottomTabButton(Button button)
        {
            if (button == null)
                return;
            button.transition = Selectable.Transition.None;

            var hitImg = button.targetGraphic as Image;
            if (hitImg == null)
                hitImg = button.GetComponent<Image>();
            if (hitImg != null)
            {
                hitImg.raycastTarget = true;
                if (!hitImg.enabled)
                    hitImg.enabled = true;
                if (hitImg.color.a < 0.01f)
                {
                    var c = hitImg.color;
                    c.a = 0.01f;
                    hitImg.color = c;
                }
            }

            var iconOpen = FindDescendantByName(button.transform, "IconOpen");
            var iconClosed = FindDescendantByName(button.transform, "IconClosed");
            DisableRaycast(iconOpen);
            DisableRaycast(iconClosed);
        }

        private static void DisableRaycast(Transform t)
        {
            if (t == null)
                return;
            var img = t.GetComponent<Image>();
            if (img != null)
                img.raycastTarget = false;
        }

        private static Transform FindDescendantByName(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
                return null;
            if (root.name == name)
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDescendantByName(root.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
