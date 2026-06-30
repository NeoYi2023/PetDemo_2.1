// SPEC §9.1 (v3.93)：农田植物 Spine 展示构建（直接加载 SkeletonDataAsset）。
using System.Collections.Generic;
using PetDemo.UI;
using Spine.Unity;
using UnityEngine;

namespace PetDemo.UI.Farm
{
    public static class PlantSpineGraphicBuilder
    {
        private const string SkeletonGraphicShaderName = "Spine/SkeletonGraphic";

        private static readonly Dictionary<string, SkeletonDataAsset> sDataAssetCache =
            new Dictionary<string, SkeletonDataAsset>(16);

        // 共享 UI 材质：禁止对 Graphic.material 实例做 Destroy（会污染 Canvas 重建并触发 native SIGSEGV）。
        private static Material sSharedUiMaterial;
        private static Shader sSharedUiShader;

        public static bool TryApply(RectTransform host, string skeletonDataPath, out SkeletonGraphic graphic)
        {
            graphic = null;
            if (host == null || string.IsNullOrWhiteSpace(skeletonDataPath))
                return false;

            var dataAsset = LoadSkeletonDataAsset(skeletonDataPath.Trim());
            if (dataAsset == null)
            {
                UnityEngine.Debug.LogWarning("[PlantSpineGraphicBuilder] SkeletonDataAsset 未找到：" + skeletonDataPath);
                return false;
            }

            graphic = host.GetComponent<SkeletonGraphic>();
            if (graphic == null)
                graphic = host.gameObject.AddComponent<SkeletonGraphic>();

            var shader = Shader.Find(SkeletonGraphicShaderName);
            if (shader == null)
            {
                UnityEngine.Debug.LogWarning("[PlantSpineGraphicBuilder] Shader 未找到：" + SkeletonGraphicShaderName);
                return false;
            }

            graphic.material = GetOrCreateSharedUiMaterial(shader);

            graphic.skeletonDataAsset = dataAsset;
            graphic.initialSkinName = string.Empty;
            graphic.initialFlipX = false;
            graphic.initialFlipY = false;
            graphic.allowMultipleCanvasRenderers = NeedsMultipleCanvasRenderers(dataAsset);

            var meshGen = graphic.MeshGenerator;
            var meshSettings = meshGen.settings;
            meshSettings.pmaVertexColors = true;
            meshSettings.useClipping = true;
            meshGen.settings = meshSettings;

#if UNITY_2018_2_OR_NEWER
            var canvasRenderer = host.GetComponent<CanvasRenderer>();
            if (canvasRenderer != null)
                canvasRenderer.cullTransparentMesh = false;
#endif

            graphic.Initialize(true);
            if (!graphic.IsValid)
            {
                UnityEngine.Debug.LogWarning("[PlantSpineGraphicBuilder] SkeletonGraphic 初始化失败：" + skeletonDataPath);
                return false;
            }

            graphic.raycastTarget = false;
            return true;
        }

        public static void Hide(SkeletonGraphic graphic)
        {
            if (graphic == null)
                return;
            graphic.enabled = false;
        }

        public static void DestroyVisual(RectTransform host)
        {
            if (host == null)
                return;

            var graphic = host.GetComponent<SkeletonGraphic>();
            if (graphic != null)
            {
                graphic.material = null;
                UnityEngine.Object.Destroy(graphic);
            }

            host.gameObject.SetActive(false);
        }

        private static Material GetOrCreateSharedUiMaterial(Shader shader)
        {
            if (sSharedUiMaterial != null && sSharedUiShader == shader)
                return sSharedUiMaterial;

            sSharedUiShader = shader;
            sSharedUiMaterial = SkeletonGraphicUiMaterialFactory.CreateForPmaVertexColors(shader);
            return sSharedUiMaterial;
        }

        private static SkeletonDataAsset LoadSkeletonDataAsset(string path)
        {
            if (sDataAssetCache.TryGetValue(path, out var cached) && cached != null)
                return cached;

            var asset = Resources.Load<SkeletonDataAsset>(path);
            if (asset != null)
                sDataAssetCache[path] = asset;
            return asset;
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

    }
}
