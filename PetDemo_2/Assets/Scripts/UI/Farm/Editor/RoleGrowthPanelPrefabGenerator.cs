#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using PetDemo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// SPEC §9.10：「角色」成长界面预制体生成器。
    /// 输出 Assets/Resources/Prefabs/Farm/RoleGrowthPanel.prefab。
    /// </summary>
    public static class RoleGrowthPanelPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Farm";
        private const string PrefabPath = PrefabDir + "/RoleGrowthPanel.prefab";

        private const float TabSlotWidth = RoleGrowthTabBarView.TabSlotWidth;
        private const float TabBarHeight = RoleGrowthTabBarView.BarHeight;
        private const int DefaultOpenTabIndex = 0;

        private static readonly string[] TabKeys = { "ShuXing", "JiNeng", "TianFu", "JingLing" };
        private static readonly string[] PageNames = { "Page_ShuXing", "Page_JiNeng", "Page_TianFu", "Page_JingLing" };

        [MenuItem("Tools/PetDemo/Generate Role Growth Panel Prefab")]
        public static void Generate()
        {
            EnsureDir(PrefabDir);
            BuildPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("RoleGrowthPanel 预制件生成完成: " + PrefabPath);
        }

        private static void BuildPrefab()
        {
            var root = new GameObject("RoleGrowthPanel", typeof(RectTransform));
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var dimGo = new GameObject("DimLayer", typeof(RectTransform), typeof(Image));
            var dimRt = dimGo.GetComponent<RectTransform>();
            dimRt.SetParent(rootRt, false);
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = Vector2.zero;
            dimRt.offsetMax = Vector2.zero;
            dimRt.pivot = new Vector2(0.5f, 0.5f);
            var dimImage = dimGo.GetComponent<Image>();
            dimImage.color = new Color(0.04f, 0.05f, 0.08f, 0.88f);
            dimImage.raycastTarget = true;

            var pageArea = new GameObject("PageArea", typeof(RectTransform));
            var pageAreaRt = pageArea.GetComponent<RectTransform>();
            pageAreaRt.SetParent(rootRt, false);
            pageAreaRt.anchorMin = Vector2.zero;
            pageAreaRt.anchorMax = Vector2.one;
            pageAreaRt.pivot = new Vector2(0.5f, 0.5f);
            pageAreaRt.offsetMin = new Vector2(0f, 0f);
            pageAreaRt.offsetMax = new Vector2(0f, -TabBarHeight);

            var pages = new List<RectTransform>(PageNames.Length);
            for (int i = 0; i < PageNames.Length; i++)
            {
                var pageGo = new GameObject(PageNames[i], typeof(RectTransform));
                var pageRt = pageGo.GetComponent<RectTransform>();
                pageRt.SetParent(pageAreaRt, false);
                pageRt.anchorMin = Vector2.zero;
                pageRt.anchorMax = Vector2.one;
                pageRt.offsetMin = Vector2.zero;
                pageRt.offsetMax = Vector2.zero;
                pageRt.pivot = new Vector2(0.5f, 0.5f);
                pageGo.SetActive(i == DefaultOpenTabIndex);
                if (PageNames[i] == "Page_JingLing")
                {
                    var mountGo = new GameObject("PetBagPageMount", typeof(RectTransform));
                    var mountRt = mountGo.GetComponent<RectTransform>();
                    mountRt.SetParent(pageRt, false);
                    mountRt.anchorMin = Vector2.zero;
                    mountRt.anchorMax = Vector2.one;
                    mountRt.offsetMin = Vector2.zero;
                    mountRt.offsetMax = Vector2.zero;
                    mountRt.pivot = new Vector2(0.5f, 0.5f);
                }
                pages.Add(pageRt);
            }

            var tabBarGo = new GameObject("RoleGrowthTabBar", typeof(RectTransform));
            var tabBarRt = tabBarGo.GetComponent<RectTransform>();
            tabBarRt.SetParent(rootRt, false);
            tabBarRt.anchorMin = new Vector2(0.5f, 1f);
            tabBarRt.anchorMax = new Vector2(0.5f, 1f);
            tabBarRt.pivot = new Vector2(0.5f, 1f);
            tabBarRt.anchoredPosition = Vector2.zero;
            tabBarRt.sizeDelta = new Vector2(RoleGrowthTabBarView.BarWidth, TabBarHeight);

            var tabBarView = tabBarGo.AddComponent<RoleGrowthTabBarView>();
            var tabButtons = new List<BottomNavButtonView>(TabKeys.Length);
            for (int i = 0; i < TabKeys.Length; i++)
            {
                bool isOpen = (i == DefaultOpenTabIndex);
                var btnView = BuildTabButton(tabBarRt, TabKeys[i], TabSlotWidth, i * TabSlotWidth, isOpen);
                tabButtons.Add(btnView);
            }

            SerializeTabBar(tabBarView, tabButtons);

            var screenView = root.AddComponent<RoleGrowthScreenView>();
            SerializeScreenView(screenView, rootRt, tabBarView, pages);

            root.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }

        private static BottomNavButtonView BuildTabButton(
            RectTransform parent, string key, float initialWidth, float anchoredX, bool isOpen)
        {
            var slot = new GameObject("RoleGrowthTabSlot_" + key, typeof(RectTransform));
            var slotRt = slot.GetComponent<RectTransform>();
            slotRt.SetParent(parent, false);
            slotRt.anchorMin = new Vector2(0f, 0f);
            slotRt.anchorMax = new Vector2(0f, 0f);
            slotRt.pivot = new Vector2(0f, 0f);
            slotRt.anchoredPosition = new Vector2(anchoredX, 0f);
            slotRt.sizeDelta = new Vector2(initialWidth, TabBarHeight);

            var openState = BuildStateSubtree(slotRt, "OpenState", "OpenBg", "OpenIcon", TabSlotWidth);
            var closedState = BuildStateSubtree(slotRt, "ClosedState", "ClosedBg", "ClosedIcon", TabSlotWidth);

            openState.gameObject.SetActive(isOpen);
            closedState.gameObject.SetActive(!isOpen);

            var hitGo = new GameObject("HitArea", typeof(RectTransform), typeof(Image), typeof(Button));
            var hitRt = hitGo.GetComponent<RectTransform>();
            hitRt.SetParent(slotRt, false);
            hitRt.anchorMin = new Vector2(0f, 0f);
            hitRt.anchorMax = new Vector2(1f, 1f);
            hitRt.pivot = new Vector2(0.5f, 0.5f);
            hitRt.offsetMin = Vector2.zero;
            hitRt.offsetMax = Vector2.zero;

            var hitImage = hitGo.GetComponent<Image>();
            hitImage.color = new Color(1f, 1f, 1f, 0f);
            hitImage.raycastTarget = true;

            var hitButton = hitGo.GetComponent<Button>();
            hitButton.transition = Selectable.Transition.None;
            hitButton.targetGraphic = hitImage;

            var btnView = slot.AddComponent<BottomNavButtonView>();
            SerializeButtonView(btnView, key, slotRt, openState, closedState, hitButton);
            return btnView;
        }

        private static RectTransform BuildStateSubtree(
            RectTransform parent, string stateName, string bgName, string iconName, float width)
        {
            var stateGo = new GameObject(stateName, typeof(RectTransform));
            var stateRt = stateGo.GetComponent<RectTransform>();
            stateRt.SetParent(parent, false);
            stateRt.anchorMin = new Vector2(0f, 0f);
            stateRt.anchorMax = new Vector2(0f, 0f);
            stateRt.pivot = new Vector2(0f, 0f);
            stateRt.anchoredPosition = Vector2.zero;
            stateRt.sizeDelta = new Vector2(width, TabBarHeight);

            var bgGo = new GameObject(bgName, typeof(RectTransform), typeof(Image));
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.SetParent(stateRt, false);
            bgRt.anchorMin = new Vector2(0f, 0f);
            bgRt.anchorMax = new Vector2(1f, 1f);
            bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImage = bgGo.GetComponent<Image>();
            bgImage.preserveAspect = false;
            bgImage.raycastTarget = false;

            var iconGo = new GameObject(iconName, typeof(RectTransform), typeof(Image));
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.SetParent(stateRt, false);
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = Vector2.zero;
            iconRt.sizeDelta = new Vector2(TabBarHeight - 16f, TabBarHeight - 16f);
            var iconImage = iconGo.GetComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            return stateRt;
        }

        private static void SerializeTabBar(RoleGrowthTabBarView view, List<BottomNavButtonView> buttons)
        {
            var so = new SerializedObject(view);
            so.FindProperty("defaultOpenIndex").intValue = DefaultOpenTabIndex;
            var list = so.FindProperty("tabButtons");
            list.arraySize = buttons.Count;
            for (int i = 0; i < buttons.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SerializeScreenView(
            RoleGrowthScreenView view,
            RectTransform rootRt,
            RoleGrowthTabBarView tabBar,
            List<RectTransform> pages)
        {
            var so = new SerializedObject(view);
            so.FindProperty("rootRt").objectReferenceValue = rootRt;
            so.FindProperty("tabBar").objectReferenceValue = tabBar;
            var list = so.FindProperty("pages");
            list.arraySize = pages.Count;
            for (int i = 0; i < pages.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = pages[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SerializeButtonView(
            BottomNavButtonView view,
            string key,
            RectTransform selfRt,
            RectTransform openState,
            RectTransform closedState,
            Button hitButton)
        {
            var so = new SerializedObject(view);
            so.FindProperty("key").stringValue = key;
            so.FindProperty("selfRt").objectReferenceValue = selfRt;
            so.FindProperty("openState").objectReferenceValue = openState;
            so.FindProperty("closedState").objectReferenceValue = closedState;
            so.FindProperty("hitButton").objectReferenceValue = hitButton;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureDir(string dir)
        {
            if (Directory.Exists(dir))
                return;
            Directory.CreateDirectory(dir);
        }
    }
}
#endif
