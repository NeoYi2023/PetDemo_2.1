#if UNITY_EDITOR
using System.IO;
using PetDemo.UI;
using UnityEditor;
using UnityEngine;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// SPEC §9.14.8 第 1 点（v3.158）：TopFriendCell 单元预制体生成器。
    /// 产出 Assets/Resources/Prefabs/Farm/TopFriendCell.prefab。
    /// 层级由 <see cref="CharacterCreationScreenLayout.BuildTopFriendCellRoot"/> 构建，
    /// 子节点名与 <see cref="TopFriendCellView.AutoWire"/> 对应；运行时由 View 实例化并 Bind。
    /// </summary>
    public static class TopFriendCellPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Farm";
        private const string PrefabPath = PrefabDir + "/TopFriendCell.prefab";

        [MenuItem("Tools/PetDemo/Generate Top Friend Cell Prefab")]
        public static void Generate()
        {
            EnsureDir(PrefabDir);

            var root = CharacterCreationScreenLayout.BuildTopFriendCellRoot(null);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (prefab != null)
            {
                Selection.activeObject = prefab;
                UnityEngine.Debug.Log("TopFriendCell 预制件生成完成: " + PrefabPath);
            }
            else
            {
                UnityEngine.Debug.LogError("TopFriendCell 预制件生成失败: " + PrefabPath);
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
