// 将子层叠图标（PestEventIcon / MoleTheftEventIcon 等）的点击转发到父级 TileSlotView。
// Unity UI 默认只向 raycast 命中的 GameObject 派发 IPointerClickHandler，不会冒泡到父节点。
using UnityEngine;
using UnityEngine.EventSystems;

namespace PetDemo.UI.Farm
{
    [DisallowMultipleComponent]
    internal sealed class TileSlotOverlayClickRelay : MonoBehaviour, IPointerClickHandler
    {
        private TileSlotView owner;

        public void Bind(TileSlotView slotView)
        {
            owner = slotView;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            owner?.ForwardPointerClick(eventData);
        }
    }
}
