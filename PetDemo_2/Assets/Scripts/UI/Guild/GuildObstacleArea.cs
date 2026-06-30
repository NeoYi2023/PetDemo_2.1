// SPEC §9.8.9.3：公会场景碰撞体标记 — 阻挡矩形 = 自身 RectTransform（人工在预制体中摆放，无视觉）。
using UnityEngine;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class GuildObstacleArea : MonoBehaviour
    {
        public RectTransform Rt => (RectTransform)transform;
    }
}
