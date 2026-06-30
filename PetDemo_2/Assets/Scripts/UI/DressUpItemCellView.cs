// SPEC §9.14.9（v3.160）：装扮商店道具单元独立预制体。
// 背景 ZhuangBan_sheetBJ2 + Icon/Condition/Price + SelectionOverlay(common_bg_2) 选中叠加。
using System;
using PetDemo.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class DressUpItemCellView : MonoBehaviour
    {
        [SerializeField] private Button rootButton;
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private GameObject conditionGroup;
        [SerializeField] private Image conditionIcon;
        [SerializeField] private Text conditionText;
        [SerializeField] private Image priceIcon;
        [SerializeField] private Text priceText;
        [SerializeField] private Image selectionOverlay;

        private static readonly Color ItemCellFallback = new Color(0.22f, 0.2f, 0.16f, 1f);
        private static readonly Color ItemIconFallback = new Color(0.5f, 0.55f, 0.62f, 1f);
        private static readonly Color IconFallback = new Color(0.9f, 0.78f, 0.3f, 1f);

        private DressUpItemConfig bound;
        private Action<DressUpItemConfig> onClick;
        private bool wired;

        public string ItemId => bound != null ? bound.itemId : null;

        /// <summary>预制体模板按子节点名自动绑定（仅一次注册点击）。</summary>
        public void AutoWire()
        {
            if (background == null)
                background = GetComponent<Image>();
            if (rootButton == null)
                rootButton = GetComponent<Button>();
            if (rootButton == null)
                rootButton = gameObject.AddComponent<Button>();
            if (icon == null)
                icon = FindImage("Icon");
            if (conditionGroup == null)
            {
                var t = FindDescendant(transform, "Condition");
                if (t != null)
                    conditionGroup = t.gameObject;
            }
            if (conditionIcon == null)
                conditionIcon = FindImage("ConditionIcon");
            if (conditionText == null)
                conditionText = FindText("ConditionText");
            if (priceIcon == null)
                priceIcon = FindImage("PriceIcon");
            if (priceText == null)
                priceText = FindText("PriceText");
            if (selectionOverlay == null)
                selectionOverlay = FindImage("SelectionOverlay");

            if (wired)
                return;
            wired = true;
            if (rootButton != null)
            {
                rootButton.transition = Selectable.Transition.None;
                if (rootButton.targetGraphic == null)
                    rootButton.targetGraphic = background;
                rootButton.onClick.AddListener(HandleClick);
            }
        }

        private void Awake()
        {
            AutoWire();
        }

        public void Bind(DressUpItemConfig config, Action<DressUpItemConfig> clickHandler)
        {
            AutoWire();
            bound = config;
            onClick = clickHandler;

            SetSpriteOrFallback(background, DressUpPanelLayout.ItemCellBackgroundResource, ItemCellFallback);
            SetSpriteOrFallback(icon, config != null ? config.icon : null, ItemIconFallback);

            if (conditionGroup != null)
                conditionGroup.SetActive(config != null && config.hasIntimacyRequire);
            if (config != null && config.hasIntimacyRequire)
            {
                SetSpriteOrFallback(conditionIcon, DressUpPanelLayout.IntimacyReqIconResource, IconFallback);
                if (conditionText != null)
                    conditionText.text = ">" + config.intimacyRequire;
            }

            SetSpriteOrFallback(priceIcon, DressUpPanelLayout.PriceIconResource, IconFallback);
            if (priceText != null)
                priceText.text = config != null ? config.price.ToString() : "";

            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (selectionOverlay != null)
                selectionOverlay.gameObject.SetActive(selected);
        }

        private void HandleClick()
        {
            if (bound != null)
                onClick?.Invoke(bound);
        }

        private static void SetSpriteOrFallback(Image img, string resource, Color fallback)
        {
            if (img == null)
                return;
            Sprite sprite = !string.IsNullOrEmpty(resource) ? Resources.Load<Sprite>(resource) : null;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = true;
            }
            else
            {
                img.sprite = null;
                img.color = fallback;
                if (!string.IsNullOrEmpty(resource))
                    UnityEngine.Debug.LogWarning("[DressUpItemCellView] 缺少素材 Resources/" + resource + "，回退纯色占位。");
            }
        }

        private Image FindImage(string nodeName)
        {
            var t = FindDescendant(transform, nodeName);
            return t != null ? t.GetComponent<Image>() : null;
        }

        private Text FindText(string nodeName)
        {
            var t = FindDescendant(transform, nodeName);
            return t != null ? t.GetComponent<Text>() : null;
        }

        private static Transform FindDescendant(Transform root, string nodeName)
        {
            if (root == null)
                return null;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == nodeName)
                    return child;
                var found = FindDescendant(child, nodeName);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
