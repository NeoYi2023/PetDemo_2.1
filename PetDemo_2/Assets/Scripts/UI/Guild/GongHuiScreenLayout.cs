// SPEC §9.8.9.14 (v3.198)：公会界面右上玩法入口按钮与 TipsToast 层级构建。
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    public static class GongHuiScreenLayout
    {
        public const string ResWfXuanShang = "AirUI/WF_XuanShang";
        public const string ResWfZuDui = "AirUI/WF_ZuDui";
        public const string ResWfJjc = "AirUI/WF_JJC";
        public const string ResWfZhuangYuan = "AirUI/WF_ZhuangYuan";

        public const string LayerName = "TopRightWorkflowLayer";
        public const string ActionsName = "TopRightWorkflowActions";
        public const string WfXuanShangButtonName = "WfXuanShangButton";
        public const string WfZuDuiButtonName = "WfZuDuiButton";
        public const string WfJjcButtonName = "WfJjcButton";
        public const string WfZhuangYuanButtonName = "WfZhuangYuanButton";

        public static readonly Vector2 TopRightButtonSize = HomeTabPanelLayout.TopRightButtonSize;
        public const float TopRightMargin = HomeTabPanelLayout.TopRightMargin;
        public const float TopRightButtonGap = HomeTabPanelLayout.TopRightButtonGap;

        private static readonly Color TopRightButtonFallbackColor = new Color(0.35f, 0.32f, 0.4f, 0.9f);
        private static readonly Color TipsBgColor = new Color(0.08f, 0.08f, 0.1f, 0.88f);
        private static readonly Color TipsTextColor = Color.white;

        private static readonly (string name, string resource)[] WorkflowButtons =
        {
            (WfXuanShangButtonName, ResWfXuanShang),
            (WfZuDuiButtonName, ResWfZuDui),
            (WfJjcButtonName, ResWfJjc),
            (WfZhuangYuanButtonName, ResWfZhuangYuan),
        };

        /// <summary>幂等创建右上竖排玩法按钮层。</summary>
        public static RectTransform EnsureTopRightWorkflowActions(RectTransform screenRoot)
        {
            if (screenRoot == null)
                return null;

            var layerRt = screenRoot.Find(LayerName) as RectTransform;
            if (layerRt == null)
            {
                layerRt = CreateChild(screenRoot, LayerName,
                    Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                StretchFull(layerRt);
                layerRt.SetAsLastSibling();
            }

            var actionsRt = layerRt.Find(ActionsName) as RectTransform;
            if (actionsRt == null)
            {
                float stackHeight = TopRightButtonSize.y * WorkflowButtons.Length
                    + TopRightButtonGap * (WorkflowButtons.Length - 1);
                actionsRt = CreateChild(layerRt, ActionsName,
                    new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(-TopRightMargin, -TopRightMargin),
                    new Vector2(TopRightButtonSize.x, stackHeight));
            }

            for (int i = 0; i < WorkflowButtons.Length; i++)
            {
                var (name, resource) = WorkflowButtons[i];
                float y = -(i * (TopRightButtonSize.y + TopRightButtonGap) + TopRightButtonSize.y * 0.5f);
                EnsureTopRightIconButton(actionsRt, name, resource, new Vector2(0f, y));
            }

            return actionsRt;
        }

        /// <summary>幂等创建居中 TipsToast。</summary>
        public static void EnsureTipsToast(RectTransform screenRoot)
        {
            if (screenRoot == null)
                return;

            var existing = screenRoot.Find("TipsToast");
            if (existing != null)
                return;

            var tips = CreateChild(screenRoot, "TipsToast",
                new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 140f));
            var bg = tips.gameObject.AddComponent<Image>();
            bg.color = TipsBgColor;
            bg.raycastTarget = false;
            CreateText(tips, "TipsText", string.Empty,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 120f), 32, TextAnchor.MiddleCenter);
            tips.gameObject.SetActive(false);
        }

        private static void EnsureTopRightIconButton(
            RectTransform parent, string name, string resourcePath, Vector2 anchoredPos)
        {
            var btnRt = parent.Find(name) as RectTransform;
            if (btnRt == null)
            {
                btnRt = CreateChild(parent, name,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 0.5f), anchoredPos, TopRightButtonSize);
            }

            var img = btnRt.GetComponent<Image>();
            if (img == null)
                img = btnRt.gameObject.AddComponent<Image>();
            ApplySpriteOrColor(img, resourcePath, TopRightButtonFallbackColor);
            img.preserveAspect = true;
            img.raycastTarget = true;

            var btn = btnRt.GetComponent<Button>();
            if (btn == null)
            {
                btn = btnRt.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.transition = Selectable.Transition.ColorTint;
                var colors = btn.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
                colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
                colors.selectedColor = Color.white;
                colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.08f;
                btn.colors = colors;
            }
        }

        private static void ApplySpriteOrColor(Image img, string resourcePath, Color fallback)
        {
            if (img == null)
                return;

            var sprite = string.IsNullOrEmpty(resourcePath) ? null : Resources.Load<Sprite>(resourcePath);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
            }
            else
            {
                img.sprite = null;
                img.color = fallback;
                UnityEngine.Debug.LogWarning(
                    "[GongHuiScreenLayout] 缺少图标 Resources/" + resourcePath + "。");
            }
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
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
            Vector2 anchor, Vector2 anchoredPos, Vector2 sizeDelta,
            int fontSize, TextAnchor alignment)
        {
            var textRt = CreateChild(parent, name, anchor, anchor,
                new Vector2(0.5f, 0.5f), anchoredPos, sizeDelta);
            var text = textRt.gameObject.AddComponent<Text>();
            text.text = content;
            text.font = FarmGridView.LoadBuiltinFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = TipsTextColor;
            text.raycastTarget = false;
            return text;
        }
    }
}
