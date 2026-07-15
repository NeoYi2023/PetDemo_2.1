// SPEC §9.8.9.6 ② / §9.8.9.9（v3.257）：公会场景角色 Spine 构建工具 —
// 任意 Resources 探针路径缓存 + LangRen/LangMeiRen 枚举回退；缺资源回退占位色块。
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    public enum GuildNpcSkeletonKind
    {
        LangRen,
        LangMeiRen,
    }

    public static class GuildSpineCharacterBuilder
    {
        public const string LangRenResourcesPrefabPath = "Prefabs/Air/Hero_Role_cunmin";
        public const string LangMeiRenResourcesPrefabPath = "Prefabs/Air/Hero_Role_langmeiren";

        // 历史兼容名（同 LangRen）。
        public const string VillagerResourcesPrefabPath = LangRenResourcesPrefabPath;

        private const string SkeletonGraphicShaderName = "Spine/SkeletonGraphic";

        // 与 MainRoleCunminPresenter 的主界面默认观感一致。
        public static readonly Vector2 DefaultGraphicSize = new Vector2(720f, 1200f);
        public static readonly Vector3 DefaultLocalScale = new Vector3(0.53f, 0.53f, 1f);
        /// <summary>公会场景主角 GuildPlayer 专用缩放（NPC 仍用 DefaultLocalScale）。</summary>
        public static readonly Vector3 GuildPlayerLocalScale = new Vector3(0.27f, 0.27f, 1f);

        private static readonly Dictionary<string, SkeletonDataAsset> sPathDataAssetCache =
            new Dictionary<string, SkeletonDataAsset>();
        private static readonly HashSet<string> sPathProbed = new HashSet<string>();

        /// <summary>
        /// 在 parent 下构建村民角色（SkeletonGraphic，默认 LangRen）；失败时回退为占位色块。
        /// SPEC §9.8.9.4（v3.247）：可选 dataAssetOverride（装扮装备路径），空则回退默认 LangRen。
        /// </summary>
        public static RectTransform BuildVillager(
            RectTransform parent,
            string name,
            Vector2 anchoredPosition,
            out SkeletonGraphic skeletonGraphic,
            Vector3? localScale = null,
            SkeletonDataAsset dataAssetOverride = null)
        {
            return BuildCharacter(
                parent, GuildNpcSkeletonKind.LangRen, name, anchoredPosition, out skeletonGraphic,
                localScale, dataAssetOverride);
        }

        /// <summary>
        /// 在 parent 下按骨骼类型构建角色（SkeletonGraphic）；失败时回退为占位色块（skeletonGraphic 返回 null）。
        /// </summary>
        public static RectTransform BuildCharacter(
            RectTransform parent,
            GuildNpcSkeletonKind skeletonKind,
            string name,
            Vector2 anchoredPosition,
            out SkeletonGraphic skeletonGraphic,
            Vector3? localScale = null,
            SkeletonDataAsset dataAssetOverride = null)
        {
            skeletonGraphic = null;

            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = DefaultGraphicSize;
            rt.localRotation = Quaternion.identity;
            rt.localScale = localScale ?? DefaultLocalScale;

            var dataAsset = dataAssetOverride != null
                ? dataAssetOverride
                : ResolveSkeletonDataAsset(skeletonKind);
            if (!TryAttachSkeletonGraphic(rt, dataAsset, out skeletonGraphic))
            {
                UnityEngine.Debug.LogWarning(
                    "[GuildSpineCharacterBuilder] Spine 不可用（kind=" + skeletonKind +
                    ", dataAsset=" + (dataAsset != null) + "），使用占位色块。");
                BuildFallbackBlock(rt);
            }

            return rt;
        }

        /// <summary>
        /// SPEC §9.8.9.4（v3.247）：就地替换已有角色节点上的 SkeletonData（保留位置/缩放/朝向）。
        /// </summary>
        public static bool TryReplaceSkeletonData(
            RectTransform roleRt, SkeletonDataAsset dataAsset, out SkeletonGraphic skeletonGraphic)
        {
            skeletonGraphic = null;
            if (roleRt == null || dataAsset == null)
                return false;

            var fallback = roleRt.Find("FallbackBlock");
            if (fallback != null)
                Object.Destroy(fallback.gameObject);

            var existing = roleRt.GetComponent<SkeletonGraphic>();
            if (existing != null)
            {
                existing.skeletonDataAsset = dataAsset;
                existing.Initialize(true);
                if (existing.IsValid)
                {
                    existing.raycastTarget = false;
                    skeletonGraphic = existing;
                    return true;
                }

                Object.Destroy(existing);
            }

            return TryAttachSkeletonGraphic(roleRt, dataAsset, out skeletonGraphic);
        }

        private static bool TryAttachSkeletonGraphic(
            RectTransform roleRt, SkeletonDataAsset dataAsset, out SkeletonGraphic skeletonGraphic)
        {
            skeletonGraphic = null;
            if (roleRt == null || dataAsset == null)
                return false;

            var shader = Shader.Find(SkeletonGraphicShaderName);
            if (shader == null)
                return false;

            var uiMaterial = SkeletonGraphicUiMaterialFactory.CreateForPmaVertexColors(shader);
            skeletonGraphic = SkeletonGraphic.AddSkeletonGraphicComponent(roleRt.gameObject, dataAsset, uiMaterial);
            if (skeletonGraphic == null || !skeletonGraphic.IsValid)
            {
                skeletonGraphic = null;
                return false;
            }

            skeletonGraphic.raycastTarget = false;
            return true;
        }

        public static SkeletonDataAsset ResolveSkeletonDataAsset(GuildNpcSkeletonKind kind)
        {
            return kind == GuildNpcSkeletonKind.LangMeiRen
                ? ResolveSkeletonDataAssetFromPrefabPath(LangMeiRenResourcesPrefabPath)
                : ResolveSkeletonDataAssetFromPrefabPath(LangRenResourcesPrefabPath);
        }

        /// <summary>
        /// SPEC §9.8.9.9（v3.257）：按 Resources 预制体路径探针取 SkeletonDataAsset（带缓存）；
        /// 空路径 / 缺预制体 / 无 SkeletonAnimation 返回 null。
        /// </summary>
        public static SkeletonDataAsset ResolveSkeletonDataAssetFromPrefabPath(string resourcesPath)
        {
            if (string.IsNullOrEmpty(resourcesPath))
                return null;

            if (sPathDataAssetCache.TryGetValue(resourcesPath, out var cached) && cached != null)
                return cached;

            if (sPathProbed.Contains(resourcesPath))
                return null;

            sPathProbed.Add(resourcesPath);

            var prefab = Resources.Load<GameObject>(resourcesPath);
            if (prefab == null)
                return null;

            var probe = Object.Instantiate(prefab);
            probe.SetActive(false);
            var srcAnim = probe.GetComponent<SkeletonAnimation>()
                ?? probe.GetComponentInChildren<SkeletonAnimation>(true);
            var dataAsset = srcAnim != null ? srcAnim.skeletonDataAsset : null;
            Object.Destroy(probe);

            if (dataAsset != null)
                sPathDataAssetCache[resourcesPath] = dataAsset;
            return dataAsset;
        }

        /// <summary>按配置名 + 回退序列播放循环动画；全部缺失时取首个动画。</summary>
        public static void PlayLoop(SkeletonGraphic sg, string configured, params string[] fallbacks)
        {
            if (sg == null || sg.Skeleton == null || sg.Skeleton.Data == null)
                return;

            var clip = ResolveClipName(sg, configured, fallbacks);
            if (!string.IsNullOrEmpty(clip))
                sg.AnimationState.SetAnimation(0, clip, true);
        }

        /// <summary>播放单次动画；动画缺失时返回 null 并打 Warning。</summary>
        public static TrackEntry PlayOnce(SkeletonGraphic sg, string clipName)
        {
            if (sg == null || sg.Skeleton == null || sg.Skeleton.Data == null)
                return null;

            var resolved = ResolveClipName(sg, clipName);
            if (string.IsNullOrEmpty(resolved))
            {
                UnityEngine.Debug.LogWarning(
                    "[GuildSpineCharacterBuilder] 动画未找到，跳过单次播放：" + clipName);
                return null;
            }

            try
            {
                return sg.AnimationState.SetAnimation(0, resolved, false);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("[GuildSpineCharacterBuilder] 单次动画失败：" + e.Message);
                return null;
            }
        }

        /// <summary>
        /// SPEC §9.8.9.15 (v3.229)：单次播放 onceClip 后队列衔接 loopConfigured 循环动画。
        /// onceClip 缺失时直接循环 loopConfigured（按回退序列解析）。
        /// </summary>
        public static void PlayOnceThenLoop(
            SkeletonGraphic sg, string onceClip, string loopConfigured, params string[] loopFallbacks)
        {
            if (sg == null || sg.Skeleton == null || sg.Skeleton.Data == null)
                return;

            var loop = ResolveClipName(sg, loopConfigured, loopFallbacks);
            var once = ResolveClipName(sg, onceClip);

            try
            {
                if (string.IsNullOrEmpty(once))
                {
                    if (!string.IsNullOrEmpty(loop))
                        sg.AnimationState.SetAnimation(0, loop, true);
                    return;
                }

                sg.AnimationState.SetAnimation(0, once, false);
                if (!string.IsNullOrEmpty(loop))
                    sg.AnimationState.AddAnimation(0, loop, true, 0f);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning(
                    "[GuildSpineCharacterBuilder] 单次+循环动画失败：" + e.Message);
                if (!string.IsNullOrEmpty(loop))
                    sg.AnimationState.SetAnimation(0, loop, true);
            }
        }

        /// <summary>村民默认骨骼面朝左；faceRight=true 时水平镜像。</summary>
        public static void SetFacing(RectTransform roleRt, bool faceRight)
        {
            if (roleRt == null)
                return;
            var s = roleRt.localScale;
            float ax = Mathf.Abs(s.x);
            roleRt.localScale = new Vector3(faceRight ? -ax : ax, s.y, s.z);
        }

        private static string ResolveClipName(SkeletonGraphic sg, string configured, string[] fallbacks)
        {
            if (!string.IsNullOrEmpty(configured) && HasAnimation(sg, configured))
                return configured;
            if (fallbacks != null)
            {
                for (int i = 0; i < fallbacks.Length; i++)
                {
                    if (!string.IsNullOrEmpty(fallbacks[i]) && HasAnimation(sg, fallbacks[i]))
                        return fallbacks[i];
                }
            }

            var anims = sg.Skeleton.Data.Animations;
            if (anims != null && anims.Count > 0 && anims.Items[0] != null)
                return anims.Items[0].Name;
            return null;
        }

        private static string ResolveClipName(SkeletonGraphic sg, string clipName)
        {
            if (sg == null || sg.Skeleton == null || sg.Skeleton.Data == null)
                return null;

            var data = sg.Skeleton.Data;
            if (string.IsNullOrEmpty(clipName))
                return null;

            var exact = data.FindAnimation(clipName);
            if (exact != null)
                return exact.Name;

            var anims = data.Animations;
            if (anims == null)
                return null;

            for (int i = 0; i < anims.Count; i++)
            {
                var anim = anims.Items[i];
                if (anim != null && string.Equals(anim.Name, clipName, System.StringComparison.OrdinalIgnoreCase))
                    return anim.Name;
            }

            return null;
        }

        private static bool HasAnimation(SkeletonGraphic sg, string animationName)
        {
            return sg.Skeleton.Data.FindAnimation(animationName) != null;
        }

        private static void BuildFallbackBlock(RectTransform roleRt)
        {
            var go = new GameObject("FallbackBlock", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(roleRt, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(220f, 320f);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.55f, 0.45f, 0.7f, 0.9f);
            img.raycastTarget = false;
        }
    }
}
