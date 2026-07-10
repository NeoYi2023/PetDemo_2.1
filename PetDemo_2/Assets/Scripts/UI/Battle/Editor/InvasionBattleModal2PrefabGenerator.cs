#if UNITY_EDITOR
using System.IO;
using PetDemo.UI.Battle;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// SPEC §12.11：生成新战斗界面预制体 InvasionBattleModal_2。
    /// 产出 Assets/Resources/Prefabs/Battle/InvasionBattleModal_2.prefab。
    /// 三段结构（上/中/下）共用背景 AirUI/ZhanDou_0；玩家 SkeletonGraphic 由 View 运行时构建（此处仅留 PlayerSlot 挂载点）。
    /// </summary>
    [InitializeOnLoad]
    public static class InvasionBattleModal2PrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Battle";
        private const string PrefabPath = PrefabDir + "/InvasionBattleModal_2.prefab";
        private const string BackgroundSpriteAsset = "Assets/Resources/AirUI/ZhanDou_0.png";
        private const string NextDayButtonSpriteAsset = "Assets/Resources/AirUI/InvasionBattleModal_2_Button_1.png";
        private const string DetailAttrButtonSpriteAsset = "Assets/Resources/AirUI/JiNengLiebiao.png";

        private static readonly Vector2 CharacterSize = new Vector2(720f, 1200f);
        private const float CharacterScale = 0.53f;
        private static readonly Vector2 NextDayButtonSize = new Vector2(360f, 140f);
        private static readonly Vector2 CloseButtonSize = new Vector2(96f, 96f);

        static InvasionBattleModal2PrefabGenerator()
        {
            EditorApplication.delayCall += EnsurePrefabExists;
        }

        private static void EnsurePrefabExists()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (File.Exists(PrefabPath))
            {
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (existing != null)
                    return;
                UnityEngine.Debug.LogWarning(
                    "[InvasionBattleModal2] 预制体文件存在但 Unity 无法导入，将重新生成: " + PrefabPath);
            }

            Generate();
        }

        [MenuItem("Tools/PetDemo/Generate Invasion Battle Modal 2 Prefab")]
        public static void Generate()
        {
            EnsureDir(PrefabDir);
            BuildPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("InvasionBattleModal_2 预制件生成完成: " + PrefabPath);
        }

        private static void BuildPrefab()
        {
            var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundSpriteAsset);
            if (bgSprite == null)
                UnityEngine.Debug.LogWarning("[InvasionBattleModal2PrefabGenerator] 未找到背景图: " + BackgroundSpriteAsset);
            var btnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(NextDayButtonSpriteAsset);

            var root = new GameObject(InvasionBattleModal2View.PanelObjectName, typeof(RectTransform));
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            root.AddComponent<InvasionBattleModal2View>();

            // 三段共用背景 ZhanDou_0
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

            // 上部：TopArea + PlayerSlot + PartyStandRoot
            var topArea = CreateArea(rootRt, InvasionBattleModal2View.TopAreaName, new Vector2(0f, 0.55f), new Vector2(1f, 1f));
            var playerSlot = CreateChild(topArea, InvasionBattleModal2View.PlayerSlotName,
                new Vector2(0.5f, 0.5f), Vector2.zero, CharacterSize);
            playerSlot.localScale = new Vector3(CharacterScale, CharacterScale, 1f);
            var partyStandRoot = CreateChild(topArea, InvasionBattleModal2View.PartyStandRootName,
                new Vector2(0.5f, 0.5f), Vector2.zero, CharacterSize);
            partyStandRoot.localScale = new Vector3(CharacterScale, CharacterScale, 1f);

            // 中部：MiddleArea + 属性文本
            var midArea = CreateArea(rootRt, InvasionBattleModal2View.MiddleAreaName, new Vector2(0f, 0.30f), new Vector2(1f, 0.55f));
            var hpText = CreateText(midArea, "HpText", new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(900f, 60f),
                "-- / --", 40, TextAnchor.MiddleCenter);
            var atkText = CreateText(midArea, "AtkText", new Vector2(0.5f, 0.5f), new Vector2(-200f, -40f), new Vector2(400f, 60f),
                "--", 40, TextAnchor.MiddleCenter);
            var speedText = CreateText(midArea, "SpeedText", new Vector2(0.5f, 0.5f), new Vector2(200f, -40f), new Vector2(400f, 60f),
                "--", 40, TextAnchor.MiddleCenter);
            var detailAttrSprite = AssetDatabase.LoadAssetAtPath<Sprite>(DetailAttrButtonSpriteAsset);
            var detailAttrButton = BuildDetailAttrButton(midArea, detailAttrSprite);

            // 下部：BottomArea + 天数 + 事件日志（ScrollRect）+ 下一天按钮
            var bottomArea = CreateArea(rootRt, InvasionBattleModal2View.BottomAreaName, new Vector2(0f, 0f), new Vector2(1f, 0.30f));
            var dayLabel = CreateText(bottomArea, "DayLabel", new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(600f, 60f),
                "第 0 天", 40, TextAnchor.UpperCenter);
            BuildEventLog(bottomArea, out var eventScroll, out var eventContent);
            var nextDayButton = BuildNextDayButton(bottomArea, btnSprite);

            // 关闭按钮（右上角）
            var closeButton = BuildCloseButton(rootRt);

            SerializeView(root.GetComponent<InvasionBattleModal2View>(),
                playerSlot, partyStandRoot, hpText, atkText, speedText, eventScroll, eventContent, dayLabel,
                nextDayButton, closeButton, detailAttrButton);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            if (prefab != null)
                Selection.activeObject = prefab;
        }

        private static Button BuildNextDayButton(RectTransform parent, Sprite sprite)
        {
            var rt = CreateChild(parent, "NextDayButton", new Vector2(0.5f, 0f), new Vector2(0f, 130f), NextDayButtonSize);
            var img = rt.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = true;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
            }
            else
            {
                UnityEngine.Debug.LogWarning("[InvasionBattleModal2PrefabGenerator] 未找到「下一天」按钮图: " + NextDayButtonSpriteAsset);
                img.color = new Color(0.9f, 0.55f, 0.2f, 0.95f);
            }
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;

            // SPEC §12.11.5：按钮上叠加只显示「下一天」三字的文字标签
            var labelRt = CreateStretch(rt, "Label");
            var txt = labelRt.gameObject.AddComponent<Text>();
            txt.text = "下一天";
            txt.font = LoadBuiltinFont();
            txt.fontSize = 44;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.black;
            txt.fontStyle = FontStyle.Bold;
            txt.raycastTarget = false;

            return btn;
        }

        private static Button BuildDetailAttrButton(RectTransform parent, Sprite sprite)
        {
            var rt = CreateChild(parent, InvasionBattleModal2View.DetailAttrButtonName,
                new Vector2(1f, 0.5f), new Vector2(-60f, 0f), new Vector2(96f, 96f));
            var img = rt.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = true;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
            }
            else
            {
                UnityEngine.Debug.LogWarning("[InvasionBattleModal2PrefabGenerator] 未找到详细属性按钮图: " + DetailAttrButtonSpriteAsset);
                img.color = new Color(0.5f, 0.55f, 0.7f, 1f);
            }
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;

            var labelRt = CreateChild(rt, "Label", new Vector2(0.5f, 0f), new Vector2(0f, -52f), new Vector2(120f, 36f));
            var txt = labelRt.gameObject.AddComponent<Text>();
            txt.text = "详细属性";
            txt.font = LoadBuiltinFont();
            txt.fontSize = 22;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.raycastTarget = false;
            return btn;
        }

        private static Button BuildCloseButton(RectTransform parent)
        {
            var rt = CreateChild(parent, "CloseButton", new Vector2(1f, 1f), new Vector2(-40f, -40f), CloseButtonSize);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.5f);
            img.raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;

            var labelRt = CreateStretch(rt, "Label");
            var txt = labelRt.gameObject.AddComponent<Text>();
            txt.text = "X";
            txt.font = LoadBuiltinFont();
            txt.fontSize = 48;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.raycastTarget = false;
            return btn;
        }

        private static void SerializeView(
            InvasionBattleModal2View view,
            RectTransform playerSlot,
            RectTransform partyStandRoot,
            Text hpText, Text atkText, Text speedText,
            ScrollRect eventScrollRect, RectTransform eventContent, Text dayLabel,
            Button nextDayButton, Button closeButton, Button detailAttrButton)
        {
            var so = new SerializedObject(view);
            so.FindProperty("playerSlot").objectReferenceValue = playerSlot;
            so.FindProperty("partyStandRoot").objectReferenceValue = partyStandRoot;
            so.FindProperty("hpText").objectReferenceValue = hpText;
            so.FindProperty("atkText").objectReferenceValue = atkText;
            so.FindProperty("speedText").objectReferenceValue = speedText;
            so.FindProperty("eventScrollRect").objectReferenceValue = eventScrollRect;
            so.FindProperty("eventContent").objectReferenceValue = eventContent;
            so.FindProperty("dayLabel").objectReferenceValue = dayLabel;
            so.FindProperty("nextDayButton").objectReferenceValue = nextDayButton;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.FindProperty("detailAttrButton").objectReferenceValue = detailAttrButton;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // SPEC §12.11.5：在 BottomArea 内烘焙可上下滑动的事件日志（EventScroll/Viewport/EventContent）。
        private static void BuildEventLog(RectTransform bottomArea, out ScrollRect scroll, out RectTransform content)
        {
            var scrollRt = CreateChild(bottomArea, InvasionBattleModal2View.EventScrollName,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(40f, 230f);
            scrollRt.offsetMax = new Vector2(-40f, -90f);

            var scrollBg = scrollRt.gameObject.AddComponent<Image>();
            scrollBg.color = new Color(0f, 0f, 0f, 0.18f);
            scrollBg.raycastTarget = true;

            scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;

            var viewportRt = CreateChild(scrollRt, "Viewport", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            var viewportImg = viewportRt.gameObject.AddComponent<Image>();
            viewportImg.color = new Color(1f, 1f, 1f, 0.01f);
            viewportRt.gameObject.AddComponent<RectMask2D>();

            var contentRt = CreateChild(viewportRt, InvasionBattleModal2View.EventContentName,
                new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = new Vector2(0f, 0f);
            contentRt.offsetMax = new Vector2(0f, 0f);

            var vlg = contentRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.spacing = 12f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var contentFitter = contentRt.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRt;
            scroll.content = contentRt;
            content = contentRt;
        }

        private static RectTransform CreateArea(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static RectTransform CreateChild(RectTransform parent, string name, Vector2 anchorPivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorPivot;
            rt.anchorMax = anchorPivot;
            rt.pivot = anchorPivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static Text CreateText(RectTransform parent, string name, Vector2 anchorPivot, Vector2 pos, Vector2 size,
            string content, int fontSize, TextAnchor anchor)
        {
            var rt = CreateChild(parent, name, anchorPivot, pos, size);
            var txt = rt.gameObject.AddComponent<Text>();
            txt.text = content;
            txt.font = LoadBuiltinFont();
            txt.fontSize = fontSize;
            txt.alignment = anchor;
            txt.color = Color.white;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            return txt;
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
