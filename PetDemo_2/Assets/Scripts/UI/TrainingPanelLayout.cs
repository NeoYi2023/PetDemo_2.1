// SPEC §9.14.12：训练页签面板层级构建（运行时回退与预制体生成器共用）。
using PetDemo.Core;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    public static class TrainingPanelLayout
    {
        public const string SolidBackgroundResource = "";
        public const string FilterBadgeResource = "AirUI/common_bg_5";
        public const string LockIconResource = "AirUI/common_bg_Suo";
        public const string DecorBackgroundResource = "AirUI/common_bg_11";

        public const int FilterAttrCount = 6;
        public const int CourseColumns = 2;
        public const float LockIconAlpha = 0.9f;

        private static readonly Color SolidBackgroundColor = new Color(0.10f, 0.12f, 0.18f, 1f);
        private static readonly Color TextColor = Color.white;
        private static readonly Color DarkTextColor = Color.black;
        private static readonly Color IdleHintColor = new Color(0.85f, 0.88f, 0.92f, 1f);
        private static readonly Color AttrGainColor = new Color(0.55f, 0.92f, 0.62f, 1f);
        private static readonly Color CourseFallbackColor = new Color(0.28f, 0.3f, 0.38f, 1f);
        private static readonly Color CompleteButtonColor = new Color(0.32f, 0.62f, 0.4f, 1f);
        private static readonly Color TipsBgColor = new Color(0.08f, 0.08f, 0.1f, 0.88f);

        private const float TopSectionAnchorMaxY = 1f;
        private const float TopSectionAnchorMinY = 0.62f;
        private const float FilterSectionAnchorMaxY = 0.62f;
        private const float FilterSectionAnchorMinY = 0.52f;
        private static readonly Vector2 RoleMountSize = new Vector2(420f, 580f);
        public static readonly Vector2 FilterIconSize = new Vector2(96f, 96f);
        public static readonly Vector2 FilterBadgeSize = new Vector2(36f, 36f);
        public static readonly Vector2 CourseCellSize = new Vector2(460f, 220f);
        public static readonly Vector2 CourseIconSize = new Vector2(128f, 128f);
        public static readonly Vector2 LockIconSize = new Vector2(72f, 72f);
        private const float CourseCellGapX = 24f;
        private const float CourseCellGapY = 20f;
        private const float FilterIconGap = 28f;
        public const float CourseScrollPosY = 40f;
        public const float CourseScrollWidth = 1080f;
        public const float CourseScrollHeight = 870f;

        public static GameObject BuildRuntime(RectTransform parent)
        {
            var root = new GameObject(TrainingPanelView.PanelObjectName, typeof(RectTransform));
            var rootRt = root.GetComponent<RectTransform>();
            if (parent != null)
                rootRt.SetParent(parent, false);
            StretchFull(rootRt);
            root.AddComponent<TrainingPanelView>();

            var solidBg = CreateChild(rootRt, "SolidBackground", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(solidBg);
            var solidImg = solidBg.gameObject.AddComponent<Image>();
            solidImg.color = SolidBackgroundColor;
            solidImg.raycastTarget = true;

            BuildTopSection(rootRt);
            BuildFilterSection(rootRt);
            BuildCourseSection(rootRt);
            BuildTipsToast(rootRt);

            root.SetActive(false);
            return root;
        }

        public static void ApplyCharacterCreationEmbedLayout(RectTransform rootRt)
        {
            if (rootRt == null)
                return;
            StretchFull(rootRt);
            rootRt.offsetMin = new Vector2(0f, CharacterCreationScreenLayout.ContentRegionBottomOffset);
            rootRt.offsetMax = Vector2.zero;
        }

        private static void BuildTopSection(RectTransform rootRt)
        {
            var top = CreateChild(rootRt, "TopSection",
                new Vector2(0f, TopSectionAnchorMinY), new Vector2(1f, TopSectionAnchorMaxY),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(top);

            var roleSide = CreateChild(top, "RoleSide",
                new Vector2(0f, 0f), new Vector2(0.48f, 1f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(roleSide);

            var decor = CreateChild(roleSide, "DecorBackground",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480f, 420f));
            ApplySpriteOrColor(decor.gameObject.AddComponent<Image>(), DecorBackgroundResource,
                new Color(0.16f, 0.18f, 0.24f, 0.6f), preserveAspect: true);

            CreateChild(roleSide, "RoleMount",
                new Vector2(0.5f, 0.1f), new Vector2(0.5f, 0.1f),
                new Vector2(0.5f, 0f), Vector2.zero, RoleMountSize);

            var trainingSide = CreateChild(top, "ActiveTrainingSlot",
                new Vector2(0.48f, 0.08f), new Vector2(0.96f, 0.92f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(trainingSide);

            CreateText(trainingSide, "IdleHintText", "请选择1项开始训练",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 120f), 36, TextAnchor.MiddleCenter,
                IdleHintColor);

            var activeRoot = CreateChild(trainingSide, "ActiveCourseRoot",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(activeRoot);
            activeRoot.gameObject.SetActive(false);

            var iconRt = CreateChild(activeRoot, "CourseIcon",
                new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 140f));
            var iconImg = iconRt.gameObject.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            CreateText(activeRoot, "CourseName", "",
                new Vector2(0.5f, 0.42f), Vector2.zero, new Vector2(400f, 60f), 34, TextAnchor.MiddleCenter,
                TextColor);

            CreateText(activeRoot, "CountdownText", "00:00",
                new Vector2(0.5f, 0.22f), Vector2.zero, new Vector2(280f, 56f), 40, TextAnchor.MiddleCenter,
                TextColor);

            var completeRt = CreateChild(activeRoot, "CompleteButton",
                new Vector2(0.5f, 0.18f), new Vector2(0.5f, 0.18f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 72f));
            var completeImg = completeRt.gameObject.AddComponent<Image>();
            completeImg.color = CompleteButtonColor;
            completeImg.raycastTarget = true;
            var completeBtn = completeRt.gameObject.AddComponent<Button>();
            completeBtn.targetGraphic = completeImg;
            completeBtn.transition = Selectable.Transition.ColorTint;
            CreateText(completeRt, "Label", "完成",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 60f), 36, TextAnchor.MiddleCenter,
                DarkTextColor);
            completeRt.gameObject.SetActive(false);
        }

        private static void BuildFilterSection(RectTransform rootRt)
        {
            var filter = CreateChild(rootRt, "FilterSection",
                new Vector2(0.04f, FilterSectionAnchorMinY), new Vector2(0.96f, FilterSectionAnchorMaxY),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(filter);

            float totalWidth = FilterAttrCount * FilterIconSize.x + (FilterAttrCount - 1) * FilterIconGap;
            float startX = -totalWidth * 0.5f + FilterIconSize.x * 0.5f;

            for (int i = 0; i < FilterAttrCount; i++)
            {
                float x = startX + i * (FilterIconSize.x + FilterIconGap);
                var itemRt = CreateChild(filter, "FilterAttr_" + i,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(x, 0f), FilterIconSize);

                var iconImg = itemRt.gameObject.AddComponent<Image>();
                ApplySpriteOrColor(iconImg, RoleTrainingCourseCatalog.FilterAttrIconPaths[i],
                    new Color(0.45f, 0.5f, 0.6f, 1f), preserveAspect: true);
                iconImg.raycastTarget = true;

                var btn = itemRt.gameObject.AddComponent<Button>();
                btn.targetGraphic = iconImg;
                btn.transition = Selectable.Transition.None;

                var badgeRt = CreateChild(itemRt, "SelectedBadge",
                    new Vector2(1f, 0f), new Vector2(1f, 0f),
                    new Vector2(1f, 0f), new Vector2(-4f, 4f), FilterBadgeSize);
                var badgeImg = badgeRt.gameObject.AddComponent<Image>();
                ApplySpriteOrColor(badgeImg, FilterBadgeResource,
                    new Color(0.9f, 0.75f, 0.2f, 1f), preserveAspect: true);
                badgeImg.raycastTarget = false;
                badgeRt.gameObject.SetActive(false);
            }
        }

        private static void BuildCourseSection(RectTransform rootRt)
        {
            var section = CreateChild(rootRt, "CourseSection",
                new Vector2(0.03f, 0.02f), new Vector2(0.97f, FilterSectionAnchorMinY - 0.01f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(section);

            var scrollGo = new GameObject("CourseScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.SetParent(section, false);
            scrollRt.anchorMin = new Vector2(0.5f, 0.5f);
            scrollRt.anchorMax = new Vector2(0.5f, 0.5f);
            scrollRt.pivot = new Vector2(0.5f, 0.5f);
            scrollRt.anchoredPosition = new Vector2(0f, CourseScrollPosY);
            scrollRt.sizeDelta = new Vector2(CourseScrollWidth, CourseScrollHeight);
            var scrollImg = scrollGo.GetComponent<Image>();
            scrollImg.color = new Color(1f, 1f, 1f, 0.01f);
            scrollImg.raycastTarget = true;
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = CreateChild(scrollRt, "Viewport", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(1f, 1f, 1f, 0.01f);
            vpImg.raycastTarget = true;

            var content = CreateChild(viewport, "CourseContent",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 0f));
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);

            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = CourseCellSize;
            grid.spacing = new Vector2(CourseCellGapX, CourseCellGapY);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = CourseColumns;
            grid.padding = new RectOffset(8, 8, 8, 8);

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;

            // 模板课程格（View 运行时复制或填充）
            var template = BuildCourseCell(content, "CourseCellTemplate");
            template.gameObject.SetActive(false);
        }

        public static RectTransform BuildCourseCell(RectTransform parent, string name)
        {
            var cell = CreateChild(parent, name,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, CourseCellSize);

            var bg = cell.gameObject.AddComponent<Image>();
            bg.color = CourseFallbackColor;
            bg.raycastTarget = true;
            var btn = cell.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.ColorTint;

            var iconRt = CreateChild(cell, "Icon",
                new Vector2(0.22f, 0.5f), new Vector2(0.22f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, CourseIconSize);
            var iconImg = iconRt.gameObject.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            CreateText(cell, "Name", "",
                new Vector2(0.68f, 0.74f), Vector2.zero, new Vector2(280f, 56f), 30, TextAnchor.MiddleLeft,
                TextColor);

            CreateText(cell, "Duration", "",
                new Vector2(0.68f, 0.52f), Vector2.zero, new Vector2(280f, 44f), 24, TextAnchor.MiddleLeft,
                IdleHintColor);

            CreateText(cell, "AttrGains", "",
                new Vector2(0.68f, 0.28f), Vector2.zero, new Vector2(280f, 44f), 24, TextAnchor.MiddleLeft,
                AttrGainColor);

            var lockRt = CreateChild(cell, "LockIcon",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, LockIconSize);
            var lockImg = lockRt.gameObject.AddComponent<Image>();
            ApplySpriteOrColor(lockImg, LockIconResource,
                new Color(0.2f, 0.2f, 0.2f, LockIconAlpha), preserveAspect: true);
            var c = lockImg.color;
            c.a = LockIconAlpha;
            lockImg.color = c;
            lockImg.raycastTarget = false;
            lockRt.gameObject.SetActive(false);

            return cell;
        }

        /// <summary>旧版 prefab 缺少 AttrGains 时运行时补齐（SPEC §9.14.12 v3.200）。</summary>
        public static Text EnsureCourseCellAttrGains(RectTransform cell)
        {
            if (cell == null)
                return null;

            var existing = cell.Find("AttrGains");
            if (existing != null)
                return existing.GetComponent<Text>();

            return CreateText(cell, "AttrGains", "",
                new Vector2(0.68f, 0.28f), Vector2.zero, new Vector2(280f, 44f), 24, TextAnchor.MiddleLeft,
                AttrGainColor);
        }

        private static void BuildTipsToast(RectTransform rootRt)
        {
            var tips = CreateChild(rootRt, "TipsToast",
                new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 140f));
            var bg = tips.gameObject.AddComponent<Image>();
            bg.color = TipsBgColor;
            bg.raycastTarget = false;
            CreateText(tips, "TipsText", "",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 120f), 32, TextAnchor.MiddleCenter,
                TextColor);
            tips.gameObject.SetActive(false);
        }

        private static void ApplySpriteOrColor(Image img, string resourcePath, Color fallback, bool preserveAspect = false)
        {
            if (img == null)
                return;
            var sprite = string.IsNullOrEmpty(resourcePath) ? null : Resources.Load<Sprite>(resourcePath);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = preserveAspect;
            }
            else
            {
                img.color = fallback;
            }
            img.raycastTarget = false;
        }

        private static RectTransform CreateChild(
            RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return rt;
        }

        private static Text CreateText(
            RectTransform parent, string name, string content,
            Vector2 anchorPivot, Vector2 anchoredPos, Vector2 size, int fontSize, TextAnchor align,
            Color color)
        {
            var rt = CreateChild(parent, name, anchorPivot, anchorPivot, anchorPivot, anchoredPos, size);
            var txt = rt.gameObject.AddComponent<Text>();
            txt.text = content;
            txt.font = FarmGridView.LoadBuiltinFont();
            txt.fontSize = fontSize;
            txt.alignment = align;
            txt.color = color;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
