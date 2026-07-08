// SPEC §9.8.9.3 / §9.8.9.4：公会场景建筑标记 — 人工摆放；主角进入半径时显示「建筑名 + 功能按钮（占位）」名牌。
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class GuildBuildingMarker : MonoBehaviour
    {
        [SerializeField] private string buildingName = "公会建筑";
        [SerializeField] private float interactRadius = 260f;
        [SerializeField] private float plateOffsetY = 140f;

        private RectTransform plateRt;
        private int savedNameFontSize = -1;

        public RectTransform Rt => (RectTransform)transform;
        public float InteractRadius => interactRadius;
        public string BuildingName => buildingName;

        public void SetBuildingName(string value)
        {
            buildingName = value;
        }

        public void SetPlateVisible(bool visible, bool panoramaOverride = false)
        {
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

        // 名牌运行时懒创建（SPEC §9.8.9.6），底部枢轴贴在建筑上方。
        private RectTransform BuildPlate()
        {
            var rt = GuildSceneUiFactory.CreatePlateRoot(
                Rt, "NamePlate", new Vector2(0f, plateOffsetY), new Vector2(320f, 160f));

            GuildSceneUiFactory.AddText(rt, "NameText", buildingName, 34,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -14f), new Vector2(300f, 48f));

            var btn = GuildSceneUiFactory.AddButton(rt, "ActionButton", "功能",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 14f), new Vector2(140f, 56f));
            string captured = buildingName;
            btn.onClick.AddListener(() =>
                UnityEngine.Debug.Log("[GongHuiScreen] 建筑功能按钮（占位）：" + captured));
            return rt;
        }
    }
}
