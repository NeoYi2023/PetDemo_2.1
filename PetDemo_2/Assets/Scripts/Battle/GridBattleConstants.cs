// SPEC §12.14.5：多单位战精灵开关。
namespace PetDemo.Battle
{
    public static class GridBattleConstants
    {
        /// <summary>SPEC §12.14.5：多单位战关闭精灵协攻。</summary>
        public const bool kGridBattlePetsEnabled = false;

        /// <summary>SPEC §12.14.11：九宫格战场预制体 Resources 路径。</summary>
        public const string GridBattleFieldPrefabPath = "Prefabs/Battle/GridBattleField";

        /// <summary>SPEC §12.14.9 (v3.224)：嵌入 TopArea 时 GridBattleField 顶部内缩（Inspector Top=200 → offsetMax.y=-200）。</summary>
        public const float GridBattleFieldTopInsetPx = 200f;

        /// <summary>SPEC §12.3 / §12.14.9 (v3.221)：战斗中 Spine 在基底缩放上的统一视觉放大（+15%）。</summary>
        public const float BattleSpineDisplayScaleMultiplier = 1.15f;

        /// <summary>SPEC §12.3 / §12.14.9 (v3.221)：血条相对 Spine 中心点向下的像素偏移（正值表示向下）。</summary>
        public const float GridHpBarOffsetBelowSpineCenterPx = 20f;

        /// <summary>SPEC §12.14.9 (v3.222)：槽位行深度步进；r3 &gt; r2 &gt; r1（下行遮挡上行）。</summary>
        public const int GridRowSortOrderStep = 10;

        /// <summary>
        /// SPEC §12.14.9 (v3.222 / v3.223)：相对父 Canvas 的槽位深度偏移（row 越大越靠前）。
        /// 最终 sortingOrder = parentCanvas.sortingOrder + 本值。
        /// </summary>
        public static int ComputeSlotDepthSortingOrder(int row, int col)
        {
            int safeRow = row < 1 ? 1 : (row > 3 ? 3 : row);
            int safeCol = col < 1 ? 1 : (col > 3 ? 3 : col);
            return safeRow * GridRowSortOrderStep + safeCol;
        }
    }
}
