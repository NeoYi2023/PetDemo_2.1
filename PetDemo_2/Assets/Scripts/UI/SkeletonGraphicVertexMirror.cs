// SPEC §9.5.1.3（v3.78 / v3.79）：左半屏精灵在 ScaleX 取负时辅以顶点 X 翻转（SkeletonGraphic UI 路径实测需双保险）。
using Spine.Unity;
using UnityEngine;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SkeletonGraphic))]
    public sealed class SkeletonGraphicVertexMirror : MonoBehaviour
    {
        private SkeletonGraphic skeletonGraphic;

        public void Bind(SkeletonGraphic target)
        {
            Unsubscribe();
            skeletonGraphic = target != null ? target : GetComponent<SkeletonGraphic>();
            Subscribe();
            if (skeletonGraphic != null && skeletonGraphic.IsValid)
                skeletonGraphic.UpdateMesh();
        }

        private void OnEnable()
        {
            if (skeletonGraphic == null)
                skeletonGraphic = GetComponent<SkeletonGraphic>();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (skeletonGraphic != null)
                skeletonGraphic.OnPostProcessVertices += FlipVertexX;
        }

        private void Unsubscribe()
        {
            if (skeletonGraphic != null)
                skeletonGraphic.OnPostProcessVertices -= FlipVertexX;
        }

        private static void FlipVertexX(MeshGeneratorBuffers buffers)
        {
            int count = buffers.vertexCount;
            Vector3[] verts = buffers.vertexBuffer;
            if (verts == null || count <= 0)
                return;

            for (int i = 0; i < count; i++)
                verts[i].x = -verts[i].x;
        }
    }
}
