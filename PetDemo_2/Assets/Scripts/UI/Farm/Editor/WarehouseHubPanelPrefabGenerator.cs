#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using PetDemo.UI.Farm;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// SPEC §9.8.13 (v3.41)：统一仓库预制体生成器。
    /// 产出 Assets/Resources/Prefabs/Farm/WarehouseHubPanel.prefab，
    /// 内含 FullscreenBackground(ChiFan_test) + CookingBackground(ChiFan_test_PengRen) +
    /// CloseButton + TabFood/TabCooking + BuffGainedStack + StaminaBarSlot(275×116) + StaminaText +
    /// FruitSlotGrid(26 个 150×150 槽) + BottomBar(3 按钮)。
    /// 槽与子节点位置由用户后续在预制体编辑器中自由调整，本生成器只确保"存在 + 必要 sizeDelta"。
    /// </summary>
    public static class WarehouseHubPanelPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Farm";
        private const string PrefabPath = PrefabDir + "/WarehouseHubPanel.prefab";

        private const string BackgroundSpriteAsset = "Assets/Resources/AirUI/ChiFan_test.png";
        private const string CookingBackgroundSpriteAsset = "Assets/Resources/AirUI/ChiFan_test_PengRen.png";

        private const int FruitSlotCount = 26;
        private static readonly Vector2 TabFoodPosition = new Vector2(-108f, 210f);
        private static readonly Vector2 TabCookingPosition = new Vector2(99f, 210f);
        private static readonly Vector2 TabButtonSize = new Vector2(155f, 104f);
        private const float FruitSlotSize = 150f;

        private static readonly Vector2 StaminaSlotSize = new Vector2(275f, 116f);
        private static readonly Vector2 CloseButtonSize = new Vector2(72f, 72f);
        private static readonly Vector2 BuffGainedStackSize = new Vector2(144f, 160f);
        private static readonly Vector2 EatButtonSize = new Vector2(260f, 110f);
        private static readonly Vector2 EatToFullButtonSize = new Vector2(320f, 110f);
        private static readonly Vector2 StartButtonSize = new Vector2(320f, 110f);

        [MenuItem("Tools/PetDemo/Generate Warehouse Hub Panel Prefab")]
        public static void Generate()
        {
            EnsureDir(PrefabDir);
            BuildPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("WarehouseHubPanel 预制件生成完成: " + PrefabPath);
        }

        private static void BuildPrefab()
        {
            var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundSpriteAsset);
            if (bgSprite == null)
                UnityEngine.Debug.LogWarning("[WarehouseHubPanelPrefabGenerator] 未找到背景图: " + BackgroundSpriteAsset);

            var cookingBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CookingBackgroundSpriteAsset);
            if (cookingBgSprite == null)
                UnityEngine.Debug.LogWarning("[WarehouseHubPanelPrefabGenerator] 未找到烹饪背景图: " + CookingBackgroundSpriteAsset);

            // 根：全屏 stretch + WarehouseHubPanelView 组件
            var root = new GameObject("WarehouseHubPanel", typeof(RectTransform));
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var view = root.AddComponent<WarehouseHubPanelView>();

            // FullscreenBackground (sibling=0)
            var bgRt = CreateStretch(rootRt, "FullscreenBackground");
            var bgImage = bgRt.gameObject.AddComponent<Image>();
            bgImage.raycastTarget = true;
            if (bgSprite != null)
            {
                bgImage.sprite = bgSprite;
                bgImage.color = Color.white;
            }
            else
            {
                bgImage.color = new Color(0.08f, 0.07f, 0.12f, 1f);
            }
            bgImage.preserveAspect = false;

            // CookingBackground (sibling=1)：烹饪 Tab 全屏背景，默认隐藏
            var cookingBgRt = CreateStretch(rootRt, "CookingBackground");
            var cookingBgImage = cookingBgRt.gameObject.AddComponent<Image>();
            cookingBgImage.raycastTarget = true;
            if (cookingBgSprite != null)
            {
                cookingBgImage.sprite = cookingBgSprite;
                cookingBgImage.color = Color.white;
            }
            else
            {
                cookingBgImage.color = new Color(0.08f, 0.07f, 0.12f, 1f);
            }
            cookingBgImage.preserveAspect = false;
            cookingBgRt.gameObject.SetActive(false);

            // CloseButton (sibling=2) — 右上角 "×"
            var closeRt = CreateRectAtCorner(rootRt, "CloseButton", CloseButtonSize, new Vector2(1f, 1f), new Vector2(-20f, -20f));
            var closeImage = closeRt.gameObject.AddComponent<Image>();
            closeImage.color = new Color(0.25f, 0.22f, 0.32f, 0.95f);
            closeImage.raycastTarget = true;
            var closeButton = closeRt.gameObject.AddComponent<Button>();
            closeButton.transition = Selectable.Transition.None;
            closeButton.targetGraphic = closeImage;

            var closeLabelRt = CreateStretch(closeRt, "Label");
            var closeLabelText = closeLabelRt.gameObject.AddComponent<Text>();
            closeLabelText.text = "×";
            closeLabelText.font = LoadBuiltinFont();
            closeLabelText.fontSize = 44;
            closeLabelText.alignment = TextAnchor.MiddleCenter;
            closeLabelText.color = Color.white;
            closeLabelText.raycastTarget = false;

            // TabFood / TabCooking（sibling=3/4）：面板根下居中锚点，无 Label 文字
            var tabFoodBg = BuildTabButton(rootRt, "TabFood", TabButtonSize, TabFoodPosition, true);
            var tabCookingBg = BuildTabButton(rootRt, "TabCooking", TabButtonSize, TabCookingPosition, false);

            // BuffGainedStack (sibling=5)：右上锚点，竖排已获得 Buff 图标；子节点由运行时吃下果实追加
            var buffStackRt = CreateRectAtCorner(rootRt, "BuffGainedStack", BuffGainedStackSize, new Vector2(1f, 1f), new Vector2(-24f, -96f));
            var buffVlg = buffStackRt.gameObject.AddComponent<VerticalLayoutGroup>();
            buffVlg.childAlignment = TextAnchor.UpperCenter;
            buffVlg.childControlWidth = true;
            buffVlg.childControlHeight = true;
            buffVlg.childForceExpandWidth = false;
            buffVlg.childForceExpandHeight = false;
            buffVlg.spacing = 6f;
            buffVlg.padding = new RectOffset(4, 4, 4, 4);
            var buffFit = buffStackRt.gameObject.AddComponent<ContentSizeFitter>();
            buffFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            buffFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // StaminaBarSlot (sibling=6) — 空 RectTransform，275×116；位置由用户后续调整
            var staminaSlotRt = CreateCenteredRect(rootRt, "StaminaBarSlot", StaminaSlotSize, new Vector2(0f, 0f));

            // StaminaText (sibling=7)
            var staminaTextRt = CreateCenteredRect(rootRt, "StaminaText", new Vector2(400f, 40f), new Vector2(0f, 0f));
            var staminaText = staminaTextRt.gameObject.AddComponent<Text>();
            staminaText.text = "0 / 100";
            staminaText.font = LoadBuiltinFont();
            staminaText.fontSize = 36;
            staminaText.alignment = TextAnchor.MiddleCenter;
            staminaText.color = Color.white;
            staminaText.raycastTarget = false;

            // FruitSlotGrid (sibling=8)：容器；不强制 LayoutGroup
            var gridRt = CreateCenteredRect(rootRt, "FruitSlotGrid", new Vector2(900f, 900f), new Vector2(0f, 0f));
            var fruitSlots = new List<RectTransform>(FruitSlotCount);
            for (int i = 1; i <= FruitSlotCount; i++)
            {
                var slot = BuildFruitSlot(gridRt, i);
                fruitSlots.Add(slot);
            }

            // BottomBar (sibling=9)
            var barRt = CreateCenteredRect(rootRt, "BottomBar", new Vector2(900f, 130f), new Vector2(0f, 0f));
            var eatButton = BuildBottomButton(barRt, "EatButton", "吃", EatButtonSize, new Color(0.31f, 0.64f, 1f, 1f), new Vector2(-300f, 0f));
            var eatToFullButton = BuildBottomButton(barRt, "EatToFullButton", "一键吃饱", EatToFullButtonSize, new Color(0.18f, 0.78f, 0.45f, 1f), new Vector2(0f, 0f));
            var startButton = BuildBottomButton(barRt, "StartButton", "开始", StartButtonSize, new Color(1f, 0.55f, 0.18f, 1f), new Vector2(300f, 0f));
            startButton.gameObject.SetActive(false);

            SerializeView(view,
                foodBackgroundImage: bgImage,
                cookingBackgroundImage: cookingBgImage,
                tabFoodButton: tabFoodBg.button,
                tabCookingButton: tabCookingBg.button,
                tabFoodBg: tabFoodBg.bg,
                tabCookingBg: tabCookingBg.bg,
                fruitSlotGrid: gridRt,
                fruitSlots: fruitSlots,
                staminaBarSlot: staminaSlotRt,
                staminaText: staminaText,
                closeButton: closeButton,
                eatButton: eatButton,
                eatToFullButton: eatToFullButton,
                startButton: startButton,
                buffGainedStackRoot: buffStackRt);

            // 预制体根默认 active=false；运行时由 WarehouseHubPanelView.Show 激活。
            root.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }

        private static RectTransform BuildFruitSlot(RectTransform parent, int index)
        {
            string name = "FruitSlot_" + index.ToString("D2");
            var slotGo = new GameObject(name, typeof(RectTransform));
            var slotRt = slotGo.GetComponent<RectTransform>();
            slotRt.SetParent(parent, false);
            slotRt.anchorMin = new Vector2(0.5f, 0.5f);
            slotRt.anchorMax = new Vector2(0.5f, 0.5f);
            slotRt.pivot = new Vector2(0.5f, 0.5f);
            // 全部叠在容器中心，用户可在预制体编辑器中拖到各自目标位置。
            slotRt.anchoredPosition = Vector2.zero;
            slotRt.sizeDelta = new Vector2(FruitSlotSize, FruitSlotSize);

            var bgImage = slotGo.AddComponent<Image>();
            bgImage.color = new Color(1f, 1f, 1f, 0.08f); // 半透明底纹占位
            bgImage.raycastTarget = true;

            var btn = slotGo.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = bgImage;

            // Icon
            var iconRt = CreateCenteredRect(slotRt, "Icon", new Vector2(FruitSlotSize, FruitSlotSize), Vector2.zero);
            var iconImage = iconRt.gameObject.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            iconImage.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            iconRt.gameObject.SetActive(false);

            // Count（底部居中）
            var countGo = new GameObject("Count", typeof(RectTransform));
            var countRt = countGo.GetComponent<RectTransform>();
            countRt.SetParent(slotRt, false);
            countRt.anchorMin = new Vector2(0.5f, 0f);
            countRt.anchorMax = new Vector2(0.5f, 0f);
            countRt.pivot = new Vector2(0.5f, 0f);
            countRt.anchoredPosition = new Vector2(0f, 8f);
            countRt.sizeDelta = new Vector2(140f, 36f);
            var countText = countGo.AddComponent<Text>();
            countText.text = string.Empty;
            countText.font = LoadBuiltinFont();
            countText.fontSize = 28;
            countText.alignment = TextAnchor.MiddleCenter;
            countText.color = Color.white;
            countText.raycastTarget = false;
            countGo.SetActive(false);

            // SelectMask：金黄半透 0.3，stretchFull，默认 active=false
            var maskRt = CreateStretch(slotRt, "SelectMask");
            var maskImage = maskRt.gameObject.AddComponent<Image>();
            maskImage.color = WarehouseHubPanelView.GetSlotSelectedColor();
            maskImage.raycastTarget = false;
            maskRt.gameObject.SetActive(false);

            return slotRt;
        }

        private static (Button button, Image bg) BuildTabButton(
            RectTransform parent, string name, Vector2 size, Vector2 anchoredPosition, bool active)
        {
            var rt = CreateCenteredRect(parent, name, size, anchoredPosition);
            var img = rt.gameObject.AddComponent<Image>();
            img.enabled = true;
            img.color = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;

            var labelRt = CreateStretch(rt, "Label");
            labelRt.gameObject.SetActive(false);

            return (btn, img);
        }

        private static Button BuildBottomButton(RectTransform parent, string name, string label,
            Vector2 size, Color color, Vector2 anchoredPosition)
        {
            var rt = CreateCenteredRect(parent, name, size, anchoredPosition);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;

            var labelRt = CreateStretch(rt, "Label");
            var text = labelRt.gameObject.AddComponent<Text>();
            text.text = label;
            text.font = LoadBuiltinFont();
            text.fontSize = 40;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            return btn;
        }

        private static void SerializeView(
            WarehouseHubPanelView view,
            Image foodBackgroundImage,
            Image cookingBackgroundImage,
            Button tabFoodButton,
            Button tabCookingButton,
            Image tabFoodBg,
            Image tabCookingBg,
            RectTransform fruitSlotGrid,
            List<RectTransform> fruitSlots,
            RectTransform staminaBarSlot,
            Text staminaText,
            Button closeButton,
            Button eatButton,
            Button eatToFullButton,
            Button startButton,
            RectTransform buffGainedStackRoot)
        {
            var so = new SerializedObject(view);
            so.FindProperty("foodBackgroundImage").objectReferenceValue = foodBackgroundImage;
            so.FindProperty("cookingBackgroundImage").objectReferenceValue = cookingBackgroundImage;
            so.FindProperty("tabFoodButton").objectReferenceValue = tabFoodButton;
            so.FindProperty("tabCookingButton").objectReferenceValue = tabCookingButton;
            so.FindProperty("tabFoodBg").objectReferenceValue = tabFoodBg;
            so.FindProperty("tabCookingBg").objectReferenceValue = tabCookingBg;
            so.FindProperty("fruitSlotGrid").objectReferenceValue = fruitSlotGrid;
            var list = so.FindProperty("fruitSlots");
            list.arraySize = fruitSlots.Count;
            for (int i = 0; i < fruitSlots.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = fruitSlots[i];
            so.FindProperty("staminaBarSlot").objectReferenceValue = staminaBarSlot;
            so.FindProperty("staminaText").objectReferenceValue = staminaText;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.FindProperty("eatButton").objectReferenceValue = eatButton;
            so.FindProperty("eatToFullButton").objectReferenceValue = eatToFullButton;
            so.FindProperty("startButton").objectReferenceValue = startButton;
            so.FindProperty("buffGainedStackRoot").objectReferenceValue = buffGainedStackRoot;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform CreateStretch(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static RectTransform CreateCenteredRect(
            RectTransform parent, string name, Vector2 sizeDelta, Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            return rt;
        }

        private static RectTransform CreateRectAtCorner(
            RectTransform parent, string name, Vector2 sizeDelta, Vector2 anchorPivot, Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorPivot;
            rt.anchorMax = anchorPivot;
            rt.pivot = anchorPivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            return rt;
        }

        private static Font LoadBuiltinFont()
        {
            return Resources.GetBuiltinResource<Font>("Arial.ttf")
                ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
