// SPEC §9.10.5：精灵背包上场槽 Spine 预览构建（复用 PetCompanionPresenter 约定）。
using System;
using PetDemo.Core;
using PetDemo.UI;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace PetDemo.UI.Farm
{
    public static class PetBagSpineGraphicBuilder
    {
        private const string SkeletonGraphicShaderName = "Spine/SkeletonGraphic";

        private struct PrefabSkeletonSource
        {
            public SkeletonDataAsset DataAsset;
            public string InitialSkinName;
            public bool InitialFlipX;
            public bool InitialFlipY;
            public bool PmaVertexColors;
            public bool UseClipping;
        }

        /// <summary>战斗上场精灵（§12.3）：与家园伴侣相同屏幕半区 ScaleX 朝向路径。</summary>
        public static SkeletonGraphic TryBuildBattleDeployedVisual(
            RectTransform slotParent, PetConfig petConfig, Vector2 sizeDelta, Vector3 baseScale)
        {
            return TryBuild(
                slotParent, petConfig, sizeDelta, baseScale,
                applyFantaziaUiMirror: true,
                attachToHost: false,
                useCompanionMirrorHost: true);
        }

        public static SkeletonGraphic TryBuild(
            RectTransform parent, PetConfig petConfig, Vector2 sizeDelta, Vector3 localScale,
            bool applyFantaziaUiMirror = true,
            bool attachToHost = false,
            bool useCompanionMirrorHost = false)
        {
            if (parent == null || petConfig == null || string.IsNullOrEmpty(petConfig.prefabResource))
                return null;

            var prefab = Resources.Load<GameObject>(petConfig.prefabResource);
            if (prefab == null)
            {
                UnityEngine.Debug.LogWarning("[PetBagSpineGraphicBuilder] 加载预制体失败：" + petConfig.prefabResource);
                return null;
            }

            if (!TryReadPrefabSkeletonSource(prefab, out var src))
                return null;

            var shader = Shader.Find(SkeletonGraphicShaderName);
            if (shader == null)
            {
                UnityEngine.Debug.LogWarning("[PetBagSpineGraphicBuilder] Shader 未找到：" + SkeletonGraphicShaderName);
                return null;
            }

            RectTransform skeletonHost;
            if (attachToHost)
            {
                skeletonHost = parent;
                skeletonHost.anchorMin = new Vector2(0.5f, 0.5f);
                skeletonHost.anchorMax = new Vector2(0.5f, 0.5f);
                skeletonHost.pivot = new Vector2(0.5f, 0.5f);
                skeletonHost.sizeDelta = sizeDelta;
            }
            else
            {
                string childName = useCompanionMirrorHost ? "PetBattleVisual" : "PetPreview";
                var previewRt = new GameObject(childName, typeof(RectTransform)).GetComponent<RectTransform>();
                previewRt.SetParent(parent, false);
                previewRt.anchorMin = new Vector2(0.5f, 0.5f);
                previewRt.anchorMax = new Vector2(0.5f, 0.5f);
                previewRt.pivot = new Vector2(0.5f, 0.5f);
                previewRt.anchoredPosition = Vector2.zero;
                previewRt.sizeDelta = sizeDelta;
                previewRt.localScale = applyFantaziaUiMirror
                    ? FantaziaMonsterDisplay.BoostScaleXY(localScale)
                    : localScale;
                skeletonHost = previewRt;
            }

            Canvas.ForceUpdateCanvases();
            bool spawnOnLeftHalf = FantaziaMonsterDisplay.IsSpawnOnScreenLeftHalf(parent);

            var uiMaterial = SkeletonGraphicUiMaterialFactory.CreateForPmaVertexColors(shader);
            var sg = skeletonHost.gameObject.AddComponent<SkeletonGraphic>();
            sg.material = uiMaterial;
            sg.skeletonDataAsset = src.DataAsset;
            sg.initialSkinName = src.InitialSkinName ?? string.Empty;
            sg.initialFlipX = spawnOnLeftHalf;
            sg.initialFlipY = src.InitialFlipY;
            sg.allowMultipleCanvasRenderers = NeedsMultipleCanvasRenderers(src.DataAsset);

            var meshGen = sg.MeshGenerator;
            var meshSettings = meshGen.settings;
            meshSettings.pmaVertexColors = src.PmaVertexColors;
            meshSettings.useClipping = src.UseClipping;
            meshGen.settings = meshSettings;

#if UNITY_2018_2_OR_NEWER
            var canvasRenderer = skeletonHost.GetComponent<CanvasRenderer>();
            if (canvasRenderer != null)
                canvasRenderer.cullTransparentMesh = false;
#endif

            sg.Initialize(false);
            if (!sg.IsValid)
            {
                if (!attachToHost)
                    UnityEngine.Object.Destroy(skeletonHost.gameObject);
                else
                    UnityEngine.Object.Destroy(sg);
                return null;
            }

            sg.raycastTarget = false;
            string idle = ResolveIdleAnimationName(sg, petConfig);
            if (!string.IsNullOrEmpty(idle))
            {
                try
                {
                    sg.AnimationState.SetAnimation(0, idle, true);
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning("[PetBagSpineGraphicBuilder] idle 动画失败：" + e.Message);
                }
            }

            if (applyFantaziaUiMirror)
                FantaziaMonsterDisplay.ConfigureFantaziaSkeletonGraphicUi(skeletonHost, sg, localScale, parent);

            return sg;
        }

        private static string ResolveIdleAnimationName(SkeletonGraphic sg, PetConfig cfg)
        {
            var data = sg?.SkeletonData;
            if (data == null)
                return null;

            var idle = FindAnimationCaseInsensitive(data, "idle");
            if (idle != null)
                return idle.Name;

            if (cfg?.randomAnimations != null)
            {
                for (int i = 0; i < cfg.randomAnimations.Count; i++)
                {
                    var a = FindAnimationCaseInsensitive(data, cfg.randomAnimations[i]);
                    if (a != null)
                        return a.Name;
                }
            }

            if (data.Animations != null && data.Animations.Count > 0)
            {
                var first = data.Animations.Items[0];
                if (first != null)
                    return first.Name;
            }

            return null;
        }

        private static bool NeedsMultipleCanvasRenderers(SkeletonDataAsset asset)
        {
            if (asset?.atlasAssets == null || asset.atlasAssets.Length == 0)
                return false;
            if (asset.atlasAssets.Length > 1)
                return true;
            var first = asset.atlasAssets[0];
            return first != null && first.MaterialCount > 1;
        }

        private static bool TryReadPrefabSkeletonSource(GameObject prefab, out PrefabSkeletonSource src)
        {
            src = default;
            var probe = UnityEngine.Object.Instantiate(prefab);
            probe.SetActive(false);
            if (CountMissingMonoScripts(probe) > 0)
            {
                UnityEngine.Object.Destroy(probe);
                return false;
            }

            var anim = probe.GetComponent<SkeletonAnimation>()
                ?? probe.GetComponentInChildren<SkeletonAnimation>(true);
            if (anim == null || anim.skeletonDataAsset == null)
            {
                UnityEngine.Object.Destroy(probe);
                return false;
            }

            src.DataAsset = anim.skeletonDataAsset;
            src.InitialSkinName = anim.initialSkinName;
            src.InitialFlipX = anim.initialFlipX;
            src.InitialFlipY = anim.initialFlipY;
            src.PmaVertexColors = anim.pmaVertexColors;
            src.UseClipping = anim.useClipping;
            UnityEngine.Object.Destroy(probe);
            return true;
        }

        private static Spine.Animation FindAnimationCaseInsensitive(SkeletonData data, string animationName)
        {
            if (data == null || string.IsNullOrEmpty(animationName))
                return null;
            var exact = data.FindAnimation(animationName);
            if (exact != null)
                return exact;
            var anims = data.Animations;
            if (anims == null)
                return null;
            for (int i = 0; i < anims.Count; i++)
            {
                var a = anims.Items[i];
                if (a != null && string.Equals(a.Name, animationName, StringComparison.OrdinalIgnoreCase))
                    return a;
            }
            return null;
        }

        private static int CountMissingMonoScripts(GameObject go)
        {
            var components = go.GetComponentsInChildren<MonoBehaviour>(true);
            int missing = 0;
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                    missing++;
            }
            return missing;
        }
    }
}
