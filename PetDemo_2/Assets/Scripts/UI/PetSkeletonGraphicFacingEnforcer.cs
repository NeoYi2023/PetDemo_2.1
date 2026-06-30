// SPEC §9.5.1.3 / §12.3（v3.79）：按屏幕半区持续校正精灵 SkeletonGraphic 朝向（防动画/布局后 ScaleX 被还原）。
using Spine.Unity;
using UnityEngine;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SkeletonGraphic))]
    public sealed class PetSkeletonGraphicFacingEnforcer : MonoBehaviour
    {
        private SkeletonGraphic skeletonGraphic;
        private RectTransform facingAnchor;
        private bool lastSpawnOnLeftHalf;
        private bool hasLast;

        public static void AttachOrRefresh(SkeletonGraphic sg, RectTransform anchor)
        {
            if (sg == null)
                return;

            var enforcer = sg.GetComponent<PetSkeletonGraphicFacingEnforcer>();
            if (enforcer == null)
                enforcer = sg.gameObject.AddComponent<PetSkeletonGraphicFacingEnforcer>();

            enforcer.skeletonGraphic = sg;
            enforcer.facingAnchor = anchor;
            enforcer.ApplyFacing(force: true);
        }

        private void LateUpdate()
        {
            ApplyFacing(force: false);
        }

        private void ApplyFacing(bool force)
        {
            if (skeletonGraphic == null || !skeletonGraphic.IsValid)
                return;

            bool spawnOnLeftHalf = FantaziaMonsterDisplay.IsSpawnOnScreenLeftHalf(
                facingAnchor != null ? facingAnchor : skeletonGraphic.rectTransform);

            if (!force && hasLast && spawnOnLeftHalf == lastSpawnOnLeftHalf)
            {
                float expected = spawnOnLeftHalf ? -1f : 1f;
                float current = skeletonGraphic.Skeleton != null ? skeletonGraphic.Skeleton.ScaleX : 0f;
                if (Mathf.Sign(current) == Mathf.Sign(expected) || Mathf.Abs(current) < 0.0001f)
                    return;
            }

            FantaziaMonsterDisplay.ApplySkeletonGraphicScaleXFacing(skeletonGraphic, spawnOnLeftHalf);
            lastSpawnOnLeftHalf = spawnOnLeftHalf;
            hasLast = true;
        }
    }
}
