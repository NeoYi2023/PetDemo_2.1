// SPEC §9.10.5：精灵背包 — 上场槽（空槽「+」闪烁 / 有精灵预览）。
using System;
using System.Collections;
using PetDemo.Core;
using PetDemo.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Farm
{
    [DisallowMultipleComponent]
    public sealed class RoleGrowthPetBagDeployedSlotView : MonoBehaviour
    {
        private const float BlinkSpeed = 2.5f;
        private const float BlinkMinAlpha = 0.35f;
        private const float BlinkMaxAlpha = 1f;

        [SerializeField] private PetFieldSlot fieldSlot;
        [SerializeField] private Button slotButton;
        [SerializeField] private GameObject emptyState;
        [SerializeField] private Text emptyPlusText;
        [SerializeField] private GameObject occupiedState;
        [SerializeField] private RectTransform previewRoot;
        [SerializeField] private Text nameText;
        [SerializeField] private RectTransform undeployAnchor;

        private IPlantingService service;
        private Coroutine blinkRoutine;
        private bool wantsBlink;
        private GameObject previewGraphicGo;

        public PetFieldSlot FieldSlot => fieldSlot;
        public RectTransform UndeployAnchor => undeployAnchor;
        public event Action<RoleGrowthPetBagDeployedSlotView> OnSlotClicked;

        public void Bind(IPlantingService plantingService)
        {
            service = plantingService;
            if (slotButton != null)
            {
                slotButton.onClick.RemoveListener(HandleClick);
                slotButton.onClick.AddListener(HandleClick);
            }
        }

        private void OnEnable()
        {
            if (wantsBlink)
                TryRunBlinkRoutine();
        }

        private void OnDisable()
        {
            if (blinkRoutine != null)
            {
                StopCoroutine(blinkRoutine);
                blinkRoutine = null;
            }
        }

        private void OnDestroy()
        {
            if (slotButton != null)
                slotButton.onClick.RemoveListener(HandleClick);
            StopBlink();
        }

        public void Refresh(bool isSelected)
        {
            if (service == null)
                return;

            string instanceId = GetDeployedInstanceId();
            bool occupied = !string.IsNullOrEmpty(instanceId);

            if (emptyState != null)
                emptyState.SetActive(!occupied);
            if (occupiedState != null)
                occupiedState.SetActive(occupied);

            if (occupied)
            {
                StopBlink();
                var inst = service.GetPetInstance(instanceId);
                var cfg = inst != null ? service.GetPetConfig(inst.petConfigId) : null;
                if (nameText != null)
                    nameText.text = cfg != null ? cfg.displayName : inst?.petConfigId ?? "";
                RebuildPreview(cfg);
            }
            else
            {
                ClearPreview();
                if (nameText != null)
                    nameText.text = string.Empty;
                StartBlink();
            }
        }

        public bool HasDeployedPet() => !string.IsNullOrEmpty(GetDeployedInstanceId());

        private string GetDeployedInstanceId()
        {
            var dep = service?.GetPetDeployment();
            if (dep == null)
                return null;
            return fieldSlot == PetFieldSlot.LowerLeft
                ? dep.lowerLeftInstanceId
                : dep.upperLeftInstanceId;
        }

        private void RebuildPreview(PetConfig cfg)
        {
            ClearPreview();
            if (previewRoot == null || cfg == null)
                return;

            var sg = PetBagSpineGraphicBuilder.TryBuild(
                previewRoot, cfg, new Vector2(200f, 280f), new Vector3(0.35f, 0.35f, 1f));
            if (sg != null)
                previewGraphicGo = sg.gameObject;
        }

        private void ClearPreview()
        {
            if (previewGraphicGo != null)
            {
                Destroy(previewGraphicGo);
                previewGraphicGo = null;
            }

            if (previewRoot == null)
                return;
            for (int i = previewRoot.childCount - 1; i >= 0; i--)
                Destroy(previewRoot.GetChild(i).gameObject);
        }

        private void HandleClick()
        {
            if (!HasDeployedPet())
                return;
            OnSlotClicked?.Invoke(this);
        }

        private void StartBlink()
        {
            if (emptyPlusText == null)
                return;

            wantsBlink = true;
            if (blinkRoutine != null)
                return;

            TryRunBlinkRoutine();
        }

        private void TryRunBlinkRoutine()
        {
            if (!wantsBlink || emptyPlusText == null || blinkRoutine != null)
                return;
            if (!isActiveAndEnabled)
                return;

            blinkRoutine = StartCoroutine(BlinkPlusRoutine());
        }

        private void StopBlink()
        {
            wantsBlink = false;
            if (blinkRoutine != null)
            {
                StopCoroutine(blinkRoutine);
                blinkRoutine = null;
            }

            if (emptyPlusText != null)
            {
                var c = emptyPlusText.color;
                c.a = 1f;
                emptyPlusText.color = c;
            }
        }

        private IEnumerator BlinkPlusRoutine()
        {
            while (true)
            {
                float wave = (Mathf.Sin(Time.unscaledTime * BlinkSpeed) + 1f) * 0.5f;
                float alpha = Mathf.Lerp(BlinkMinAlpha, BlinkMaxAlpha, wave);
                if (emptyPlusText != null)
                {
                    var c = emptyPlusText.color;
                    c.a = alpha;
                    emptyPlusText.color = c;
                }
                yield return null;
            }
        }
    }
}
