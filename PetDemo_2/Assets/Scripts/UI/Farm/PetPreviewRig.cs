// PetPreviewRig：精灵预览的渲染装置（SPEC §4.1.10.5 v3.18）。
// 单例 MonoBehaviour，懒加载创建一组「Camera + RenderTexture + Stage」用于把 Spine 预制体（SkeletonAnimation）
// 的 MeshRenderer 输出到 RawImage。布置在世界坐标远端 + 「PetPreview」Layer 双重隔离，避免与主场景互相污染。
// 若 Layer "PetPreview" 不存在，回退到 Default Layer 但仍使用远端坐标避开主场景。
// v3.51：Fantazia 根节点 XY ×1.2；v3.79：左半屏 Skeleton.ScaleX 取负（变异弹窗预览在左侧）。
using System.Collections.Generic;
using PetDemo.Core;
using PetDemo.UI;
using Spine.Unity;
using UnityEngine;

namespace PetDemo.UI.Farm
{
    public class PetPreviewRig : MonoBehaviour
    {
        public const string PreviewLayerName = "PetPreview";
        // 远端世界坐标，避开主场景内容；若主场景未来扩展到此区域，再调整。
        private static readonly Vector3 StageOriginWorld = new Vector3(10000f, 10000f, 0f);
        private const int RenderTextureSize = 512;
        private const float OrthographicSize = 1.5f;

        public static PetPreviewRig Instance { get; private set; }

        private Camera previewCamera;
        private RenderTexture renderTexture;
        private Transform stageRoot;
        private GameObject currentInstance;

        public RenderTexture Texture => renderTexture;

        public static PetPreviewRig EnsureCreated()
        {
            if (Instance != null)
                return Instance;
            var go = new GameObject("PetPreviewRig");
            DontDestroyOnLoad(go);
            var rig = go.AddComponent<PetPreviewRig>();
            rig.BuildRig();
            Instance = rig;
            return rig;
        }

        private void BuildRig()
        {
            renderTexture = new RenderTexture(RenderTextureSize, RenderTextureSize, 16, RenderTextureFormat.ARGB32);
            renderTexture.name = "PetPreviewRT";

            int previewLayer = LayerMask.NameToLayer(PreviewLayerName);
            if (previewLayer < 0)
                previewLayer = 0; // 回退到 Default

            var camGo = new GameObject("PetPreviewCamera");
            camGo.transform.SetParent(transform, false);
            camGo.transform.position = StageOriginWorld + new Vector3(0f, 0f, -10f);
            previewCamera = camGo.AddComponent<Camera>();
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = OrthographicSize;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            previewCamera.targetTexture = renderTexture;
            previewCamera.cullingMask = (previewLayer >= 0) ? (1 << previewLayer) : ~0;
            previewCamera.depth = -50;
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = false;

            var stageGo = new GameObject("PetStage");
            stageGo.transform.SetParent(transform, false);
            stageGo.transform.position = StageOriginWorld;
            stageRoot = stageGo.transform;
        }

        public void Hide()
        {
            DestroyCurrentInstance();
            if (previewCamera != null)
                previewCamera.enabled = false;
        }

        /// <summary>
        /// 实例化精灵预制体并播放随机动画；返回供 RawImage 使用的 RenderTexture。
        /// 若加载失败，返回 null（弹窗 View 应做空检查）。
        /// </summary>
        public RenderTexture ShowPet(PetConfig petConfig)
        {
            if (petConfig == null || string.IsNullOrEmpty(petConfig.prefabResource))
                return null;

            DestroyCurrentInstance();

            var prefab = Resources.Load<GameObject>(petConfig.prefabResource);
            if (prefab == null)
            {
                UnityEngine.Debug.LogWarning("[PetPreviewRig] 加载预制体失败：" + petConfig.prefabResource);
                return null;
            }

            currentInstance = Instantiate(prefab, stageRoot);
            currentInstance.transform.localPosition = Vector3.zero;
            // 部分 Spine 预制体自带 0.35 缩放，预览相机的 ortho size 已按比例适配。
            currentInstance.transform.localScale =
                FantaziaMonsterDisplay.BoostScaleXY(currentInstance.transform.localScale);
            ApplyLayerRecursively(currentInstance, PreviewLayerName);

            var sa = currentInstance.GetComponentInChildren<SkeletonAnimation>(true);
            // 变异弹窗 RawImage 在面板左侧，世界舞台固定于屏外，按产品约定视为左半屏出生。
            if (sa != null)
                FantaziaMonsterDisplay.ApplySkeletonAnimationScaleXFacing(sa, spawnOnLeftHalf: true);

            if (sa != null)
            {
                string animName = PickAnimationName(sa, petConfig.randomAnimations);
                if (!string.IsNullOrEmpty(animName))
                {
                    try
                    {
                        sa.AnimationState.SetAnimation(0, animName, true);
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogWarning($"[PetPreviewRig] 动画 '{animName}' 设置失败，使用预制体默认。{e.Message}");
                    }
                }
            }

            if (previewCamera != null)
                previewCamera.enabled = true;
            return renderTexture;
        }

        private void DestroyCurrentInstance()
        {
            if (currentInstance != null)
            {
                Destroy(currentInstance);
                currentInstance = null;
            }
        }

        private static void ApplyLayerRecursively(GameObject root, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
                return;
            var queue = new Queue<Transform>();
            queue.Enqueue(root.transform);
            while (queue.Count > 0)
            {
                var t = queue.Dequeue();
                t.gameObject.layer = layer;
                for (int i = 0; i < t.childCount; i++)
                    queue.Enqueue(t.GetChild(i));
            }
        }

        private static string PickAnimationName(SkeletonAnimation sa, List<string> candidates)
        {
            if (candidates != null && candidates.Count > 0)
            {
                int idx = Random.Range(0, candidates.Count);
                return candidates[idx];
            }
            // 回退：若 candidates 为空，挑选 SkeletonData 的第一个动画（与 InvasionBattleView 同方法）。
            if (sa != null && sa.Skeleton != null && sa.Skeleton.Data != null)
            {
                var animations = sa.Skeleton.Data.Animations;
                if (animations != null && animations.Count > 0)
                {
                    var first = animations.Items[0];
                    if (first != null)
                        return first.Name;
                }
            }
            return null;
        }
    }
}
