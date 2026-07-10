// SPEC §9.8.12.4 (v3.40 / v3.204)：体力条 StaminaBarView。
// 四层结构：IconLayer(TiLi_0) + BarTrack{ BottomLayer(TiLi_1) + FillLayer(TiLi_2) + TopLayer(TiLi_3) }。
// 提供 Bind / Refresh / 静态 BuildInto 三个 API，事件订阅在 OnDestroy 解除。
using PetDemo.Core;
using PetDemo.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    public class StaminaBarView : MonoBehaviour
    {
        // SPEC §9.8.12.4：资源路径与默认尺寸。
        private const string ResIcon = "AirUI/TiLi_0";
        private const string ResBottom = "AirUI/TiLi_1";
        private const string ResFill = "AirUI/TiLi_2";
        private const string ResTop = "AirUI/TiLi_3";
        private const string ResPrefab = "Prefabs/Farm/StaminaBar";
        public const float DefaultWidth = 800f;
        public const float DefaultHeight = 60f;
        public const float DefaultIconWidth = 72f;

        [SerializeField] private Image iconImage;
        [SerializeField] private RectTransform barTrackRect;
        [SerializeField] private Image bottomImage;
        [SerializeField] private Image fillImage;
        [SerializeField] private Image topImage;

        private RoleStats role;
        private IPlantingService service;
        private RectTransform fillRect;
        private float baseWidth;

        public void Bind(RoleStats roleStats)
        {
            role = roleStats;
            Refresh();
        }

        public void SubscribeService(IPlantingService plantingService)
        {
            if (service != null)
                service.OnStaminaChanged -= HandleStaminaChanged;
            service = plantingService;
            if (service != null)
                service.OnStaminaChanged += HandleStaminaChanged;
        }

        private void HandleStaminaChanged(int newValue, int max)
        {
            Refresh();
        }

        public void Refresh()
        {
            EnsureFillRect();
            if (fillRect == null)
                return;

            int cur = 0, max = 100;
            if (role != null)
            {
                cur = Mathf.Clamp(role.stamina, 0, role.staminaMax);
                max = role.staminaMax > 0 ? role.staminaMax : 100;
            }

            float ratio = max <= 0 ? 0f : Mathf.Clamp01((float)cur / max);
            float width = baseWidth * ratio;
            var sd = fillRect.sizeDelta;
            sd.x = width;
            fillRect.sizeDelta = sd;
            if (fillImage != null)
                fillImage.gameObject.SetActive(cur > 0);
        }

        private void EnsureFillRect()
        {
            if (fillImage != null && fillRect == null)
                fillRect = fillImage.rectTransform;

            if (barTrackRect != null)
            {
                float trackWidth = barTrackRect.rect.width;
                if (trackWidth > 0f)
                    baseWidth = trackWidth;
            }
            else if (baseWidth <= 0f)
            {
                var rootRect = transform as RectTransform;
                baseWidth = rootRect != null ? rootRect.rect.width : DefaultWidth;
                if (baseWidth <= 0f)
                    baseWidth = DefaultWidth;
            }
        }

        private void OnDestroy()
        {
            if (service != null)
                service.OnStaminaChanged -= HandleStaminaChanged;
            service = null;
        }

        // SPEC §9.14.11 (v3.205)：优先复用 parent 下已嵌入的 StaminaBarView（预制体手动摆放），否则 BuildInto。
        public static StaminaBarView GetOrCreateIn(RectTransform parent, RoleStats role, IPlantingService service = null)
        {
            if (parent == null)
                return null;

            var existing = parent.GetComponentInChildren<StaminaBarView>(true);
            if (existing != null)
            {
                existing.EnsureFieldsFromHierarchy();
                existing.Bind(role);
                if (service != null)
                    existing.SubscribeService(service);
                return existing;
            }

            return BuildInto(parent, role, service);
        }

        // SPEC §9.8.12.4：静态构建入口。
        // 资源优先级：Resources/Prefabs/Farm/StaminaBar.prefab → 否则代码搭建。
        // SPEC §9.8.13.4 (v3.41)：实例化后若 parent.rect 有有效尺寸，将根 sizeDelta 同步为 parent（填满 275×116 槽）。
        public static StaminaBarView BuildInto(RectTransform parent, RoleStats role, IPlantingService service = null)
        {
            if (parent == null)
                return null;

            GameObject go = null;
            var prefab = Resources.Load<GameObject>(ResPrefab);
            if (prefab != null)
                go = Instantiate(prefab, parent, false);
            else
                go = BuildRuntime(parent);

            if (go == null)
                return null;

            var view = go.GetComponent<StaminaBarView>();
            if (view == null)
                view = go.AddComponent<StaminaBarView>();

            var rootRect = go.transform as RectTransform;
            if (rootRect != null && parent.rect.width > 0f && parent.rect.height > 0f)
            {
                rootRect.anchorMin = new Vector2(0.5f, 0.5f);
                rootRect.anchorMax = new Vector2(0.5f, 0.5f);
                rootRect.pivot = new Vector2(0.5f, 0.5f);
                rootRect.anchoredPosition = Vector2.zero;
                rootRect.sizeDelta = new Vector2(parent.rect.width, parent.rect.height);
            }

            view.EnsureFieldsFromHierarchy();
            view.Bind(role);
            if (service != null)
                view.SubscribeService(service);
            return view;
        }

        /// <summary>供预制体生成器调用：无 parent，默认 275×116。</summary>
        public static GameObject BuildRuntimeForPrefab()
        {
            return BuildRuntime(null, new Vector2(275f, 116f));
        }

        private static GameObject BuildRuntime(RectTransform parent)
        {
            float w = (parent != null && parent.rect.width > 0f) ? parent.rect.width : DefaultWidth;
            float h = (parent != null && parent.rect.height > 0f) ? parent.rect.height : DefaultHeight;
            return BuildRuntime(parent, new Vector2(w, h));
        }

        private static GameObject BuildRuntime(RectTransform parent, Vector2 size)
        {
            var root = new GameObject("StaminaBar", typeof(RectTransform));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(parent, false);
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = size;
            rootRect.anchoredPosition = Vector2.zero;

            var view = root.AddComponent<StaminaBarView>();
            BuildLayers(rootRect, view, size);
            return root;
        }

        private static void BuildLayers(RectTransform rootRect, StaminaBarView view, Vector2 size)
        {
            float iconWidth = Mathf.Min(DefaultIconWidth, size.y > 0f ? size.y : DefaultIconWidth);

            view.iconImage = CreateIconLayer(rootRect, iconWidth);
            view.barTrackRect = CreateBarTrack(rootRect, iconWidth);

            view.bottomImage = CreateLayerImage(view.barTrackRect, "BottomLayer", ResBottom, stretchFull: true);
            view.fillImage = CreateLayerImage(view.barTrackRect, "FillLayer", ResFill, stretchFull: false);
            view.topImage = CreateLayerImage(view.barTrackRect, "TopLayer", ResTop, stretchFull: true);

            var fillRect = view.fillImage.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(0f, 0f);
        }

        private static Image CreateIconLayer(RectTransform parent, float iconWidth)
        {
            var go = new GameObject("IconLayer", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(iconWidth, 0f);

            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            var sp = Resources.Load<Sprite>(ResIcon);
            if (sp != null)
                img.sprite = sp;
            else
                UnityEngine.Debug.LogWarning("[StaminaBarView] 缺失精灵：" + ResIcon);
            return img;
        }

        private static RectTransform CreateBarTrack(RectTransform parent, float iconWidth)
        {
            var go = new GameObject("BarTrack", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.offsetMin = new Vector2(iconWidth, 0f);
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static Image CreateLayerImage(RectTransform parent, string name, string spritePath, bool stretchFull)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            if (stretchFull)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = Vector2.zero;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            var sp = Resources.Load<Sprite>(spritePath);
            if (sp != null)
                img.sprite = sp;
            else
                UnityEngine.Debug.LogWarning("[StaminaBarView] 缺失精灵：" + spritePath);
            img.preserveAspect = false;
            return img;
        }

        // 当预制体不带 SerializeField 引用时，按子节点名兜底寻找。
        public void EnsureFieldsFromHierarchy()
        {
            if (iconImage == null)
                iconImage = FindChildImage("IconLayer");
            if (barTrackRect == null)
            {
                var t = transform.Find("BarTrack");
                if (t != null)
                    barTrackRect = t as RectTransform;
            }

            if (barTrackRect != null)
            {
                if (bottomImage == null)
                    bottomImage = FindChildImage(barTrackRect, "BottomLayer");
                if (fillImage == null)
                    fillImage = FindChildImage(barTrackRect, "FillLayer");
                if (topImage == null)
                    topImage = FindChildImage(barTrackRect, "TopLayer");
            }
            else
            {
                // 旧版三层直挂根节点（向后兼容）
                if (bottomImage == null)
                    bottomImage = FindChildImage("BottomLayer");
                if (fillImage == null)
                    fillImage = FindChildImage("FillLayer");
                if (topImage == null)
                    topImage = FindChildImage("TopLayer");
            }

            if (iconImage != null)
                iconImage.transform.SetSiblingIndex(1);
            if (barTrackRect != null)
                barTrackRect.SetSiblingIndex(0);

            if (barTrackRect != null)
            {
                if (bottomImage != null) bottomImage.transform.SetSiblingIndex(0);
                if (fillImage != null) fillImage.transform.SetSiblingIndex(1);
                if (topImage != null) topImage.transform.SetSiblingIndex(2);
            }
            else
            {
                if (bottomImage != null) bottomImage.transform.SetSiblingIndex(0);
                if (fillImage != null) fillImage.transform.SetSiblingIndex(1);
                if (topImage != null) topImage.transform.SetSiblingIndex(2);
            }

            if (barTrackRect != null && barTrackRect.rect.width > 0f)
                baseWidth = barTrackRect.rect.width;
            else
            {
                var rootRect = transform as RectTransform;
                if (rootRect != null && rootRect.rect.width > 0f)
                    baseWidth = rootRect.rect.width;
            }

            if (fillImage != null)
                fillRect = fillImage.rectTransform;
        }

        private Image FindChildImage(string childName)
        {
            var t = transform.Find(childName);
            return t != null ? t.GetComponent<Image>() : null;
        }

        private static Image FindChildImage(RectTransform parent, string childName)
        {
            var t = parent.Find(childName);
            return t != null ? t.GetComponent<Image>() : null;
        }
    }
}
