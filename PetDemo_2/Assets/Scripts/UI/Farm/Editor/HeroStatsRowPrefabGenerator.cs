#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using PetDemo.UI.Farm;

namespace PetDemo.EditorTools
{
    public static class HeroStatsRowPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Air";
        private const string HeroStatsRowPrefabPath = PrefabDir + "/HeroStatsRow.prefab";

        private const float RowPosY = 106f;
        private const float AtkPosX = -378f;
        private const float DefPosX = -105f;
        private const float HpPosX = 154f;
        private const float AgilityPosX = 421f;
        private const int FontSize = 36;

        [MenuItem("Tools/PetDemo/Generate Hero Stats Row Prefab")]
        public static void Generate()
        {
            EnsureDir(PrefabDir);
            BuildHeroStatsRowPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("HeroStatsRow 预制体生成完成: " + HeroStatsRowPrefabPath);
        }

        private static void BuildHeroStatsRowPrefab()
        {
            var rowGo = new GameObject("HeroStatsRow", typeof(RectTransform));
            var rowRt = rowGo.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0.5f, 0.5f);
            rowRt.anchorMax = new Vector2(0.5f, 0.5f);
            rowRt.pivot = new Vector2(0.5f, 0.5f);
            rowRt.anchoredPosition = new Vector2(0f, RowPosY);
            rowRt.sizeDelta = new Vector2(900f, 80f);

            CreateStatText(rowRt, "AtkText", "120", AtkPosX);
            CreateStatText(rowRt, "DefText", "80", DefPosX);
            CreateStatText(rowRt, "HpText", "1000", HpPosX);
            CreateStatText(rowRt, "AgilityText", "60", AgilityPosX);

            PrefabUtility.SaveAsPrefabAsset(rowGo, HeroStatsRowPrefabPath);
            Object.DestroyImmediate(rowGo);
        }

        private static void CreateStatText(RectTransform parent, string name, string content, float xOffset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(xOffset, 0f);
            rt.sizeDelta = new Vector2(210f, 72f);

            var text = go.AddComponent<Text>();
            text.text = content;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontSize = FontSize;
            text.font = FarmGridView.LoadBuiltinFont();
            text.raycastTarget = false;
        }

        private static void EnsureDir(string dir)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
#endif
