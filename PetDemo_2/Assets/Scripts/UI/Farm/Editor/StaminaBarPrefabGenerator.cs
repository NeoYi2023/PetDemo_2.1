#if UNITY_EDITOR
using System.IO;
using PetDemo.UI;
using UnityEditor;
using UnityEngine;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// SPEC §9.8.12.4 (v3.204)：体力条预制体生成器。
    /// 产出 Assets/Resources/Prefabs/Farm/StaminaBar.prefab。
    /// </summary>
    public static class StaminaBarPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Farm";
        private const string PrefabPath = PrefabDir + "/StaminaBar.prefab";

        [MenuItem("Tools/PetDemo/Generate Stamina Bar Prefab")]
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

            var root = StaminaBarView.BuildRuntimeForPrefab();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (prefab != null)
            {
                Selection.activeObject = prefab;
                UnityEngine.Debug.Log("StaminaBar 预制件生成完成: " + PrefabPath);
            }
            else
            {
                UnityEngine.Debug.LogError("StaminaBar 预制件生成失败: " + PrefabPath);
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
