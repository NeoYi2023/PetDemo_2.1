#if UNITY_EDITOR
using System.IO;
using PetDemo.UI.Companion;
using UnityEditor;
using UnityEngine;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// SPEC §9.8.19（v3.238；Partner NPC + Waypoints v3.259；Obstacles v3.260；建造弹图 v3.262；悬浮框 v3.263）：伴侣庄园场景预制体生成器。
    /// 产出 CompanionManorScreenPanel.prefab（含 Obstacles、FloatingFrames、动作按钮与 BuildOverlay）。
    /// 同源范式见 InvitePartnerModalPrefabGenerator（§9.8.18.3.1）。
    /// </summary>
    public static class CompanionManorScreenPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Farm";
        private const string PrefabPath = PrefabDir + "/CompanionManorScreenPanel.prefab";

        [MenuItem("Tools/PetDemo/Generate Companion Manor Screen Prefab")]
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
            BuildPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("CompanionManorScreenPanel 预制件生成完成: " + PrefabPath);
        }

        private static void BuildPrefab()
        {
            var root = new GameObject("CompanionManorScreenPanel", typeof(RectTransform));
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var view = root.AddComponent<CompanionManorScreenView>();
            CompanionManorScreenView.BuildSceneSkeleton(rootRt, view);

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
            AssetDatabase.Refresh();
        }
    }
}
#endif
