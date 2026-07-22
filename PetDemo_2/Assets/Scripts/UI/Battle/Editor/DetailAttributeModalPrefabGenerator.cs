#if UNITY_EDITOR
using System.IO;
using PetDemo.UI.Battle;
using UnityEditor;
using UnityEngine;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// SPEC §12.13：生成详细属性弹窗预制体 DetailAttributeModal。
    /// 产出 Assets/Resources/Prefabs/Battle/DetailAttributeModal.prefab。
    /// </summary>
    [InitializeOnLoad]
    public static class DetailAttributeModalPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Battle";
        private const string PrefabPath = PrefabDir + "/DetailAttributeModal.prefab";

        static DetailAttributeModalPrefabGenerator()
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
                {
                    // v3.271：旧预制体缺 AttrModeSwitch 时强制重生成。
                    var bottom = existing.transform.Find(DetailAttributeModalView.BottomAreaName);
                    if (bottom != null && bottom.Find(DetailAttributeModalView.AttrModeSwitchName) != null)
                        return;
                }
            }

            Generate();
        }

        [MenuItem("Tools/PetDemo/Generate Detail Attribute Modal Prefab")]
        public static void Generate()
        {
            EnsureDir(PrefabDir);
            BuildPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("DetailAttributeModal 预制件生成完成: " + PrefabPath);
        }

        private static void BuildPrefab()
        {
            var root = new GameObject(DetailAttributeModalView.PanelObjectName, typeof(RectTransform));
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var view = root.AddComponent<DetailAttributeModalView>();
            DetailAttributeModalBuilder.Build(rootRt, view);

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
