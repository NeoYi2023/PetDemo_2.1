// SPEC §9.10.5：精灵背包 — 仓库格子单元。
using System;
using PetDemo.Core;
using PetDemo.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Farm
{
    [DisallowMultipleComponent]
    public sealed class RoleGrowthPetBagCellView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image frameImage;
        [SerializeField] private Text nameText;
        [SerializeField] private Text deployedBadgeText;

        private static readonly Color NormalFrame = new Color(0.22f, 0.26f, 0.34f, 0.92f);
        private static readonly Color DeployedFrame = new Color(0.35f, 0.35f, 0.38f, 0.75f);

        private PetInstance boundInstance;
        private Action<PetInstance> clickHandler;

        /// <summary>
        /// 预制体模板仅含 UI 组件；运行时 AddComponent 后自动绑定子节点。
        /// </summary>
        public void AutoWire()
        {
            if (button == null)
                button = GetComponent<Button>();
            if (frameImage == null)
                frameImage = GetComponent<Image>();
            if (nameText == null)
            {
                var nameTr = transform.Find("NameText");
                if (nameTr != null)
                    nameText = nameTr.GetComponent<Text>();
            }

            if (deployedBadgeText == null)
            {
                var badgeTr = transform.Find("DeployedBadge");
                if (badgeTr != null)
                    deployedBadgeText = badgeTr.GetComponent<Text>();
            }
        }

        private void Awake()
        {
            AutoWire();
            if (button != null)
                button.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClick);
        }

        public void Bind(PetInstance inst, PetConfig cfg, bool deployed, Action<PetInstance> onClick)
        {
            boundInstance = inst;
            clickHandler = onClick;

            if (nameText != null)
                nameText.text = cfg != null ? cfg.displayName : inst?.petConfigId ?? "";

            if (deployedBadgeText != null)
                deployedBadgeText.gameObject.SetActive(deployed);

            if (frameImage != null)
                frameImage.color = deployed ? DeployedFrame : NormalFrame;

            if (button != null)
                button.interactable = !deployed;
        }

        private void HandleClick()
        {
            if (boundInstance != null)
                clickHandler?.Invoke(boundInstance);
        }
    }
}
