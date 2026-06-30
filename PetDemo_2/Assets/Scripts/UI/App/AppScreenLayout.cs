// SPEC §9.15：APP 入口界面层级构建（运行时回退与预制体生成器共用，保证结构一致）。
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    public static class AppScreenLayout
    {
        public const string HomeBackgroundResource = "AirUI/App_1";
        public const string MessagesBackgroundResource = "AirUI/App_2";

        private static readonly Color TransparentHitColor = new Color(1f, 1f, 1f, 0.01f);
        private static readonly Color BackgroundFallback = new Color(0.1f, 0.12f, 0.18f, 1f);

        /// <summary>
        /// 构建完整 APP 入口界面层级并挂载 <see cref="AppScreenView"/>。
        /// 字段在运行时由 View.EnsureFieldsFromHierarchy 按节点名绑定；prefab 生成器直接复用此结构。
        /// </summary>
        public static GameObject BuildRuntime(RectTransform parent)
        {
            var root = new GameObject(AppScreenView.PanelObjectName, typeof(RectTransform));
            var rootRt = root.GetComponent<RectTransform>();
            if (parent != null)
                rootRt.SetParent(parent, false);
            StretchFull(rootRt);
            root.AddComponent<AppScreenView>();

            var pageHome = CreatePage(rootRt, "PageHome", HomeBackgroundResource);
            CreateTransparentButton(pageHome, "HomeEnterHit",
                new Vector2(0.05f, 0.38f), new Vector2(0.95f, 0.58f));

            var pageMessages = CreatePage(rootRt, "PageMessages", MessagesBackgroundResource);
            pageMessages.gameObject.SetActive(false);
            CreateTransparentButton(pageMessages, "MessageHit",
                new Vector2(0f, 0.62f), new Vector2(1f, 0.70f));

            var tabBar = CreateChild(rootRt, "TabBar",
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 160f));
            CreateTransparentButton(tabBar, "TabHomeBtn",
                new Vector2(0f, 0f), new Vector2(0.5f, 1f));
            CreateTransparentButton(tabBar, "TabMessagesBtn",
                new Vector2(0.5f, 0f), new Vector2(1f, 1f));

            return root;
        }

        private static RectTransform CreatePage(RectTransform parent, string name, string spritePath)
        {
            var page = CreateChild(parent, name, Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(page);

            var bg = CreateChild(page, "Background", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(bg);
            var bgImg = bg.gameObject.AddComponent<Image>();
            ApplySprite(bgImg, spritePath);
            bgImg.raycastTarget = true;

            return page;
        }

        private static Button CreateTransparentButton(
            RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rt = CreateChild(parent, name, anchorMin, anchorMax,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(rt);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = TransparentHitColor;
            img.raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;
            return btn;
        }

        private static void ApplySprite(Image image, string spritePath)
        {
            var sprite = Resources.Load<Sprite>(spritePath);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.preserveAspect = false;
                image.color = Color.white;
            }
            else
            {
                image.color = BackgroundFallback;
                UnityEngine.Debug.LogWarning(
                    "[AppScreenLayout] 缺少背景图 Resources/" + spritePath);
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
