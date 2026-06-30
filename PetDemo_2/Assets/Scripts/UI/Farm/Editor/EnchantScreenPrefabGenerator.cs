#if UNITY_EDITOR
// SPEC §9.12.6（v3.82）：一次性生成「附魔」转盘玩法全屏界面预制件 EnchantScreen.prefab。
// 生成后请在 Unity 内手动微调 PlantImage / Wheel 各节点的位置、尺寸与 Pointer 旋转中心（pivot）。
using System.IO;
using PetDemo.UI.Farm;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.EditorTools
{
    public static class EnchantScreenPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Farm";
        private const string PrefabPath = PrefabDir + "/EnchantScreen.prefab";

        private const string ResBackground = "AirUI/Game_2_1_0";
        private const string ResPointer = "AirUI/Game_2_1_1";
        private const string ResWheelBase = "AirUI/Game_2_1_2";
        private const string ResIndicator = "AirUI/Game_2_1_3";

        [MenuItem("Tools/PetDemo/Generate Enchant Screen Prefab")]
        public static void Generate()
        {
            EnsureDir(PrefabDir);

            var rootGo = new GameObject("EnchantScreen", typeof(RectTransform), typeof(EnchantScreenView));
            var rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // 背景铺满（吃穿透）。
            var background = CreateStretchImage(rootRt, "Background", ResBackground);
            background.raycastTarget = true;

            // 中上部：激活本次玩法的植物（运行时由 EnchantScreenView 填充 sprite）。
            var plantImg = CreateCenteredImage(rootRt, "PlantImage", null, new Vector2(240f, 240f), new Vector2(0f, 560f));
            plantImg.enabled = false;

            // 转盘容器（居中偏下）。
            var wheelGo = new GameObject("Wheel", typeof(RectTransform));
            var wheelRt = wheelGo.GetComponent<RectTransform>();
            wheelRt.SetParent(rootRt, false);
            wheelRt.anchorMin = new Vector2(0.5f, 0.5f);
            wheelRt.anchorMax = new Vector2(0.5f, 0.5f);
            wheelRt.pivot = new Vector2(0.5f, 0.5f);
            wheelRt.anchoredPosition = new Vector2(0f, -360f);
            wheelRt.sizeDelta = new Vector2(640f, 640f);

            // 指示灯（Game_2_1_3）：层级低于底座 → 先创建（更小 siblingIndex）。
            var indicator = CreateCenteredImage(wheelRt, "Indicator", ResIndicator, new Vector2(96f, 96f), new Vector2(0f, 230f));
            indicator.raycastTarget = false;
            indicator.enabled = false;

            // 底座（Game_2_1_2）：点击区。
            var wheelBase = CreateCenteredImage(wheelRt, "WheelBase", ResWheelBase, new Vector2(560f, 560f), Vector2.zero);
            wheelBase.raycastTarget = true;

            // 指针（Game_2_1_1）：旋转中心由人工微调 pivot/anchoredPosition。
            var pointer = CreateCenteredImage(wheelRt, "Pointer", ResPointer, new Vector2(80f, 360f), Vector2.zero);
            pointer.raycastTarget = false;

            // 失败结算面板（默认隐藏）。
            var panelGo = new GameObject("ResultPanel", typeof(RectTransform), typeof(Image));
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.SetParent(rootRt, false);
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(720f, 480f);
            var panelImg = panelGo.GetComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.82f);
            panelImg.raycastTarget = true;

            var retryBtn = CreateButton(panelRt, "RetryButton", "重新挑战", new Vector2(0f, 80f), new Color(0.25f, 0.6f, 0.95f, 1f));
            var abandonBtn = CreateButton(panelRt, "AbandonButton", "放弃", new Vector2(0f, -90f), new Color(0.8f, 0.3f, 0.2f, 1f));
            panelGo.SetActive(false);

            // 绑定到 EnchantScreenView（用 SerializedObject 写入私有 [SerializeField]）。
            var view = rootGo.GetComponent<EnchantScreenView>();
            var so = new SerializedObject(view);
            so.FindProperty("background").objectReferenceValue = background;
            so.FindProperty("plantImage").objectReferenceValue = plantImg;
            so.FindProperty("wheelRoot").objectReferenceValue = wheelRt;
            so.FindProperty("wheelBase").objectReferenceValue = wheelBase;
            so.FindProperty("pointer").objectReferenceValue = pointer.rectTransform;
            so.FindProperty("indicator").objectReferenceValue = indicator;
            so.FindProperty("resultPanel").objectReferenceValue = panelGo;
            so.FindProperty("retryButton").objectReferenceValue = retryBtn;
            so.FindProperty("abandonButton").objectReferenceValue = abandonBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabPath);
            Object.DestroyImmediate(rootGo);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("EnchantScreen 预制件生成完成: " + PrefabPath);
        }

        private static Image CreateStretchImage(RectTransform parent, string name, string resPath)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            var sp = string.IsNullOrEmpty(resPath) ? null : Resources.Load<Sprite>(resPath);
            if (sp != null) { img.sprite = sp; img.preserveAspect = false; }
            else img.color = new Color(0.1f, 0.08f, 0.05f, 1f);
            return img;
        }

        private static Image CreateCenteredImage(RectTransform parent, string name, string resPath, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            var sp = string.IsNullOrEmpty(resPath) ? null : Resources.Load<Sprite>(resPath);
            if (sp != null) { img.sprite = sp; img.preserveAspect = true; }
            else img.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            return img;
        }

        private static Button CreateButton(RectTransform parent, string name, string label, Vector2 pos, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(320f, 110f);
            var img = go.GetComponent<Image>();
            img.color = color;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.SetParent(rt, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = FarmGridView.LoadBuiltinFont();
            text.fontSize = 40;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.raycastTarget = false;
            text.supportRichText = false;
            return btn;
        }

        private static void EnsureDir(string dir)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
#endif
