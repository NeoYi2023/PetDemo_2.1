// SPEC §9.8.17（v3.112 / v3.114）：主界面 HUD 层根节点，与世界层 sortingOrder 频段分离。
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    public static class MainHudLayerRoot
    {
        public const string RootObjectName = "MainHudLayerRoot";

        public static RectTransform Instance { get; private set; }

        /// <summary>
        /// 在 <paramref name="mainCanvas"/> 下创建全屏 HUD 根，默认 <see cref="MainUiSortTier.HudChrome"/>。
        /// 嵌套 Canvas 须自带 <see cref="GraphicRaycaster"/>（v3.114）。
        /// </summary>
        public static RectTransform BuildUnder(RectTransform mainCanvas)
        {
            if (mainCanvas == null)
                return null;

            if (Instance != null)
                return Instance;

            var go = new GameObject(RootObjectName, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(mainCanvas, false);
            StretchFull(rt);

            var canvas = go.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = MainUiSortTier.HudChrome;
            EnsureGraphicRaycaster(go);

            Instance = rt;
            return rt;
        }

        /// <summary>为 Modal / Screen / Overlay 根节点写入独立 sortingOrder。</summary>
        public static void ApplySortTier(RectTransform node, int tier)
        {
            if (node == null)
                return;

            var canvas = node.GetComponent<Canvas>();
            if (canvas == null)
                canvas = node.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = tier;
            EnsureGraphicRaycaster(node.gameObject);
        }

        /// <summary>为带 Canvas 的节点补齐 GraphicRaycaster，供 HUD 与世界层嵌套 Canvas 共用。</summary>
        public static void EnsureGraphicRaycaster(GameObject go)
        {
            if (go == null)
                return;
            if (go.GetComponent<Canvas>() == null)
                return;
            if (go.GetComponent<GraphicRaycaster>() == null)
                go.AddComponent<GraphicRaycaster>();
        }

        /// <summary>移除动态嵌套 Canvas 时先销毁 GraphicRaycaster，避免 Unity 依赖报错。</summary>
        public static void DestroyNestedCanvasComponents(GameObject go)
        {
            if (go == null)
                return;

            var raycaster = go.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                Object.Destroy(raycaster);

            var canvas = go.GetComponent<Canvas>();
            if (canvas != null)
                Object.Destroy(canvas);
        }

        public static void ClearInstance()
        {
            Instance = null;
        }

        /// <summary>SPEC §9.14：创角界面期间隐藏/恢复整个 HUD 层（底栏、属性条、仓库入口等）。</summary>
        public static void SetVisible(bool visible)
        {
            if (Instance == null)
                return;
            if (Instance.gameObject.activeSelf != visible)
                Instance.gameObject.SetActive(visible);
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
