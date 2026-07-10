// SPEC §12.14.2 / §12.14.11：九宫格槽位标记。
using UnityEngine;

namespace PetDemo.Battle
{
    public sealed class BattleGridSlotMarker : MonoBehaviour
    {
        public BattleSide side;
        public int row = 1;
        public int col = 1;
    }
}
