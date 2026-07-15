#if UNITY_EDITOR
using System.IO;
using PetDemo.UI.Companion;
using UnityEditor;
using UnityEngine;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// SPEC §9.8.18.3.1（v3.242）：邀请伴侣弹窗预制体生成器。
    /// 产出 Assets/Resources/Prefabs/Farm/InvitePartnerModal.prefab。
    /// </summary>
    [InitializeOnLoad]
    public static class InvitePartnerModalPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Farm";
        private const string PrefabPath = PrefabDir + "/InvitePartnerModal.prefab";

        static InvitePartnerModalPrefabGenerator()
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
                    return;
            }

            GenerateInternal();
        }

        [MenuItem("Tools/PetDemo/Generate Invite Partner Modal Prefab")]
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
            UnityEngine.Debug.Log("InvitePartnerModal 预制件生成完成: " + PrefabPath);
        }

        private static void BuildPrefab()
        {
            var root = new GameObject(InvitePartnerModalView.HostRootName, typeof(RectTransform));
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var view = root.AddComponent<InvitePartnerModalView>();
            InvitePartnerModalView.BuildModalSkeleton(rootRt, view);

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
