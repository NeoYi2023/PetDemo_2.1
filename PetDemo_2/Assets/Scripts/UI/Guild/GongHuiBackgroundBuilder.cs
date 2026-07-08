// SPEC §9.8.9 (v3.133；3×3 切块 v3.176)：公会世界背景分块拼图（方案 A）。
// 扫描 Resources/AirUI/GongHui_0_1_r{row}_c{col} 矩形网格（当前 3×3×1043×1500→3129×4500），在 Background 容器下生成 Tile 子 Image。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    public static class GongHuiBackgroundBuilder
    {
        public const string TileResourcePrefix = "AirUI/GongHui_0_1";
        public const string ResGongHuiBackgroundSingle = "AirUI/GongHui_0_1";
        public const string ResGongHuiBackgroundLegacy = "AirUI/Gonghui_0";

        private static readonly Vector2 MinWorldSize = new Vector2(1620f, 2880f);

        /// <summary>
        /// 尝试在 <paramref name="backgroundRoot"/> 下拼切块背景；成功时返回世界总尺寸。
        /// </summary>
        public static bool TryBuild(RectTransform backgroundRoot, out Vector2 worldSize)
        {
            worldSize = Vector2.zero;
            if (backgroundRoot == null)
                return false;

            if (!TryLoadTileGrid(out var rows, out var cols, out var tileSize, out var tiles))
                return false;

            ClearBackgroundVisuals(backgroundRoot);

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    var sprite = tiles[row][col];
                    if (sprite == null)
                        continue;

                    var tileRt = BottomNavAttachedScreenLayout.CreateChildRect(
                        backgroundRoot,
                        "Tile_r" + row + "_c" + col,
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        TileAnchoredPosition(col, cols, row, rows, tileSize),
                        tileSize);
                    var image = tileRt.gameObject.AddComponent<Image>();
                    image.sprite = sprite;
                    image.preserveAspect = false;
                    image.color = Color.white;
                    image.raycastTarget = false;
                }
            }

            worldSize = Vector2.Max(
                new Vector2(tileSize.x * cols, tileSize.y * rows),
                MinWorldSize);
            backgroundRoot.sizeDelta = worldSize;
            backgroundRoot.localScale = Vector3.one;
            return true;
        }

        /// <summary>单图回退：在 Background 上挂一张 Image。</summary>
        public static bool TryBuildSingleSpriteFallback(RectTransform backgroundRoot, out Vector2 worldSize)
        {
            worldSize = Vector2.zero;
            if (backgroundRoot == null)
                return false;

            var sprite = Resources.Load<Sprite>(ResGongHuiBackgroundSingle)
                ?? Resources.Load<Sprite>(ResGongHuiBackgroundLegacy);
            if (sprite == null)
                return false;

            ClearBackgroundVisuals(backgroundRoot);

            var image = backgroundRoot.gameObject.GetComponent<Image>();
            if (image == null)
                image = backgroundRoot.gameObject.AddComponent<Image>();

            worldSize = Vector2.Max(sprite.rect.size, MinWorldSize);
            image.sprite = sprite;
            image.preserveAspect = false;
            image.color = Color.white;
            image.raycastTarget = false;
            backgroundRoot.sizeDelta = worldSize;
            backgroundRoot.localScale = Vector3.one;
            return true;
        }

        public static Vector2 TileAnchoredPosition(int col, int cols, int row, int rows, Vector2 tileSize)
        {
            float x = (col - (cols - 1) * 0.5f) * tileSize.x;
            float y = ((rows - 1) * 0.5f - row) * tileSize.y;
            return new Vector2(x, y);
        }

        private static bool TryLoadTileGrid(
            out int rows,
            out int cols,
            out Vector2 tileSize,
            out List<List<Sprite>> tiles)
        {
            rows = 0;
            cols = 0;
            tileSize = Vector2.zero;
            tiles = new List<List<Sprite>>();

            for (int row = 0; ; row++)
            {
                var rowSprites = new List<Sprite>();
                for (int col = 0; ; col++)
                {
                    var sprite = Resources.Load<Sprite>(TileResourcePrefix + "_r" + row + "_c" + col);
                    if (sprite == null)
                        break;
                    if (row == 0 && col == 0)
                        tileSize = sprite.rect.size;
                    rowSprites.Add(sprite);
                }

                if (rowSprites.Count == 0)
                    break;

                if (rows == 0)
                    cols = rowSprites.Count;
                else if (rowSprites.Count != cols)
                {
                    UnityEngine.Debug.LogWarning(
                        "[GongHuiBackgroundBuilder] 切块网格非矩形（第 " + row +
                        " 行列数 " + rowSprites.Count + " ≠ " + cols + "），已中止拼图。");
                    tiles.Clear();
                    rows = 0;
                    return false;
                }

                tiles.Add(rowSprites);
                rows++;
            }

            return rows > 0 && cols > 0;
        }

        private static void ClearBackgroundVisuals(RectTransform backgroundRoot)
        {
            var rootImage = backgroundRoot.GetComponent<Image>();
            if (rootImage != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(rootImage);
                else
                    Object.DestroyImmediate(rootImage);
            }

            for (int i = backgroundRoot.childCount - 1; i >= 0; i--)
            {
                var child = backgroundRoot.GetChild(i);
                if (!child.name.StartsWith("Tile_r"))
                    continue;
                if (Application.isPlaying)
                    Object.Destroy(child.gameObject);
                else
                    Object.DestroyImmediate(child.gameObject);
            }
        }
    }
}
