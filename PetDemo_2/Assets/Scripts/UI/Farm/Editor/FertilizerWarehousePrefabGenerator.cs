#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// SPEC §9.7 / §3.12：生成肥料仓库面板预制体（背景 FeiLiaoUI_0、槽 FeiLiaoUI_1，根宽 1080）。
    /// </summary>
    public static class FertilizerWarehousePrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Farm";
        private const string PrefabPath = PrefabDir + "/FertilizerWarehousePanel.prefab";

        private const string BackgroundSpriteAsset = "Assets/Scenes/Air/UI/FeiLiaoUI_0.png";
        private const string SlotSpriteAsset = "Assets/Scenes/Air/UI/FeiLiaoUI_1.png";

        [MenuItem("Tools/PetDemo/Generate Fertilizer Warehouse Prefab")]
        public static void Generate()
        {
            EnsureDir(PrefabDir);
            BuildPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("FertilizerWarehousePanel 预制体生成完成: " + PrefabPath);
        }

        private static void BuildPrefab()
        {
            var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundSpriteAsset);
            var slotSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SlotSpriteAsset);
            if (bgSprite == null)
                UnityEngine.Debug.LogWarning("[FertilizerWarehousePrefabGenerator] 未找到背景图: " + BackgroundSpriteAsset);
            if (slotSprite == null)
                UnityEngine.Debug.LogWarning("[FertilizerWarehousePrefabGenerator] 未找到槽位图: " + SlotSpriteAsset);

            var root = new GameObject("FertilizerWarehousePanel", typeof(RectTransform), typeof(Image));
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.5f, 0.5f);
            rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.anchoredPosition = new Vector2(0f, -355f);
            rootRt.sizeDelta = new Vector2(1080f, 860f);

            var rootImage = root.GetComponent<Image>();
            rootImage.sprite = bgSprite;
            rootImage.preserveAspect = true;
            rootImage.raycastTarget = true;

            var view = root.AddComponent<PetDemo.UI.Farm.FertilizerWarehousePanelView>();

            var upper = CreateRect(root.transform, "UpperSection",
                anchorMin: new Vector2(0f, 0.5f),
                anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0.5f, 1f),
                anchoredPosition: Vector2.zero,
                sizeDelta: Vector2.zero,
                offsetsMin: Vector2.zero,
                offsetsMax: Vector2.zero);

            var bigIconRt = CreateRect(upper, "DetailBigIcon",
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                pivot: new Vector2(0.5f, 1f),
                anchoredPosition: new Vector2(0f, -48f),
                sizeDelta: new Vector2(280f, 280f));
            var bigIcon = bigIconRt.gameObject.AddComponent<Image>();
            bigIcon.preserveAspect = true;
            bigIcon.raycastTarget = false;

            var descRt = CreateRect(upper, "DescriptionText",
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                pivot: new Vector2(0.5f, 1f),
                anchoredPosition: new Vector2(0f, -360f),
                sizeDelta: new Vector2(980f, 140f));
            var descText = descRt.gameObject.AddComponent<Text>();
            descText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            descText.fontSize = 28;
            descText.color = new Color(1f, 1f, 1f, 0.95f);
            descText.alignment = TextAnchor.UpperCenter;
            descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descText.verticalOverflow = VerticalWrapMode.Truncate;
            descText.raycastTarget = false;

            var btnRow = CreateRect(upper, "ButtonRow",
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                pivot: new Vector2(0.5f, 1f),
                anchoredPosition: new Vector2(0f, -520f),
                sizeDelta: new Vector2(900f, 88f));
            var hlg = btnRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 48f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var btnAll = CreateButtonChild(btnRow, "BtnApplyAll", "全部施肥", new Vector2(320f, 80f));
            var btnOne = CreateButtonChild(btnRow, "BtnApplyOne", "施肥1个", new Vector2(320f, 80f));

            var emptyRt = CreateRect(upper, "EmptyHint",
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f),
                anchoredPosition: Vector2.zero,
                sizeDelta: new Vector2(900f, 80f));
            var emptyText = emptyRt.gameObject.AddComponent<Text>();
            emptyText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            emptyText.fontSize = 32;
            emptyText.color = new Color(0.92f, 0.92f, 0.92f, 0.9f);
            emptyText.alignment = TextAnchor.MiddleCenter;
            emptyText.text = "暂无肥料";
            emptyText.raycastTarget = false;
            emptyRt.gameObject.SetActive(false);

            var lower = CreateRect(root.transform, "LowerSection",
                anchorMin: new Vector2(0f, 0f),
                anchorMax: new Vector2(1f, 0.5f),
                pivot: new Vector2(0.5f, 0f),
                anchoredPosition: new Vector2(0f, 16f),
                sizeDelta: Vector2.zero,
                offsetsMin: new Vector2(40f, 16f),
                offsetsMax: new Vector2(-40f, -8f));

            var stripRt = CreateRect(lower.transform, "SlotStrip",
                anchorMin: new Vector2(0f, 0f),
                anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0.5f, 0.5f),
                anchoredPosition: Vector2.zero,
                sizeDelta: Vector2.zero,
                offsetsMin: Vector2.zero,
                offsetsMax: Vector2.zero);
            var stripH = stripRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            stripH.spacing = 16f;
            stripH.padding = new RectOffset(8, 8, 8, 8);
            stripH.childAlignment = TextAnchor.MiddleCenter;
            stripH.childControlWidth = false;
            stripH.childControlHeight = false;
            stripH.childForceExpandWidth = false;
            stripH.childForceExpandHeight = false;
            stripRt.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var template = CreateSlotTemplate(stripRt.transform, slotSprite);

            SerializeView(view, bigIcon, descText, btnAll, btnOne, emptyText, stripRt, template);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void SerializeView(
            PetDemo.UI.Farm.FertilizerWarehousePanelView view,
            Image detailBigIcon,
            Text descriptionText,
            Button btnApplyAll,
            Button btnApplyOne,
            Text emptyHint,
            RectTransform slotStrip,
            GameObject slotTemplate)
        {
            var so = new SerializedObject(view);
            so.FindProperty("detailBigIcon").objectReferenceValue = detailBigIcon;
            so.FindProperty("descriptionText").objectReferenceValue = descriptionText;
            so.FindProperty("btnApplyAll").objectReferenceValue = btnApplyAll;
            so.FindProperty("btnApplyOne").objectReferenceValue = btnApplyOne;
            so.FindProperty("emptyHint").objectReferenceValue = emptyHint;
            so.FindProperty("slotStrip").objectReferenceValue = slotStrip;
            so.FindProperty("slotTemplate").objectReferenceValue = slotTemplate;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateSlotTemplate(Transform parent, Sprite slotSprite)
        {
            var go = new GameObject("SlotTemplate", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(120f, 120f);

            var bg = go.GetComponent<Image>();
            bg.sprite = slotSprite;
            bg.preserveAspect = true;
            bg.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = bg;

            var iconRt = CreateRect(go.transform, "Icon",
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f),
                anchoredPosition: new Vector2(0f, 6f),
                sizeDelta: new Vector2(120f, 120f));
            var icon = iconRt.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var countRt = CreateRect(go.transform, "Count",
                anchorMin: new Vector2(0.5f, 0f),
                anchorMax: new Vector2(0.5f, 0f),
                pivot: new Vector2(0.5f, 0f),
                anchoredPosition: new Vector2(0f, 8f),
                sizeDelta: new Vector2(100f, 36f));
            var countTxt = countRt.gameObject.AddComponent<Text>();
            countTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            countTxt.fontSize = 22;
            countTxt.color = Color.white;
            countTxt.alignment = TextAnchor.MiddleCenter;
            countTxt.raycastTarget = false;

            go.SetActive(false);
            return go;
        }

        private static Button CreateButtonChild(RectTransform row, string name, string label, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(row, false);
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.15f, 0.65f);
            img.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;

            var textRt = CreateRect(go.transform, "Text",
                anchorMin: Vector2.zero,
                anchorMax: Vector2.one,
                pivot: new Vector2(0.5f, 0.5f),
                anchoredPosition: Vector2.zero,
                sizeDelta: Vector2.zero,
                offsetsMin: Vector2.zero,
                offsetsMax: Vector2.zero);
            var txt = textRt.gameObject.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 30;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = label;
            txt.raycastTarget = false;

            return btn;
        }

        private static RectTransform CreateRect(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Vector2 offsetsMin,
            Vector2 offsetsMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            rt.offsetMin = offsetsMin;
            rt.offsetMax = offsetsMax;
            return rt;
        }

        private static RectTransform CreateRect(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            return CreateRect(parent, name, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta,
                Vector2.zero, Vector2.zero);
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
