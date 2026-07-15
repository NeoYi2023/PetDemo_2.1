// SPEC §12.13：详细属性弹窗 DetailAttributeModal。
// 上：角色待机；中：与主界面一致的 HP/攻击/速度；下：六宫雷达图。
using System;
using System.Collections.Generic;
using PetDemo.Battle;
using PetDemo.Core;
using PetDemo.UI;
using PetDemo.UI.Farm;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Battle
{
    [DisallowMultipleComponent]
    public sealed class DetailAttributeModalView : MonoBehaviour
    {
        public const string ResPrefabPath = "Prefabs/Battle/DetailAttributeModal";
        public const string ResLiuGong = "AirUI/LiuGong_1";
        public const string ResPlayerPrefab = "Prefabs/Air/Hero_Role_cunmin";
        public const string SkeletonGraphicShaderName = "Spine/SkeletonGraphic";

        public const string PanelObjectName = "DetailAttributeModal";
        public const string DimName = "Dim";
        public const string TopAreaName = "TopArea";
        public const string MiddleAreaName = "MiddleArea";
        public const string BottomAreaName = "BottomArea";
        public const string PlayerSlotName = "PlayerSlot";
        public const string HexChartName = "HexRadarChart";
        public const string HexLabelsName = "HexLabels";
        public const string CloseButtonName = "CloseButton";

        private static readonly Vector2 CharacterSize = new Vector2(720f, 1200f);
        private const float CharacterScale = 0.45f;
        private static readonly Vector2 CloseButtonSize = new Vector2(96f, 96f);
        private const float HexLabelRadius = 210f;
        public const int HexLabelCount = 6;

        private static readonly string[] IdleAnimCandidates =
            { "standby_1", "standby", "idle", "exclusive_2", "animation" };

        private static DetailAttributeModalView instance;

        [SerializeField] private Image dim;
        [SerializeField] private RectTransform playerSlot;
        [SerializeField] private Text hpText;
        [SerializeField] private Text atkText;
        [SerializeField] private Text speedText;
        [SerializeField] private HexRadarChartGraphic hexChart;
        [SerializeField] private Text[] hexLabels = new Text[HexLabelCount];
        [SerializeField] private Button closeButton;

        private RectTransform panelRt;
        private RectTransform canvasRectCache;
        private bool wired;
        private bool playerBuilt;

        public bool IsShown => gameObject != null && gameObject.activeSelf;

        internal void AssignRuntimeRefs(Image dimImg, RectTransform slot, Text hp, Text atk, Text speed,
            HexRadarChartGraphic chart, Text[] labels, Button close)
        {
            dim = dimImg;
            playerSlot = slot;
            hpText = hp;
            atkText = atk;
            speedText = speed;
            hexChart = chart;
            hexLabels = labels;
            closeButton = close;
        }

        public static DetailAttributeModalView GetOrCreate(RectTransform canvasRect)
        {
            if (canvasRect == null)
            {
                UnityEngine.Debug.LogWarning("[DetailAttributeModalView] GetOrCreate: canvasRect 为空");
                return null;
            }

            if (instance != null && instance.panelRt != null)
            {
                instance.canvasRectCache = canvasRect;
                return instance;
            }

            var existing = canvasRect.Find(PanelObjectName);
            if (existing != null)
            {
                var existView = existing.GetComponent<DetailAttributeModalView>();
                if (existView == null)
                    existView = existing.gameObject.AddComponent<DetailAttributeModalView>();
                existView.panelRt = existing as RectTransform;
                existView.canvasRectCache = canvasRect;
                instance = existView;
                return existView;
            }

            var prefab = Resources.Load<GameObject>(ResPrefabPath);
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, canvasRect, false);
            }
            else
            {
                UnityEngine.Debug.LogWarning(
                    "[DetailAttributeModalView] 缺少预制体 Resources/" + ResPrefabPath +
                    "，使用运行时回退 UI；请在编辑器执行 Tools/PetDemo/Generate Detail Attribute Modal Prefab。");
                go = BuildRuntimeFallback(canvasRect);
            }

            go.name = PanelObjectName;
            var rt = go.GetComponent<RectTransform>();
            if (rt == null)
                rt = go.AddComponent<RectTransform>();
            BottomNavAttachedScreenLayout.StretchFull(rt);

            var view = go.GetComponent<DetailAttributeModalView>();
            if (view == null)
                view = go.AddComponent<DetailAttributeModalView>();
            view.panelRt = rt;
            view.canvasRectCache = canvasRect;
            instance = view;
            return view;
        }

        public void Show(RoleStats stats, IReadOnlyDictionary<string, int> enhanceBonuses)
        {
            EnsureFieldsFromHierarchy();
            WireOnce();

            RefreshMiddleStats(stats);
            RefreshHexChart(enhanceBonuses);
            EnsurePlayerBuilt();
            ForceRefreshHexChartLayout();

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public static void HideIfAny()
        {
            if (instance != null && instance.IsShown)
                instance.Hide();
        }

        private void WireOnce()
        {
            if (wired)
                return;

            if (dim != null)
            {
                var dimBtn = dim.GetComponent<Button>();
                if (dimBtn == null)
                    dimBtn = dim.gameObject.AddComponent<Button>();
                dimBtn.transition = Selectable.Transition.None;
                dimBtn.targetGraphic = dim;
                dimBtn.onClick.RemoveAllListeners();
                dimBtn.onClick.AddListener(Hide);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Hide);
            }

            wired = true;
        }

        private void RefreshMiddleStats(RoleStats role)
        {
            if (role == null)
            {
                if (hpText != null) hpText.text = "-- / --";
                if (atkText != null) atkText.text = "--";
                if (speedText != null) speedText.text = "--";
                return;
            }

            if (hpText != null) hpText.text = $"{role.currentHp} / {role.maxHp}";
            if (atkText != null) atkText.text = $"{role.atk}";
            if (speedText != null) speedText.text = $"{role.agility}";
        }

        private void RefreshHexChart(IReadOnlyDictionary<string, int> enhanceBonuses)
        {
            var values = new int[AttrEnhanceConfigCatalog.HexRadarAttrIds.Length];
            for (int i = 0; i < values.Length; i++)
            {
                string id = AttrEnhanceConfigCatalog.HexRadarAttrIds[i];
                values[i] = 0;
                if (enhanceBonuses != null && enhanceBonuses.TryGetValue(id, out int v))
                    values[i] = Mathf.Max(0, v);
            }

            if (hexChart != null)
                hexChart.SetValuesFromInts(values);

            if (hexLabels != null)
            {
                for (int i = 0; i < hexLabels.Length && i < values.Length; i++)
                {
                    if (hexLabels[i] == null)
                        continue;
                    string displayName = i < AttrEnhanceConfigCatalog.HexRadarDisplayNames.Length
                        ? AttrEnhanceConfigCatalog.HexRadarDisplayNames[i]
                        : AttrEnhanceConfigCatalog.HexRadarAttrIds[i];
                    hexLabels[i].text = $"{displayName}\n{values[i]}";
                }
            }
        }

        private void ForceRefreshHexChartLayout()
        {
            if (hexChart == null)
                return;
            var chartRt = hexChart.rectTransform;
            if (chartRt != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(chartRt);
            HexRadarChartGraphic.EnsureCanvasRenderer(hexChart.gameObject);
            hexChart.SetVerticesDirty();
            hexChart.SetMaterialDirty();
        }

        private void EnsurePlayerBuilt()
        {
            if (playerBuilt || playerSlot == null)
                return;

            for (int i = playerSlot.childCount - 1; i >= 0; i--)
                Destroy(playerSlot.GetChild(i).gameObject);

            TryBuildSkeletonGraphic(ResPlayerPrefab, playerSlot);
            playerBuilt = true;
        }

        private void TryBuildSkeletonGraphic(string resourcesPath, RectTransform parent)
        {
            var prefab = Resources.Load<GameObject>(resourcesPath);
            if (prefab == null)
            {
                BuildFallbackBlock(parent);
                return;
            }

            var probe = Instantiate(prefab);
            probe.SetActive(false);
            var srcAnim = probe.GetComponent<SkeletonAnimation>()
                ?? probe.GetComponentInChildren<SkeletonAnimation>(true);
            var dataAsset = srcAnim != null ? srcAnim.skeletonDataAsset : null;
            // SPEC §12.14.9 (v3.225)：缓存预制体权威皮肤名，构建后运行时应用，避免变体皮肤未生效导致「有节点无外形」。
            string initialSkinName = srcAnim != null ? srcAnim.initialSkinName : null;
            Destroy(probe);

            if (dataAsset == null)
            {
                BuildFallbackBlock(parent);
                return;
            }

            var shader = Shader.Find(SkeletonGraphicShaderName);
            if (shader == null)
            {
                BuildFallbackBlock(parent);
                return;
            }

            var uiMaterial = SkeletonGraphicUiMaterialFactory.CreateForPmaVertexColors(shader);
            var roleGo = new GameObject("Skeleton");
            var rt = roleGo.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = CharacterSize;
            rt.localScale = new Vector3(-1f, 1f, 1f);

            var skel = SkeletonGraphic.AddSkeletonGraphicComponent(roleGo, dataAsset, uiMaterial);
            if (skel == null || !skel.IsValid)
            {
                Destroy(roleGo);
                BuildFallbackBlock(parent);
                return;
            }

            skel.raycastTarget = false;
            ApplyInitialSkin(skel, initialSkinName);
            PlayIdleLoop(skel);
            parent.localScale = new Vector3(CharacterScale, CharacterScale, 1f);
        }

        // SPEC §12.14.9 / §9.5.1.3 (v3.225)：SkeletonGraphic 构建后同步预制体权威 initialSkinName，
        // 避免变体皮肤（如 V3）未应用、默认皮肤为空时渲染为空（有节点无外形）。
        private static void ApplyInitialSkin(SkeletonGraphic skel, string skinName)
        {
            if (skel == null || string.IsNullOrEmpty(skinName))
                return;
            var skeleton = skel.Skeleton;
            if (skeleton == null || skeleton.Data == null)
                return;
            if (skeleton.Data.FindSkin(skinName) == null)
            {
                UnityEngine.Debug.LogWarning("[DetailAttributeModalView] 皮肤不存在，保留默认皮肤：" + skinName);
                return;
            }
            skeleton.SetSkin(skinName);
            skeleton.SetSlotsToSetupPose();
            skel.LateUpdate();
        }

        private static void PlayIdleLoop(SkeletonGraphic skel)
        {
            if (skel == null || skel.Skeleton == null || skel.Skeleton.Data == null)
                return;

            var data = skel.Skeleton.Data;
            Spine.Animation chosen = null;
            for (int i = 0; i < IdleAnimCandidates.Length && chosen == null; i++)
            {
                var exact = data.FindAnimation(IdleAnimCandidates[i]);
                if (exact != null)
                    chosen = exact;
            }
            if (chosen == null)
            {
                var animations = data.Animations;
                if (animations != null && animations.Count > 0)
                    chosen = animations.Items[0];
            }
            if (chosen != null)
                skel.AnimationState.SetAnimation(0, chosen, true);
        }

        private static void BuildFallbackBlock(RectTransform parent)
        {
            var block = new GameObject("Placeholder", typeof(RectTransform), typeof(Image));
            var rt = block.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(200f, 300f);
            block.GetComponent<Image>().color = new Color(0.4f, 0.35f, 0.5f, 1f);
        }

        private void EnsureFieldsFromHierarchy()
        {
            if (panelRt == null)
                panelRt = transform as RectTransform;
            if (dim == null)
                dim = FindDescendantImage(DimName);
            if (playerSlot == null)
                playerSlot = FindDescendantRect(PlayerSlotName);
            if (hpText == null)
                hpText = FindDescendantText("HpText");
            if (atkText == null)
                atkText = FindDescendantText("AtkText");
            if (speedText == null)
                speedText = FindDescendantText("SpeedText");
            if (hexChart == null)
            {
                var t = FindDescendantByName(transform, HexChartName);
                if (t != null)
                    hexChart = t.GetComponent<HexRadarChartGraphic>();
            }
            if (hexChart != null)
                HexRadarChartGraphic.EnsureCanvasRenderer(hexChart.gameObject);
            if (hexLabels == null || hexLabels.Length == 0)
            {
                var labelsRoot = FindDescendantByName(transform, HexLabelsName);
                if (labelsRoot != null)
                {
                    hexLabels = new Text[HexLabelCount];
                    for (int i = 0; i < HexLabelCount; i++)
                    {
                        var labelT = labelsRoot.Find("Label" + i);
                        hexLabels[i] = labelT != null ? labelT.GetComponent<Text>() : null;
                    }
                }
            }
            if (closeButton == null)
                closeButton = FindDescendantButton(CloseButtonName);
        }

        private Image FindDescendantImage(string name)
        {
            var t = FindDescendantByName(transform, name);
            return t != null ? t.GetComponent<Image>() : null;
        }

        private RectTransform FindDescendantRect(string name)
        {
            var t = FindDescendantByName(transform, name);
            return t as RectTransform;
        }

        private Text FindDescendantText(string name)
        {
            var t = FindDescendantByName(transform, name);
            return t != null ? t.GetComponent<Text>() : null;
        }

        private Button FindDescendantButton(string name)
        {
            var t = FindDescendantByName(transform, name);
            return t != null ? t.GetComponent<Button>() : null;
        }

        private static Transform FindDescendantByName(Transform root, string name)
        {
            if (root == null)
                return null;
            if (string.Equals(root.name, name, StringComparison.Ordinal))
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDescendantByName(root.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private static GameObject BuildRuntimeFallback(RectTransform canvasRect)
        {
            var rootGo = new GameObject(PanelObjectName, typeof(RectTransform));
            var rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.SetParent(canvasRect, false);
            BottomNavAttachedScreenLayout.StretchFull(rootRt);

            var view = rootGo.AddComponent<DetailAttributeModalView>();
            DetailAttributeModalBuilder.Build(rootRt, view);
            return rootGo;
        }
    }

    /// <summary>SPEC §12.13：详细属性弹窗结构构建（运行时回退与编辑器生成器共用）。</summary>
    public static class DetailAttributeModalBuilder
    {
        public static void Build(RectTransform root, DetailAttributeModalView view)
        {
            var dimRt = BottomNavAttachedScreenLayout.CreateChildRect(
                root, DetailAttributeModalView.DimName, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(dimRt);
            var dim = dimRt.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.72f);
            dim.raycastTarget = true;

            var topArea = CreateArea(root, DetailAttributeModalView.TopAreaName,
                new Vector2(0f, 0.62f), new Vector2(1f, 1f));
            var playerSlot = BottomNavAttachedScreenLayout.CreateChildRect(
                topArea, DetailAttributeModalView.PlayerSlotName,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(720f, 1200f));

            var midArea = CreateArea(root, DetailAttributeModalView.MiddleAreaName,
                new Vector2(0f, 0.38f), new Vector2(1f, 0.62f));
            AddLiuGongBackground(midArea);
            var hpText = CreateAreaText(midArea, "HpText", new Vector2(0f, 40f), new Vector2(900f, 60f),
                "-- / --", 40, TextAnchor.MiddleCenter);
            var atkText = CreateAreaText(midArea, "AtkText", new Vector2(-200f, -40f), new Vector2(400f, 60f),
                "--", 40, TextAnchor.MiddleCenter);
            var speedText = CreateAreaText(midArea, "SpeedText", new Vector2(200f, -40f), new Vector2(400f, 60f),
                "--", 40, TextAnchor.MiddleCenter);

            var bottomArea = CreateArea(root, DetailAttributeModalView.BottomAreaName,
                new Vector2(0f, 0f), new Vector2(1f, 0.38f));
            AddLiuGongBackground(bottomArea);

            var chartRt = BottomNavAttachedScreenLayout.CreateChildRect(
                bottomArea, DetailAttributeModalView.HexChartName,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f), new Vector2(520f, 520f));
            HexRadarChartGraphic.EnsureCanvasRenderer(chartRt.gameObject);
            var hexChart = chartRt.gameObject.AddComponent<HexRadarChartGraphic>();
            hexChart.raycastTarget = false;

            var labelsRoot = BottomNavAttachedScreenLayout.CreateChildRect(
                bottomArea, DetailAttributeModalView.HexLabelsName,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(520f, 520f));
            var hexLabels = new Text[DetailAttributeModalView.HexLabelCount];
            for (int i = 0; i < hexLabels.Length; i++)
            {
                float rad = i * 60f * Mathf.Deg2Rad;
                var pos = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * 210f;
                hexLabels[i] = CreateAreaText(labelsRoot, "Label" + i, pos, new Vector2(140f, 70f),
                    "0", 24, TextAnchor.MiddleCenter);
            }

            var closeButton = BuildCloseButton(root);

            view.AssignRuntimeRefs(dim, playerSlot, hpText, atkText, speedText, hexChart, hexLabels, closeButton);
        }

        private static void AddLiuGongBackground(RectTransform area)
        {
            var img = area.gameObject.GetComponent<Image>();
            if (img == null)
                img = area.gameObject.AddComponent<Image>();
            var sprite = Resources.Load<Sprite>(DetailAttributeModalView.ResLiuGong);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.preserveAspect = true;
                img.color = Color.white;
            }
            else
            {
                img.color = new Color(0.12f, 0.1f, 0.15f, 0.9f);
            }
            img.raycastTarget = false;
        }

        private static RectTransform CreateArea(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rt = BottomNavAttachedScreenLayout.CreateChildRect(
                parent, name, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static Text CreateAreaText(RectTransform parent, string name, Vector2 pos, Vector2 size,
            string content, int fontSize, TextAnchor anchor)
        {
            var rt = BottomNavAttachedScreenLayout.CreateChildRect(
                parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            var txt = rt.gameObject.AddComponent<Text>();
            txt.text = content;
            txt.font = FarmGridView.LoadBuiltinFont();
            txt.fontSize = fontSize;
            txt.alignment = anchor;
            txt.color = Color.white;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            return txt;
        }

        private static Button BuildCloseButton(RectTransform parent)
        {
            var rt = BottomNavAttachedScreenLayout.CreateChildRect(
                parent, DetailAttributeModalView.CloseButtonName,
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-40f, -40f), new Vector2(96f, 96f));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.5f);
            img.raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;

            var labelRt = BottomNavAttachedScreenLayout.CreateChildRect(
                rt, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(labelRt);
            var txt = labelRt.gameObject.AddComponent<Text>();
            txt.text = "X";
            txt.font = FarmGridView.LoadBuiltinFont();
            txt.fontSize = 48;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.raycastTarget = false;
            return btn;
        }
    }
}
