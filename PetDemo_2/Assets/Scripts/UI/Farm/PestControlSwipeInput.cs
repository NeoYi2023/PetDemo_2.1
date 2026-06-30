// SPEC §9.11.3：灭虫小游戏滑动输入。
using PetDemo.Farm;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PetDemo.UI.Farm
{
    public class PestControlSwipeInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private const float SwipeThresholdPx = 40f;

        private Vector2 pointerStart;
        private bool tracking;
        private PestControlScreenView screenView;

        public void Init(PestControlScreenView owner)
        {
            screenView = owner;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (screenView != null && !screenView.CanAcceptInput)
                return;
            tracking = true;
            pointerStart = eventData.position;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!tracking)
                return;
            tracking = false;

            Vector2 delta = eventData.position - pointerStart;
            if (delta.sqrMagnitude < SwipeThresholdPx * SwipeThresholdPx)
                return;

            PestControlSwipeDirection dir;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                dir = delta.x > 0f ? PestControlSwipeDirection.Right : PestControlSwipeDirection.Left;
            else
                dir = delta.y > 0f ? PestControlSwipeDirection.Up : PestControlSwipeDirection.Down;

            screenView?.HandleSwipe(dir);
        }
    }
}
