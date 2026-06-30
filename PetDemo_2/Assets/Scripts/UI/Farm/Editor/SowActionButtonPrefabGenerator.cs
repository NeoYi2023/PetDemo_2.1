#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using PetDemo.UI.Farm;

namespace PetDemo.EditorTools
{
    public static class SowActionButtonPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Farm";
        private const string PrefabPath = PrefabDir + "/SowActionButton.prefab";

        private static readonly Color BackgroundNormal = new Color(0.85f, 0.55f, 0.18f, 0.95f);

        [MenuItem("Tools/PetDemo/Generate Sow Action Button Prefab")]
        public static void Generate()
        {
            EnsureDir(PrefabDir);

            var go = new GameObject(
                "SowActionButton",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Image),
                typeof(SowActionButtonView));

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -680f);
            rt.sizeDelta = new Vector2(360f, 120f);

            var img = go.GetComponent<Image>();
            img.color = BackgroundNormal;
            img.raycastTarget = true;

            var view = go.GetComponent<SowActionButtonView>();
            var canvasGroup = go.GetComponent<CanvasGroup>();

            var labelGo = new GameObject("LabelText", typeof(RectTransform), typeof(Text));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var text = labelGo.GetComponent<Text>();
            text.text = "播种";
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 48;
            text.raycastTarget = false;

            var so = new SerializedObject(view);
            so.FindProperty("background").objectReferenceValue = img;
            so.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);

            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("SowActionButton 预制体生成完成: " + PrefabPath);
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
