// SPEC §4.1.10.5 / §9.5.1.3 / §12.3 / 附录 B.11（v3.79）：Fantazia 精灵 UI 展示（+20% 缩放 + 按屏幕半区 ScaleX 朝向）。
using Spine.Unity;
using UnityEngine;

namespace PetDemo.UI
{
    public static class FantaziaMonsterDisplay
    {
        /// <summary>Fantazia 包怪物在朝向调整前的统一视觉放大：相对预制体/基底缩放的 XY 乘数（+20% = 1.2）。</summary>
        public const float PackVisualScaleMultiplier = 1.2f;

        /// <summary>将当前 localScale 在 X 轴上镜像（幅度取绝对值后加负号）。用于战斗敌方等非精灵 Transform 路径。</summary>
        public static void ApplyHorizontalMirror(Transform t)
        {
            if (t == null)
                return;
            Vector3 s = t.localScale;
            t.localScale = HorizontallyMirroredScale(s);
        }

        /// <summary>先将根节点 XY 乘以 <see cref="PackVisualScaleMultiplier"/>，再水平镜像（战斗敌方等 Transform 路径）。</summary>
        public static void ApplyBoostAndHorizontalMirror(Transform t)
        {
            if (t == null)
                return;
            t.localScale = BoostedHorizontallyMirroredScale(t.localScale);
        }

        public static Vector3 HorizontallyMirroredScale(Vector3 scale)
        {
            return new Vector3(-Mathf.Abs(scale.x), scale.y, scale.z);
        }

        public static Vector3 BoostScaleXY(Vector3 scale, float multiplier = PackVisualScaleMultiplier)
        {
            return new Vector3(scale.x * multiplier, scale.y * multiplier, scale.z);
        }

        public static Vector3 BoostedHorizontallyMirroredScale(Vector3 baseScale)
        {
            return HorizontallyMirroredScale(BoostScaleXY(baseScale));
        }

        /// <summary>均匀 XY 缩放（如战斗敌方槽）先 ×<see cref="PackVisualScaleMultiplier"/> 再镜像（Transform 路径）。</summary>
        public static Vector3 BoostedMirroredUniform(float uniformXY, float z = 1f)
        {
            float u = uniformXY * PackVisualScaleMultiplier;
            return HorizontallyMirroredScale(new Vector3(u, u, z));
        }

        /// <summary>
        /// 出生/展示锚点中心是否落在根 Canvas 左半区（相对根 Canvas 中心，x &lt; 0）。
        /// 使用 Canvas 局部坐标，避免 Screen.width 与 Game 视图不一致导致误判。
        /// </summary>
        public static bool IsSpawnOnScreenLeftHalf(RectTransform facingAnchor)
        {
            if (facingAnchor == null)
                return true;

            Canvas.ForceUpdateCanvases();

            var canvas = facingAnchor.GetComponentInParent<Canvas>();
            var rootRt = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            if (rootRt != null)
            {
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(rootRt, facingAnchor);
                return bounds.center.x < 0f;
            }

            var corners = new Vector3[4];
            facingAnchor.GetWorldCorners(corners);
            var center = (corners[0] + corners[2]) * 0.5f;
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, center);
            return screenPoint.x < Screen.width * 0.5f;
        }

        /// <summary>
        /// Fantazia + SkeletonGraphic（Canvas UI）：正缩放 + 按屏幕半区设置朝向（左：ScaleX 负 + 顶点镜像）。
        /// </summary>
        public static void ConfigureFantaziaSkeletonGraphicUi(
            RectTransform host, SkeletonGraphic skeletonGraphic, Vector3 baseScale,
            RectTransform facingAnchor = null)
        {
            if (host == null || skeletonGraphic == null)
                return;

            host.localScale = BoostScaleXY(baseScale);

            var anchor = facingAnchor != null ? facingAnchor : host;
            bool spawnOnLeftHalf = IsSpawnOnScreenLeftHalf(anchor);
            ApplySkeletonGraphicScaleXFacing(skeletonGraphic, spawnOnLeftHalf);
            PetSkeletonGraphicFacingEnforcer.AttachOrRefresh(skeletonGraphic, anchor);
        }

        /// <summary>按当前锚点屏幕半区刷新精灵 SkeletonGraphic 朝向。</summary>
        public static void RefreshPetSkeletonGraphicFacing(SkeletonGraphic skeletonGraphic, RectTransform facingAnchor)
        {
            if (skeletonGraphic == null)
                return;

            var anchor = facingAnchor != null ? facingAnchor : skeletonGraphic.rectTransform;
            ApplySkeletonGraphicScaleXFacing(skeletonGraphic, IsSpawnOnScreenLeftHalf(anchor));
            PetSkeletonGraphicFacingEnforcer.AttachOrRefresh(skeletonGraphic, anchor);
        }

        /// <summary>左半屏：Skeleton.ScaleX 取负 + 顶点镜像；右半屏：正 ScaleX，移除顶点镜像。</summary>
        public static void ApplySkeletonGraphicScaleXFacing(SkeletonGraphic skeletonGraphic, bool spawnOnLeftHalf)
        {
            if (skeletonGraphic?.Skeleton == null)
                return;

            skeletonGraphic.initialFlipX = spawnOnLeftHalf;
            skeletonGraphic.initialFlipY = false;

            float magnitude = Mathf.Abs(skeletonGraphic.Skeleton.ScaleX);
            if (magnitude < 0.0001f)
                magnitude = 1f;

            skeletonGraphic.Skeleton.ScaleX = spawnOnLeftHalf ? -magnitude : magnitude;
            skeletonGraphic.Skeleton.ScaleY = Mathf.Abs(skeletonGraphic.Skeleton.ScaleY);
            skeletonGraphic.Skeleton.UpdateWorldTransform();

            if (spawnOnLeftHalf)
                EnsureVertexMirror(skeletonGraphic);
            else
                RemoveVertexMirrorIfPresent(skeletonGraphic);

            if (skeletonGraphic.IsValid)
                skeletonGraphic.UpdateMesh();
        }

        /// <summary>世界空间 SkeletonAnimation（PetPreviewRig）：左半屏 ScaleX 取负，右半屏不变。</summary>
        public static void ApplySkeletonAnimationScaleXFacing(SkeletonAnimation skeletonAnimation, bool spawnOnLeftHalf)
        {
            if (skeletonAnimation?.Skeleton == null)
                return;

            float magnitude = Mathf.Abs(skeletonAnimation.Skeleton.ScaleX);
            if (magnitude < 0.0001f)
                magnitude = 1f;

            skeletonAnimation.Skeleton.ScaleX = spawnOnLeftHalf ? -magnitude : magnitude;
            skeletonAnimation.Skeleton.UpdateWorldTransform();
        }

        /// <summary>战斗上场精灵朝向（与 <see cref="ConfigureFantaziaSkeletonGraphicUi"/> 相同）。</summary>
        public static void ApplyBattleDeployedFacing(
            SkeletonGraphic skeletonGraphic, Vector3 baseScale, RectTransform facingAnchor)
        {
            if (skeletonGraphic == null)
                return;
            ConfigureFantaziaSkeletonGraphicUi(
                skeletonGraphic.rectTransform, skeletonGraphic, baseScale, facingAnchor);
        }

        private static void EnsureVertexMirror(SkeletonGraphic skeletonGraphic)
        {
            var mirror = skeletonGraphic.GetComponent<SkeletonGraphicVertexMirror>();
            if (mirror == null)
                mirror = skeletonGraphic.gameObject.AddComponent<SkeletonGraphicVertexMirror>();
            mirror.Bind(skeletonGraphic);
        }

        private static void RemoveVertexMirrorIfPresent(SkeletonGraphic skeletonGraphic)
        {
            var vertexMirror = skeletonGraphic.GetComponent<SkeletonGraphicVertexMirror>();
            if (vertexMirror == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(vertexMirror);
            else
                Object.DestroyImmediate(vertexMirror);
        }
    }
}
