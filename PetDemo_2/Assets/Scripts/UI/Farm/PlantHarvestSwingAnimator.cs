// SPEC §9.1.2 (v3.92)：可收获植物 / 变异果实图标底部中心 pivot 摆动动画。
using System.Collections;
using UnityEngine;

namespace PetDemo.UI.Farm
{
    public static class PlantHarvestSwingAnimator
    {
        public const float SwingLeftDegrees = 15f;
        public const float SwingRightDeltaDegrees = 45f;
        public const float PhaseDuration = 0.2f;
        public const int CycleCount = 2;

        public static readonly Vector2 BottomCenterPivot = new Vector2(0.5f, 0f);

        /// <summary>普通田收获或变异收获摆动播放中的全局计数，用于防抖。</summary>
        public static int ActiveSwingCount { get; private set; }

        public static void BeginSwing() => ActiveSwingCount++;

        public static void EndSwing() => ActiveSwingCount = Mathf.Max(0, ActiveSwingCount - 1);

        public struct PivotSnapshot
        {
            public Vector2 pivot;
            public Vector2 anchoredPosition;
            public Vector3 localEulerAngles;
        }

        public static PivotSnapshot Capture(RectTransform rt)
        {
            return new PivotSnapshot
            {
                pivot = rt.pivot,
                anchoredPosition = rt.anchoredPosition,
                localEulerAngles = rt.localEulerAngles,
            };
        }

        public static void SetPivotWithCompensation(RectTransform rt, Vector2 newPivot)
        {
            if (rt == null)
                return;
            var size = rt.rect.size;
            var deltaPivot = newPivot - rt.pivot;
            rt.pivot = newPivot;
            rt.anchoredPosition += new Vector2(deltaPivot.x * size.x, deltaPivot.y * size.y);
        }

        public static void Restore(RectTransform rt, PivotSnapshot snapshot)
        {
            if (rt == null)
                return;
            SetPivotWithCompensation(rt, snapshot.pivot);
            rt.anchoredPosition = snapshot.anchoredPosition;
            rt.localEulerAngles = snapshot.localEulerAngles;
        }

        /// <summary>临时底 pivot → 2 遍摆动 → 恢复（不含全局计数，由调用方在 finally 中配对增减）。</summary>
        public static IEnumerator RunSwingSequence(RectTransform targetRt)
        {
            if (targetRt == null)
                yield break;

            var snapshot = Capture(targetRt);
            SetPivotWithCompensation(targetRt, BottomCenterPivot);
            try
            {
                yield return PlaySwing(targetRt);
            }
            finally
            {
                Restore(targetRt, snapshot);
            }
        }

        public static IEnumerator PlaySwing(RectTransform plantRt)
        {
            if (plantRt == null)
                yield break;

            for (int cycle = 0; cycle < CycleCount; cycle++)
            {
                yield return LerpZ(plantRt, 0f, SwingLeftDegrees, PhaseDuration);
                yield return LerpZ(plantRt, SwingLeftDegrees, SwingLeftDegrees - SwingRightDeltaDegrees, PhaseDuration);
                plantRt.localEulerAngles = new Vector3(0f, 0f, 0f);
            }
        }

        private static IEnumerator LerpZ(RectTransform rt, float fromZ, float toZ, float duration)
        {
            if (duration <= 0f)
            {
                var e = rt.localEulerAngles;
                e.z = toZ;
                rt.localEulerAngles = e;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float z = Mathf.Lerp(fromZ, toZ, t);
                var euler = rt.localEulerAngles;
                euler.z = z;
                rt.localEulerAngles = euler;
                yield return null;
            }

            var end = rt.localEulerAngles;
            end.z = toZ;
            rt.localEulerAngles = end;
        }
    }
}
