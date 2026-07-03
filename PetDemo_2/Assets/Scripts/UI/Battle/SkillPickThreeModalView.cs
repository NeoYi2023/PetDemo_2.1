// SPEC §12.11.9：三选一技能界面 SkillPickThreeModal（领悟/顿悟）。
// 职责：
//   1) 预制体优先 / 代码回退地构建全屏 modal：顶部标题框（九宫格 pet_bg_3）+ 三个条目框（九宫格 pet_bg_1/pet_bg_2）+ 底部「确定」；
//   2) Show(quality, options, onConfirm)：按品质切换条目框素材，填充技能图标/名称/富文本描述；
//   3) 玩家点选一项高亮，三条目下方出现「确定」；点「确定」回调 onConfirm(selected) 并 Hide()。
// 品质标签（普通/传说）已烘焙在 pet_bg_1/pet_bg_2 美术内，故本界面不再叠加品质文字。
using System;
using System.Collections.Generic;
using PetDemo.Battle;
using PetDemo.UI;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Battle
{
    [DisallowMultipleComponent]
    public sealed class SkillPickThreeModalView : MonoBehaviour
    {
        public const string ResPrefabPath = "Prefabs/Battle/SkillPickThreeModal";
        public const string ResTitleSprite = "AirUI/pet_bg_3";
        public const string ResFrameNormal = "AirUI/pet_bg_1";
        public const string ResFrameLegendary = "AirUI/pet_bg_2";

        public const string PanelObjectName = "SkillPickThreeModal";
        public const string DimName = "Dim";
        public const string TitleName = "TitleImage";
        public const string OptionsRowName = "OptionsRow";
        public const string ConfirmButtonName = "ConfirmButton";
        public const int OptionCount = 3;

        public static readonly Vector2 TitleSize = new Vector2(600f, 394f);
        public static readonly Vector2 TitlePos = new Vector2(0f, 720f);
        // 三行模式：每个选项独占一行，横向排版（左图标 + 右名称/描述）。
        public static readonly Vector2 OptionSize = new Vector2(760f, 230f);
        public const float OptionSpacing = 30f;
        public static readonly Vector2 OptionsCenter = new Vector2(0f, 60f);
        public static readonly Vector2 ConfirmSize = new Vector2(320f, 120f);
        public static readonly Vector2 ConfirmPos = new Vector2(0f, -540f);
        public static readonly Vector2 IconSize = new Vector2(150f, 150f);

        private static readonly Color SelectedTint = Color.white;
        private static readonly Color UnselectedTint = new Color(0.72f, 0.72f, 0.72f, 1f);
        private const float SelectedScale = 1.06f;

        private static SkillPickThreeModalView instance;

        [SerializeField] private Image dim;
        [SerializeField] private Image titleImage;
        [SerializeField] private RectTransform optionsRow;
        [SerializeField] private Button[] optionButtons = new Button[OptionCount];
        [SerializeField] private Image[] optionFrames = new Image[OptionCount];
        [SerializeField] private Image[] optionIcons = new Image[OptionCount];
        [SerializeField] private Text[] optionNames = new Text[OptionCount];
        [SerializeField] private Text[] optionDescs = new Text[OptionCount];
        [SerializeField] private Button confirmButton;

        private RectTransform panelRt;
        private RectTransform canvasRectCache;

        private List<BattleSkillConfig> currentOptions;
        private Action<BattleSkillConfig> onConfirm;
        private int selectedIndex = -1;
        private bool wired;

        public bool IsShown => gameObject != null && gameObject.activeSelf;

        /// <summary>运行时回退构建时注入引用（编辑器生成器走 SerializedObject）。</summary>
        internal void AssignRuntimeRefs(Image dimImg, Image title, RectTransform row,
            Button[] buttons, Image[] frames, Image[] icons, Text[] names, Text[] descs, Button confirm)
        {
            dim = dimImg;
            titleImage = title;
            optionsRow = row;
            optionButtons = buttons;
            optionFrames = frames;
            optionIcons = icons;
            optionNames = names;
            optionDescs = descs;
            confirmButton = confirm;
        }

        public static SkillPickThreeModalView GetOrCreate(RectTransform canvasRect)
        {
            if (canvasRect == null)
            {
                UnityEngine.Debug.LogWarning("[SkillPickThreeModalView] GetOrCreate: canvasRect 为空");
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
                var existView = existing.GetComponent<SkillPickThreeModalView>();
                if (existView == null)
                    existView = existing.gameObject.AddComponent<SkillPickThreeModalView>();
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
                    "[SkillPickThreeModalView] 缺少预制体 Resources/" + ResPrefabPath +
                    "，使用运行时回退 UI；请在编辑器执行 Tools/PetDemo/Generate Skill Pick Three Modal Prefab。");
                go = BuildRuntimeFallback(canvasRect);
                if (go == null)
                    return null;
            }

            go.name = PanelObjectName;
            go.SetActive(false);
            var rt = go.transform as RectTransform;
            if (rt != null)
                BottomNavAttachedScreenLayout.StretchFull(rt);

            var view = go.GetComponent<SkillPickThreeModalView>();
            if (view == null)
                view = go.AddComponent<SkillPickThreeModalView>();
            view.panelRt = rt;
            view.canvasRectCache = canvasRect;
            instance = view;
            return view;
        }

        public void Show(SkillQuality quality, List<BattleSkillConfig> options, Action<BattleSkillConfig> confirmCallback)
        {
            EnsureFieldsFromHierarchy();
            WireOnce();

            currentOptions = options;
            onConfirm = confirmCallback;
            selectedIndex = -1;

            string framePath = quality == SkillQuality.Legendary ? ResFrameLegendary : ResFrameNormal;
            var frameSprite = Resources.Load<Sprite>(framePath);

            for (int i = 0; i < OptionCount; i++)
            {
                bool hasData = options != null && i < options.Count && options[i] != null;
                var optGo = optionButtons != null && i < optionButtons.Length && optionButtons[i] != null
                    ? optionButtons[i].gameObject
                    : null;
                if (optGo != null)
                    optGo.SetActive(hasData);

                if (!hasData)
                    continue;

                var cfg = options[i];
                if (optionFrames != null && optionFrames[i] != null && frameSprite != null)
                {
                    optionFrames[i].sprite = frameSprite;
                    optionFrames[i].type = Image.Type.Sliced;
                    optionFrames[i].fillCenter = true;
                    optionFrames[i].color = UnselectedTint;
                }
                if (optionIcons != null && optionIcons[i] != null)
                {
                    var iconSprite = LoadIcon(cfg.iconName);
                    optionIcons[i].sprite = iconSprite;
                    optionIcons[i].enabled = iconSprite != null;
                    optionIcons[i].preserveAspect = true;
                }
                if (optionNames != null && optionNames[i] != null)
                    optionNames[i].text = cfg.skillName;
                if (optionDescs != null && optionDescs[i] != null)
                    optionDescs[i].text = cfg.description;

                var optRt = optGo != null ? optGo.transform as RectTransform : null;
                if (optRt != null)
                    optRt.localScale = Vector3.one;
            }

            SetConfirmVisible(false);

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            currentOptions = null;
            onConfirm = null;
            selectedIndex = -1;
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
            for (int i = 0; i < OptionCount; i++)
            {
                if (optionButtons == null || i >= optionButtons.Length || optionButtons[i] == null)
                    continue;
                int idx = i;
                optionButtons[i].onClick.RemoveAllListeners();
                optionButtons[i].onClick.AddListener(() => OnOptionClicked(idx));
            }
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(OnConfirmClicked);
            }
            wired = true;
        }

        private void OnOptionClicked(int index)
        {
            if (currentOptions == null || index < 0 || index >= currentOptions.Count)
                return;
            selectedIndex = index;
            RefreshSelectionVisuals();
            SetConfirmVisible(true);
        }

        private void RefreshSelectionVisuals()
        {
            for (int i = 0; i < OptionCount; i++)
            {
                bool selected = i == selectedIndex;
                if (optionFrames != null && i < optionFrames.Length && optionFrames[i] != null)
                    optionFrames[i].color = selected ? SelectedTint : UnselectedTint;
                if (optionButtons != null && i < optionButtons.Length && optionButtons[i] != null)
                {
                    var rt = optionButtons[i].transform as RectTransform;
                    if (rt != null)
                        rt.localScale = selected ? new Vector3(SelectedScale, SelectedScale, 1f) : Vector3.one;
                }
            }
        }

        private void OnConfirmClicked()
        {
            if (currentOptions == null || selectedIndex < 0 || selectedIndex >= currentOptions.Count)
                return;
            var chosen = currentOptions[selectedIndex];
            var cb = onConfirm;
            Hide();
            cb?.Invoke(chosen);
        }

        private void SetConfirmVisible(bool visible)
        {
            if (confirmButton != null)
                confirmButton.gameObject.SetActive(visible);
        }

        private static Sprite LoadIcon(string iconName)
        {
            string path = SkillConfigCatalog.IconResourcePath(iconName);
            return string.IsNullOrEmpty(path) ? null : Resources.Load<Sprite>(path);
        }

        // ============================================================
        // 字段回填（预制体路径按名查找）
        // ============================================================
        private void EnsureFieldsFromHierarchy()
        {
            if (panelRt == null)
                panelRt = transform as RectTransform;
            if (dim == null)
                dim = FindDescendantImage(DimName);
            if (titleImage == null)
                titleImage = FindDescendantImage(TitleName);
            if (optionsRow == null)
                optionsRow = FindDescendantRect(OptionsRowName);
            if (confirmButton == null)
                confirmButton = FindDescendantButton(ConfirmButtonName);

            EnsureArrays();
            for (int i = 0; i < OptionCount; i++)
            {
                var optT = FindDescendantByName(transform, "Option" + i);
                if (optT == null)
                    continue;
                if (optionButtons[i] == null)
                    optionButtons[i] = optT.GetComponent<Button>();
                if (optionFrames[i] == null)
                    optionFrames[i] = optT.GetComponent<Image>();
                if (optionIcons[i] == null)
                {
                    var t = FindDescendantByName(optT, "Icon");
                    optionIcons[i] = t != null ? t.GetComponent<Image>() : null;
                }
                if (optionNames[i] == null)
                {
                    var t = FindDescendantByName(optT, "Name");
                    optionNames[i] = t != null ? t.GetComponent<Text>() : null;
                }
                if (optionDescs[i] == null)
                {
                    var t = FindDescendantByName(optT, "Desc");
                    optionDescs[i] = t != null ? t.GetComponent<Text>() : null;
                }
            }
        }

        private void EnsureArrays()
        {
            if (optionButtons == null || optionButtons.Length != OptionCount) optionButtons = new Button[OptionCount];
            if (optionFrames == null || optionFrames.Length != OptionCount) optionFrames = new Image[OptionCount];
            if (optionIcons == null || optionIcons.Length != OptionCount) optionIcons = new Image[OptionCount];
            if (optionNames == null || optionNames.Length != OptionCount) optionNames = new Text[OptionCount];
            if (optionDescs == null || optionDescs.Length != OptionCount) optionDescs = new Text[OptionCount];
        }

        private RectTransform FindDescendantRect(string nodeName)
        {
            return FindDescendantByName(transform, nodeName) as RectTransform;
        }

        private Image FindDescendantImage(string nodeName)
        {
            var t = FindDescendantByName(transform, nodeName);
            return t != null ? t.GetComponent<Image>() : null;
        }

        private Button FindDescendantButton(string nodeName)
        {
            var t = FindDescendantByName(transform, nodeName);
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

        // ============================================================
        // 运行时代码回退（缺预制体时，与生成器布局对齐）
        // ============================================================
        private static GameObject BuildRuntimeFallback(RectTransform canvasRect)
        {
            var rootGo = new GameObject(PanelObjectName, typeof(RectTransform));
            var rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.SetParent(canvasRect, false);
            BottomNavAttachedScreenLayout.StretchFull(rootRt);

            var view = rootGo.AddComponent<SkillPickThreeModalView>();
            SkillPickThreeModalBuilder.Build(rootRt, view);
            return rootGo;
        }
    }

    /// <summary>
    /// SPEC §12.11.9：三选一界面结构构建（运行时回退与编辑器生成器共用同一布局）。
    /// </summary>
    public static class SkillPickThreeModalBuilder
    {
        public static void Build(RectTransform root, SkillPickThreeModalView view)
        {
            // 半透明遮罩（阻挡点击，但不关闭——必须做出选择）
            var dimRt = BottomNavAttachedScreenLayout.CreateChildRect(
                root, SkillPickThreeModalView.DimName, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(dimRt);
            var dim = dimRt.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.72f);
            dim.raycastTarget = true;

            // 标题横幅（pet_bg_3，整图显示）
            var titleRt = BottomNavAttachedScreenLayout.CreateChildRect(
                root, SkillPickThreeModalView.TitleName,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                SkillPickThreeModalView.TitlePos, SkillPickThreeModalView.TitleSize);
            var titleImg = titleRt.gameObject.AddComponent<Image>();
            var titleSprite = Resources.Load<Sprite>(SkillPickThreeModalView.ResTitleSprite);
            if (titleSprite != null)
            {
                titleImg.sprite = titleSprite;
                titleImg.preserveAspect = true;
            }
            else
            {
                titleImg.color = new Color(0.9f, 0.7f, 0.2f, 1f);
            }
            titleImg.raycastTarget = false;

            // 三条目容器（三行模式：每项独占一行，纵向排列）
            int count = SkillPickThreeModalView.OptionCount;
            float step = SkillPickThreeModalView.OptionSize.y + SkillPickThreeModalView.OptionSpacing;
            var rowRt = BottomNavAttachedScreenLayout.CreateChildRect(
                root, SkillPickThreeModalView.OptionsRowName,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), SkillPickThreeModalView.OptionsCenter,
                new Vector2(SkillPickThreeModalView.OptionSize.x,
                    SkillPickThreeModalView.OptionSize.y * count + SkillPickThreeModalView.OptionSpacing * (count - 1)));

            var buttons = new Button[count];
            var frames = new Image[count];
            var icons = new Image[count];
            var names = new Text[count];
            var descs = new Text[count];

            // 从上到下：Option0 在最上（+step），Option(count-1) 在最下。
            float startY = step * (count - 1) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                BuildOption(rowRt, i, new Vector2(0f, startY - step * i),
                    out buttons[i], out frames[i], out icons[i], out names[i], out descs[i]);
            }

            // 「确定」按钮（默认隐藏）
            var confirm = BuildConfirmButton(root);
            confirm.gameObject.SetActive(false);

            ApplyToView(view, dim, titleImg, rowRt, buttons, frames, icons, names, descs, confirm);
        }

        public static void BuildOption(RectTransform parent, int index, Vector2 pos,
            out Button button, out Image frame, out Image icon, out Text name, out Text desc)
        {
            var optRt = BottomNavAttachedScreenLayout.CreateChildRect(
                parent, "Option" + index,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, SkillPickThreeModalView.OptionSize);

            frame = optRt.gameObject.AddComponent<Image>();
            var frameSprite = Resources.Load<Sprite>(SkillPickThreeModalView.ResFrameNormal);
            if (frameSprite != null)
            {
                frame.sprite = frameSprite;
                frame.type = Image.Type.Sliced;
                frame.fillCenter = true;
                frame.color = Color.white;
            }
            else
            {
                frame.color = new Color(0.5f, 0.5f, 0.6f, 1f);
            }
            frame.raycastTarget = true;

            button = optRt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = frame;

            // 内容区（避开顶部品质标签，留出四周边距）——横向排版：左图标 + 右名称/描述
            var innerRt = BottomNavAttachedScreenLayout.CreateChildRect(
                optRt, "Content", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            innerRt.offsetMin = new Vector2(45f, 30f);
            innerRt.offsetMax = new Vector2(-45f, -70f);

            // 图标（左侧居中）
            float iconW = SkillPickThreeModalView.IconSize.x;
            float iconH = SkillPickThreeModalView.IconSize.y;
            var iconRt = BottomNavAttachedScreenLayout.CreateChildRect(
                innerRt, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(iconW * 0.5f, 0f), new Vector2(iconW, iconH));
            icon = iconRt.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            // 右侧文本列（图标右侧到内容区右边）
            var textCol = BottomNavAttachedScreenLayout.CreateChildRect(
                innerRt, "TextColumn", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            textCol.offsetMin = new Vector2(iconW + 24f, 0f);
            textCol.offsetMax = new Vector2(0f, 0f);

            var vlg = textCol.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(0, 0, 6, 6);
            vlg.childAlignment = TextAnchor.MiddleLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // 名称
            var nameRt = BottomNavAttachedScreenLayout.CreateChildRect(
                textCol, "Name", new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 44f));
            name = nameRt.gameObject.AddComponent<Text>();
            name.font = FarmGridView.LoadBuiltinFont();
            name.fontSize = 32;
            name.fontStyle = FontStyle.Bold;
            name.alignment = TextAnchor.MiddleLeft;
            name.color = new Color(0.15f, 0.1f, 0.05f, 1f);
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Overflow;
            name.raycastTarget = false;
            var nameLe = nameRt.gameObject.AddComponent<LayoutElement>();
            nameLe.preferredHeight = 44f;

            // 描述（富文本）
            var descRt = BottomNavAttachedScreenLayout.CreateChildRect(
                textCol, "Desc", new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 100f));
            desc = descRt.gameObject.AddComponent<Text>();
            desc.font = FarmGridView.LoadBuiltinFont();
            desc.fontSize = 26;
            desc.alignment = TextAnchor.UpperLeft;
            desc.color = new Color(0.2f, 0.15f, 0.1f, 1f);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.verticalOverflow = VerticalWrapMode.Overflow;
            desc.supportRichText = true;
            desc.raycastTarget = false;
            var descLe = descRt.gameObject.AddComponent<LayoutElement>();
            descLe.flexibleHeight = 1f;
        }

        public static Button BuildConfirmButton(RectTransform parent)
        {
            var rt = BottomNavAttachedScreenLayout.CreateChildRect(
                parent, SkillPickThreeModalView.ConfirmButtonName,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                SkillPickThreeModalView.ConfirmPos, SkillPickThreeModalView.ConfirmSize);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.95f, 0.6f, 0.15f, 1f);
            img.raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;

            var labelRt = BottomNavAttachedScreenLayout.CreateChildRect(
                rt, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(labelRt);
            var txt = labelRt.gameObject.AddComponent<Text>();
            txt.text = "确定";
            txt.font = FarmGridView.LoadBuiltinFont();
            txt.fontSize = 44;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.raycastTarget = false;
            return btn;
        }

        private static void ApplyToView(SkillPickThreeModalView view,
            Image dim, Image titleImage, RectTransform optionsRow,
            Button[] buttons, Image[] frames, Image[] icons, Text[] names, Text[] descs, Button confirm)
        {
            view.AssignRuntimeRefs(dim, titleImage, optionsRow, buttons, frames, icons, names, descs, confirm);
        }
    }
}
