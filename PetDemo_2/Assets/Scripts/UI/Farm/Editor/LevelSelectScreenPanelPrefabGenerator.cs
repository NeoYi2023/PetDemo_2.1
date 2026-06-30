#if UNITY_EDITOR
using System.IO;
using PetDemo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// SPEC §9.8.8.6 / §9.8.8.7：主线「选择关卡」全屏界面预制体生成器。
    /// 产出 Assets/Resources/Prefabs/Farm/LevelSelectScreenPanel.prefab
    /// </summary>
    public static class LevelSelectScreenPanelPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Farm";
        private const string PrefabPath = PrefabDir + "/LevelSelectScreenPanel.prefab";
        private const string BackgroundSpriteAsset = "Assets/Resources/AirUI/ZhuXian_2.png";
        private const string GoButtonSpriteAsset = "Assets/Resources/AirUI/ZhanDouKaiShi.png";

        private static readonly Vector2 GoButtonSize = new Vector2(120f, 120f);
        private static readonly Vector2 BackButtonSize = new Vector2(120f, 80f);

        [MenuItem("Tools/PetDemo/Generate Level Select Screen Panel Prefab")]
        public static void Generate()
        {
            EnsureDir(PrefabDir);
            BuildPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("LevelSelectScreenPanel 预制件生成完成: " + PrefabPath);
        }

        private static void BuildPrefab()
        {
            var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundSpriteAsset);
            if (bgSprite == null)
                UnityEngine.Debug.LogWarning("[LevelSelectScreenPanelPrefabGenerator] 未找到背景图: " + BackgroundSpriteAsset);

            var goSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GoButtonSpriteAsset);

            var root = new GameObject("LevelSelectScreenPanel", typeof(RectTransform));
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            root.AddComponent<LevelSelectScreenPanelView>();

            var bgRt = CreateStretch(rootRt, "Background");
            var bgImage = bgRt.gameObject.AddComponent<Image>();
            bgImage.raycastTarget = true;
            if (bgSprite != null)
            {
                bgImage.sprite = bgSprite;
                bgImage.preserveAspect = false;
                bgImage.color = Color.white;
            }
            else
            {
                bgImage.color = new Color(0.08f, 0.07f, 0.12f, 1f);
            }

            var slotsRoot = CreateStretch(rootRt, LevelSelectScreenPanelView.LevelSlotsRootName);

            var goBtnRt = BuildGoButton(rootRt, goSprite, out var goBtn);
            var backBtn = BuildLabeledButton(
                rootRt, "BackButton", "返回",
                BackButtonSize, new Vector2(0f, 0f), new Vector2(20f, 20f),
                new Color(0.35f, 0.35f, 0.4f, 0.95f));

            SerializeView(root.GetComponent<LevelSelectScreenPanelView>(), goBtn, goBtnRt, backBtn, slotsRoot);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            if (prefab != null)
                Selection.activeObject = prefab;
        }

        private static RectTransform BuildGoButton(RectTransform parent, Sprite sprite, out Button btn)
        {
            var go = new GameObject("GoButton", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = GoButtonSize;
            go.SetActive(false);

            var img = go.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = true;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
            }
            else
            {
                UnityEngine.Debug.LogWarning("[LevelSelectScreenPanelPrefabGenerator] 未找到前往按钮图: " + GoButtonSpriteAsset);
                img.color = new Color(1f, 0.55f, 0.18f, 0.95f);
            }

            btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;
            return rt;
        }

        private static Button BuildLabeledButton(
            RectTransform parent,
            string name,
            string label,
            Vector2 size,
            Vector2 anchorPivot,
            Vector2 anchoredPosition,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorPivot;
            rt.anchorMax = anchorPivot;
            rt.pivot = anchorPivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;

            var labelRt = CreateStretch(rt, "Label");
            var text = labelRt.gameObject.AddComponent<Text>();
            text.text = label;
            text.font = LoadBuiltinFont();
            text.fontSize = 36;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            return btn;
        }

        private static void SerializeView(
            LevelSelectScreenPanelView view,
            Button goButton,
            RectTransform goButtonRt,
            Button backButton,
            RectTransform levelSlotsRoot)
        {
            var so = new SerializedObject(view);
            so.FindProperty("goButton").objectReferenceValue = goButton;
            so.FindProperty("goButtonRt").objectReferenceValue = goButtonRt;
            so.FindProperty("backButton").objectReferenceValue = backButton;
            so.FindProperty("levelSlotsRoot").objectReferenceValue = levelSlotsRoot;
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
