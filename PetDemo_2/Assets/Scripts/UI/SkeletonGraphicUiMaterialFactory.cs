// Spine 4.x：Spine/SkeletonGraphic 着色器默认开启 CanvasGroup Compatible，
// 与运行时从 SkeletonAnimation 同步的 PMA Vertex Colors 冲突（Inspector 警告 + 透明度异常）。
using UnityEngine;

namespace PetDemo.UI
{
    public static class SkeletonGraphicUiMaterialFactory
    {
        private static readonly int CanvasGroupCompatiblePropertyId =
            Shader.PropertyToID("_CanvasGroupCompatible");
        private const string CanvasGroupCompatibleKeyword = "_CANVAS_GROUP_COMPATIBLE";

        /// <summary>
        /// 创建与 PMA 顶点色匹配的 UI 材质（关闭 CanvasGroup Compatible）。
        /// </summary>
        public static Material CreateForPmaVertexColors(Shader skeletonGraphicShader)
        {
            var material = new Material(skeletonGraphicShader);
            ConfigureForPmaVertexColors(material);
            return material;
        }

        public static void ConfigureForPmaVertexColors(Material material)
        {
            if (material == null)
                return;

            if (material.HasProperty(CanvasGroupCompatiblePropertyId))
                material.SetFloat(CanvasGroupCompatiblePropertyId, 0f);
            material.DisableKeyword(CanvasGroupCompatibleKeyword);
        }
    }
}
