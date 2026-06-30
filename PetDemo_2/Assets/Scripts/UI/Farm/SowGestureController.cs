// SPEC §9.4.6：仓库内播种触发按钮 + 手势的状态机控制器。
// 维护四态 Idle / Armed / SlideMode / ClickMode；
// SlideMode 期间通过 EventSystem.RaycastAll 命中 TileSlotView 后调用 TrySeedTile；
// ClickMode 期间监听下一次 TileSlotView.IPointerClick 完成一次性播种。
using System;
using System.Collections.Generic;
using PetDemo.Farm;
using PetDemo.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PetDemo.UI.Farm
{
    public class SowGestureController : MonoBehaviour
    {
        public enum Mode { Idle, Armed, SlideMode, ClickMode }

        public static SowGestureController Instance { get; private set; }

        private IPlantingService service;
        private RectTransform modalRt;
        private SowActionButtonView buttonView;
        private JiaYuanViewportFollowController cameraFollow;
        private Func<RectTransform> resolveZhongTianAnchor;

        private Mode mode = Mode.Idle;
        private readonly HashSet<string> sownThisGesture = new HashSet<string>();
        private readonly List<RaycastResult> raycastBuffer = new List<RaycastResult>(16);

        public Mode CurrentMode => mode;
        public bool IsClickMode => mode == Mode.ClickMode;
        public bool IsSlideMode => mode == Mode.SlideMode;

        public bool IsWarehouseModalOpen
        {
            get { return modalRt != null && modalRt.gameObject.activeSelf; }
        }

        public bool ShouldButtonBeVisible
        {
            get
            {
                if (service == null)
                    return false;
                if (mode != Mode.Idle)
                    return false;
                if (!IsWarehouseModalOpen)
                    return false;
                return service.GetActive() != null;
            }
        }

        public static SowGestureController GetOrCreate(RectTransform canvasRoot)
        {
            if (Instance != null)
                return Instance;
            var go = new GameObject("SowGestureController");
            go.transform.SetParent(canvasRoot, false);
            return go.AddComponent<SowGestureController>();
        }

        public void Init(IPlantingService svc, RectTransform modalRect)
        {
            service = svc;
            modalRt = modalRect;
        }

        public void RegisterButton(SowActionButtonView view)
        {
            buttonView = view;
        }

        public void BindCameraFollow(
            JiaYuanViewportFollowController follow,
            Func<RectTransform> resolveZhongTian)
        {
            cameraFollow = follow;
            resolveZhongTianAnchor = resolveZhongTian;
        }

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            cameraFollow?.CancelSowCameraTransition();
        }

        // SowActionButtonView 在 SetActive(false) 时其 LateUpdate 不会运行，
        // 无法感知仓库 modal 被重新打开。本控制器始终活跃，由这里统一驱动按钮显隐同步。
        // 手势进行中（Armed/SlideMode/ClickMode）的按钮视觉由 controller 主动控制，跳过该同步。
        private void LateUpdate()
        {
            if (buttonView == null)
                return;
            if (mode != Mode.Idle)
                return;
            buttonView.ApplyVisibility(ShouldButtonBeVisible);
        }

        // ====================================================================
        // 来自 SowActionButtonView 的手势事件
        // ====================================================================
        public void OnButtonPointerDown(PointerEventData eventData)
        {
            if (mode != Mode.Idle)
                return;

            // 关闭仓库 modal：按钮挂在 Canvas 根节点（不属 modal 子树），关闭后自身仍存活
            // 以接收后续 IDrag/IPointerUp/IEndDrag。
            if (modalRt != null && modalRt.gameObject.activeSelf)
                modalRt.gameObject.SetActive(false);

            sownThisGesture.Clear();
            mode = Mode.Armed;
            NotifySowCamera(true);
            if (buttonView != null)
                buttonView.SetVisualPressed(true);
        }

        public void OnButtonBeginDrag(PointerEventData eventData)
        {
            if (mode != Mode.Armed)
                return;
            mode = Mode.SlideMode;
            if (buttonView != null)
                buttonView.SetButtonHidden(true);
            // BeginDrag 触发瞬间也尝试一次播种（指针刚跨过阈值时已可能在 tile 上方）
            TrySowAtPointer(eventData);
        }

        public void OnButtonDrag(PointerEventData eventData)
        {
            if (mode != Mode.SlideMode)
                return;
            TrySowAtPointer(eventData);
        }

        public void OnButtonPointerUp(PointerEventData eventData)
        {
            switch (mode)
            {
                case Mode.SlideMode:
                    EndGestureToIdle();
                    break;
                case Mode.Armed:
                    // 按下后未达拖拽阈值即松开 → 进入一次性点击播种待命态
                    mode = Mode.ClickMode;
                    if (buttonView != null)
                    {
                        buttonView.SetVisualPressed(false);
                        buttonView.SetButtonHidden(true);
                    }
                    break;
            }
        }

        public void OnButtonEndDrag(PointerEventData eventData)
        {
            if (mode == Mode.SlideMode)
                EndGestureToIdle();
        }

        // ====================================================================
        // 来自 TileSlotView 的点击（仅 ClickMode 时被消费）
        // ====================================================================
        public void RequestClickModeSow(string tileId)
        {
            if (mode != Mode.ClickMode)
                return;
            if (!string.IsNullOrEmpty(tileId) && service != null)
                service.TrySeedTile(tileId);
            EndGestureToIdle();
        }

        // ====================================================================
        // 内部
        // ====================================================================
        private void TrySowAtPointer(PointerEventData eventData)
        {
            if (service == null || EventSystem.current == null || eventData == null)
                return;

            raycastBuffer.Clear();
            EventSystem.current.RaycastAll(eventData, raycastBuffer);
            for (int i = 0; i < raycastBuffer.Count; i++)
            {
                var hit = raycastBuffer[i].gameObject;
                if (hit == null)
                    continue;
                var slot = hit.GetComponentInParent<TileSlotView>();
                if (slot == null)
                    continue;
                string tileId = slot.TileId;
                if (string.IsNullOrEmpty(tileId))
                    continue;
                // 一次手势内同一 tile 仅消费一次种子
                if (sownThisGesture.Contains(tileId))
                    return;
                if (service.TrySeedTile(tileId))
                    sownThisGesture.Add(tileId);
                // 一次 IDrag 仅尝试首个命中的 TileSlot；
                // 用户继续拖动时会触发新的 IDrag 再次扫描下一个 tile。
                return;
            }
        }

        private void EndGestureToIdle()
        {
            mode = Mode.Idle;
            sownThisGesture.Clear();
            ReleaseSowCamera();
            if (buttonView != null)
            {
                buttonView.SetVisualPressed(false);
                buttonView.SetButtonHidden(false);
                buttonView.RefreshVisibility();
            }
        }

        private void NotifySowCamera(bool active)
        {
            if (!active || cameraFollow == null)
                return;

            var anchor = resolveZhongTianAnchor != null ? resolveZhongTianAnchor() : null;
            if (anchor == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[SowGestureController] ZhongTian 锚点不可用，播种期间镜头继续跟随村民。");
                cameraFollow.CancelSowCameraTransition();
                return;
            }

            cameraFollow.SetSowAnchorLock(true, anchor);
        }

        private void ReleaseSowCamera()
        {
            if (cameraFollow == null)
                return;

            cameraFollow.BeginSowReleaseTransition(1f, 0.8f);
        }
    }
}
