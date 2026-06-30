// SPEC §9.8.12 / §9.8.13 (v3.41)：FoodWarehouseModalView 自 v3.41 起退化为薄桥接层，
// 内部委托给 §9.8.13 「统一仓库预制体」运行时组件 WarehouseHubPanelView，
// 保留 GetOrCreate / Show / Hide / IsShown 公共 API 以兼容 §9.8.8 主线层入口
// （MainStoryLineScreenView 在饿肚子提示框「确定」时仍调用本类）。
using PetDemo.Farm;
using PetDemo.UI.Farm;
using UnityEngine;

namespace PetDemo.UI
{
    /// <summary>
    /// SPEC §9.8.12 / §9.8.13：食物仓库桥接层（v3.41 起改为代理 WarehouseHubPanelView）。
    /// 该 MonoBehaviour 自身不绘制任何 UI，挂在 WarehouseHubPanel 预制体根上协同生命周期。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FoodWarehouseModalView : MonoBehaviour
    {
        private WarehouseHubPanelView hub;

        public bool IsShown => hub != null && hub.IsShown;

        /// <summary>
        /// SPEC §9.8.13.1：在指定 Canvas 上查找或实例化统一仓库面板，并在其根上挂一份桥接组件。
        /// 缺失预制体时返回 null（与 WarehouseHubPanelView.GetOrCreate 行为对齐）。
        /// </summary>
        public static FoodWarehouseModalView GetOrCreate(RectTransform canvasRect)
        {
            if (canvasRect == null)
                return null;

            var hub = WarehouseHubPanelView.GetOrCreate(canvasRect);
            if (hub == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[FoodWarehouseModalView] GetOrCreate: WarehouseHubPanelView 实例化失败，" +
                    "请执行 Tools/PetDemo/Generate Warehouse Hub Panel Prefab 后再重试。");
                return null;
            }

            var bridge = hub.GetComponent<FoodWarehouseModalView>();
            if (bridge == null)
                bridge = hub.gameObject.AddComponent<FoodWarehouseModalView>();
            bridge.hub = hub;
            return bridge;
        }

        public void Show(IPlantingService plantingService)
        {
            if (hub == null)
                hub = GetComponent<WarehouseHubPanelView>();
            if (hub == null)
            {
                UnityEngine.Debug.LogWarning("[FoodWarehouseModalView] Show: 未绑定 WarehouseHubPanelView");
                return;
            }
            hub.Show(plantingService);
        }

        public void Hide()
        {
            if (hub == null)
                hub = GetComponent<WarehouseHubPanelView>();
            if (hub != null)
                hub.Hide();
        }
    }
}
