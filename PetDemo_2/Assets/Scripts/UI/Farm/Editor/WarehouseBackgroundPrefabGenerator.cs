#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using PetDemo.UI.Farm;

namespace PetDemo.EditorTools
{
    public static class WarehouseBackgroundPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Farm";
        private const string WarehouseBackgroundPrefabPath = PrefabDir + "/WarehouseBackground.prefab";
        private const string WarehouseSpriteResource = "AirUI/ZhongZiCangKu_1";

        [MenuItem("Tools/PetDemo/Generate Warehouse Background Prefab")]
        public static void Generate()
        {
            EnsureDir(PrefabDir);

            if (File.Exists(WarehouseBackgroundPrefabPath))
            {
                var root = PrefabUtility.LoadPrefabContents(WarehouseBackgroundPrefabPath);
                EnsureWarehouseStructure(root);
                PrefabUtility.SaveAsPrefabAsset(root, WarehouseBackgroundPrefabPath);
                PrefabUtility.UnloadPrefabContents(root);
                UnityEngine.Debug.Log(
                    "WarehouseBackground 预制体已补齐缺失节点（未覆盖已有子树）: " + WarehouseBackgroundPrefabPath);
            }
            else
            {
                var go = BuildNewWarehouseRoot();
                PrefabUtility.SaveAsPrefabAsset(go, WarehouseBackgroundPrefabPath);
                Object.DestroyImmediate(go);
                UnityEngine.Debug.Log("WarehouseBackground 预制体生成完成: " + WarehouseBackgroundPrefabPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static GameObject BuildNewWarehouseRoot()
        {
            var go = CreateWarehouseRoot();
            EnsureWarehouseStructure(go);
            return go;
        }

        private static GameObject CreateWarehouseRoot()
        {
            var go = new GameObject("WarehouseBackground", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -355f);
            rt.sizeDelta = new Vector2(1080f, 800f);

            var image = go.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>(WarehouseSpriteResource);
            image.preserveAspect = true;
            image.raycastTarget = true;
            return go;
        }

        private static void EnsureWarehouseStructure(GameObject root)
        {
            var rootRt = root.GetComponent<RectTransform>();
            if (rootRt == null)
                return;

            var contentRt = EnsureSeedWarehouseContent(rootRt);
            EnsureTabBarAndList(contentRt);
            EnsureSeedPackOptionTemplate(rootRt);
            WireListViewReferences(contentRt, rootRt);
        }

        private static RectTransform EnsureSeedWarehouseContent(RectTransform rootRt)
        {
            var contentTr = rootRt.Find("SeedWarehouseContent");
            RectTransform contentRt;
            if (contentTr != null)
            {
                contentRt = contentTr as RectTransform;
            }
            else
            {
                var go = new GameObject("SeedWarehouseContent", typeof(RectTransform));
                contentRt = go.GetComponent<RectTransform>();
                contentRt.SetParent(rootRt, false);
            }

            contentRt.anchorMin = Vector2.zero;
            contentRt.anchorMax = Vector2.one;
            contentRt.pivot = new Vector2(0.5f, 0.5f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;

            if (contentRt.GetComponent<SeedWarehouseListView>() == null)
                contentRt.gameObject.AddComponent<SeedWarehouseListView>();

            return contentRt;
        }

        private static void EnsureTabBarAndList(RectTransform contentRt)
        {
            var tabBar = EnsureChildRect(contentRt, "TabBar",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), new Vector2(1000f, 80f));

            EnsureTab(tabBar, "TabSeed", new Vector2(-260f, 0f), "种子", true);
            EnsureTab(tabBar, "TabPack", new Vector2(260f, 0f), "种子包", false);

            var listFrame = EnsureChildRect(contentRt, "ListFrame",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -128f), new Vector2(1000f, 600f));

            var emptyTr = listFrame.Find("EmptyHint");
            if (emptyTr == null)
            {
                var emptyHint = CreateText(listFrame, "EmptyHint", Vector2.zero, new Vector2(900f, 80f),
                    "暂无种子，去开包看看吧", 32);
                emptyHint.alignment = TextAnchor.MiddleCenter;
                emptyHint.color = new Color(0.92f, 0.92f, 0.92f, 0.85f);
                emptyHint.gameObject.SetActive(false);
            }
        }

        private static void EnsureTab(RectTransform tabBar, string name, Vector2 pos, string label, bool activeTab)
        {
            var tabTr = tabBar.Find(name);
            Image tabBg;
            if (tabTr != null)
            {
                tabBg = tabTr.GetComponent<Image>();
            }
            else
            {
                tabBg = CreateImage(tabBar, name, pos, new Vector2(480f, 80f));
                tabBg.raycastTarget = true;
                var btn = tabBg.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = tabBg;

                var tabLabel = CreateText(tabBg.rectTransform, "Label", Vector2.zero, new Vector2(480f, 80f),
                    label, 40);
                tabLabel.alignment = TextAnchor.MiddleCenter;
                tabLabel.color = new Color(0.18f, 0.18f, 0.18f, 1f);
            }

            if (tabBg != null)
                tabBg.color = activeTab ? Color.white : new Color(1f, 1f, 1f, 0.5f);
        }

        private static void EnsureSeedPackOptionTemplate(RectTransform rootRt)
        {
            Transform template = null;
            for (int i = 0; i < rootRt.childCount; i++)
            {
                var child = rootRt.GetChild(i);
                if (child.name == "SeedPackOptionTemplate")
                {
                    template = child;
                    break;
                }
            }

            if (template != null)
                return;

            var go = new GameObject("SeedPackOptionTemplate", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(rootRt, false);
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(360f, 360f);

            var bg = go.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.25f);
            bg.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = bg;

            var bigIcon = CreateImage(rt, "BigIcon", new Vector2(0f, -160f), new Vector2(260f, 260f));
            bigIcon.sprite = Resources.Load<Sprite>(WarehouseSpriteResource);
            bigIcon.preserveAspect = true;
            bigIcon.raycastTarget = false;

            var smallIcon = CreateImage(rt, "SmallIcon", new Vector2(118f, -50f), new Vector2(96f, 96f));
            smallIcon.sprite = Resources.Load<Sprite>(WarehouseSpriteResource);
            smallIcon.preserveAspect = true;
            smallIcon.raycastTarget = false;

            var count = CreateText(smallIcon.rectTransform, "Count", new Vector2(0f, -58f), new Vector2(100f, 40f),
                "x 0", 24);
            count.alignment = TextAnchor.MiddleCenter;
            count.color = Color.white;

            go.SetActive(false);
        }

        private static void WireListViewReferences(RectTransform contentRt, RectTransform panelRootRt)
        {
            var view = contentRt.GetComponent<SeedWarehouseListView>();
            if (view == null)
                return;

            var tabBar = contentRt.Find("TabBar");
            var listFrame = contentRt.Find("ListFrame") as RectTransform;
            var tabSeedTr = tabBar != null ? tabBar.Find("TabSeed") : null;
            var tabPackTr = tabBar != null ? tabBar.Find("TabPack") : null;

            Image tabSeedBg = tabSeedTr != null ? tabSeedTr.GetComponent<Image>() : null;
            Image tabPackBg = tabPackTr != null ? tabPackTr.GetComponent<Image>() : null;
            Button tabSeedButton = tabSeedTr != null ? tabSeedTr.GetComponent<Button>() : null;
            Button tabPackButton = tabPackTr != null ? tabPackTr.GetComponent<Button>() : null;

            Text emptyHint = null;
            if (listFrame != null)
            {
                var emptyTr = listFrame.Find("EmptyHint");
                if (emptyTr != null)
                    emptyHint = emptyTr.GetComponent<Text>();
            }

            RectTransform packTemplate = null;
            var templateTr = panelRootRt.Find("SeedPackOptionTemplate");
            if (templateTr != null)
                packTemplate = templateTr as RectTransform;

            var so = new SerializedObject(view);
            so.FindProperty("tabSeedButton").objectReferenceValue = tabSeedButton;
            so.FindProperty("tabPackButton").objectReferenceValue = tabPackButton;
            so.FindProperty("tabSeedBg").objectReferenceValue = tabSeedBg;
            so.FindProperty("tabPackBg").objectReferenceValue = tabPackBg;
            so.FindProperty("listRoot").objectReferenceValue = listFrame;
            so.FindProperty("emptyHint").objectReferenceValue = emptyHint;
            so.FindProperty("packOptionTemplate").objectReferenceValue = packTemplate;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform EnsureChildRect(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var tr = parent.Find(name);
            if (tr != null)
                return tr as RectTransform;

            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            return rt;
        }

        private static void EnsureDir(string dir)
        {
            if (Directory.Exists(dir))
                return;
            Directory.CreateDirectory(dir);
        }

        private static Image CreateImage(RectTransform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return go.GetComponent<Image>();
        }

        private static Text CreateText(RectTransform parent, string name, Vector2 pos, Vector2 size, string content, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var text = go.GetComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.raycastTarget = false;
            return text;
        }
    }
}
#endif
