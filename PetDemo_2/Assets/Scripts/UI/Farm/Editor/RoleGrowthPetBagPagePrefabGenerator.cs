#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using PetDemo.Core;
using PetDemo.UI.Farm;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// SPEC §9.10.5：精灵背包页独立预制体。
    /// 输出 Assets/Resources/Prefabs/Farm/RoleGrowthPetBagPage.prefab
    /// </summary>
    public static class RoleGrowthPetBagPagePrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Farm";
        private const string PrefabPath = PrefabDir + "/RoleGrowthPetBagPage.prefab";

        private const int GridColumns = 5;
        private const float CellSize = 168f;

        [MenuItem("Tools/PetDemo/Generate Role Growth Pet Bag Page Prefab")]
        public static void Generate()
        {
            EnsureDir(PrefabDir);
            BuildPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("RoleGrowthPetBagPage 预制件生成完成: " + PrefabPath);
        }

        [MenuItem("Tools/PetDemo/Repair Role Growth Pet Bag Page Prefab")]
        public static void RepairMissingScripts()
        {
            if (!File.Exists(PrefabPath))
            {
                UnityEngine.Debug.LogWarning("预制体不存在，改为完整生成: " + PrefabPath);
                Generate();
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var cellGo = FindDeepChild(root.transform, "PetBagCellTemplate")?.gameObject;
                if (cellGo == null)
                {
                    UnityEngine.Debug.LogError("未找到 PetBagCellTemplate，请执行完整生成。");
                    return;
                }

                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(cellGo);
                var staleCell = cellGo.GetComponent<RoleGrowthPetBagCellView>();
                if (staleCell != null)
                    Object.DestroyImmediate(staleCell);

                var warehouse = root.GetComponentInChildren<RoleGrowthPetBagWarehouseView>(true);
                if (warehouse != null)
                {
                    var content = cellGo.transform.parent as RectTransform;
                    SerializeWarehouse(warehouse, content, cellGo);
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                UnityEngine.Debug.Log("已修复 RoleGrowthPetBagPage（PetBagCellTemplate 不再挂载 CellView 脚本）: " + PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent.name == name)
                return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var found = FindDeepChild(parent.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static void BuildPrefab()
        {
            var root = new GameObject("RoleGrowthPetBagPage", typeof(RectTransform));
            var rootRt = root.GetComponent<RectTransform>();
            StretchFull(rootRt);

            var deployedSection = CreateStretchChild(rootRt, "DeployedSection", 0.62f, 1f);
            var warehouseSection = CreateStretchChild(rootRt, "WarehouseSection", 0f, 0.62f);

            AddTitle(deployedSection, "上场", new Vector2(0f, -8f));
            var lowerSlot = BuildDeployedSlot(deployedSection, "Slot_LowerLeft", PetFieldSlot.LowerLeft,
                new Vector2(-180f, -120f));
            var upperSlot = BuildDeployedSlot(deployedSection, "Slot_UpperLeft", PetFieldSlot.UpperLeft,
                new Vector2(180f, -120f));

            var undeployBtn = BuildUndeployButton(deployedSection);

            AddTitle(warehouseSection, "精灵仓库", new Vector2(0f, -8f));
            var warehouseView = BuildWarehouseScroll(warehouseSection);

            var pageView = root.AddComponent<RoleGrowthPetBagPageView>();
            SerializePageView(pageView, lowerSlot, upperSlot, undeployBtn, warehouseView);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }

        private static RoleGrowthPetBagDeployedSlotView BuildDeployedSlot(
            RectTransform parent, string name, PetFieldSlot slot, Vector2 pos)
        {
            var slotRt = CreateCenteredRect(parent, name, new Vector2(220f, 320f), pos);

            var emptyGo = new GameObject("EmptyState", typeof(RectTransform));
            var emptyRt = emptyGo.GetComponent<RectTransform>();
            emptyRt.SetParent(slotRt, false);
            StretchFull(emptyRt);

            var frame = AddImage(emptyRt, "EmptyFrame", new Color(0.15f, 0.18f, 0.24f, 0.9f));
            frame.raycastTarget = false;

            var plusGo = new GameObject("PlusText", typeof(RectTransform), typeof(Text));
            var plusRt = plusGo.GetComponent<RectTransform>();
            plusRt.SetParent(emptyRt, false);
            StretchFull(plusRt);
            var plusText = plusGo.GetComponent<Text>();
            plusText.text = "+";
            plusText.font = LoadFont();
            plusText.fontSize = 72;
            plusText.alignment = TextAnchor.MiddleCenter;
            plusText.color = new Color(0.75f, 0.82f, 0.95f, 1f);
            plusText.raycastTarget = false;

            var occupiedGo = new GameObject("OccupiedState", typeof(RectTransform));
            var occupiedRt = occupiedGo.GetComponent<RectTransform>();
            occupiedRt.SetParent(slotRt, false);
            StretchFull(occupiedRt);
            occupiedGo.SetActive(false);

            var previewRoot = CreateCenteredRect(occupiedRt, "PreviewRoot", new Vector2(200f, 260f), new Vector2(0f, 20f));

            var nameGo = new GameObject("NameText", typeof(RectTransform), typeof(Text));
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.SetParent(occupiedRt, false);
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 0f);
            nameRt.pivot = new Vector2(0.5f, 0f);
            nameRt.anchoredPosition = new Vector2(0f, 8f);
            nameRt.sizeDelta = new Vector2(0f, 40f);
            var nameText = nameGo.GetComponent<Text>();
            nameText.font = LoadFont();
            nameText.fontSize = 28;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.white;
            nameText.raycastTarget = false;

            var undeployAnchor = CreateCenteredRect(slotRt, "UndeployAnchor", new Vector2(160f, 56f), new Vector2(0f, -200f));

            var hitGo = new GameObject("HitArea", typeof(RectTransform), typeof(Image), typeof(Button));
            var hitRt = hitGo.GetComponent<RectTransform>();
            hitRt.SetParent(slotRt, false);
            StretchFull(hitRt);
            var hitImg = hitGo.GetComponent<Image>();
            hitImg.color = new Color(1f, 1f, 1f, 0f);
            var hitBtn = hitGo.GetComponent<Button>();
            hitBtn.transition = Selectable.Transition.None;

            var slotView = slotRt.gameObject.AddComponent<RoleGrowthPetBagDeployedSlotView>();
            SerializeDeployedSlot(slotView, slot, hitBtn, emptyGo, plusText, occupiedGo, previewRoot, nameText, undeployAnchor);
            return slotView;
        }

        private static Button BuildUndeployButton(RectTransform parent)
        {
            var btnRt = CreateCenteredRect(parent, "UndeployButton", new Vector2(160f, 56f), Vector2.zero);
            var img = btnRt.gameObject.AddComponent<Image>();
            img.color = new Color(0.75f, 0.28f, 0.28f, 0.95f);
            var btn = btnRt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(btnRt, false);
            StretchFull(labelRt);
            var label = labelGo.GetComponent<Text>();
            label.text = "卸下";
            label.font = LoadFont();
            label.fontSize = 30;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;

            btnRt.gameObject.SetActive(false);
            return btn;
        }

        private static RoleGrowthPetBagWarehouseView BuildWarehouseScroll(RectTransform parent)
        {
            var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.SetParent(parent, false);
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(16f, 16f);
            scrollRt.offsetMax = new Vector2(-16f, -56f);
            scrollGo.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.5f);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.SetParent(scrollRt, false);
            StretchFull(viewportRt);
            var vpImg = viewportGo.GetComponent<Image>();
            vpImg.color = new Color(1f, 1f, 1f, 0.02f);
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.SetParent(viewportRt, false);
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 0f);

            var grid = contentGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(CellSize, CellSize);
            grid.spacing = new Vector2(8f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = GridColumns;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.padding = new RectOffset(8, 8, 8, 8);

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewportRt;
            scroll.content = contentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var cellTemplate = BuildCellTemplate(contentRt);

            var warehouseView = scrollGo.AddComponent<RoleGrowthPetBagWarehouseView>();
            SerializeWarehouse(warehouseView, contentRt, cellTemplate.gameObject);
            return warehouseView;
        }

        private static RectTransform BuildCellTemplate(RectTransform parent)
        {
            var cellRt = CreateCenteredRect(parent, "PetBagCellTemplate", new Vector2(CellSize, CellSize), Vector2.zero);
            var frame = cellRt.gameObject.AddComponent<Image>();
            frame.color = new Color(0.22f, 0.26f, 0.34f, 0.92f);

            var btn = cellRt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = frame;

            var nameGo = new GameObject("NameText", typeof(RectTransform), typeof(Text));
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.SetParent(cellRt, false);
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 0f);
            nameRt.pivot = new Vector2(0.5f, 0f);
            nameRt.anchoredPosition = new Vector2(0f, 8f);
            nameRt.sizeDelta = new Vector2(-8f, 36f);
            var nameText = nameGo.GetComponent<Text>();
            nameText.font = LoadFont();
            nameText.fontSize = 22;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.white;
            nameText.raycastTarget = false;

            var badgeGo = new GameObject("DeployedBadge", typeof(RectTransform), typeof(Text));
            var badgeRt = badgeGo.GetComponent<RectTransform>();
            badgeRt.SetParent(cellRt, false);
            badgeRt.anchorMin = new Vector2(1f, 1f);
            badgeRt.anchorMax = new Vector2(1f, 1f);
            badgeRt.pivot = new Vector2(1f, 1f);
            badgeRt.anchoredPosition = new Vector2(-6f, -6f);
            badgeRt.sizeDelta = new Vector2(72f, 28f);
            var badgeText = badgeGo.GetComponent<Text>();
            badgeText.text = "上场";
            badgeText.font = LoadFont();
            badgeText.fontSize = 18;
            badgeText.alignment = TextAnchor.MiddleCenter;
            badgeText.color = new Color(1f, 0.85f, 0.4f, 1f);
            badgeText.raycastTarget = false;
            badgeGo.SetActive(false);

            cellRt.gameObject.SetActive(false);
            return cellRt;
        }

        private static void SerializePageView(
            RoleGrowthPetBagPageView view,
            RoleGrowthPetBagDeployedSlotView lower,
            RoleGrowthPetBagDeployedSlotView upper,
            Button undeploy,
            RoleGrowthPetBagWarehouseView warehouse)
        {
            var so = new SerializedObject(view);
            so.FindProperty("lowerSlot").objectReferenceValue = lower;
            so.FindProperty("upperSlot").objectReferenceValue = upper;
            so.FindProperty("undeployButton").objectReferenceValue = undeploy;
            so.FindProperty("warehouse").objectReferenceValue = warehouse;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SerializeDeployedSlot(
            RoleGrowthPetBagDeployedSlotView view,
            PetFieldSlot slot,
            Button hit,
            GameObject emptyState,
            Text plusText,
            GameObject occupiedState,
            RectTransform previewRoot,
            Text nameText,
            RectTransform undeployAnchor)
        {
            var so = new SerializedObject(view);
            so.FindProperty("fieldSlot").enumValueIndex = (int)slot;
            so.FindProperty("slotButton").objectReferenceValue = hit;
            so.FindProperty("emptyState").objectReferenceValue = emptyState;
            so.FindProperty("emptyPlusText").objectReferenceValue = plusText;
            so.FindProperty("occupiedState").objectReferenceValue = occupiedState;
            so.FindProperty("previewRoot").objectReferenceValue = previewRoot;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("undeployAnchor").objectReferenceValue = undeployAnchor;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SerializeWarehouse(
            RoleGrowthPetBagWarehouseView view,
            RectTransform content,
            GameObject template)
        {
            var so = new SerializedObject(view);
            so.FindProperty("contentRoot").objectReferenceValue = content;
            so.FindProperty("cellTemplate").objectReferenceValue = template;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddTitle(RectTransform parent, string title, Vector2 pos)
        {
            var go = new GameObject("SectionTitle", typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(400f, 44f);
            var t = go.GetComponent<Text>();
            t.text = title;
            t.font = LoadFont();
            t.fontSize = 34;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(0.9f, 0.92f, 0.98f, 1f);
            t.raycastTarget = false;
        }

        private static Image AddImage(RectTransform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            StretchFull(rt);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        private static RectTransform CreateStretchChild(RectTransform parent, string name, float anchorMinY, float anchorMaxY)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, anchorMinY);
            rt.anchorMax = new Vector2(1f, anchorMaxY);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        private static RectTransform CreateCenteredRect(RectTransform parent, string name, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static Font LoadFont() =>
            Resources.GetBuiltinResource<Font>("Arial.ttf");

        private static void EnsureDir(string dir)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
#endif
