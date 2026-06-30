// SPEC §9.8.8.7：选择关卡单个槽位（按钮 + 角色叠层）。
using System;
using PetDemo.Core;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class LevelSelectLevelSlotView : MonoBehaviour
    {
        private const float CharacterOverlayWidth = 120f;
        private const float CharacterOverlayHeight = 245f;

        private RectTransform slotRt;
        private RectTransform buttonRt;
        private Image buttonImage;
        private Button button;
        private Text levelLabel;
        private RectTransform overlayRt;
        private Image overlayImage;

        private MainStoryLevelConfig boundLevel;
        private Action<MainStoryLevelConfig> onClicked;

        public RectTransform SlotRect => slotRt;
        public float SlotHeight => buttonRt != null ? buttonRt.sizeDelta.y : 120f;
        public Vector2 SlotAnchoredPosition => slotRt != null ? slotRt.anchoredPosition : Vector2.zero;
        public MainStoryLevelConfig BoundLevel => boundLevel;

        public static LevelSelectLevelSlotView Create(
            RectTransform parent,
            int slotIndex,
            MainStoryLevelSlotLayout layout,
            Sprite lockedSprite,
            Sprite availableSprite,
            Sprite clearedSprite,
            Action<MainStoryLevelConfig> clickHandler)
        {
            var slotGo = new GameObject("LevelSlot_" + slotIndex, typeof(RectTransform));
            var slotRtLocal = slotGo.GetComponent<RectTransform>();
            slotRtLocal.SetParent(parent, false);
            slotRtLocal.anchorMin = new Vector2(0.5f, 0.5f);
            slotRtLocal.anchorMax = new Vector2(0.5f, 0.5f);
            slotRtLocal.pivot = new Vector2(0.5f, 0.5f);
            slotRtLocal.anchoredPosition = new Vector2(layout.posX, layout.posY);
            slotRtLocal.sizeDelta = new Vector2(layout.width, layout.height);

            var view = slotGo.AddComponent<LevelSelectLevelSlotView>();
            view.slotRt = slotRtLocal;
            view.onClicked = clickHandler;
            view.BuildChildren(layout, lockedSprite, availableSprite, clearedSprite);
            slotGo.SetActive(false);
            return view;
        }

        private void BuildChildren(
            MainStoryLevelSlotLayout layout,
            Sprite lockedSprite,
            Sprite availableSprite,
            Sprite clearedSprite)
        {
            var btnGo = new GameObject("LevelButton", typeof(RectTransform));
            buttonRt = btnGo.GetComponent<RectTransform>();
            buttonRt.SetParent(slotRt, false);
            buttonRt.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRt.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRt.pivot = new Vector2(0.5f, 0.5f);
            buttonRt.anchoredPosition = Vector2.zero;
            buttonRt.sizeDelta = new Vector2(layout.width, layout.height);

            buttonImage = btnGo.AddComponent<Image>();
            buttonImage.preserveAspect = true;
            buttonImage.raycastTarget = true;
            if (lockedSprite != null)
                buttonImage.sprite = lockedSprite;

            button = btnGo.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(OnButtonClicked);

            var labelRt = BottomNavAttachedScreenLayout.CreateChildRect(
                buttonRt, "LevelLabel",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(labelRt);
            levelLabel = labelRt.gameObject.AddComponent<Text>();
            levelLabel.font = FarmGridView.LoadBuiltinFont();
            levelLabel.fontSize = 28;
            levelLabel.alignment = TextAnchor.MiddleCenter;
            levelLabel.color = Color.white;
            levelLabel.raycastTarget = false;

            var overlayGo = new GameObject("CharacterOverlay", typeof(RectTransform));
            overlayRt = overlayGo.GetComponent<RectTransform>();
            overlayRt.SetParent(slotRt, false);
            overlayRt.anchorMin = new Vector2(0.5f, 0.5f);
            overlayRt.anchorMax = new Vector2(0.5f, 0.5f);
            overlayRt.pivot = new Vector2(0.5f, 0f);
            overlayRt.anchoredPosition = new Vector2(0f, layout.height * 0.5f);
            overlayRt.sizeDelta = new Vector2(CharacterOverlayWidth, CharacterOverlayHeight);

            overlayImage = overlayGo.AddComponent<Image>();
            overlayImage.preserveAspect = true;
            overlayImage.raycastTarget = false;
            overlayGo.SetActive(false);

            // 缓存三态 sprite 引用在 Image 上通过 Refresh 切换；available/cleared 在 Refresh 传入。
            _lockedSprite = lockedSprite;
            _availableSprite = availableSprite;
            _clearedSprite = clearedSprite;
        }

        private Sprite _lockedSprite;
        private Sprite _availableSprite;
        private Sprite _clearedSprite;

        public void ApplyLayout(MainStoryLevelSlotLayout layout)
        {
            if (slotRt == null || layout == null)
                return;
            slotRt.anchoredPosition = new Vector2(layout.posX, layout.posY);
            slotRt.sizeDelta = new Vector2(layout.width, layout.height);
            if (buttonRt != null)
                buttonRt.sizeDelta = new Vector2(layout.width, layout.height);
            if (overlayRt != null)
            {
                overlayRt.anchoredPosition = new Vector2(0f, layout.height * 0.5f);
                overlayRt.sizeDelta = new Vector2(CharacterOverlayWidth, CharacterOverlayHeight);
            }
        }

        public void Refresh(
            MainStoryLevelConfig levelConfig,
            MainStoryLevelState state,
            Sprite overlaySprite,
            bool overlayVisible)
        {
            boundLevel = levelConfig;
            bool visible = levelConfig != null;
            gameObject.SetActive(visible);
            if (!visible)
                return;

            levelLabel.text = levelConfig.levelNumber.ToString();

            Sprite btnSprite = _lockedSprite;
            if (state == MainStoryLevelState.Available && _availableSprite != null)
                btnSprite = _availableSprite;
            else if (state == MainStoryLevelState.Cleared && _clearedSprite != null)
                btnSprite = _clearedSprite;
            if (btnSprite != null)
                buttonImage.sprite = btnSprite;

            if (overlayVisible && overlaySprite != null)
            {
                overlayImage.sprite = overlaySprite;
                overlayImage.enabled = true;
                overlayRt.gameObject.SetActive(true);
            }
            else
            {
                overlayRt.gameObject.SetActive(false);
            }
        }

        private void OnButtonClicked()
        {
            if (boundLevel != null && onClicked != null)
                onClicked(boundLevel);
        }
    }
}
