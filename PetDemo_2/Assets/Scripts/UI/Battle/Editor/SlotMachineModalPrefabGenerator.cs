#if UNITY_EDITOR
using System.IO;
using PetDemo.UI.Battle;
using UnityEditor;
using UnityEngine;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// SPEC §12.12：生成老虎机抽奖界面预制体 SlotMachineModal_3 / SlotMachineModal_5。
    /// 产出 Assets/Resources/Prefabs/Battle/SlotMachineModal_3.prefab 与 SlotMachineModal_5.prefab。
    /// 结构由运行时/编辑器共用的 SlotMachineModalBuilder 构建，保证运行时回退与预制体一致。
    /// </summary>
    [InitializeOnLoad]
    public static class SlotMachineModalPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Battle";
        private const string Prefab3Path = PrefabDir + "/SlotMachineModal_3.prefab";
        private const string Prefab5Path = PrefabDir + "/SlotMachineModal_5.prefab";

        static SlotMachineModalPrefabGenerator()
        {
            EditorApplication.delayCall += EnsurePrefabsExist;
        }

        private static void EnsurePrefabsExist()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            bool need3 = !PrefabValid(Prefab3Path);
            bool need5 = !PrefabValid(Prefab5Path);
            if (!need3 && !need5)
                return;

            Generate();
        }

        private static bool PrefabValid(string path)
        {
            if (!File.Exists(path))
                return false;
            return AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        }

        [MenuItem("Tools/PetDemo/Generate Slot Machine Modal Prefabs")]
        public static void Generate()
        {
            EnsureDir(PrefabDir);
            BuildPrefab(SlotMachineModalView.PanelObjectName3, 3, Prefab3Path);
            BuildPrefab(SlotMachineModalView.PanelObjectName5, 5, Prefab5Path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("SlotMachineModal 预制件生成完成: " + Prefab3Path + " / " + Prefab5Path);
        }

        private static void BuildPrefab(string panelName, int reelCount, string prefabPath)
        {
            var root = new GameObject(panelName, typeof(RectTransform));
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var view = root.AddComponent<SlotMachineModalView>();
            SlotMachineModalBuilder.Build(rootRt, view, reelCount);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
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
