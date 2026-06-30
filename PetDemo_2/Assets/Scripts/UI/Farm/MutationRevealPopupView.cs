// MutationRevealPopupView：变异植物收获后的双分支弹窗（SPEC §4.1.10.5 v3.18）。
// Pet 分支：通过 PetPreviewRig 渲染 Spine SkeletonAnimation 到 RawImage，并显示 displayName / traitDescription。
// Skill 分支：通过 Resources.Load<Sprite>(iconResource) 显示图标，并显示 displayName / description。
// 弹窗由 OnMutationHarvested 驱动；点击遮罩或关闭按钮关闭，关闭时销毁 Pet 实例并停止 Camera 渲染。
using PetDemo.Core;
using PetDemo.Farm;
using PetDemo.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PetDemo.UI.Farm
{
    public class MutationRevealPopupView : MonoBehaviour
    {
        public const int SortingOrder = MainUiSortTier.HudPopup;
        private const string ModalBackgroundResPath = "AirUI/FeiLiaoUI_0";
        private const float PanelWidth = 880f;
        private const float PanelHeight = 600f;
        private const float PreviewSize = 360f;

        public static MutationRevealPopupView Instance { get; private set; }

        private IPlantingService service;
        private Canvas canvas;
        private RectTransform dimRect;
        private RectTransform panelRect;
        private RawImage petPreviewImage;
        private Image skillIconImage;
        private Text titleText;
        private Text descText;

        public static MutationRevealPopupView CreateAndAttach(IPlantingService svc, RectTransform hudRoot = null)
        {
            if (svc == null)
                return null;
            if (Instance != null)
                return Instance;
            var go = new GameObject("MutationRevealPopup");
            if (hudRoot != null)
            {
                go.transform.SetParent(hudRoot, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
            }

            var view = go.AddComponent<MutationRevealPopupView>();
            view.service = svc;
            view.BuildHierarchy(hudRoot != null);
            view.SubscribeEvents();
            view.SetVisible(false);
            Instance = view;
            return view;
        }

        private void BuildHierarchy(bool underMainHud = false)
        {
            if (underMainHud)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = SortingOrder;
                MainHudLayerRoot.EnsureGraphicRaycaster(gameObject);
            }
            else
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = SortingOrder;
                gameObject.AddComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode =
                    UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                var scaler = GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.matchWidthOrHeight = 0.5f;
                gameObject.AddComponent<GraphicRaycaster>();
            }

            // 半透明遮罩 + 点击关闭
            var dimGo = new GameObject("Dim");
            dimRect = dimGo.AddComponent<RectTransform>();
            dimRect.SetParent(transform, false);
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImage = dimGo.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.55f);
            dimImage.raycastTarget = true;
            var dimClick = dimGo.AddComponent<DimClickClose>();
            dimClick.Bind(this);

            // 主面板
            var panelGo = new GameObject("Panel");
            panelRect = panelGo.AddComponent<RectTransform>();
            panelRect.SetParent(transform, false);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panelRect.anchoredPosition = Vector2.zero;
            var panelImage = panelGo.AddComponent<Image>();
            panelImage.sprite = Resources.Load<Sprite>(ModalBackgroundResPath);
            panelImage.preserveAspect = false;
            if (panelImage.sprite == null)
                panelImage.color = new Color(0.18f, 0.14f, 0.10f, 0.95f);
            // 阻止穿透 Dim 的点击
            panelImage.raycastTarget = true;

            // 左侧：Pet 预览（RawImage）+ Skill 图标（Image）共用一个容器；显示时只激活其中一个
            var leftGo = new GameObject("LeftPreview");
            var leftRect = leftGo.AddComponent<RectTransform>();
            leftRect.SetParent(panelRect, false);
            leftRect.anchorMin = new Vector2(0f, 0.5f);
            leftRect.anchorMax = new Vector2(0f, 0.5f);
            leftRect.pivot = new Vector2(0.5f, 0.5f);
            leftRect.sizeDelta = new Vector2(PreviewSize, PreviewSize);
            leftRect.anchoredPosition = new Vector2(60f + PreviewSize * 0.5f, 0f);

            var petGo = new GameObject("PetPreview");
            var petRect = petGo.AddComponent<RectTransform>();
            petRect.SetParent(leftRect, false);
            petRect.anchorMin = Vector2.zero;
            petRect.anchorMax = Vector2.one;
            petRect.offsetMin = Vector2.zero;
            petRect.offsetMax = Vector2.zero;
            petPreviewImage = petGo.AddComponent<RawImage>();
            petPreviewImage.color = Color.white;
            petPreviewImage.raycastTarget = false;

            var skillGo = new GameObject("SkillIcon");
            var skillRect = skillGo.AddComponent<RectTransform>();
            skillRect.SetParent(leftRect, false);
            skillRect.anchorMin = Vector2.zero;
            skillRect.anchorMax = Vector2.one;
            skillRect.offsetMin = Vector2.zero;
            skillRect.offsetMax = Vector2.zero;
            skillRect.localScale = new Vector3(0.5f, 0.5f, 1f);
            skillIconImage = skillGo.AddComponent<Image>();
            skillIconImage.preserveAspect = true;
            skillIconImage.raycastTarget = false;

            // 右侧：标题 + 描述
            titleText = AddText(panelRect, "Title", new Vector2(420f, 100f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(120f, -120f), 48, FontStyle.Bold);
            descText = AddText(panelRect, "Desc", new Vector2(420f, 280f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(120f, -240f), 32, FontStyle.Normal);

            // 关闭按钮
            var closeGo = new GameObject("CloseButton");
            var closeRect = closeGo.AddComponent<RectTransform>();
            closeRect.SetParent(panelRect, false);
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.sizeDelta = new Vector2(80f, 80f);
            closeRect.anchoredPosition = new Vector2(-20f, -20f);
            var closeImage = closeGo.AddComponent<Image>();
            closeImage.color = new Color(0.85f, 0.20f, 0.20f, 1f);
            var closeBtn = closeGo.AddComponent<Button>();
            closeBtn.onClick.AddListener(Close);
            var closeText = AddText(closeRect, "X", new Vector2(80f, 80f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, 36, FontStyle.Bold);
            closeText.text = "X";
            closeText.color = Color.white;
        }

        private static Text AddText(
            RectTransform parent, string name, Vector2 size,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, int fontSize, FontStyle style)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            var t = go.AddComponent<Text>();
            t.font = FarmGridView.LoadBuiltinFont();
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = new Color(1f, 1f, 1f, 0.95f);
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        private void SubscribeEvents()
        {
            if (service == null)
                return;
            service.OnMutationHarvested += HandleMutationHarvested;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (service == null)
                return;
            service.OnMutationHarvested -= HandleMutationHarvested;
        }

        private void HandleMutationHarvested(string mutationId, MutationKind kind, string refId)
        {
            if (kind == MutationKind.Pet)
                ShowPet(refId);
            else
                ShowSkill(refId);
        }

        private void ShowPet(string petId)
        {
            var cfg = service?.GetPetConfig(petId);
            if (cfg == null)
            {
                UnityEngine.Debug.LogWarning("[MutationRevealPopupView] PetConfig 缺失：" + petId);
                ShowFallbackText("未知精灵", "（配置缺失）");
                return;
            }

            if (petPreviewImage != null) petPreviewImage.gameObject.SetActive(true);
            if (skillIconImage != null) skillIconImage.gameObject.SetActive(false);

            var rig = PetPreviewRig.EnsureCreated();
            var rt = rig.ShowPet(cfg);
            if (petPreviewImage != null)
            {
                petPreviewImage.texture = rt;
                petPreviewImage.color = (rt != null) ? Color.white : new Color(1f, 1f, 1f, 0f);
            }

            if (titleText != null) titleText.text = cfg.displayName;
            if (descText != null) descText.text = cfg.traitDescription;

            SetVisible(true);
        }

        private void ShowSkill(string skillId)
        {
            var cfg = service?.GetSkillConfig(skillId);
            if (cfg == null)
            {
                UnityEngine.Debug.LogWarning("[MutationRevealPopupView] SkillConfig 缺失：" + skillId);
                ShowFallbackText("未知技能", "（配置缺失）");
                return;
            }

            if (petPreviewImage != null) petPreviewImage.gameObject.SetActive(false);
            if (skillIconImage != null)
            {
                skillIconImage.gameObject.SetActive(true);
                var sprite = Resources.Load<Sprite>(cfg.iconResource);
                skillIconImage.sprite = sprite;
                skillIconImage.color = (sprite != null) ? Color.white : new Color(1f, 0.8f, 0.3f, 1f);
            }

            if (titleText != null) titleText.text = cfg.displayName;
            if (descText != null) descText.text = cfg.description;

            SetVisible(true);
        }

        private void ShowFallbackText(string title, string desc)
        {
            if (petPreviewImage != null) petPreviewImage.gameObject.SetActive(false);
            if (skillIconImage != null) skillIconImage.gameObject.SetActive(false);
            if (titleText != null) titleText.text = title;
            if (descText != null) descText.text = desc;
            SetVisible(true);
        }

        public void Close()
        {
            SetVisible(false);
            var rig = PetPreviewRig.Instance;
            if (rig != null)
                rig.Hide();
        }

        private void SetVisible(bool visible)
        {
            if (canvas != null)
                canvas.enabled = visible;
            if (dimRect != null)
                dimRect.gameObject.SetActive(visible);
            if (panelRect != null)
                panelRect.gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// Dim 层点击关闭：转发到 MutationRevealPopupView.Close()。
    /// </summary>
    public class DimClickClose : MonoBehaviour, IPointerClickHandler
    {
        private MutationRevealPopupView owner;

        public void Bind(MutationRevealPopupView popup)
        {
            owner = popup;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (owner != null)
                owner.Close();
        }
    }
}
