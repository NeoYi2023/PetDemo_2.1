#if UNITY_EDITOR
using System.IO;
using PetDemo.UI;
using UnityEditor;
using UnityEngine;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// SPEC §9.14.13：主角升级全屏面板预制体生成器。
    /// 产出 Assets/Resources/Prefabs/Farm/RoleLevelUpPanel.prefab。
    /// </summary>
    public static class RoleLevelUpPanelPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Farm";
        private const string PrefabPath = PrefabDir + "/RoleLevelUpPanel.prefab";

        [MenuItem("Tools/PetDemo/Generate Role Level Up Panel Prefab")]
        public static void Generate()
        {
            GenerateInternal();
        }

        public static void GenerateFromCommandLine()
        {
            GenerateInternal();
        }

        private static void GenerateInternal()
        {
            EnsureDir(PrefabDir);

            var root = RoleLevelUpPanelLayout.BuildRuntime(null);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (prefab != null)
            {
                Selection.activeObject = prefab;
                Debug.Log("RoleLevelUpPanel 预制件生成完成: " + PrefabPath);
            }
            else
            {
                Debug.LogError("RoleLevelUpPanel 预制件生成失败: " + PrefabPath);
            }
        }

        private static void EnsureDir(string dir)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
#endif
