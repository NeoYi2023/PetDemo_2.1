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
    /// SPEC §9.8 / §9.8.5：主界面底部一级导航切换栏预制件生成器。
    /// 输出 Assets/Resources/Prefabs/Farm/BottomNavBar.prefab：
    ///   - 容器 1080×160 贴 Canvas 底边；
    ///   - 5 个按钮固定顺序 GongHui / JueSe / JiaYuan / ZhuXian / ShangDian；
    ///   - 每个按钮包含 OpenState（364×160）/ ClosedState（179×160）/ HitArea 三组子节点；
    ///   - 默认打开索引 = 2（JiaYuan）。
    /// 20 个 Sprite 槽位（每按钮 OpenBg / OpenIcon / ClosedBg / ClosedIcon）由用户在
    /// 预制件 Inspector 中手动配置，本生成器仅构建空壳结构 + 组件引用。
    /// </summary>
    public static class BottomNavBarPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Farm";
        private const string PrefabPath = PrefabDir + "/BottomNavBar.prefab";

        private const float BarWidth = 1080f;
        private const float BarHeight = 160f;
        private const float OpenWidth = 364f;
        private const float ClosedWidth = 179f;
        private const int DefaultOpenIndex = 2; // JiaYuan

        private static readonly string[] ButtonKeys = { "GongHui", "JueSe", "JiaYuan", "ZhuXian", "ShangDian" };

        [MenuItem("Tools/PetDemo/Generate Bottom Nav Bar Prefab")]
        public static void Generate()
        {
            EnsureDir(PrefabDir);
            BuildPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("BottomNavBar 预制件生成完成: " + PrefabPath);
        }

        private static void BuildPrefab()
        {
            var root = new GameObject("BottomNavBar", typeof(RectTransform));
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.5f, 0f);
            rootRt.anchorMax = new Vector2(0.5f, 0f);
            rootRt.pivot = new Vector2(0.5f, 0f);
            rootRt.anchoredPosition = new Vector2(0f, 0f);
            rootRt.sizeDelta = new Vector2(BarWidth, BarHeight);

            var barView = root.AddComponent<BottomNavBarView>();

            var buttons = new List<BottomNavButtonView>(ButtonKeys.Length);
            float runningX = 0f;
            for (int i = 0; i < ButtonKeys.Length; i++)
            {
                bool isOpen = (i == DefaultOpenIndex);
                float width = isOpen ? OpenWidth : ClosedWidth;
                var btnView = BuildButton(rootRt, ButtonKeys[i], width, runningX, isOpen);
                buttons.Add(btnView);
                runningX += width;
            }

            SerializeBarView(barView, buttons);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }

        private static BottomNavButtonView BuildButton(
            RectTransform parent, string key, float initialWidth, float anchoredX, bool isOpen)
        {
            var slot = new GameObject("BottomNavSlot_" + key, typeof(RectTransform));
            var slotRt = slot.GetComponent<RectTransform>();
            slotRt.SetParent(parent, false);
            slotRt.anchorMin = new Vector2(0f, 0f);
            slotRt.anchorMax = new Vector2(0f, 0f);
            slotRt.pivot = new Vector2(0f, 0f);
            slotRt.anchoredPosition = new Vector2(anchoredX, 0f);
            slotRt.sizeDelta = new Vector2(initialWidth, BarHeight);

            var openState = BuildStateSubtree(slotRt, "OpenState", "OpenBg", "OpenIcon", OpenWidth);
            var closedState = BuildStateSubtree(slotRt, "ClosedState", "ClosedBg", "ClosedIcon", ClosedWidth);

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
            stateRt.sizeDelta = new Vector2(width, BarHeight);

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
            iconRt.sizeDelta = new Vector2(BarHeight - 24f, BarHeight - 24f);
            var iconImage = iconGo.GetComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            return stateRt;
        }

        private static void SerializeBarView(BottomNavBarView view, List<BottomNavButtonView> buttons)
        {
            var so = new SerializedObject(view);
            so.FindProperty("defaultOpenIndex").intValue = DefaultOpenIndex;
            var list = so.FindProperty("buttons");
            list.arraySize = buttons.Count;
            for (int i = 0; i < buttons.Count; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.objectReferenceValue = buttons[i];
            }
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
