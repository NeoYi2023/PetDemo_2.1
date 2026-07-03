#if UNITY_EDITOR
using System.IO;
using PetDemo.UI.Battle;
using UnityEditor;
using UnityEngine;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// SPEC §12.11.9：生成三选一技能界面预制体 SkillPickThreeModal。
    /// 产出 Assets/Resources/Prefabs/Battle/SkillPickThreeModal.prefab。
    /// 结构由运行时/编辑器共用的 SkillPickThreeModalBuilder 构建，保证运行时回退与预制体一致。
    /// </summary>
    [InitializeOnLoad]
    public static class SkillPickThreeModalPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Battle";
        private const string PrefabPath = PrefabDir + "/SkillPickThreeModal.prefab";

        static SkillPickThreeModalPrefabGenerator()
        {
            EditorApplication.delayCall += EnsurePrefabExists;
        }

        private static void EnsurePrefabExists()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (File.Exists(PrefabPath))
            {
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (existing != null)
                    return;
                UnityEngine.Debug.LogWarning(
                    "[SkillPickThreeModal] 预制体文件存在但 Unity 无法导入，将重新生成: " + PrefabPath);
            }

            Generate();
        }

        [MenuItem("Tools/PetDemo/Generate Skill Pick Three Modal Prefab")]
        public static void Generate()
        {
            EnsureDir(PrefabDir);
            BuildPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("SkillPickThreeModal 预制件生成完成: " + PrefabPath);
        }

        private static void BuildPrefab()
        {
            var root = new GameObject(SkillPickThreeModalView.PanelObjectName, typeof(RectTransform));
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var view = root.AddComponent<SkillPickThreeModalView>();
            SkillPickThreeModalBuilder.Build(rootRt, view);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            if (prefab != null)
                Selection.activeObject = prefab;
        }

        private static void EnsureDir(string dir)
        {
            if (Directory.Exists(dir))
                return;
            Directory.CreateDirectory(dir);
        }
    }
}
#endif
