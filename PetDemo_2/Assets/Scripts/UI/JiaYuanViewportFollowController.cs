// SPEC §9.8.14：平移 JiaYuanWorldContent，使目标锚点保持在视口中心（UGUI 镜头跟随）。
// v3.80.5：播种态冻结 ZhongTian；结束后 1s 停留 + 平滑回跟村民。
// v3.110：§9.7.1 收获视角自由拖动 — Enter/ExitFreePanMode + 固定钳位。
using System.Collections;
using UnityEngine;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class JiaYuanViewportFollowController : MonoBehaviour
    {
        private const float DefaultSowHoldSeconds = 1f;
        private const float DefaultSowReturnSeconds = 0.8f;

        public static readonly Vector2 HarvestFreePanMin = new Vector2(-696f, -748f);
        public static readonly Vector2 HarvestFreePanMax = new Vector2(507f, 1050f);

        [SerializeField] private Vector2 followOffset = Vector2.zero;
        [SerializeField] private float snapThreshold = 0.5f;

        private RectTransform viewportRt;
        private RectTransform worldContentRt;
        private RectTransform followTargetRt;
        private RectTransform overrideTargetRt;
        private bool followEnabled;
        private bool followFrozen;
        private bool sowAnchorLocked;
        private bool freePanActive;
        private bool releaseTransitionActive;
        private Coroutine releaseCoroutine;

        public bool IsFreePanActive => freePanActive;

        private RectTransform EffectiveTarget => overrideTargetRt != null ? overrideTargetRt : followTargetRt;

        public void Initialize(RectTransform viewport, RectTransform worldContent)
        {
            viewportRt = viewport;
            worldContentRt = worldContent;
        }

        public void SetFollowTarget(RectTransform target)
        {
            followTargetRt = target;
        }

        public void SetOverrideTarget(RectTransform target)
        {
            overrideTargetRt = target;
        }

        public void SetFollowEnabled(bool enabled)
        {
            followEnabled = enabled;
            if (!enabled)
            {
                CancelSowCameraTransition();
                ExitFreePanMode();
                return;
            }

            SnapOnce();
        }

        /// <summary>SPEC §9.7.1：进入自由拖动，停止跟随与锚点锁，保持当前 content 位置。</summary>
        public void EnterFreePanMode()
        {
            StopReleaseCoroutine();
            releaseTransitionActive = false;
            sowAnchorLocked = false;
            overrideTargetRt = null;
            freePanActive = true;
            if (worldContentRt != null)
                worldContentRt.anchoredPosition = ClampHarvestFreePanPosition(worldContentRt.anchoredPosition);
        }

        /// <summary>SPEC §9.7.1：退出自由拖动，恢复 LateUpdate 跟随村民。</summary>
        public void ExitFreePanMode()
        {
            freePanActive = false;
        }

        /// <summary>SPEC §9.7.1：自由模式下根据屏幕位移平移 content，并按固定区间钳位。</summary>
        public void ApplyFreePanScreenDelta(Vector2 screenDelta)
        {
            if (!freePanActive || worldContentRt == null)
                return;

            var pos = worldContentRt.anchoredPosition + screenDelta;
            worldContentRt.anchoredPosition = ClampHarvestFreePanPosition(pos);
        }

        /// <summary>播种态：对准 ZhongTian 并冻结 LateUpdate；解锁请用 BeginSowReleaseTransition。</summary>
        public void SetSowAnchorLock(bool locked, RectTransform zhongTian)
        {
            if (locked)
            {
                ExitFreePanMode();
                StopReleaseCoroutine();
                releaseTransitionActive = false;
                sowAnchorLocked = zhongTian != null;
                overrideTargetRt = zhongTian;
                if (!followEnabled || zhongTian == null)
                    return;
                SnapOnce();
                return;
            }

            sowAnchorLocked = false;
            overrideTargetRt = null;
        }

        /// <summary>播种手势结束：在 ZhongTian 构图停留后平滑回到跟随村民。</summary>
        public void BeginSowReleaseTransition(
            float holdSeconds = DefaultSowHoldSeconds,
            float returnSeconds = DefaultSowReturnSeconds)
        {
            if (!followEnabled || viewportRt == null || worldContentRt == null)
            {
                CancelSowCameraTransition();
                return;
            }

            sowAnchorLocked = false;
            StopReleaseCoroutine();
            releaseCoroutine = StartCoroutine(SowReleaseTransitionCoroutine(
                Mathf.Max(0f, holdSeconds),
                Mathf.Max(0.01f, returnSeconds)));
        }

        public void CancelSowCameraTransition()
        {
            StopReleaseCoroutine();
            releaseTransitionActive = false;
            sowAnchorLocked = false;
            overrideTargetRt = null;
            ExitFreePanMode();
        }

        /// <summary>SPEC §9.7.1：收获视角退出，清除 ZhongTian 锁并立即恢复跟随村民（无播种回跟过渡）。</summary>
        public void ExitAnchorViewLock()
        {
            StopReleaseCoroutine();
            releaseTransitionActive = false;
            sowAnchorLocked = false;
            overrideTargetRt = null;
            ExitFreePanMode();
        }

        public void SetFollowFrozen(bool frozen)
        {
            followFrozen = frozen;
        }

        public void SnapOnce()
        {
            SnapToTarget(EffectiveTarget);
        }

        public void SnapToTarget(RectTransform target)
        {
            if (viewportRt == null || worldContentRt == null || target == null)
                return;

            if (TryComputeContentPositionForTarget(target, out var pos))
                worldContentRt.anchoredPosition = pos;
        }

        /// <summary>
        /// 主角在 worldContent 局部空间位移后，同帧反向平移 content（v3.179 公会即时跟随）。
        /// 与绝对 Snap 相比，按帧 delta 补偿可避免 scale/Canvas 换算误差累积。
        /// </summary>
        public void ApplyPlayerContentDelta(Vector2 playerDeltaInContentSpace)
        {
            if (worldContentRt == null || viewportRt == null)
                return;
            if (!followEnabled || releaseTransitionActive || sowAnchorLocked || freePanActive || followFrozen)
                return;
            if (playerDeltaInContentSpace.sqrMagnitude <= 0f)
                return;

            var scale = worldContentRt.localScale;
            var viewportDelta = new Vector2(
                playerDeltaInContentSpace.x * scale.x,
                playerDeltaInContentSpace.y * scale.y);
            worldContentRt.anchoredPosition = ClampContentPosition(
                (Vector2)worldContentRt.anchoredPosition - viewportDelta);
        }

        private void OnDisable()
        {
            CancelSowCameraTransition();
        }

        private void LateUpdate()
        {
            if (!followEnabled || releaseTransitionActive || sowAnchorLocked || freePanActive ||
                followFrozen || viewportRt == null || worldContentRt == null)
                return;

            var target = EffectiveTarget;
            if (target == null)
                return;

            if (!TryComputeContentPositionForTarget(target, out var desiredContentPos))
                return;

            worldContentRt.anchoredPosition = desiredContentPos;
        }

        private IEnumerator SowReleaseTransitionCoroutine(float holdSeconds, float returnSeconds)
        {
            releaseTransitionActive = true;

            if (holdSeconds > 0f)
                yield return new WaitForSeconds(holdSeconds);

            if (!followEnabled || followTargetRt == null)
            {
                FinishReleaseTransition();
                yield break;
            }

            if (!TryComputeContentPositionForTarget(followTargetRt, out var targetPos))
            {
                FinishReleaseTransition();
                yield break;
            }

            var startPos = worldContentRt.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < returnSeconds)
            {
                if (!followEnabled)
                {
                    FinishReleaseTransition();
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / returnSeconds));
                worldContentRt.anchoredPosition = ClampContentPosition(Vector2.Lerp(startPos, targetPos, t));
                yield return null;
            }

            worldContentRt.anchoredPosition = ClampContentPosition(targetPos);
            FinishReleaseTransition();
        }

        private void FinishReleaseTransition()
        {
            StopReleaseCoroutine();
            releaseTransitionActive = false;
            sowAnchorLocked = false;
            overrideTargetRt = null;
        }

        private void StopReleaseCoroutine()
        {
            if (releaseCoroutine == null)
                return;
            StopCoroutine(releaseCoroutine);
            releaseCoroutine = null;
        }

        private bool TryComputeContentPositionForTarget(RectTransform target, out Vector2 contentPos)
        {
            contentPos = Vector2.zero;
            if (viewportRt == null || worldContentRt == null || target == null)
                return false;

            // 在 viewport 局部空间测量目标相对中心的偏移，再平移 content 抵消（自动吸收 localScale / Canvas 缩放）。
            var targetInViewport = (Vector2)viewportRt.InverseTransformPoint(target.position);
            contentPos = ClampContentPosition(
                (Vector2)worldContentRt.anchoredPosition + followOffset - targetInViewport);
            return true;
        }

        private Vector2 ClampContentPosition(Vector2 pos)
        {
            var viewSize = viewportRt.rect.size;
            var scale = worldContentRt.localScale;
            var contentSize = new Vector2(
                worldContentRt.rect.width * Mathf.Abs(scale.x),
                worldContentRt.rect.height * Mathf.Abs(scale.y));
            float halfW = Mathf.Max(0f, (contentSize.x - viewSize.x) * 0.5f);
            float halfH = Mathf.Max(0f, (contentSize.y - viewSize.y) * 0.5f);
            pos.x = Mathf.Clamp(pos.x, -halfW, halfW);
            pos.y = Mathf.Clamp(pos.y, -halfH, halfH);
            return pos;
        }

        public static Vector2 ClampHarvestFreePanPosition(Vector2 pos)
        {
            pos.x = Mathf.Clamp(pos.x, HarvestFreePanMin.x, HarvestFreePanMax.x);
            pos.y = Mathf.Clamp(pos.y, HarvestFreePanMin.y, HarvestFreePanMax.y);
            return pos;
        }
    }
}
