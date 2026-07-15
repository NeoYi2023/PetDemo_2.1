// SPEC §9.8.9.15 (v3.230)：公会场景 NPC 游走目标点标记 —
// 目标坐标 = 自身 RectTransform（人工在预制体 Waypoints/ 下摆放，无视觉）。
// 被 GuildNpcWanderController 收集为随机游走目标，并按占用锁排他选点。
using UnityEngine;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class GuildWaypointMarker : MonoBehaviour
    {
        public RectTransform Rt => (RectTransform)transform;

#if UNITY_EDITOR
        // Scene 视图可视化，便于人工摆放/调整目标点坐标（构建时剥离）。
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 24f);
            Gizmos.DrawLine(transform.position + Vector3.left * 32f, transform.position + Vector3.right * 32f);
            Gizmos.DrawLine(transform.position + Vector3.down * 32f, transform.position + Vector3.up * 32f);
        }
#endif
    }
}
