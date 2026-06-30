#if UNITY_EDITOR
using System.IO;
using PetDemo.UI;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// SPEC §9.14.9：装扮界面预制体生成器。
    /// 产出 Assets/Resources/Prefabs/Farm/DressUpPanel.prefab。
    /// 层级与运行时回退共用 <see cref="DressUpPanelLayout.BuildRuntime"/>；
    /// 字段在运行时由 View.EnsureFieldsFromHierarchy 按节点名绑定。
    /// </summary>
    public static class DressUpPanelPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Farm";
        private const string PrefabPath = PrefabDir + "/DressUpPanel.prefab";
        private const string PlayerSkeletonAssetPath =
            "Assets/Scenes/Air/LangRen/Role_cslangren/Role_cslangren_SkeletonData.asset";
        private const string FriendSkeletonAssetPath =
            "Assets/Scenes/Air/LangMeiRen/Role_langmeiren/Role_langmeiren_SkeletonData.asset";

        [MenuItem("Tools/PetDemo/Generate Dress-Up Panel Prefab")]
        public static void Generate()
        {
            EnsureDir(PrefabDir);

            var root = DressUpPanelLayout.BuildRuntime(null);
            var view = root.GetComponent<DressUpPanelView>();
            if (view != null)
                AssignActionSkeletonReferences(view);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            if (prefab != null)
            {
                var prefabView = prefab.GetComponent<DressUpPanelView>();
                if (prefabView != null)
                    AssignActionSkeletonReferences(prefabView);
                EditorUtility.SetDirty(prefab);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (prefab != null)
            {
                Selection.activeObject = prefab;
                UnityEngine.Debug.Log("DressUpPanel 预制件生成完成: " + PrefabPath);
            }
            else
            {
                UnityEngine.Debug.LogError("DressUpPanel 预制件生成失败: " + PrefabPath);
            }
        }

        private static void AssignActionSkeletonReferences(DressUpPanelView view)
        {
            var playerSkeleton = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(PlayerSkeletonAssetPath);
            var friendSkeleton = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(FriendSkeletonAssetPath);

            var so = new SerializedObject(view);
            so.FindProperty("playerActionSkeletonData").objectReferenceValue = playerSkeleton;
            so.FindProperty("friendActionSkeletonData").objectReferenceValue = friendSkeleton;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (playerSkeleton == null)
                UnityEngine.Debug.LogWarning("DressUpPanelPrefabGenerator: 未找到 Player SkeletonData：" + PlayerSkeletonAssetPath);
            if (friendSkeleton == null)
                UnityEngine.Debug.LogWarning("DressUpPanelPrefabGenerator: 未找到 Friend SkeletonData：" + FriendSkeletonAssetPath);
        }

        private static void EnsureDir(string dir)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
#endif
