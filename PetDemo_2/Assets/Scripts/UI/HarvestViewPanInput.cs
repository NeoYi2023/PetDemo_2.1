// SPEC §9.7.1 (v3.110)：家园视口滑动手势 — Entry/FreePan 态拖动镜头。
// 使用全局指针轮询 + RaycastAll，避免农田格/主角挡住底层 Pan 层导致拖不动。
using System.Collections.Generic;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class HarvestViewPanInput : MonoBehaviour
    {
        private static readonly List<RaycastResult> RaycastBuffer = new List<RaycastResult>(32);

        private HarvestViewEntryView entryView;
        private JiaYuanViewportFollowController viewportFollow;

        private bool tracking;
        private bool panning;
        private int trackingPointerId;
        private Vector2 pointerDownScreen;
        private Vector2 lastScreen;

        public static HarvestViewPanInput Attach(
            HarvestViewEntryView view,
            JiaYuanViewportFollowController follow)
        {
            if (view == null || follow == null)
                return null;

            var input = view.gameObject.GetComponent<HarvestViewPanInput>();
            if (input == null)
                input = view.gameObject.AddComponent<HarvestViewPanInput>();
            input.entryView = view;
            input.viewportFollow = follow;
            return input;
        }

        private void Update()
        {
            if (entryView == null || viewportFollow == null)
                return;

            if (!entryView.ShouldAcceptPanInput())
            {
                ResetTracking();
                return;
            }

            if (Input.touchSupported && Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                    ProcessTouch(Input.GetTouch(i));
                return;
            }

            ProcessMouse();
        }

        private void ProcessMouse()
        {
            var pos = (Vector2)Input.mousePosition;

            if (Input.GetMouseButtonDown(0))
                TryBeginTracking(pos, 0);

            if (tracking && trackingPointerId == 0 && Input.GetMouseButton(0))
                ProcessMove(pos);

            if (tracking && trackingPointerId == 0 && Input.GetMouseButtonUp(0))
                ResetTracking();
        }

        private void ProcessTouch(Touch touch)
        {
            if (!tracking)
            {
                if (touch.phase == TouchPhase.Began)
                    TryBeginTracking(touch.position, touch.fingerId);
                return;
            }

            if (touch.fingerId != trackingPointerId)
                return;

            if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                ProcessMove(touch.position);

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                ResetTracking();
        }

        private void TryBeginTracking(Vector2 screenPos, int pointerId)
        {
            if (IsPointerOverExcludedUi(screenPos))
                return;

            tracking = true;
            panning = entryView.CurrentState == HarvestViewEntryView.HarvestViewUiState.FreePan;
            trackingPointerId = pointerId;
            pointerDownScreen = screenPos;
            lastScreen = screenPos;
        }

        private void ProcessMove(Vector2 screenPos)
        {
            if (!panning)
            {
                if (!HomeCharacterDragUtility.ExceedsDragThreshold(pointerDownScreen, screenPos) &&
                    (screenPos - pointerDownScreen).sqrMagnitude < 4f)
                    return;

                if (entryView.CurrentState == HarvestViewEntryView.HarvestViewUiState.Entry)
                    entryView.EnterFreePan();

                panning = true;
                lastScreen = screenPos;
                return;
            }

            var delta = screenPos - lastScreen;
            lastScreen = screenPos;
            viewportFollow.ApplyFreePanScreenDelta(delta);
        }

        private void ResetTracking()
        {
            tracking = false;
            panning = false;
            trackingPointerId = -1;
        }

        private bool IsPointerOverExcludedUi(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null)
                return true;

            var eventData = new PointerEventData(es) { position = screenPos };
            RaycastBuffer.Clear();
            es.RaycastAll(eventData, RaycastBuffer);

            for (int i = 0; i < RaycastBuffer.Count; i++)
            {
                var go = RaycastBuffer[i].gameObject;
                if (go == null)
                    continue;

                if (entryView.IsPointerOverHarvestButtons(go))
                    return true;

                if (go.GetComponentInParent<VillagerDragRelay>() != null)
                    return true;
                if (go.GetComponentInParent<PetCompanionClickRelay>() != null)
                    return true;
                if (go.GetComponentInParent<SowActionButtonView>() != null)
                    return true;
                if (go.GetComponentInParent<BottomNavButtonView>() != null)
                    return true;
                if (IsNamedHudButton(go))
                    return true;
            }

            return false;
        }

        private static bool IsNamedHudButton(GameObject go)
        {
            var t = go.transform;
            while (t != null)
            {
                switch (t.name)
                {
                    case "SeedWarehouseButton":
                    case "FertilizeEntryButton":
                    case "FruitBagButton":
                    case "UnifiedActionButton":
                    case "SowActionButton":
                        return true;
                }

                t = t.parent;
            }

            return false;
        }
    }
}
