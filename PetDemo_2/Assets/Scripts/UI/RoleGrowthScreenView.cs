// SPEC §9.10 / §9.10.5：底部导航 JueSe 打开时的「角色成长」全屏层；四子页 + 页签栏由预制体搭建。
using System;
using System.Collections.Generic;
using PetDemo.Farm;
using PetDemo.UI.Farm;
using UnityEngine;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public class RoleGrowthScreenView : MonoBehaviour
    {
        public const string JueSeNavKey = "JueSe";
        public const int TianFuTabIndex = 2;
        public const string TianFuTabKey = "TianFu";
        public const int JingLingTabIndex = 3;
        public const string JingLingTabKey = "JingLing";

        [SerializeField] private RectTransform rootRt;
        [SerializeField] private RoleGrowthTabBarView tabBar;
        [SerializeField] private List<RectTransform> pages = new List<RectTransform>(RoleGrowthTabBarView.ExpectedTabCount);

        private BottomNavBarView bottomNav;
        private int? pendingTabIndexWhenShowingJueSe;

        private void Awake()
        {
            if (rootRt == null)
                rootRt = transform as RectTransform;
        }

        public void BindBottomNavBar(BottomNavBarView bar)
        {
            if (bottomNav != null)
                bottomNav.OnOpenChanged -= OnBottomNavOpenChanged;
            bottomNav = bar;
            if (bottomNav != null)
                bottomNav.OnOpenChanged += OnBottomNavOpenChanged;
        }

        private void OnDestroy()
        {
            if (bottomNav != null)
                bottomNav.OnOpenChanged -= OnBottomNavOpenChanged;
        }

        private void OnBottomNavOpenChanged(int index, string key)
        {
            ApplyMainBottomNavKey(key);
        }

        public void ApplyMainBottomNavKey(string bottomNavKey)
        {
            bool show = !string.IsNullOrEmpty(bottomNavKey) &&
                         string.Equals(bottomNavKey, JueSeNavKey, StringComparison.Ordinal);
            if (rootRt != null)
                rootRt.gameObject.SetActive(show);

            if (!show || tabBar == null)
            {
                pendingTabIndexWhenShowingJueSe = null;
                return;
            }

            int tabIndex = pendingTabIndexWhenShowingJueSe ?? 0;
            pendingTabIndexWhenShowingJueSe = null;
            tabBar.ForceSetOpenTabIndex(tabIndex);
            ApplyPageVisibility(tabIndex);
        }

        private void OnEnable()
        {
            if (tabBar != null)
                tabBar.OnTabChanged += OnTabBarChanged;
        }

        private void OnDisable()
        {
            if (tabBar != null)
                tabBar.OnTabChanged -= OnTabBarChanged;
        }

        private void OnTabBarChanged(int index, string key)
        {
            ApplyPageVisibility(index);
        }

        private void ApplyPageVisibility(int openIndex)
        {
            if (pages == null)
                return;
            for (int i = 0; i < pages.Count; i++)
            {
                var page = pages[i];
                if (page != null)
                    page.gameObject.SetActive(i == openIndex);
            }
        }

        /// <summary>SPEC §12.10：打开底栏「角色」并切到天赋页 <c>Page_TianFu</c>。</summary>
        public void NavigateToTianFuPage(BottomNavBarView bar)
        {
            pendingTabIndexWhenShowingJueSe = TianFuTabIndex;
            if (bar != null)
            {
                bar.SetOpenKey(JueSeNavKey);
                return;
            }

            pendingTabIndexWhenShowingJueSe = null;
            if (rootRt != null)
                rootRt.gameObject.SetActive(true);
            if (tabBar != null)
                tabBar.ForceSetOpenTabIndex(TianFuTabIndex);
            ApplyPageVisibility(TianFuTabIndex);
        }

        /// <summary>SPEC §9.10.5：在 <c>Page_JingLing</c> 挂载精灵背包子预制体。</summary>
        public void WirePetBagPage(IPlantingService service, GameObject petBagPagePrefab)
        {
            if (service == null || petBagPagePrefab == null || pages == null || pages.Count <= JingLingTabIndex)
                return;

            var pageRt = pages[JingLingTabIndex];
            if (pageRt == null)
                return;

            var mount = pageRt.Find("PetBagPageMount") as RectTransform;
            if (mount == null)
                mount = pageRt;

            for (int i = mount.childCount - 1; i >= 0; i--)
            {
                var child = mount.GetChild(i);
                if (child.GetComponent<RoleGrowthPetBagPageView>() != null)
                    Destroy(child.gameObject);
            }

            var inst = Instantiate(petBagPagePrefab, mount, false);
            inst.name = "RoleGrowthPetBagPage";
            var rt = inst.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
            }

            var view = inst.GetComponent<RoleGrowthPetBagPageView>();
            if (view != null)
                view.Initialize(service);
            else
                UnityEngine.Debug.LogError("[RoleGrowthScreenView] RoleGrowthPetBagPage 预制体缺少 RoleGrowthPetBagPageView。");
        }
    }
}
