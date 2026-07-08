// SPEC §9.8.9.11：公会场景地图响应区域 — 人工摆放；主角进入半径时显示 NamePlate，
// 沿边进入（outside→inside）自动触发 Entered 回调；跳转逻辑由 GongHuiScreenView 装配。
using System;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class GuildResponseAreaMarker : MonoBehaviour
    {
        private static readonly Color IconFallbackColor = new Color(0.35f, 0.55f, 0.72f, 1f);

        [SerializeField] private string areaId = "response_area_1";
        [SerializeField] private string displayName = "响应区域";
        [SerializeField] private float interactRadius = 220f;
        [SerializeField] private float plateOffsetY = 140f;
        [SerializeField] private string navTargetKey = "";
        [SerializeField] private bool triggerOncePerVisit;
        [SerializeField] private Sprite iconOverride;

        private RectTransform plateRt;
        private bool consumedThisVisit;
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
            if (plateRt != null && plateRt.gameObject.activeSelf != visible)
                plateRt.gameObject.SetActive(visible);
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
            var text = plateRt == null ? null : plateRt.Find("NameText")?.GetComponent<Text>();
            if (text == null)
                return;
            if (savedNameFontSize < 0)
                savedNameFontSize = text.fontSize;
            text.fontSize = fontSize;
        }

        public void ResetPlateScale()
        {
            if (plateRt == null)
                return;
            plateRt.localScale = Vector3.one;
            if (savedNameFontSize >= 0)
            {
                var text = plateRt.Find("NameText")?.GetComponent<Text>();
                if (text != null)
                    text.fontSize = savedNameFontSize;
                savedNameFontSize = -1;
            }
        }

        public bool TryConsumeEnter()
        {
            if (triggerOncePerVisit && consumedThisVisit)
                return false;
            if (triggerOncePerVisit)
                consumedThisVisit = true;
            return true;
        }

        public void ResetVisitState()
        {
            consumedThisVisit = false;
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
        }

        private RectTransform BuildPlate()
        {
            return GuildSceneUiFactory.BuildResponseAreaNamePlate(
                Rt, displayName, iconOverride, IconFallbackColor, plateOffsetY);
        }
    }
}
