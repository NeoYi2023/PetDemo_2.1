// SPEC §9.8.19（v3.238）：伴侣庄园世界背景 2×2 切块拼图。
// 扫描 Resources/AirUI/BLZY_r{row}_c{col}（当前 2×2），在 Background 容器下生成 Tile 子 Image。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Companion
{
    public static class CompanionManorBackgroundBuilder
    {
        public const string TileResourcePrefix = "AirUI/BLZY";

        private static readonly Vector2 MinWorldSize = new Vector2(1080f, 1920f);

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
                        "[CompanionManorBackgroundBuilder] 切块网格非矩形（第 " + row +
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
