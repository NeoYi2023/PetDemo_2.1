#if UNITY_EDITOR
using System.IO;
using PetDemo.UI;
using UnityEditor;
using UnityEngine;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// SPEC §9.14.12：训练页签面板预制体生成器。
    /// 产出 Assets/Resources/Prefabs/Farm/TrainingPanel.prefab。
    /// </summary>
    public static class TrainingPanelPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Farm";
        private const string PrefabPath = PrefabDir + "/TrainingPanel.prefab";

        [MenuItem("Tools/PetDemo/Generate Training Panel Prefab")]
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

            var root = TrainingPanelLayout.BuildRuntime(null);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (prefab != null)
            {
                Selection.activeObject = prefab;
                UnityEngine.Debug.Log("TrainingPanel 预制件生成完成: " + PrefabPath);
            }
            else
            {
                UnityEngine.Debug.LogError("TrainingPanel 预制件生成失败: " + PrefabPath);
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
