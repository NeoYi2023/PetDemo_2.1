#if UNITY_EDITOR
using System.IO;
using PetDemo.Battle;
using UnityEditor;
using UnityEngine;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// SPEC §12.14.2 / §12.14.11：生成九宫格战场预制体 GridBattleField.prefab。
    /// 产出 Assets/Resources/Prefabs/Battle/GridBattleField.prefab。
    /// </summary>
    [InitializeOnLoad]
    public static class GridBattleFieldPrefabGenerator
    {
        public const string PrefabResourcePath = "Prefabs/Battle/GridBattleField";
        private const string PrefabDir = "Assets/Resources/Prefabs/Battle";
        private const string PrefabPath = PrefabDir + "/GridBattleField.prefab";

        private static readonly Vector2 SlotSize = new Vector2(200f, 200f);
        private static readonly float GridSpacingX = 220f;
        private static readonly float GridSpacingY = 220f;

        static GridBattleFieldPrefabGenerator()
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
                UnityEngine.Debug.LogWarning(
                    "[GridBattleField] 预制体文件存在但 Unity 无法导入，将重新生成: " + PrefabPath);
            }

            Generate();
        }

        [MenuItem("Tools/PetDemo/Generate Grid Battle Field Prefab")]
        public static void Generate()
        {
            EnsureDir(PrefabDir);
            BuildPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("GridBattleField 预制件生成完成: " + PrefabPath);
        }

        private static void BuildPrefab()
        {
            var root = new GameObject("GridBattleField", typeof(RectTransform));
            var rootRt = root.GetComponent<RectTransform>();
            StretchFull(rootRt);

            var layout = root.AddComponent<GridBattleFieldLayout>();

            var allyGrid = CreateGridRoot(rootRt, "AllyGrid", new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f), new Vector2(-20f, 0f));
            var enemyGrid = CreateGridRoot(rootRt, "EnemyGrid", new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(20f, 0f), new Vector2(0f, 0f));

            BuildSlots(allyGrid, BattleSide.Ally);
            BuildSlots(enemyGrid, BattleSide.Enemy);

            layout.allyGridRoot = allyGrid;
            layout.enemyGridRoot = enemyGrid;

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            else
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

            Object.DestroyImmediate(root);
        }

        private static RectTransform CreateGridRoot(
            RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        private static void BuildSlots(RectTransform gridRoot, BattleSide side)
        {
            for (int row = 1; row <= 3; row++)
            {
                for (int col = 1; col <= 3; col++)
                {
                    float x = (col - 2) * GridSpacingX;
                    float y = (2 - row) * GridSpacingY;
                    string slotName = "Slot_r" + row + "c" + col;

                    var slotRt = new GameObject(slotName, typeof(RectTransform)).GetComponent<RectTransform>();
                    slotRt.SetParent(gridRoot, false);
                    slotRt.anchorMin = new Vector2(0.5f, 0.5f);
                    slotRt.anchorMax = new Vector2(0.5f, 0.5f);
                    slotRt.pivot = new Vector2(0.5f, 0.5f);
                    slotRt.anchoredPosition = new Vector2(x, y);
                    slotRt.sizeDelta = SlotSize;

                    var marker = slotRt.gameObject.AddComponent<BattleGridSlotMarker>();
                    marker.side = side;
                    marker.row = row;
                    marker.col = col;
                }
            }
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void EnsureDir(string dir)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
#endif
