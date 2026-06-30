// SPEC §9.15.2：单人聊天面板层级构建（运行时回退与预制体生成器共用，保证结构一致）。
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    public static class AppSingleChatPanelLayout
    {
        public const string BackgroundResource = "AirUI/App_3";
        public const string WolfPortraitResource = "AirUI/ZhuJue_Q";
        public const string WolfBadgeResource = "AirUI/ZhuJue_Q_XI";

        private static readonly Color BackgroundFallback = new Color(0.1f, 0.12f, 0.18f, 1f);
        private static readonly Vector2 WolfButtonSize = new Vector2(200f, 280f);
        private static readonly Vector2 WolfButtonOffset = new Vector2(-120f, 280f);
        private static readonly Vector2 WolfBadgeSize = new Vector2(220f, 80f);
        private static readonly Vector2 WolfBadgeOffset = new Vector2(-120f, 520f);

        /// <summary>
        /// 构建完整单人聊天面板层级并挂载 <see cref="AppSingleChatPanelView"/>。
        /// </summary>
        public static GameObject BuildRuntime(RectTransform parent)
        {
            var root = new GameObject(AppSingleChatPanelView.PanelObjectName, typeof(RectTransform));
            var rootRt = root.GetComponent<RectTransform>();
            if (parent != null)
                rootRt.SetParent(parent, false);
            StretchFull(rootRt);
            root.AddComponent<AppSingleChatPanelView>();

            var bg = CreateChild(rootRt, "Background", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(bg);
            var bgImg = bg.gameObject.AddComponent<Image>();
            ApplySprite(bgImg, BackgroundResource, false);
            bgImg.raycastTarget = true;

            var wolfBadge = CreateCornerChild(rootRt, "WolfBadge",
                new Vector2(1f, 0f), WolfBadgeOffset, WolfBadgeSize);
            var badgeImg = wolfBadge.gameObject.AddComponent<Image>();
            ApplySprite(badgeImg, WolfBadgeResource, true);
            badgeImg.raycastTarget = false;
            wolfBadge.gameObject.SetActive(false);

            var wolfButton = CreateCornerChild(rootRt, "WolfButton",
                new Vector2(1f, 0f), WolfButtonOffset, WolfButtonSize);
            var wolfImg = wolfButton.gameObject.AddComponent<Image>();
            ApplySprite(wolfImg, WolfPortraitResource, true);
            wolfImg.raycastTarget = true;
            var wolfBtn = wolfButton.gameObject.AddComponent<Button>();
            wolfBtn.transition = Selectable.Transition.None;
            wolfBtn.targetGraphic = wolfImg;
            wolfButton.gameObject.SetActive(false);

            return root;
        }

        private static void ApplySprite(Image image, string spritePath, bool preserveAspect)
        {
            var sprite = Resources.Load<Sprite>(spritePath);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.preserveAspect = preserveAspect;
                image.color = Color.white;
            }
            else
            {
                image.color = BackgroundFallback;
                UnityEngine.Debug.LogWarning(
                    "[AppSingleChatPanelLayout] 缺少图片 Resources/" + spritePath);
            }
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

        private static RectTransform CreateCornerChild(
            RectTransform parent, string name,
            Vector2 anchorPivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            return CreateChild(parent, name, anchorPivot, anchorPivot, anchorPivot, anchoredPos, sizeDelta);
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
