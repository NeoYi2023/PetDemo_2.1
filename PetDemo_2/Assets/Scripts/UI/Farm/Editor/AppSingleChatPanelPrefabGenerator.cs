#if UNITY_EDITOR
using System.IO;
using PetDemo.UI;
using UnityEditor;
using UnityEngine;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// SPEC §9.15.2：单人聊天面板预制体生成器。
    /// 产出 Assets/Resources/Prefabs/Farm/AppSingleChatPanel.prefab。
    /// </summary>
    public static class AppSingleChatPanelPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Farm";
        private const string PrefabPath = PrefabDir + "/AppSingleChatPanel.prefab";

        [InitializeOnLoadMethod]
        private static void EnsurePrefabOnLoad()
        {
            if (!File.Exists(GetFullPrefabPath()))
                Generate();
        }

        [MenuItem("Tools/PetDemo/Generate App Single Chat Panel Prefab")]
        public static void Generate()
        {
            EnsureDir(PrefabDir);

            var root = AppSingleChatPanelLayout.BuildRuntime(null);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (prefab != null)
            {
                Selection.activeObject = prefab;
                UnityEngine.Debug.Log("AppSingleChatPanel 预制件生成完成: " + PrefabPath);
            }
            else
            {
                UnityEngine.Debug.LogError("AppSingleChatPanel 预制件生成失败: " + PrefabPath);
            }
        }

        private static void EnsureDir(string dir)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        private static string GetFullPrefabPath()
        {
            return Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, PrefabPath);
        }
    }
}
#endif
