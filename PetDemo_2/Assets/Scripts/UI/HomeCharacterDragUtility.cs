// SPEC §9.5.4 (v3.91)：家园主角/精灵拖动 — 坐标换算、阈值、边界与缩放。
using UnityEngine;
using UnityEngine.EventSystems;

namespace PetDemo.UI
{
    public static class HomeCharacterDragUtility
    {
        public const float DragScaleFactor = 0.7f;

        public static float GetDragThreshold()
        {
            var es = EventSystem.current;
            return es != null ? es.pixelDragThreshold : 10f;
        }

        public static bool ExceedsDragThreshold(Vector2 pointerDownScreen, Vector2 currentScreen)
        {
            return (currentScreen - pointerDownScreen).sqrMagnitude >=
                GetDragThreshold() * GetDragThreshold();
        }

        /// <summary>
        /// Overlay Canvas 必须用 null 相机；误用 pressEventCamera 会导致坐标换算失败并跳到 (0,0)。
        /// </summary>
        public static Camera ResolveEventCamera(RectTransform referenceInHierarchy)
        {
            if (referenceInHierarchy == null)
                return null;

            var canvas = referenceInHierarchy.GetComponentInParent<Canvas>();
            if (canvas == null)
                return null;

            return canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
        }

        public static bool TryScreenToLocalInParent(
            RectTransform parent,
            PointerEventData eventData,
            out Vector2 localPoint)
        {
            localPoint = Vector2.zero;
            if (parent == null || eventData == null)
                return false;

            var cam = ResolveEventCamera(parent);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent,
                eventData.position,
                cam,
                out localPoint);
        }

        public static Vector2 ComputeAnchoredWithOffset(
            RectTransform parent,
            PointerEventData eventData,
            Vector2 dragOffset,
            Vector2 fallbackAnchored)
        {
            if (!TryScreenToLocalInParent(parent, eventData, out var local))
                return fallbackAnchored;
            return local - dragOffset;
        }

        public static Vector2 ComputeDragOffset(
            RectTransform target,
            RectTransform parent,
            PointerEventData eventData)
        {
            if (target == null || parent == null || eventData == null)
                return Vector2.zero;

            if (!TryScreenToLocalInParent(parent, eventData, out var local))
                return Vector2.zero;

            return local - target.anchoredPosition;
        }

        /// <summary>
        /// 向上查找首个有实际尺寸的父 Rect，用于边界 Clamp（跳过 sizeDelta 为 0 的中间层）。
        /// </summary>
        public static RectTransform ResolveClampBoundsParent(RectTransform draggedChild)
        {
            if (draggedChild == null)
                return null;

            var current = draggedChild.parent as RectTransform;
            while (current != null)
            {
                var rect = current.rect;
                if (rect.width > 2f && rect.height > 2f)
                    return current;

                current = current.parent as RectTransform;
            }

            return draggedChild.parent as RectTransform;
        }

        public static Vector2 ClampAnchoredInBoundsParent(
            Vector2 anchoredInPositionParent,
            RectTransform childInPositionParent,
            RectTransform positionParent,
            RectTransform boundsParent)
        {
            if (childInPositionParent == null || positionParent == null || boundsParent == null)
                return anchoredInPositionParent;

            if (boundsParent == positionParent)
                return ClampAnchoredToParentRect(anchoredInPositionParent, childInPositionParent, positionParent);

            var worldPos = positionParent.TransformPoint(anchoredInPositionParent);
            var localInBounds = boundsParent.InverseTransformPoint(worldPos);

            var half = childInPositionParent.rect.size * 0.5f;
            var scale = childInPositionParent.lossyScale;
            var boundsRect = boundsParent.rect;
            float halfX = half.x * Mathf.Abs(scale.x);
            float halfY = half.y * Mathf.Abs(scale.y);

            float minX = boundsRect.xMin + halfX;
            float maxX = boundsRect.xMax - halfX;
            float minY = boundsRect.yMin + halfY;
            float maxY = boundsRect.yMax - halfY;

            if (minX > maxX)
            {
                float cx = boundsRect.center.x;
                minX = maxX = cx;
            }

            if (minY > maxY)
            {
                float cy = boundsRect.center.y;
                minY = maxY = cy;
            }

            localInBounds.x = Mathf.Clamp(localInBounds.x, minX, maxX);
            localInBounds.y = Mathf.Clamp(localInBounds.y, minY, maxY);

            var clampedWorld = boundsParent.TransformPoint(localInBounds);
            return positionParent.InverseTransformPoint(clampedWorld);
        }

        public static Vector2 ClampAnchoredToParentRect(
            Vector2 anchored,
            RectTransform child,
            RectTransform parent)
        {
            if (child == null || parent == null)
                return anchored;

            var parentRect = parent.rect;
            if (parentRect.width < 2f || parentRect.height < 2f)
                return anchored;

            var half = child.rect.size * 0.5f;
            float minX = parentRect.xMin + half.x;
            float maxX = parentRect.xMax - half.x;
            float minY = parentRect.yMin + half.y;
            float maxY = parentRect.yMax - half.y;

            if (minX > maxX)
            {
                float cx = parentRect.center.x;
                minX = maxX = cx;
            }

            if (minY > maxY)
            {
                float cy = parentRect.center.y;
                minY = maxY = cy;
            }

            return new Vector2(
                Mathf.Clamp(anchored.x, minX, maxX),
                Mathf.Clamp(anchored.y, minY, maxY));
        }

        public static Vector3 ApplyScaleFactor(Vector3 baseScale, float factor)
        {
            return new Vector3(baseScale.x * factor, baseScale.y * factor, baseScale.z * factor);
        }
    }
}
