// SPEC §9.8.8 / §9.8.9 / §9.8.10：底栏附属全屏面板的公共 RectTransform 与 Resources 背景构建（主线、公会、商店等复用）。
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    public static class BottomNavAttachedScreenLayout
    {
        /// <summary>
        /// 与 <see cref="BottomNavBarView"/> 同 Canvas、同级，<c>SetSiblingIndex</c> 置于底栏之下。
        /// </summary>
        public static RectTransform CreateRootBelowBottomNav(
            RectTransform canvasRect,
            BottomNavBarView barView,
            string objectName)
        {
            var rootGo = new GameObject(objectName, typeof(RectTransform));
            var root = rootGo.GetComponent<RectTransform>();
            root.SetParent(canvasRect, false);
            StretchFull(root);

            var barRt = barView.transform as RectTransform;
            if (barRt != null)
                root.SetSiblingIndex(barRt.GetSiblingIndex());

            return root;
        }

        /// <summary>
        /// 在父节点下创建全屏拉伸的 <c>Background</c>，从 <c>Resources</c> 加载精灵；缺失时深色回退并 <c>LogWarning</c>。
        /// </summary>
        public static Image AddStretchedResourcesBackground(
            RectTransform parent,
            string resourcesSpritePath,
            string logSourceTag,
            string logContextSuffix = null)
        {
            var bgRt = CreateChildRect(parent, "Background",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            StretchFull(bgRt);
            var bgImage = bgRt.gameObject.AddComponent<Image>();
            var bgSprite = Resources.Load<Sprite>(resourcesSpritePath);
            if (bgSprite != null)
            {
                bgImage.sprite = bgSprite;
                bgImage.preserveAspect = false;
                bgImage.color = Color.white;
            }
            else
            {
                bgImage.color = new Color(0.08f, 0.07f, 0.12f, 1f);
                var msg = "[" + logSourceTag + "] 缺少背景 Resources/" + resourcesSpritePath + "，已使用深色回退。";
                if (!string.IsNullOrEmpty(logContextSuffix))
                    msg += logContextSuffix;
                UnityEngine.Debug.LogWarning(msg);
            }

            bgImage.raycastTarget = true;
            return bgImage;
        }

        public static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        public static RectTransform CreateChildRect(
            RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
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
