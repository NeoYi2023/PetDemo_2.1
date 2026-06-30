// SPEC §9.14.10（v3.141）：进入家园页签跳转长框行（图标/名称 + 右侧「前往」按钮）。
using System;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class EnterHomeNavCellView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image iconImage;
        [SerializeField] private Text nameText;
        [SerializeField] private Button navigateButton;

        private static readonly Color IconFallback = new Color(0.3f, 0.36f, 0.46f, 1f);

        private string boundNavKey;
        private Action<string> onNavigate;
        private bool wired;

        /// <summary>预制体模板仅含 UI 组件；运行时按子节点名自动绑定并挂载按钮事件（仅一次）。</summary>
        public void AutoWire()
        {
            if (background == null)
                background = GetComponent<Image>();
            if (iconImage == null)
                iconImage = FindImage("Icon");
            if (nameText == null)
                nameText = FindText("NameText");
            if (navigateButton == null)
                navigateButton = FindButton("NavigateButton");

            if (wired)
                return;
            wired = true;
            if (navigateButton != null)
                navigateButton.onClick.AddListener(HandleNavigate);
        }

        private void Awake()
        {
            AutoWire();
        }

        public void Bind(string navKey, string displayName, string iconResource, Action<string> navigateHandler)
        {
            AutoWire();
            boundNavKey = navKey;
            onNavigate = navigateHandler;

            if (nameText != null)
                nameText.text = displayName ?? "";

            if (iconImage != null)
            {
                Sprite sprite = null;
                if (!string.IsNullOrEmpty(iconResource))
                    sprite = Resources.Load<Sprite>(iconResource);
                if (sprite != null)
                {
                    iconImage.sprite = sprite;
                    iconImage.color = Color.white;
                    iconImage.preserveAspect = true;
                }
                else
                {
                    iconImage.sprite = null;
                    iconImage.color = IconFallback;
                }
            }
        }

        private void HandleNavigate()
        {
            if (!string.IsNullOrEmpty(boundNavKey))
                onNavigate?.Invoke(boundNavKey);
        }

        private Image FindImage(string nodeName)
        {
            var t = transform.Find(nodeName);
            return t != null ? t.GetComponent<Image>() : null;
        }

        private Text FindText(string nodeName)
        {
            var t = transform.Find(nodeName);
            return t != null ? t.GetComponent<Text>() : null;
        }

        private Button FindButton(string nodeName)
        {
            var t = transform.Find(nodeName);
            return t != null ? t.GetComponent<Button>() : null;
        }
    }
}
