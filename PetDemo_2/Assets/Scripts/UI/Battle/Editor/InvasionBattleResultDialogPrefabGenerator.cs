#if UNITY_EDITOR
using System.IO;
using PetDemo.UI.Battle;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// SPEC §12.3 (v3.53)：生成入侵战斗结算弹窗预制体。
    /// 产出 Assets/Resources/Prefabs/Battle/InvasionBattleResultDialog.prefab。
    /// </summary>
    [InitializeOnLoad]
    public static class InvasionBattleResultDialogPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Battle";
        private const string PrefabPath = PrefabDir + "/InvasionBattleResultDialog.prefab";

        static InvasionBattleResultDialogPrefabGenerator()
        {
            EditorApplication.delayCall += EnsurePrefabExists;
        }

        /// <summary>编辑器启动时若预制体缺失或无法导入则自动生成。</summary>
        private static void EnsurePrefabExists()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (File.Exists(PrefabPath))
            {
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (existing != null)
                {
                    if (!HasRewardRowTemplate(existing))
                        UpgradeRewardListLayout();
                    return;
                }
                UnityEngine.Debug.LogWarning(
                    "[InvasionBattleResultDialog] 预制体文件存在但 Unity 无法导入，将重新生成: " + PrefabPath);
            }

            Generate();
        }

        [MenuItem("Tools/PetDemo/Upgrade Invasion Battle Result Dialog (RewardRow Layout)")]
        public static void UpgradeRewardListLayoutMenu()
        {
            if (!File.Exists(PrefabPath))
            {
                Generate();
                return;
            }
            UpgradeRewardListLayout();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("[InvasionBattleResultDialog] RewardList/RewardRow 布局已升级: " + PrefabPath);
        }

        private static bool HasRewardRowTemplate(GameObject prefabRoot)
        {
            if (prefabRoot == null)
                return false;
            var list = prefabRoot.transform.Find("RewardList");
            return list != null && list.Find("RewardRow") != null;
        }

        /// <summary>在既有预制体上补全 RewardList 布局与 RewardRow 模板，保留其它节点位置。</summary>
        private static void UpgradeRewardListLayout()
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefabAsset == null)
            {
                Generate();
                return;
            }

            var root = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
            if (root == null)
            {
                Generate();
                return;
            }

            try
            {
                var listRt = root.transform.Find("RewardList") as RectTransform;
                if (listRt == null)
                {
                    listRt = BuildRewardList(root.GetComponent<RectTransform>());
                }
                else
                {
                    EnsureRewardListLayoutComponents(listRt);
                }

                InvasionBattleRewardRowView rowTemplate = null;
                var existingRow = listRt.Find("RewardRow");
                if (existingRow != null)
                    rowTemplate = existingRow.GetComponent<InvasionBattleRewardRowView>();
                if (rowTemplate == null)
                    rowTemplate = BuildRewardRowTemplate(listRt);

                var view = root.GetComponent<InvasionBattleResultDialogView>();
                if (view != null)
                {
                    var titleText = root.transform.Find("ResultText")?.GetComponent<Text>();
                    var autoToggle = root.transform.Find("AutoAdvanceWinRow")?.GetComponent<Toggle>();
                    var closeBtn = root.GetComponent<Button>();
                    SerializeView(view, titleText, listRt, rowTemplate, autoToggle, closeBtn);
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void EnsureRewardListLayoutComponents(RectTransform listRt)
        {
            if (listRt.GetComponent<VerticalLayoutGroup>() == null)
            {
                var vlg = listRt.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.spacing = 8f;
                vlg.padding = new RectOffset(0, 0, 4, 4);
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
            }

            if (listRt.GetComponent<ContentSizeFitter>() == null)
            {
                var fitter = listRt.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        private static readonly Vector2 DialogSize = new Vector2(880f, 750f);
        private static readonly Vector3 DialogScale = new Vector3(1.4f, 1.4f, 1f);
        private static readonly Color PanelColor = new Color(0.10f, 0.10f, 0.14f, 0.95f);

        [MenuItem("Tools/PetDemo/Generate Invasion Battle Result Dialog Prefab")]
        public static void Generate()
        {
            EnsureDir(PrefabDir);
            BuildPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("InvasionBattleResultDialog 预制件生成完成: " + PrefabPath);
        }

        private static void BuildPrefab()
        {
            var root = new GameObject("InvasionBattleResultDialog", typeof(RectTransform));
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.5f, 0.5f);
            rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.anchoredPosition = Vector2.zero;
            rootRt.sizeDelta = DialogSize;
            rootRt.localScale = DialogScale;

            var panelImage = root.AddComponent<Image>();
            panelImage.color = PanelColor;
            panelImage.raycastTarget = true;

            var closeButton = root.AddComponent<Button>();
            closeButton.transition = Selectable.Transition.None;
            closeButton.targetGraphic = panelImage;

            var titleText = BuildText(rootRt, "ResultText",
                new Vector2(0f, 100f), new Vector2(560f, 120f),
                string.Empty, 64, TextAnchor.MiddleCenter, Color.white);

            var rewardListRt = BuildRewardList(rootRt);
            var rewardRowTemplate = BuildRewardRowTemplate(rewardListRt);

            var autoAdvanceToggle = BuildAutoAdvanceRow(rootRt, "AutoAdvanceWinRow",
                new Vector2(0f, -280f), new Vector2(520f, 56f));
            autoAdvanceToggle.gameObject.SetActive(false);

            BuildText(rootRt, "HintText",
                new Vector2(0f, -420f), new Vector2(560f, 60f),
                "点击关闭", 28, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.78f));

            var view = root.AddComponent<InvasionBattleResultDialogView>();
            SerializeView(view, titleText, rewardListRt, rewardRowTemplate, autoAdvanceToggle, closeButton);

            root.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }

        private static RectTransform BuildRewardList(RectTransform parent)
        {
            var listRt = CreateCenteredRect(parent, "RewardList",
                new Vector2(0f, -30f), new Vector2(520f, 210f));
            listRt.gameObject.SetActive(false);

            var vlg = listRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(0, 0, 4, 4);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = listRt.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return listRt;
        }

        private static InvasionBattleRewardRowView BuildRewardRowTemplate(RectTransform listRt)
        {
            var rowRt = CreateCenteredRect(listRt, "RewardRow",
                Vector2.zero, new Vector2(500f, 120f));
            rowRt.anchorMin = new Vector2(0.5f, 0.5f);
            rowRt.anchorMax = new Vector2(0.5f, 0.5f);
            rowRt.pivot = new Vector2(0.5f, 0.5f);

            var rowLayout = rowRt.gameObject.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 120f;
            rowLayout.minHeight = 120f;
            rowLayout.ignoreLayout = true;

            var hlg = rowRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.spacing = 16f;
            hlg.padding = new RectOffset(8, 8, 0, 0);
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var iconRt = CreateCenteredRect(rowRt, "Icon",
                Vector2.zero, new Vector2(120f, 120f));
            var iconLayout = iconRt.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = 120f;
            iconLayout.preferredHeight = 120f;
            iconLayout.minWidth = 120f;
            iconLayout.minHeight = 120f;
            var iconImage = iconRt.gameObject.AddComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
            iconImage.color = Color.white;

            var countRt = CreateCenteredRect(rowRt, "Count",
                Vector2.zero, new Vector2(320f, 120f));
            var countLayout = countRt.gameObject.AddComponent<LayoutElement>();
            countLayout.flexibleWidth = 1f;
            countLayout.preferredHeight = 120f;
            var countText = countRt.gameObject.AddComponent<Text>();
            countText.text = "x 1";
            countText.font = LoadBuiltinFont();
            countText.fontSize = 30;
            countText.alignment = TextAnchor.MiddleLeft;
            countText.color = new Color(1f, 0.95f, 0.70f, 1f);
            countText.raycastTarget = false;

            var rowView = rowRt.gameObject.AddComponent<InvasionBattleRewardRowView>();
            SerializeRewardRow(rowView, iconImage, countText);
            rowRt.gameObject.SetActive(false);
            return rowView;
        }

        private static void SerializeRewardRow(
            InvasionBattleRewardRowView rowView, Image iconImage, Text countText)
        {
            var so = new SerializedObject(rowView);
            so.FindProperty("iconImage").objectReferenceValue = iconImage;
            so.FindProperty("countText").objectReferenceValue = countText;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Toggle BuildAutoAdvanceRow(RectTransform parent, string rowName, Vector2 pos, Vector2 size)
        {
            var rowRt = CreateCenteredRect(parent, rowName, pos, size);

            var bgRt = CreateCenteredRect(rowRt, "Background",
                new Vector2(-194f, 0f), new Vector2(48f, 48f));
            bgRt.anchorMin = new Vector2(0f, 0.5f);
            bgRt.anchorMax = new Vector2(0f, 0.5f);
            bgRt.pivot = new Vector2(0f, 0.5f);
            bgRt.anchoredPosition = new Vector2(28f, 0f);
            var bgImg = bgRt.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0.22f, 0.22f, 0.28f, 1f);
            bgImg.raycastTarget = true;

            var checkRt = CreateCenteredRect(bgRt, "Checkmark", Vector2.zero, new Vector2(36f, 36f));
            var checkImg = checkRt.gameObject.AddComponent<Image>();
            checkImg.color = new Color(0.32f, 0.82f, 0.45f, 1f);
            checkImg.raycastTarget = false;

            var labelRt = CreateCenteredRect(rowRt, "Label",
                new Vector2(76f, 0f), new Vector2(size.x - 168f, 48f));
            labelRt.anchorMin = new Vector2(0f, 0.5f);
            labelRt.anchorMax = new Vector2(1f, 0.5f);
            labelRt.pivot = new Vector2(0f, 0.5f);
            labelRt.anchoredPosition = new Vector2(152f, 0f);
            var label = labelRt.gameObject.AddComponent<Text>();
            label.text = "自动推进关卡";
            label.font = LoadBuiltinFont();
            label.fontSize = 28;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = Color.white;
            label.raycastTarget = false;

            var toggle = rowRt.gameObject.AddComponent<Toggle>();
            toggle.transition = Selectable.Transition.None;
            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;
            toggle.isOn = false;
            return toggle;
        }

        private static Text BuildText(
            RectTransform parent, string name, Vector2 pos, Vector2 size,
            string text, int fontSize, TextAnchor alignment, Color color)
        {
            var rt = CreateCenteredRect(parent, name, pos, size);
            var label = rt.gameObject.AddComponent<Text>();
            label.text = text;
            label.font = LoadBuiltinFont();
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        private static void SerializeView(
            InvasionBattleResultDialogView view,
            Text titleText,
            RectTransform rewardListRoot,
            InvasionBattleRewardRowView rewardRowTemplate,
            Toggle autoAdvanceToggle,
            Button closePanelButton)
        {
            var so = new SerializedObject(view);
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("rewardListRoot").objectReferenceValue = rewardListRoot;
            so.FindProperty("rewardRowTemplate").objectReferenceValue = rewardRowTemplate;
            so.FindProperty("autoAdvanceToggle").objectReferenceValue = autoAdvanceToggle;
            so.FindProperty("closePanelButton").objectReferenceValue = closePanelButton;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform CreateCenteredRect(
            RectTransform parent, string name, Vector2 anchoredPosition, Vector2 sizeDelta)
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
