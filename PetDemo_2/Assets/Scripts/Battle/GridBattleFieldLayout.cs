// SPEC §12.14.2：GridBattleField 预制体根布局与槽位查询。
using System.Collections.Generic;
using UnityEngine;

namespace PetDemo.Battle
{
    public sealed class GridBattleFieldLayout : MonoBehaviour
    {
        public RectTransform allyGridRoot;
        public RectTransform enemyGridRoot;

        private readonly Dictionary<long, RectTransform> slotByKey = new Dictionary<long, RectTransform>();

        private void Awake()
        {
            RebuildSlotIndex();
        }

        public void RebuildSlotIndex()
        {
            slotByKey.Clear();
            IndexGrid(allyGridRoot, BattleSide.Ally);
            IndexGrid(enemyGridRoot, BattleSide.Enemy);
            ApplySlotSiblingDepthOrder();
        }

        /// <summary>
        /// SPEC §12.14.9 (v3.222)：按行重排槽位 sibling，使 Slot_r3 最后绘制、Slot_r1 最先。
        /// </summary>
        public void ApplySlotSiblingDepthOrder()
        {
            ApplyGridSiblingDepthOrder(allyGridRoot);
            ApplyGridSiblingDepthOrder(enemyGridRoot);
        }

        private static void ApplyGridSiblingDepthOrder(RectTransform gridRoot)
        {
            if (gridRoot == null)
                return;

            var rowBuckets = new List<RectTransform>[4];
            for (int i = 0; i < rowBuckets.Length; i++)
                rowBuckets[i] = new List<RectTransform>();

            for (int i = 0; i < gridRoot.childCount; i++)
            {
                var child = gridRoot.GetChild(i) as RectTransform;
                if (child == null)
                    continue;
                var marker = child.GetComponent<BattleGridSlotMarker>();
                if (marker == null)
                    continue;
                int row = marker.row < 1 ? 1 : (marker.row > 3 ? 3 : marker.row);
                rowBuckets[row].Add(child);
            }

            for (int row = 1; row <= 3; row++)
            {
                var bucket = rowBuckets[row];
                for (int i = 0; i < bucket.Count; i++)
                    bucket[i].SetAsLastSibling();
            }
        }

        public RectTransform GetSlot(BattleSide side, int row, int col)
        {
            slotByKey.TryGetValue(MakeKey(side, row, col), out var rt);
            return rt;
        }

        public BattleGridPos? GetSlotPosition(RectTransform slot)
        {
            if (slot == null)
                return null;
            var marker = slot.GetComponent<BattleGridSlotMarker>();
            if (marker == null)
                return null;
            return new BattleGridPos(marker.row, marker.col);
        }

        private void IndexGrid(RectTransform root, BattleSide side)
        {
            if (root == null)
                return;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i) as RectTransform;
                if (child == null)
                    continue;
                var marker = child.GetComponent<BattleGridSlotMarker>();
                if (marker == null)
                    continue;
                marker.side = side;
                slotByKey[MakeKey(side, marker.row, marker.col)] = child;
            }
        }

        private static long MakeKey(BattleSide side, int row, int col)
        {
            return ((long)(int)side << 32) | (uint)((row & 0xF) << 4 | (col & 0xF));
        }
    }
}
