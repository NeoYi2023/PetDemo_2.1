#if UNITY_EDITOR
using System.IO;
using PetDemo.UI;
using UnityEditor;
using UnityEngine;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// SPEC §9.14.9（v3.160）：DressUpItemCell 单元预制体生成器。
    /// 产出 Assets/Resources/Prefabs/Farm/DressUpItemCell.prefab。
    /// 层级由 <see cref="DressUpPanelLayout.BuildDressUpItemCellRoot"/> 构建，
    /// 子节点名与 <see cref="DressUpItemCellView.AutoWire"/> 对应；运行时由 View 实例化并 Bind。
    /// </summary>
    public static class DressUpItemCellPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Farm";
        private const string PrefabPath = PrefabDir + "/DressUpItemCell.prefab";

        [MenuItem("Tools/PetDemo/Generate Dress-Up Item Cell Prefab")]
        public static void Generate()
        {
            EnsureDir(PrefabDir);

            var root = DressUpPanelLayout.BuildDressUpItemCellRoot(null);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (prefab != null)
            {
                Selection.activeObject = prefab;
                UnityEngine.Debug.Log("DressUpItemCell 预制件生成完成: " + PrefabPath);
            }
            else
            {
                UnityEngine.Debug.LogError("DressUpItemCell 预制件生成失败: " + PrefabPath);
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
