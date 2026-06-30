// SPEC §9.10.5：角色成长 — 精灵背包页（独立预制体根）。
using PetDemo.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Farm
{
    [DisallowMultipleComponent]
    public sealed class RoleGrowthPetBagPageView : MonoBehaviour
    {
        public const string ResPrefabPath = "Prefabs/Farm/RoleGrowthPetBagPage";

        [SerializeField] private RoleGrowthPetBagDeployedSlotView lowerSlot;
        [SerializeField] private RoleGrowthPetBagDeployedSlotView upperSlot;
        [SerializeField] private Button undeployButton;
        [SerializeField] private RoleGrowthPetBagWarehouseView warehouse;

        private IPlantingService service;
        private RoleGrowthPetBagDeployedSlotView selectedSlot;
        private bool subscribed;

        public void Initialize(IPlantingService plantingService)
        {
            service = plantingService;
            lowerSlot?.Bind(service);
            upperSlot?.Bind(service);
            warehouse?.Bind(service);

            if (lowerSlot != null)
            {
                lowerSlot.OnSlotClicked -= OnDeployedSlotClicked;
                lowerSlot.OnSlotClicked += OnDeployedSlotClicked;
            }

            if (upperSlot != null)
            {
                upperSlot.OnSlotClicked -= OnDeployedSlotClicked;
                upperSlot.OnSlotClicked += OnDeployedSlotClicked;
            }

            if (undeployButton != null)
            {
                undeployButton.onClick.RemoveListener(OnUndeployClicked);
                undeployButton.onClick.AddListener(OnUndeployClicked);
                undeployButton.gameObject.SetActive(false);
            }

            SubscribeEvents();
            RefreshAll();
        }

        private void OnDestroy()
        {
            if (lowerSlot != null)
                lowerSlot.OnSlotClicked -= OnDeployedSlotClicked;
            if (upperSlot != null)
                upperSlot.OnSlotClicked -= OnDeployedSlotClicked;
            if (undeployButton != null)
                undeployButton.onClick.RemoveListener(OnUndeployClicked);
            UnsubscribeEvents();
        }

        private void OnEnable()
        {
            if (service != null)
                RefreshAll();
        }

        private void SubscribeEvents()
        {
            if (subscribed || service == null)
                return;
            service.OnPetBagChanged += RefreshAll;
            service.OnPetDeploymentChanged += RefreshAll;
            subscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!subscribed || service == null)
                return;
            service.OnPetBagChanged -= RefreshAll;
            service.OnPetDeploymentChanged -= RefreshAll;
            subscribed = false;
        }

        private void RefreshAll()
        {
            if (service == null)
                return;

            if (selectedSlot != null && !selectedSlot.HasDeployedPet())
                selectedSlot = null;

            lowerSlot?.Refresh(selectedSlot == lowerSlot);
            upperSlot?.Refresh(selectedSlot == upperSlot);
            warehouse?.Refresh();
            UpdateUndeployButton();
        }

        private void OnDeployedSlotClicked(RoleGrowthPetBagDeployedSlotView slot)
        {
            selectedSlot = slot;
            RefreshAll();
        }

        private void UpdateUndeployButton()
        {
            if (undeployButton == null)
                return;

            bool show = selectedSlot != null && selectedSlot.HasDeployedPet();
            undeployButton.gameObject.SetActive(show);
            if (!show)
                return;

            var anchor = selectedSlot.UndeployAnchor;
            if (anchor != null)
            {
                var btnRt = undeployButton.transform as RectTransform;
                if (btnRt != null)
                {
                    btnRt.SetParent(anchor, false);
                    btnRt.anchorMin = new Vector2(0.5f, 0.5f);
                    btnRt.anchorMax = new Vector2(0.5f, 0.5f);
                    btnRt.pivot = new Vector2(0.5f, 0.5f);
                    btnRt.anchoredPosition = Vector2.zero;
                    btnRt.localScale = Vector3.one;
                }
            }
        }

        private void OnUndeployClicked()
        {
            if (service == null || selectedSlot == null)
                return;

            service.UndeployPet(selectedSlot.FieldSlot);
            selectedSlot = null;
            RefreshAll();
        }
    }
}
