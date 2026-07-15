// SPEC §9.14.9（v3.244 / v3.245）：解析玩家已装备 / 默认主角 SkeletonDataAsset。
// 支持两类路径：
// 1) Resources 相对路径（无扩展名），例 Spines/Role_cslieren/Role_cslieren_SkeletonData
// 2) 工程 Assets/ 路径（Editor；可带 .asset），例 Assets/Scenes/Air/Role_cslieren/.../Role_cslieren_SkeletonData.asset
using Spine.Unity;
using UnityEngine;

namespace PetDemo.Core
{
    public static class PlayerSpineAppearanceResolver
    {
        public const string DefaultHeroPrefabResourcesPath = "Prefabs/Air/Hero_Role_cunmin";
        private const string DefaultHeroPrefabEditorPath = "Assets/Scenes/Air/Role/Hero_Role_cunmin.prefab";

        /// <summary>
        /// 按会话装备路径解析 SkeletonDataAsset；path 空或加载失败时回退默认主角预制体探针。
        /// </summary>
        public static SkeletonDataAsset Resolve(string equippedSpinePath)
        {
            if (!string.IsNullOrEmpty(equippedSpinePath))
            {
                var equipped = TryLoadSkeletonData(equippedSpinePath);
                if (equipped != null)
                    return equipped;

                Debug.LogWarning(
                    "[PlayerSpineAppearanceResolver] 无法加载装备 Spine：" + equippedSpinePath.Trim() +
                    "，回退默认主角骨骼。");
            }

            return ResolveDefaultHeroSkeleton();
        }

        /// <summary>
        /// 尝试加载装备路径（不回退默认）。用于 TryEquip 校验是否可装备。
        /// </summary>
        public static SkeletonDataAsset TryLoadSkeletonData(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            string normalized = path.Trim().Replace('\\', '/');
            if (normalized.Length == 0)
                return null;

            // 工程 Assets/ 路径（Editor）：须带或不带 .asset，统一补全。
            if (normalized.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
#if UNITY_EDITOR
                string assetPath = normalized;
                if (!assetPath.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase))
                    assetPath += ".asset";
                return UnityEditor.AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(assetPath);
#else
                Debug.LogWarning(
                    "[PlayerSpineAppearanceResolver] Assets/ 工程路径仅 Editor 可用：" + normalized);
                return null;
#endif
            }

            // Resources 相对路径：去掉可选 Resources/ 前缀与 .asset 后缀。
            string resourcesPath = normalized;
            if (resourcesPath.StartsWith("Resources/", System.StringComparison.OrdinalIgnoreCase))
                resourcesPath = resourcesPath.Substring("Resources/".Length);
            if (resourcesPath.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase))
                resourcesPath = resourcesPath.Substring(0, resourcesPath.Length - ".asset".Length);

            return Resources.Load<SkeletonDataAsset>(resourcesPath);
        }

        public static SkeletonDataAsset ResolveDefaultHeroSkeleton()
        {
            var prefab = Resources.Load<GameObject>(DefaultHeroPrefabResourcesPath);
            var fromPrefab = ExtractSkeletonData(prefab);
            if (fromPrefab != null)
                return fromPrefab;

#if UNITY_EDITOR
            var editorPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DefaultHeroPrefabEditorPath);
            return ExtractSkeletonData(editorPrefab);
#else
            return null;
#endif
        }

        private static SkeletonDataAsset ExtractSkeletonData(GameObject prefab)
        {
            if (prefab == null)
                return null;

            var sg = prefab.GetComponentInChildren<SkeletonGraphic>(true);
            if (sg != null && sg.skeletonDataAsset != null)
                return sg.skeletonDataAsset;

            var anim = prefab.GetComponentInChildren<SkeletonAnimation>(true);
            if (anim != null && anim.skeletonDataAsset != null)
                return anim.skeletonDataAsset;

            return null;
        }
    }
}
