#if UNITY_EDITOR
using System.IO;
using PetDemo.UI;
using UnityEditor;
using UnityEngine;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// SPEC §9.14：创角界面预制体生成器。
    /// 产出 Assets/Resources/Prefabs/Farm/CharacterCreationScreen.prefab。
    /// 层级与运行时回退共用 <see cref="CharacterCreationScreenLayout.BuildRuntime"/>；
    /// 字段在运行时由 View.EnsureFieldsFromHierarchy 按节点名绑定。
    /// </summary>
    public static class CharacterCreationScreenPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Farm";
        private const string PrefabPath = PrefabDir + "/CharacterCreationScreen.prefab";

        [MenuItem("Tools/PetDemo/Generate Character Creation Screen Prefab")]
        public static void Generate()
        {
            EnsureDir(PrefabDir);

            var root = CharacterCreationScreenLayout.BuildRuntime(null);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (prefab != null)
            {
                Selection.activeObject = prefab;
                UnityEngine.Debug.Log("CharacterCreationScreen 预制件生成完成: " + PrefabPath);
            }
            else
            {
                UnityEngine.Debug.LogError("CharacterCreationScreen 预制件生成失败: " + PrefabPath);
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
