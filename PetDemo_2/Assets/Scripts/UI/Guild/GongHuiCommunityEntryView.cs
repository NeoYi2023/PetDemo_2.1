// SPEC §9.8.9.10：公会 Tab 下 TopDingBar 左下方「打开社区」入口。
using System;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class GongHuiCommunityEntryView : MonoBehaviour
    {
        public const string ResEntryIcon = "AirUI/SheQu_Icon_1";

        private const float EntryPosX = 16f;
        private const float GapBelowDingUi = 12f;
        private const float IconSize = 100f;
        private const int LabelFontSize = 36;
        private const float FallbackPosY = -12f;

        private BottomNavBarView bottomNav;
        private RectTransform rootRt;
        private RectTransform canvasRectCache;

        public static GongHuiCommunityEntryView BuildInto(
            RectTransform hudRoot,
            BottomNavBarView barView,
            RectTransform canvasRect)
        {
            if (hudRoot == null || barView == null)
                return null;

            var rootGo = new GameObject("GongHuiCommunityEntryLayer", typeof(RectTransform));
            var root = rootGo.GetComponent<RectTransform>();
            root.SetParent(hudRoot, false);
            StretchFull(root);

            float posY = ComputePosYBelowDingUi();
            var buttonRt = CreateChildRect(root, "OpenCommunityButton",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(EntryPosX, posY), Vector2.zero);
            buttonRt.pivot = new Vector2(0f, 1f);

            var buttonBg = buttonRt.gameObject.AddComponent<Image>();
            buttonBg.color = new Color(0f, 0f, 0f, 0f);
            buttonBg.raycastTarget = true;

            var layout = buttonRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 8f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var iconRt = CreateChildRect(buttonRt, "Icon",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(IconSize, IconSize));
            var iconImg = iconRt.gameObject.AddComponent<Image>();
            var iconSprite = Resources.Load<Sprite>(ResEntryIcon);
            if (iconSprite != null)
            {
                iconImg.sprite = iconSprite;
                iconImg.preserveAspect = true;
                iconImg.color = Color.white;
            }
            else
            {
                iconImg.color = new Color(0.25f, 0.22f, 0.3f, 0.9f);
                UnityEngine.Debug.LogWarning(
                    "[GongHuiCommunityEntryView] 缺少图标 Resources/" + ResEntryIcon + "。");
            }
            iconImg.raycastTarget = false;
            var iconLe = iconRt.gameObject.AddComponent<LayoutElement>();
            iconLe.preferredWidth = IconSize;
            iconLe.preferredHeight = IconSize;

            var labelRt = CreateChildRect(buttonRt, "Label",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(200f, IconSize));
            var label = labelRt.gameObject.AddComponent<Text>();
            label.text = "乐园社区";
            label.font = FarmGridView.LoadBuiltinFont();
            label.fontSize = LabelFontSize;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = Color.white;
            label.raycastTarget = false;
            var labelLe = labelRt.gameObject.AddComponent<LayoutElement>();
            labelLe.preferredWidth = 200f;
            labelLe.preferredHeight = IconSize;

            var entryBtn = buttonRt.gameObject.AddComponent<Button>();
            entryBtn.transition = Selectable.Transition.None;
            entryBtn.targetGraphic = buttonBg;

            var fitter = buttonRt.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var view = rootGo.AddComponent<GongHuiCommunityEntryView>();
            view.rootRt = root;
            view.bottomNav = barView;
            view.canvasRectCache = canvasRect;
            entryBtn.onClick.AddListener(view.OnEntryClicked);

            barView.OnOpenChanged += view.OnBottomNavOpenChanged;
            view.OnBottomNavOpenChanged(barView.OpenIndex, barView.OpenKey);

            return view;
        }

        private static float ComputePosYBelowDingUi()
        {
            var dingSprite = Resources.Load<Sprite>(TopDingBarView.ResDingSprite);
            if (dingSprite != null)
                return -(dingSprite.rect.height + GapBelowDingUi);
            return FallbackPosY;
        }

        private void OnEntryClicked()
        {
            if (canvasRectCache == null)
                return;
            var overlay = GongHuiCommunityOverlayView.GetOrCreate(canvasRectCache);
            overlay?.Show();
        }

        private void OnBottomNavOpenChanged(int index, string key)
        {
            bool gongHui = !string.IsNullOrEmpty(key) &&
                           string.Equals(key, GongHuiScreenView.GongHuiNavKey, StringComparison.Ordinal);
            if (rootRt != null)
                rootRt.gameObject.SetActive(gongHui);
            if (!gongHui)
                GongHuiCommunityOverlayView.HideIfAny();
        }

        private void OnDestroy()
        {
            if (bottomNav != null)
                bottomNav.OnOpenChanged -= OnBottomNavOpenChanged;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static RectTransform CreateChildRect(
            RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }
    }
}
