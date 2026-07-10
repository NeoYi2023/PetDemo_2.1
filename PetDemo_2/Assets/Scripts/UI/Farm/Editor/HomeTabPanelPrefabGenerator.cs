#if UNITY_EDITOR
using System.IO;
using PetDemo.UI;
using UnityEditor;
using UnityEngine;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// SPEC §9.14.11：家园页签面板预制体生成器。
    /// 产出 Assets/Resources/Prefabs/Farm/HomeTabPanel.prefab。
    /// </summary>
    public static class HomeTabPanelPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Farm";
        private const string PrefabPath = PrefabDir + "/HomeTabPanel.prefab";

        [MenuItem("Tools/PetDemo/Generate Home Tab Panel Prefab")]
        public static void Generate()
        {
            GenerateInternal();
        }

        /// <summary>供 Unity 批处理调用。</summary>
        public static void GenerateFromCommandLine()
        {
            GenerateInternal();
        }

        private static void GenerateInternal()
        {
            EnsureDir(PrefabDir);

            var root = HomeTabPanelLayout.BuildRuntime(null);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (prefab != null)
            {
                Selection.activeObject = prefab;
                UnityEngine.Debug.Log("HomeTabPanel 预制件生成完成: " + PrefabPath);
            }
            else
            {
                UnityEngine.Debug.LogError("HomeTabPanel 预制件生成失败: " + PrefabPath);
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
