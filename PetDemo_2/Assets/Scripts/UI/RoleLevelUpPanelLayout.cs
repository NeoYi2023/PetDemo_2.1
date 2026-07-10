// SPEC §9.14.13 (v3.208)：主角升级全屏面板层级构建。
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    public static class RoleLevelUpPanelLayout
    {
        private static readonly Color DimOverlayColor = new Color(0.05f, 0.05f, 0.08f, 0.92f);
        private static readonly Color PanelBgColor = new Color(0.12f, 0.14f, 0.2f, 0.98f);
        private static readonly Color OkButtonColor = new Color(0.1f, 0.1f, 0.12f, 1f);
        private static readonly Color TitleColor = Color.white;
        private static readonly Color UnlockTitleColor = Color.white;
        private static readonly Color UnlockDescColor = new Color(0.9f, 0.92f, 0.95f, 1f);
        private static readonly Color UnlockRowFallback = new Color(0.22f, 0.24f, 0.3f, 1f);

        private const int LevelUpTitleFontSize = 60;
        private const int LevelNumberFontSize = 100;
        private const int OkButtonFontSize = 40;
        private const int UnlockTitleFontSize = 36;
        private const int UnlockDescFontSize = 28;
        public const int OverlayCanvasSortOrder = 5000;

        public static readonly Vector2 UnlockIconSize = new Vector2(96f, 96f);
        public const float UnlockRowHeight = 120f;
        public const float UnlockRowGap = 12f;
        public static readonly Vector2 OkButtonSize = new Vector2(320f, 96f);

        public static GameObject BuildRuntime(RectTransform parent)
        {
            var root = new GameObject(RoleLevelUpPanelView.PanelObjectName, typeof(RectTransform));
            var rootRt = root.GetComponent<RectTransform>();
            if (parent != null)
                rootRt.SetParent(parent, false);
            StretchFull(rootRt);

            EnsureOverlayCanvas(root);
            root.AddComponent<RoleLevelUpPanelView>();

            var dim = CreateChild(rootRt, "DimOverlay", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(dim);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = DimOverlayColor;
            dimImg.raycastTarget = true;

            float bottomInset = CharacterCreationScreenLayout.ContentRegionBottomOffset;
            var content = CreateChild(rootRt, "Content",
                Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            content.offsetMin = new Vector2(32f, bottomInset + 120f);
            content.offsetMax = new Vector2(-32f, -48f);
            var contentImg = content.gameObject.AddComponent<Image>();
            contentImg.color = PanelBgColor;
            contentImg.raycastTarget = false;

            BuildUpperSection(content);
            BuildUnlockSection(content);

            // OK 挂在根节点，始终在最上层，避免被 Content 子节点挡住。
            BuildOkButton(rootRt);

            root.SetActive(false);
            return root;
        }

        public static void EnsureOverlayCanvas(GameObject root)
        {
            if (root == null)
                return;

            var canvas = root.GetComponent<Canvas>();
            if (canvas == null)
                canvas = root.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = OverlayCanvasSortOrder;

            if (root.GetComponent<GraphicRaycaster>() == null)
                root.AddComponent<GraphicRaycaster>();

            var cg = root.GetComponent<CanvasGroup>();
            if (cg == null)
                cg = root.AddComponent<CanvasGroup>();
            cg.interactable = true;
            cg.blocksRaycasts = true;
            cg.alpha = 1f;
        }

        private static void BuildUpperSection(RectTransform content)
        {
            var upper = CreateChild(content, "UpperSection",
                new Vector2(0f, 0.48f), new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            upper.offsetMin = Vector2.zero;
            upper.offsetMax = Vector2.zero;

            CreateText(upper, "LevelUpTitle", "Level UP!",
                new Vector2(0.5f, 0.82f), Vector2.zero, new Vector2(900f, 80f),
                LevelUpTitleFontSize, TextAnchor.MiddleCenter, TitleColor);

            CreateText(upper, "LevelNumber", "2",
                new Vector2(0.5f, 0.52f), Vector2.zero, new Vector2(400f, 140f),
                LevelNumberFontSize, TextAnchor.MiddleCenter, TitleColor);

            HomeTabPanelLayout.BuildLevelExpRow(upper,
                new Vector2(0f, 0.02f), new Vector2(1f, 0.28f));
        }

        private static void BuildUnlockSection(RectTransform content)
        {
            var unlock = CreateChild(content, "UnlockSection",
                new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.46f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            unlock.offsetMin = Vector2.zero;
            unlock.offsetMax = Vector2.zero;

            // 不用 ScrollRect：避免 Viewport/Mask 把内容裁没或挡住点击。
            var list = CreateChild(unlock, "UnlockContent",
                Vector2.zero, Vector2.one,
                new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            StretchFull(list);
            list.pivot = new Vector2(0.5f, 1f);

            var vlg = list.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.spacing = UnlockRowGap;
            vlg.padding = new RectOffset(8, 8, 8, 8);

            var template = BuildUnlockRowTemplate(list);
            template.gameObject.SetActive(false);
        }

        public static RectTransform BuildUnlockRowTemplate(RectTransform parent)
        {
            var row = CreateChild(parent, "UnlockRowTemplate",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, UnlockRowHeight));
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = UnlockRowHeight;
            le.preferredHeight = UnlockRowHeight;
            le.flexibleWidth = 1f;

            var rowImg = row.gameObject.AddComponent<Image>();
            rowImg.color = UnlockRowFallback;
            rowImg.raycastTarget = false;

            var icon = CreateChild(row, "Icon",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(16f, 0f), UnlockIconSize);
            var iconImg = icon.gameObject.AddComponent<Image>();
            iconImg.color = new Color(0.55f, 0.58f, 0.65f, 1f);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            var texts = CreateChild(row, "Texts",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            texts.offsetMin = new Vector2(UnlockIconSize.x + 28f, 10f);
            texts.offsetMax = new Vector2(-16f, -10f);

            var title = CreateText(texts, "Title", "功能标题",
                new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 44f),
                UnlockTitleFontSize, TextAnchor.MiddleLeft, UnlockTitleColor);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 0.5f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Truncate;

            var desc = CreateText(texts, "Description", "功能描述",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 40f),
                UnlockDescFontSize, TextAnchor.UpperLeft, UnlockDescColor);
            var descRt = desc.rectTransform;
            descRt.anchorMin = new Vector2(0f, 0f);
            descRt.anchorMax = new Vector2(1f, 0.5f);
            descRt.offsetMin = Vector2.zero;
            descRt.offsetMax = Vector2.zero;
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            desc.verticalOverflow = VerticalWrapMode.Truncate;

            return row;
        }

        private static void BuildOkButton(RectTransform rootRt)
        {
            float bottomInset = CharacterCreationScreenLayout.ContentRegionBottomOffset;
            var btnRt = CreateChild(rootRt, "OkButton",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, bottomInset + 24f),
                OkButtonSize);
            btnRt.SetAsLastSibling();

            // SPEC §9.14.13 v3.210：不透明 HitArea 作为 targetGraphic，避免透明 Sprite 穿透。
            var hit = EnsureOpaqueHitArea(btnRt);
            var btn = btnRt.gameObject.GetComponent<Button>();
            if (btn == null)
                btn = btnRt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = hit;
            btn.interactable = true;

            CreateText(btnRt, "Label", "OK",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 70f),
                OkButtonFontSize, TextAnchor.MiddleCenter, Color.white);
        }

        /// <summary>
        /// SPEC §9.14.13 v3.210：保证 OkButton 有不透明矩形命中区。
        /// 旧 prefab 若把带透明像素的 Sprite 挂在根 Image 上，点击会穿透到 DimOverlay。
        /// </summary>
        public static Image EnsureOpaqueHitArea(RectTransform okButtonRt)
        {
            if (okButtonRt == null)
                return null;

            // 根上若有装饰 Sprite，关掉其射线，避免透明区穿透。
            var rootImg = okButtonRt.GetComponent<Image>();
            if (rootImg != null)
            {
                if (rootImg.sprite != null)
                {
                    rootImg.raycastTarget = false;
                }
                else
                {
                    rootImg.sprite = null;
                    rootImg.color = OkButtonColor;
                    rootImg.raycastTarget = true;
                    return rootImg;
                }
            }

            var hitRt = okButtonRt.Find("HitArea") as RectTransform;
            if (hitRt == null)
            {
                hitRt = CreateChild(okButtonRt, "HitArea",
                    Vector2.zero, Vector2.one,
                    new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                StretchFull(hitRt);
                hitRt.SetAsFirstSibling();
            }
            else
            {
                StretchFull(hitRt);
                hitRt.SetAsFirstSibling();
            }

            var hitImg = hitRt.GetComponent<Image>();
            if (hitImg == null)
                hitImg = hitRt.gameObject.AddComponent<Image>();
            hitImg.sprite = null;
            hitImg.color = OkButtonColor;
            hitImg.raycastTarget = true;

            var label = okButtonRt.Find("Label");
            if (label != null)
                label.SetAsLastSibling();

            return hitImg;
        }

        /// <summary>运行时校正已有 prefab：独立 Canvas、分区、根级 OkButton、简化解锁列表。</summary>
        public static void EnsureClickableLayout(RectTransform rootRt)
        {
            if (rootRt == null)
                return;

            EnsureOverlayCanvas(rootRt.gameObject);

            float bottomInset = CharacterCreationScreenLayout.ContentRegionBottomOffset;

            var dim = rootRt.Find("DimOverlay") as RectTransform;
            if (dim != null)
                dim.SetAsFirstSibling();

            var content = rootRt.Find("Content") as RectTransform;
            if (content != null)
            {
                content.anchorMin = Vector2.zero;
                content.anchorMax = Vector2.one;
                content.pivot = new Vector2(0.5f, 0.5f);
                content.offsetMin = new Vector2(32f, bottomInset + 120f);
                content.offsetMax = new Vector2(-32f, -48f);

                var upper = content.Find("UpperSection") as RectTransform;
                if (upper != null)
                {
                    upper.anchorMin = new Vector2(0f, 0.48f);
                    upper.anchorMax = new Vector2(1f, 1f);
                    upper.offsetMin = Vector2.zero;
                    upper.offsetMax = Vector2.zero;
                }

                EnsureUnlockSection(content);
            }

            EnsureRootOkButton(rootRt, bottomInset);

            // Content 背景不必拦截：DimOverlay 已全屏挡底层；避免与根级 OkButton 边缘重叠误伤。
            if (content != null)
            {
                var contentImg = content.GetComponent<Image>();
                if (contentImg != null)
                    contentImg.raycastTarget = false;
            }
        }

        private static void DisableRaycastsRecursive(Transform root)
        {
            if (root == null)
                return;
            var graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null)
                    graphics[i].raycastTarget = false;
            }
        }

        private static void EnsureUnlockSection(RectTransform content)
        {
            var unlock = content.Find("UnlockSection") as RectTransform;
            if (unlock == null)
            {
                BuildUnlockSection(content);
                return;
            }

            unlock.anchorMin = new Vector2(0.04f, 0.02f);
            unlock.anchorMax = new Vector2(0.96f, 0.46f);
            unlock.offsetMin = Vector2.zero;
            unlock.offsetMax = Vector2.zero;

            // 旧 prefab 可能是 UnlockScroll/Viewport/UnlockContent；统一迁到 UnlockSection/UnlockContent。
            var list = unlock.Find("UnlockContent") as RectTransform;
            if (list == null)
            {
                var nested = unlock.Find("UnlockScroll/Viewport/UnlockContent") as RectTransform;
                if (nested != null)
                {
                    nested.SetParent(unlock, false);
                    nested.name = "UnlockContent";
                    list = nested;
                }
            }

            // 销毁旧 Scroll 壳，避免透明 Image 继续挡点击 / 裁切内容。
            // Destroy 延迟到帧末，先立刻关掉射线，避免当帧仍吞掉 OK 点击。
            var oldScroll = unlock.Find("UnlockScroll");
            if (oldScroll != null)
            {
                DisableRaycastsRecursive(oldScroll);
                Object.Destroy(oldScroll.gameObject);
            }

            if (list == null)
            {
                for (int i = unlock.childCount - 1; i >= 0; i--)
                    Object.Destroy(unlock.GetChild(i).gameObject);

                var listRt = CreateChild(unlock, "UnlockContent",
                    Vector2.zero, Vector2.one,
                    new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
                StretchFull(listRt);
                listRt.pivot = new Vector2(0.5f, 1f);

                var vlgNew = listRt.gameObject.AddComponent<VerticalLayoutGroup>();
                vlgNew.childAlignment = TextAnchor.UpperCenter;
                vlgNew.childControlHeight = true;
                vlgNew.childControlWidth = true;
                vlgNew.childForceExpandHeight = false;
                vlgNew.childForceExpandWidth = true;
                vlgNew.spacing = UnlockRowGap;
                vlgNew.padding = new RectOffset(8, 8, 8, 8);

                var templateNew = BuildUnlockRowTemplate(listRt);
                templateNew.gameObject.SetActive(false);
                return;
            }

            StretchFull(list);
            list.pivot = new Vector2(0.5f, 1f);

            if (list.GetComponent<VerticalLayoutGroup>() == null)
            {
                var vlg = list.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childControlHeight = true;
                vlg.childControlWidth = true;
                vlg.childForceExpandHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.spacing = UnlockRowGap;
                vlg.padding = new RectOffset(8, 8, 8, 8);
            }

            if (list.Find("UnlockRowTemplate") == null)
            {
                var template = BuildUnlockRowTemplate(list);
                template.gameObject.SetActive(false);
            }
        }

        private static void EnsureRootOkButton(RectTransform rootRt, float bottomInset)
        {
            // 若 OkButton 仍在 Content 下，挪到根节点。
            var contentOk = rootRt.Find("Content/OkButton") as RectTransform;
            var rootOk = rootRt.Find("OkButton") as RectTransform;
            if (rootOk == null && contentOk != null)
            {
                contentOk.SetParent(rootRt, false);
                rootOk = contentOk;
            }

            if (rootOk == null)
            {
                BuildOkButton(rootRt);
                rootOk = rootRt.Find("OkButton") as RectTransform;
            }

            if (rootOk == null)
                return;

            rootOk.anchorMin = new Vector2(0.5f, 0f);
            rootOk.anchorMax = new Vector2(0.5f, 0f);
            rootOk.pivot = new Vector2(0.5f, 0f);
            rootOk.anchoredPosition = new Vector2(0f, bottomInset + 24f);
            rootOk.sizeDelta = OkButtonSize;
            rootOk.SetAsLastSibling();

            var hitImg = EnsureOpaqueHitArea(rootOk);

            var btn = rootOk.GetComponent<Button>();
            if (btn == null)
                btn = rootOk.gameObject.AddComponent<Button>();
            btn.interactable = true;
            btn.targetGraphic = hitImg;
            btn.transition = Selectable.Transition.ColorTint;

            var label = rootOk.Find("Label")?.GetComponent<Text>();
            if (label != null)
            {
                label.color = Color.white;
                label.raycastTarget = false;
                if (string.IsNullOrEmpty(label.text))
                    label.text = "OK";
            }
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static RectTransform CreateChild(
            RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return rt;
        }

        private static Text CreateText(
            RectTransform parent, string name, string text,
            Vector2 anchorPivot, Vector2 anchoredPos, Vector2 size,
            int fontSize, TextAnchor align, Color color)
        {
            var rt = CreateChild(parent, name,
                anchorPivot, anchorPivot, anchorPivot, anchoredPos, size);
            var txt = rt.gameObject.AddComponent<Text>();
            txt.text = text;
            txt.font = FarmGridView.LoadBuiltinFont();
            txt.fontSize = fontSize;
            txt.alignment = align;
            txt.color = color;
            txt.raycastTarget = false;
            return txt;
        }
    }
}
