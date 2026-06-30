// SPEC §9.11.7 / §9.11.8 / §9.11.9：灭虫小游戏数据类型。
using System.Collections.Generic;

namespace PetDemo.Farm
{
    public enum PestControlEntityType
    {
        Bug,
        Werewolf,
    }

    public enum PestControlSwipeDirection
    {
        Up,
        Down,
        Left,
        Right,
    }

    public enum PestControlGameResult
    {
        Playing,
        Victory,
        Defeat,
    }

    public enum PestControlCellPulseKind
    {
        Merge,
        Eat,
    }

    public struct PestControlCell
    {
        public bool isEmpty;
        public PestControlEntityType type;
        public int value;

        public static PestControlCell Empty => new PestControlCell { isEmpty = true };

        public static PestControlCell Of(PestControlEntityType entityType, int cellValue)
        {
            return new PestControlCell
            {
                isEmpty = false,
                type = entityType,
                value = cellValue,
            };
        }

        public bool EqualsCell(PestControlCell other)
        {
            if (isEmpty != other.isEmpty)
                return false;
            if (isEmpty)
                return true;
            return type == other.type && value == other.value;
        }
    }

    public struct PestControlSpawnEntry
    {
        public int turn;
        public PestControlEntityType type;
        public int value;
        public int spawnCount;
    }

    public struct PestControlPieceMove
    {
        public int tokenId;
        public int fromRow;
        public int fromCol;
        public int toRow;
        public int toCol;
        public int cellDistance;
        public int actionOrder;
    }

    public struct PestControlCellPulse
    {
        public int row;
        public int col;
        public int tokenId;
        public int actionOrder;
        public PestControlCellPulseKind kind;
    }

    // SPEC §9.11.7（v3.79）：一次「狼吃虫」事件的完整表现数据（起始/最终坐标 + token 配对）。
    public struct PestControlEatEvent
    {
        public int wolfTokenId;
        public int wolfFromRow;
        public int wolfFromCol;
        public int bugTokenId;
        public int bugFromRow;
        public int bugFromCol;
        public int finalRow;
        public int finalCol;
        public int actionOrder;
    }

    public class PestControlSwipePlan
    {
        public bool changed;
        public PestControlCell[,] gridAfterSwipe;
        public readonly List<PestControlPieceMove> moves = new List<PestControlPieceMove>(16);
        public readonly List<PestControlCellPulse> pulses = new List<PestControlCellPulse>(8);
        public readonly List<int> consumedTokenIds = new List<int>(8);
        public readonly List<int> eatConsumedTokenIds = new List<int>(8);
        public readonly List<int> mergeConsumedTokenIds = new List<int>(8);
        public readonly List<PestControlEatEvent> eatEvents = new List<PestControlEatEvent>(8);
    }
}
