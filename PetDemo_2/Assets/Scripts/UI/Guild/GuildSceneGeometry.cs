// SPEC §9.8.9.6：公会场景 UGUI 局部空间几何工具（AABB 碰撞与距离检测共用）。
using UnityEngine;

namespace PetDemo.UI
{
    public static class GuildSceneGeometry
    {
        /// <summary>把 rt 的矩形换算到 worldContent 局部空间（以 content 枢轴为原点）。</summary>
        public static Rect RectInContentSpace(RectTransform rt, RectTransform content)
        {
            var r = rt.rect;
            var p0 = (Vector2)content.InverseTransformPoint(
                rt.TransformPoint(new Vector3(r.xMin, r.yMin, 0f)));
            var p1 = (Vector2)content.InverseTransformPoint(
                rt.TransformPoint(new Vector3(r.xMax, r.yMax, 0f)));
            var min = Vector2.Min(p0, p1);
            var max = Vector2.Max(p0, p1);
            return new Rect(min, max - min);
        }

        /// <summary>把 rt 的枢轴点换算到 worldContent 局部空间。</summary>
        public static Vector2 PointInContentSpace(RectTransform rt, RectTransform content)
        {
            return content.InverseTransformPoint(rt.position);
        }

        public static Rect CenteredRect(Vector2 center, Vector2 size)
        {
            return new Rect(center - size * 0.5f, size);
        }
    }
}
