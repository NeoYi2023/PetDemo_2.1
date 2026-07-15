// SPEC §12.5 / §12.14：多单位阵型战纯数据类型（零 Unity 依赖）。
using System.Collections.Generic;
using PetDemo.Core;

namespace PetDemo.Battle
{
    /// <summary>SPEC §12.14.1.1：局内队伍名册（InvasionBattleModal_2 一局 Show→Hide 内持久）。</summary>
    public sealed class RunPartyRoster
    {
        /// <summary>顺序：Role 首位，其余按拉手顺序。</summary>
        public readonly List<RunAllyEntry> members = new List<RunAllyEntry>();
    }

    /// <summary>SPEC §12.5：局内一名我方成员。</summary>
    public sealed class RunAllyEntry
    {
        public string rosterId;
        public BattleUnitKind kind;
        /// <summary>FollowerNpc 时 = GuildNpcMarker.NpcId；Role 为空。</summary>
        public string sourceNpcId;
        public RoleStats stats;
        public readonly Dictionary<string, int> runEnhanceBonuses = new Dictionary<string, int>();
        public readonly List<string> acquiredSkillIds = new List<string>();
        public string displayName;
        public string skeletonPrefab;
    }

    /// <summary>SPEC §12.5：阵营。</summary>
    public enum BattleSide
    {
        Ally,
        Enemy,
    }

    /// <summary>SPEC §12.5：九宫格坐标（row/col 均 ∈ {1,2,3}）。</summary>
    public struct BattleGridPos
    {
        /// <summary>1=上，2=中，3=下。</summary>
        public int row;
        /// <summary>1=靠战场中线前排，3=最外侧。</summary>
        public int col;

        public BattleGridPos(int row, int col)
        {
            this.row = row;
            this.col = col;
        }
    }

    /// <summary>SPEC §12.5：我方单位种类。</summary>
    public enum BattleUnitKind
    {
        Role,
        FollowerNpc,
    }

    /// <summary>SPEC §12.5：单场战斗中的一个参战单位运行时快照。</summary>
    public sealed class BattleUnitRuntime
    {
        public string instanceId;
        /// <summary>对应 RunAllyEntry.rosterId（敌方为空）。</summary>
        public string rosterId;
        public BattleSide side;
        public BattleUnitKind kind;
        public string sourceNpcId;
        public BattleGridPos gridPos;
        public RoleStats stats;
        public bool isBattleDead;
        public string displayName;
        public string skeletonPrefab;
        // SPEC §12.14.9 (v3.226)：战斗展示比例（来自 InvasionUnitConfig.displayScale）；默认 1。
        public float displayScale = 1f;
    }

    /// <summary>SPEC §12.5：多单位阵型战运行时状态。</summary>
    public sealed class GridBattleSession
    {
        public int roundIndex = 1;
        public readonly List<BattleUnitRuntime> allies = new List<BattleUnitRuntime>();
        public readonly List<BattleUnitRuntime> enemies = new List<BattleUnitRuntime>();
        public readonly List<BattleUnitRuntime> turnQueue = new List<BattleUnitRuntime>();
        public int turnCursor;
        public int battleSeed;
        public bool playerWon;
        public bool finished;
        /// <summary>触发本场的事件 id（如 evt_fight_small_2）。</summary>
        public string pendingEventId;
    }
}
