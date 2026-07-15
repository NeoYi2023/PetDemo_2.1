#if UNITY_EDITOR
using System.IO;
using PetDemo.UI.Farm;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.EditorTools
{
    public static class FarmGridPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Farm";
        private const string GridPrefabPath = PrefabDir + "/FarmGridRoot.prefab";
        private const string SlotPrefabPath = PrefabDir + "/TileSlot.prefab";
        private const string UnifiedButtonPrefabPath = PrefabDir + "/UnifiedActionButton.prefab";

        private const int Cols = FarmGridView.Cols;
        private const int Rows = FarmGridView.Rows;
        private const float DefaultTileWidth = 220f;
        private const float DefaultTileHeight = 160f;
        private const float DefaultColGap = 24f;
        private const float DefaultRowGap = 12f;

        // 生成后请在 JianYuan_2 世界背景下于 Unity 中手调 20 格位置（§9.8.14 / SPEC §9.1 手动布局模式）。

        [MenuItem("Tools/PetDemo/Generate Farm Grid Prefabs")]
        public static void Generate()
        {
            EnsureDir(PrefabDir);
            var slotPrefab = BuildTileSlotPrefab();
            BuildFarmGridRootPrefab(slotPrefab);
            BuildUnifiedActionButtonPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("Farm UI prefab 生成完成: " + GridPrefabPath + " , " + SlotPrefabPath + " , " + UnifiedButtonPrefabPath);
        }

        private static GameObject BuildTileSlotPrefab()
        {
            var slotGo = new GameObject("TileSlot", typeof(RectTransform));
            var slotRt = slotGo.GetComponent<RectTransform>();
            slotRt.anchorMin = new Vector2(0.5f, 0.5f);
            slotRt.anchorMax = new Vector2(0.5f, 0.5f);
            slotRt.pivot = new Vector2(0.5f, 0.5f);
            slotRt.sizeDelta = new Vector2(DefaultTileWidth, DefaultTileHeight);

            var soil = CreateImage(slotRt, "SoilImage", Vector2.zero, new Vector2(DefaultTileWidth, DefaultTileHeight));
            soil.color = new Color(0xA0 / 255f, 0x76 / 255f, 0x3A / 255f, 1f);
            soil.raycastTarget = false;

            var plant = CreateImage(slotRt, "PlantImage", Vector2.zero, new Vector2(DefaultTileWidth - 20f, DefaultTileHeight - 20f));
            plant.preserveAspect = true;
            plant.raycastTarget = false;
            plant.enabled = false;

            var water = CreateImage(slotRt, "WaterBadge", new Vector2(-78f, -64f), new Vector2(20f, 20f));
            var fert = CreateImage(slotRt, "FertilizerBadge", new Vector2(-26f, -64f), new Vector2(20f, 20f));
            var pest = CreateImage(slotRt, "PestBadge", new Vector2(26f, -64f), new Vector2(20f, 20f));
            var harvest = CreateImage(slotRt, "HarvestBadge", new Vector2(78f, -64f), new Vector2(20f, 20f));
            foreach (var badge in new[] { water, fert, pest, harvest })
            {
                badge.raycastTarget = false;
                badge.enabled = false;
            }

            var focus = CreateImage(slotRt, "FocusRing", Vector2.zero, new Vector2(DefaultTileWidth, DefaultTileHeight));
            focus.color = new Color(1.00f, 0.82f, 0.31f, 0.35f);
            focus.raycastTarget = false;
            focus.enabled = false;

            CreateText(slotRt, "OrderLabel",
                new Vector2(-DefaultTileWidth * 0.5f + 18f, DefaultTileHeight * 0.5f - 14f),
                new Vector2(48f, 24f),
                "1");

            slotGo.AddComponent<TileSlotView>();
            var saved = PrefabUtility.SaveAsPrefabAsset(slotGo, SlotPrefabPath);
            Object.DestroyImmediate(slotGo);
            return saved;
        }

        private static void BuildFarmGridRootPrefab(GameObject slotPrefab)
        {
            var rootGo = new GameObject("FarmGridRoot", typeof(RectTransform), typeof(FarmGridView));
            var rt = rootGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 60f);
            rt.sizeDelta = new Vector2(
                Cols * DefaultTileWidth + (Cols - 1) * DefaultColGap,
                Rows * DefaultTileHeight + (Rows - 1) * DefaultRowGap);

            CreateZhongTianAnchor(rootGo.transform);

            if (slotPrefab == null)
            {
                UnityEngine.Debug.LogWarning("TileSlot 预制体生成失败，跳过 FarmGridRoot 内 20 子节点的嵌套实例化: " + SlotPrefabPath);
            }
            else
            {
                NestTileSlotsInto(rootGo, slotPrefab);
            }

            PrefabUtility.SaveAsPrefabAsset(rootGo, GridPrefabPath);
            Object.DestroyImmediate(rootGo);
        }

        private static void CreateZhongTianAnchor(Transform parent)
        {
            var go = new GameObject(FarmGridView.ZhongTianAnchorName, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.SetAsFirstSibling();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        private static void NestTileSlotsInto(GameObject rootGo, GameObject slotPrefab)
        {
            float totalW = Cols * DefaultTileWidth + (Cols - 1) * DefaultColGap;
            float totalH = Rows * DefaultTileHeight + (Rows - 1) * DefaultRowGap;
            float startX = -totalW * 0.5f + DefaultTileWidth * 0.5f;
            float startY = totalH * 0.5f - DefaultTileHeight * 0.5f;

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    int orderIndex = r * Cols + c + 1;
                    float x = startX + c * (DefaultTileWidth + DefaultColGap);
                    float y = startY - r * (DefaultTileHeight + DefaultRowGap);

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab, rootGo.transform);
                    instance.name = "TileSlot_" + orderIndex.ToString("D2");
                    var slotRt = instance.GetComponent<RectTransform>();
                    slotRt.anchoredPosition = new Vector2(x, y);

                    var orderLabelTr = instance.transform.Find("OrderLabel");
                    if (orderLabelTr != null)
                    {
                        var orderText = orderLabelTr.GetComponent<Text>();
                        if (orderText != null)
                            orderText.text = orderIndex.ToString();
                    }
                }
            }
        }

        private static Image CreateImage(RectTransform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return go.GetComponent<Image>();
        }

        private static Text CreateText(RectTransform parent, string name, Vector2 pos, Vector2 size, string content)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.text = content;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 1f, 1f, 0.85f);
            text.font = FarmGridView.LoadBuiltinFont();
            text.fontSize = 16;
            text.raycastTarget = false;
            return text;
        }

        private static void EnsureDir(string dir)
        {
            if (Directory.Exists(dir))
                return;
            Directory.CreateDirectory(dir);
        }

        private static void BuildUnifiedActionButtonPrefab()
        {
            var go = new GameObject("UnifiedActionButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(UnifiedActionButtonView));
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -820f);
            rt.sizeDelta = new Vector2(282f, 193f);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.18f, 0.45f, 0.22f, 0.92f);
            image.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = image;
            var colors = button.colors;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            button.colors = colors;

            CreateText(rt, "LabelText", Vector2.zero, rt.sizeDelta, "暂无操作", 48, TextAnchor.MiddleCenter, Color.white);
            PrefabUtility.SaveAsPrefabAsset(go, UnifiedButtonPrefabPath);
            Object.DestroyImmediate(go);
        }

        private static Text CreateText(
            RectTransform parent,
            string name,
            Vector2 pos,
            Vector2 size,
            string content,
            int fontSize,
            TextAnchor anchor,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.text = content;
            text.alignment = anchor;
            text.color = color;
            text.font = FarmGridView.LoadBuiltinFont();
            text.fontSize = fontSize;
            text.raycastTarget = false;
            return text;
        }
    }
}
#endif
