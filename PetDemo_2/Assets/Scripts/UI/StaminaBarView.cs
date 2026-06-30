// SPEC §9.8.12.4 (v3.40)：体力条 StaminaBarView。
// 三层结构：BottomLayer(TiLi_1) + FillLayer(TiLi_2, sizeDelta.x 左对齐缩放) + TopLayer(TiLi_3)。
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
        private const string ResBottom = "AirUI/TiLi_1";
        private const string ResFill = "AirUI/TiLi_2";
        private const string ResTop = "AirUI/TiLi_3";
        private const string ResPrefab = "Prefabs/Farm/StaminaBar";
        public const float DefaultWidth = 800f;
        public const float DefaultHeight = 60f;

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
            if (fillRect != null && baseWidth <= 0f)
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

        // 代码搭建路径：当预制体缺失时使用。
        // SPEC §9.8.13.4 (v3.41)：当 parent.rect.size 有效时，根 sizeDelta 跟随 parent（适配 275×116 槽），
        // parent 尺寸无效时回退到 DefaultWidth/DefaultHeight，保证 §9.6 等其它代码路径不受影响。
        private static GameObject BuildRuntime(RectTransform parent)
        {
            var root = new GameObject("StaminaBar", typeof(RectTransform));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(parent, false);
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            float w = (parent != null && parent.rect.width > 0f) ? parent.rect.width : DefaultWidth;
            float h = (parent != null && parent.rect.height > 0f) ? parent.rect.height : DefaultHeight;
            rootRect.sizeDelta = new Vector2(w, h);
            rootRect.anchoredPosition = Vector2.zero;

            var view = root.AddComponent<StaminaBarView>();

            // 严格三层：bottom (sibling=0) → fill (sibling=1) → top (sibling=2)
            view.bottomImage = CreateLayerImage(rootRect, "BottomLayer", ResBottom, stretchFull: true);
            view.fillImage = CreateLayerImage(rootRect, "FillLayer", ResFill, stretchFull: false);
            view.topImage = CreateLayerImage(rootRect, "TopLayer", ResTop, stretchFull: true);

            // FillLayer 左对齐拉伸：anchorMin/Max=(0,0)/(0,1)、pivot=(0,0.5)。
            var fillRect = view.fillImage.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(0f, 0f); // Refresh 时改写 sizeDelta.x
            return root;
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
        private void EnsureFieldsFromHierarchy()
        {
            if (bottomImage == null)
                bottomImage = FindChildImage("BottomLayer");
            if (fillImage == null)
                fillImage = FindChildImage("FillLayer");
            if (topImage == null)
                topImage = FindChildImage("TopLayer");

            // 强制保证层级：bottom=0, fill=1, top=2
            if (bottomImage != null) bottomImage.transform.SetSiblingIndex(0);
            if (fillImage != null) fillImage.transform.SetSiblingIndex(1);
            if (topImage != null) topImage.transform.SetSiblingIndex(2);

            // 记录基础宽度
            var rootRect = transform as RectTransform;
            if (rootRect != null && rootRect.rect.width > 0f)
                baseWidth = rootRect.rect.width;
            if (fillImage != null)
                fillRect = fillImage.rectTransform;
        }

        private Image FindChildImage(string childName)
        {
            var t = transform.Find(childName);
            return t != null ? t.GetComponent<Image>() : null;
        }
    }
}
