// SPEC §9.8.9.11：公会场景地图响应区域 — 人工摆放；主角进入半径时显示 NamePlate，
// 区域内静止 2s 后触发 Entered 回调（v3.197）；跳转逻辑由 GongHuiScreenView 装配。
using System;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class GuildResponseAreaMarker : MonoBehaviour
    {
        public const float NavigateDelaySeconds = 2f;
        public const string NavigateCountdownText = "正在前往....";

        private static readonly Color IconFallbackColor = new Color(0.35f, 0.55f, 0.72f, 1f);

        [SerializeField] private string areaId = "response_area_1";
        [SerializeField] private string displayName = "响应区域";
        [SerializeField] private float interactRadius = 220f;
        [SerializeField] private float plateOffsetY = 140f;
        [SerializeField] private string navTargetKey = "";
        [SerializeField] private bool triggerOncePerVisit;
        [SerializeField] private Sprite iconOverride;

        private RectTransform plateRt;
        private Text nameText;
        private bool consumedThisVisit;
        private bool navigateCountdownActive;
        private int savedNameFontSize = -1;

        public event Action<GuildResponseAreaMarker> Entered;

        public RectTransform Rt => (RectTransform)transform;
        public float InteractRadius => interactRadius;
        public string AreaId => areaId;
        public string DisplayName => displayName;
        public string NavTargetKey => navTargetKey;

        private void Awake()
        {
            TryAcquirePlateFromHierarchy();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(areaId))
                areaId = name;
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = name;
        }
#endif

        public void SetDisplayName(string value)
        {
            displayName = value;
            if (!navigateCountdownActive && nameText != null)
                nameText.text = displayName;
        }

        public void SetAreaId(string value)
        {
            areaId = value;
        }

        public void SetNavTargetKey(string value)
        {
            navTargetKey = value;
        }

        public void SetPlateVisible(bool visible, bool panoramaOverride = false)
        {
            if (visible && plateRt == null)
                TryAcquirePlateFromHierarchy();
            if (visible && plateRt == null)
                plateRt = BuildPlate();
            if (!visible)
                SetNavigateCountdownActive(false);
            if (plateRt != null && plateRt.gameObject.activeSelf != visible)
                plateRt.gameObject.SetActive(visible);
        }

        /// <summary>SPEC §9.8.9.11（v3.197）：静止倒计时期间将 NameText 替换为「正在前往....」。</summary>
        public void SetNavigateCountdownActive(bool active)
        {
            if (navigateCountdownActive == active)
                return;

            navigateCountdownActive = active;
            if (nameText == null && plateRt != null)
                nameText = plateRt.Find("NameText")?.GetComponent<Text>();
            if (nameText == null)
                return;

            nameText.text = active ? NavigateCountdownText : displayName;
        }

        public void ApplyPlateScaleCompensation(float compensation)
        {
            if (plateRt == null)
                return;
            plateRt.localScale = Vector3.one * compensation;
        }

        /// <summary>SPEC §9.8.9.12：全景模式覆盖 NameText 字号（缓存原值，退出时还原）。</summary>
        public void SetPanoramaNameFontSize(int fontSize)
        {
            EnsureNameText();
            if (nameText == null)
                return;
            if (savedNameFontSize < 0)
                savedNameFontSize = nameText.fontSize;
            nameText.fontSize = fontSize;
        }

        public void ResetPlateScale()
        {
            if (plateRt == null)
                return;
            plateRt.localScale = Vector3.one;
            SetNavigateCountdownActive(false);
            if (savedNameFontSize >= 0)
            {
                EnsureNameText();
                if (nameText != null)
                    nameText.fontSize = savedNameFontSize;
                savedNameFontSize = -1;
            }
        }

        public void ResetVisitState()
        {
            consumedThisVisit = false;
            SetNavigateCountdownActive(false);
        }

        public bool TryConsumeEnter()
        {
            if (triggerOncePerVisit && consumedThisVisit)
                return false;
            if (triggerOncePerVisit)
                consumedThisVisit = true;
            return true;
        }

        public void NotifyEntered()
        {
            Entered?.Invoke(this);
        }

        private void TryAcquirePlateFromHierarchy()
        {
            if (plateRt != null)
                return;
            var existing = Rt.Find("NamePlate") as RectTransform;
            if (existing == null)
                return;
            plateRt = existing;
            EnsureNameText();
        }

        private void EnsureNameText()
        {
            if (nameText != null)
                return;
            if (plateRt == null)
                return;
            nameText = plateRt.Find("NameText")?.GetComponent<Text>();
        }

        private RectTransform BuildPlate()
        {
            plateRt = GuildSceneUiFactory.BuildResponseAreaNamePlate(
                Rt, displayName, iconOverride, IconFallbackColor, plateOffsetY);
            EnsureNameText();
            return plateRt;
        }
    }
}
