// 编辑器菜单：将 TaskListRowView.BuildRuntimeTemplate() 烘焙为 Assets/Resources/Prefabs/Farm/TaskListRow.prefab。
// 范式同 CharacterCreationScreenPrefabGenerator。运行 Tools/PetDemo/Generate TaskListRow Prefab 后即可被 Resources.Load 加载。
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using PetDemo.UI;

namespace PetDemo.UI.Farm.Editor
{
    public static class TaskListRowPrefabGenerator
    {
        private const string OutputPath = "Assets/Resources/Prefabs/Farm/TaskListRow.prefab";

        [MenuItem("Tools/PetDemo/Generate TaskListRow Prefab")]
        public static void Generate()
        {
            var templateGo = TaskListRowView.BuildRuntimeTemplate();
            templateGo.name = "TaskListRow";
            templateGo.SetActive(true);

            // 确保目录存在。
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs/Farm"))
                AssetDatabase.CreateFolder("Assets/Resources/Prefabs", "Farm");

            var prefab = PrefabUtility.SaveAsPrefabAsset(templateGo, OutputPath);
            Object.DestroyImmediate(templateGo);

            if (prefab != null)
                Debug.Log("[TaskListRowPrefabGenerator] 已生成预制体：" + OutputPath);
            else
                Debug.LogError("[TaskListRowPrefabGenerator] 生成预制体失败：" + OutputPath);
        }
    }
}
#endif
