// SPEC §9.11.7 / §9.11.9：灭虫小游戏纯逻辑（5×5 滑动/合并/捕食/回合生成 + 动画计划）。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PetDemo.Farm
{
    public class PestControlGameModel
    {
        public const int GridSize = 5;
        public const int VictoryTurnThreshold = 20;
        public const int VictoryKillScorePerPestIcon = 40;

        private struct TokenPiece
        {
            public int tokenId;
            public PestControlEntityType type;
            public int value;
        }

        private readonly System.Random rng;
        private readonly Dictionary<int, List<PestControlSpawnEntry>> spawnByTurn = new Dictionary<int, List<PestControlSpawnEntry>>();
        private PestControlCell[,] grid = new PestControlCell[GridSize, GridSize];

        public int Turn { get; private set; }
        public int KillScore { get; private set; }
        public int RequiredKillScore { get; private set; }
        public PestControlGameResult Result { get; private set; } = PestControlGameResult.Playing;

        public PestControlGameModel(int? seed = null)
        {
            rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        }

        public void Reset(IReadOnlyList<PestControlSpawnEntry> spawnConfig, int requiredKillScore = 0)
        {
            BuildSpawnLookup(spawnConfig);
            ClearGrid();
            Turn = 0;
            KillScore = 0;
            RequiredKillScore = requiredKillScore;
            Result = PestControlGameResult.Playing;
            ApplySpawnForTurn(0);
            EvaluateEndState();
        }

        public PestControlCell GetCell(int row, int col)
        {
            return grid[row, col];
        }

        public int CountEntity(PestControlEntityType entityType)
        {
            int count = 0;
            for (int r = 0; r < GridSize; r++)
            {
                for (int c = 0; c < GridSize; c++)
                {
                    if (!grid[r, c].isEmpty && grid[r, c].type == entityType)
                        count++;
                }
            }

            return count;
        }

        public bool TrySwipe(PestControlSwipeDirection direction)
        {
            if (!TryPrepareSwipe(direction, out var plan))
                return false;
            CommitSwipeAndSpawn(plan);
            return true;
        }

        public bool TryPrepareSwipe(PestControlSwipeDirection direction, out PestControlSwipePlan plan)
        {
            plan = null;
            if (Result != PestControlGameResult.Playing)
                return false;

            plan = BuildSwipePlan(grid, direction);
            return plan.changed;
        }

        public void CommitSwipeAndSpawn(PestControlSwipePlan plan)
        {
            if (plan == null || !plan.changed || plan.gridAfterSwipe == null)
                return;

            AccumulateKillScoreFromEatEvents(plan);
            grid = CloneGrid(plan.gridAfterSwipe);
            Turn++;
            ApplySpawnForTurn(Turn);
            EvaluateEndState();
        }

        private void AccumulateKillScoreFromEatEvents(PestControlSwipePlan plan)
        {
            if (plan.eatEvents == null || plan.eatEvents.Count == 0)
                return;

            for (int i = 0; i < plan.eatEvents.Count; i++)
            {
                var ev = plan.eatEvents[i];
                var cell = grid[ev.bugFromRow, ev.bugFromCol];
                if (!cell.isEmpty && cell.type == PestControlEntityType.Bug)
                    KillScore += cell.value;
            }
        }

        private static PestControlSwipePlan BuildSwipePlan(PestControlCell[,] source, PestControlSwipeDirection direction)
        {
            var plan = new PestControlSwipePlan();
            var startPos = new Dictionary<int, (int row, int col)>();
            int nextTokenId = 1;
            var tokenIds = new int[GridSize, GridSize];
            var cells = CloneGrid(source);

            for (int r = 0; r < GridSize; r++)
            {
                for (int c = 0; c < GridSize; c++)
                {
                    if (cells[r, c].isEmpty)
                        continue;
                    int id = nextTokenId++;
                    tokenIds[r, c] = id;
                    startPos[id] = (r, c);
                }
            }

            var pulses = new List<PestControlCellPulse>();
            var wolfEatsBugPairs = new List<(int wolfId, int bugId)>();
            ApplySwipeWithTokens(cells, tokenIds, direction, pulses, plan.eatConsumedTokenIds, plan.mergeConsumedTokenIds, wolfEatsBugPairs);

            if (GridsEqual(source, cells))
            {
                plan.changed = false;
                plan.gridAfterSwipe = CloneGrid(source);
                return plan;
            }

            plan.changed = true;
            plan.gridAfterSwipe = cells;
            plan.pulses.AddRange(pulses);
            ApplyPulseActionOrders(plan.pulses, direction);
            plan.pulses.Sort(ComparePulsesByActionOrder);

            var finalPos = new Dictionary<int, (int row, int col)>();
            for (int r = 0; r < GridSize; r++)
            {
                for (int c = 0; c < GridSize; c++)
                {
                    int id = tokenIds[r, c];
                    if (id > 0)
                        finalPos[id] = (r, c);
                }
            }

            foreach (var kv in startPos)
            {
                int id = kv.Key;
                var from = kv.Value;
                if (!finalPos.TryGetValue(id, out var to))
                {
                    plan.consumedTokenIds.Add(id);
                    continue;
                }

                if (from.row == to.row && from.col == to.col)
                    continue;

                int dist = Mathf.Abs(to.row - from.row) + Mathf.Abs(to.col - from.col);
                plan.moves.Add(new PestControlPieceMove
                {
                    tokenId = id,
                    fromRow = from.row,
                    fromCol = from.col,
                    toRow = to.row,
                    toCol = to.col,
                    cellDistance = dist,
                    actionOrder = GetActionOrder(direction, from.row, from.col),
                });
            }

            plan.moves.Sort(CompareMovesByActionOrder);

            // SPEC §9.11.7（v3.79）：为每个「狼吃虫」生成完整表现事件（起始/最终坐标）。
            for (int p = 0; p < wolfEatsBugPairs.Count; p++)
            {
                var pair = wolfEatsBugPairs[p];
                if (!startPos.TryGetValue(pair.wolfId, out var wolfFrom))
                    continue;
                if (!startPos.TryGetValue(pair.bugId, out var bugFrom))
                    continue;
                if (!finalPos.TryGetValue(pair.wolfId, out var wolfFinal))
                    continue;

                plan.eatEvents.Add(new PestControlEatEvent
                {
                    wolfTokenId = pair.wolfId,
                    wolfFromRow = wolfFrom.row,
                    wolfFromCol = wolfFrom.col,
                    bugTokenId = pair.bugId,
                    bugFromRow = bugFrom.row,
                    bugFromCol = bugFrom.col,
                    finalRow = wolfFinal.row,
                    finalCol = wolfFinal.col,
                    actionOrder = GetActionOrder(direction, wolfFrom.row, wolfFrom.col),
                });
            }

            plan.eatEvents.Sort(CompareEatEventsByActionOrder);
            return plan;
        }

        private static int CompareEatEventsByActionOrder(PestControlEatEvent a, PestControlEatEvent b)
        {
            int byOrder = a.actionOrder.CompareTo(b.actionOrder);
            if (byOrder != 0)
                return byOrder;
            int byRow = a.bugFromRow.CompareTo(b.bugFromRow);
            if (byRow != 0)
                return byRow;
            int byCol = a.bugFromCol.CompareTo(b.bugFromCol);
            if (byCol != 0)
                return byCol;
            return a.wolfTokenId.CompareTo(b.wolfTokenId);
        }

        private static int GetActionOrder(PestControlSwipeDirection direction, int row, int col)
        {
            switch (direction)
            {
                case PestControlSwipeDirection.Left:
                    return col;
                case PestControlSwipeDirection.Right:
                    return GridSize - 1 - col;
                case PestControlSwipeDirection.Up:
                    return row;
                case PestControlSwipeDirection.Down:
                    return GridSize - 1 - row;
                default:
                    return 0;
            }
        }

        private static void ApplyPulseActionOrders(List<PestControlCellPulse> pulses, PestControlSwipeDirection direction)
        {
            for (int i = 0; i < pulses.Count; i++)
            {
                var pulse = pulses[i];
                pulse.actionOrder = GetActionOrder(direction, pulse.row, pulse.col);
                pulses[i] = pulse;
            }
        }

        private static int CompareMovesByActionOrder(PestControlPieceMove a, PestControlPieceMove b)
        {
            int byOrder = a.actionOrder.CompareTo(b.actionOrder);
            if (byOrder != 0)
                return byOrder;
            int byRow = a.fromRow.CompareTo(b.fromRow);
            if (byRow != 0)
                return byRow;
            int byCol = a.fromCol.CompareTo(b.fromCol);
            if (byCol != 0)
                return byCol;
            return a.tokenId.CompareTo(b.tokenId);
        }

        private static int ComparePulsesByActionOrder(PestControlCellPulse a, PestControlCellPulse b)
        {
            int byOrder = a.actionOrder.CompareTo(b.actionOrder);
            if (byOrder != 0)
                return byOrder;
            int byRow = a.row.CompareTo(b.row);
            if (byRow != 0)
                return byRow;
            int byCol = a.col.CompareTo(b.col);
            if (byCol != 0)
                return byCol;
            return a.tokenId.CompareTo(b.tokenId);
        }

        private static void ApplySwipeWithTokens(
            PestControlCell[,] cells,
            int[,] tokenIds,
            PestControlSwipeDirection direction,
            List<PestControlCellPulse> pulses,
            List<int> eatConsumed,
            List<int> mergeConsumed,
            List<(int wolfId, int bugId)> wolfEatsBugPairs)
        {
            switch (direction)
            {
                case PestControlSwipeDirection.Left:
                    for (int r = 0; r < GridSize; r++)
                        ProcessLineIntoRowWithTokens(cells, tokenIds, r, GetRowTokens(cells, tokenIds, r), pulses, eatConsumed, mergeConsumed, wolfEatsBugPairs);
                    break;
                case PestControlSwipeDirection.Right:
                    for (int r = 0; r < GridSize; r++)
                    {
                        var line = GetRowTokens(cells, tokenIds, r);
                        line.Reverse();
                        ProcessLineIntoRowWithTokens(cells, tokenIds, r, line, pulses, eatConsumed, mergeConsumed, wolfEatsBugPairs, reverse: true);
                    }

                    break;
                case PestControlSwipeDirection.Up:
                    for (int c = 0; c < GridSize; c++)
                        ProcessLineIntoColumnWithTokens(cells, tokenIds, c, GetColumnTokens(cells, tokenIds, c), pulses, eatConsumed, mergeConsumed, wolfEatsBugPairs);
                    break;
                case PestControlSwipeDirection.Down:
                    for (int c = 0; c < GridSize; c++)
                    {
                        var line = GetColumnTokens(cells, tokenIds, c);
                        line.Reverse();
                        ProcessLineIntoColumnWithTokens(cells, tokenIds, c, line, pulses, eatConsumed, mergeConsumed, wolfEatsBugPairs, reverse: true);
                    }

                    break;
            }
        }

        private static List<TokenPiece> GetRowTokens(PestControlCell[,] cells, int[,] tokenIds, int row)
        {
            var list = new List<TokenPiece>(GridSize);
            for (int c = 0; c < GridSize; c++)
            {
                if (cells[row, c].isEmpty)
                    continue;
                list.Add(new TokenPiece
                {
                    tokenId = tokenIds[row, c],
                    type = cells[row, c].type,
                    value = cells[row, c].value,
                });
            }

            return list;
        }

        private static List<TokenPiece> GetColumnTokens(PestControlCell[,] cells, int[,] tokenIds, int col)
        {
            var list = new List<TokenPiece>(GridSize);
            for (int r = 0; r < GridSize; r++)
            {
                if (cells[r, col].isEmpty)
                    continue;
                list.Add(new TokenPiece
                {
                    tokenId = tokenIds[r, col],
                    type = cells[r, col].type,
                    value = cells[r, col].value,
                });
            }

            return list;
        }

        private static void ProcessLineIntoRowWithTokens(
            PestControlCell[,] cells,
            int[,] tokenIds,
            int row,
            List<TokenPiece> line,
            List<PestControlCellPulse> pulses,
            List<int> eatConsumed,
            List<int> mergeConsumed,
            List<(int wolfId, int bugId)> wolfEatsBugPairs,
            bool reverse = false)
        {
            var result = ProcessLineTokens(line);
            eatConsumed.AddRange(result.eatConsumed);
            mergeConsumed.AddRange(result.mergeConsumed);
            wolfEatsBugPairs.AddRange(result.wolfEatsBugPairs);
            WriteRowWithTokens(cells, tokenIds, row, result.tokens, pulses, result.pulseSlots, result.pulseKinds, reverse);
        }

        private static void ProcessLineIntoColumnWithTokens(
            PestControlCell[,] cells,
            int[,] tokenIds,
            int col,
            List<TokenPiece> line,
            List<PestControlCellPulse> pulses,
            List<int> eatConsumed,
            List<int> mergeConsumed,
            List<(int wolfId, int bugId)> wolfEatsBugPairs,
            bool reverse = false)
        {
            var result = ProcessLineTokens(line);
            eatConsumed.AddRange(result.eatConsumed);
            mergeConsumed.AddRange(result.mergeConsumed);
            wolfEatsBugPairs.AddRange(result.wolfEatsBugPairs);
            WriteColumnWithTokens(cells, tokenIds, col, result.tokens, pulses, result.pulseSlots, result.pulseKinds, reverse);
        }

        private static void WriteRowWithTokens(
            PestControlCell[,] cells,
            int[,] tokenIds,
            int row,
            List<TokenPiece> tokens,
            List<PestControlCellPulse> pulses,
            List<int> pulseSlots,
            List<PestControlCellPulseKind> pulseKinds,
            bool reverse)
        {
            for (int c = 0; c < GridSize; c++)
            {
                cells[row, c] = PestControlCell.Empty;
                tokenIds[row, c] = 0;
            }

            for (int i = 0; i < tokens.Count && i < GridSize; i++)
            {
                int col = reverse ? GridSize - 1 - i : i;
                var t = tokens[i];
                cells[row, col] = PestControlCell.Of(t.type, t.value);
                tokenIds[row, col] = t.tokenId;
            }

            AppendPulsesForLine(row, true, reverse, tokens, pulseSlots, pulseKinds, pulses);
        }

        private static void WriteColumnWithTokens(
            PestControlCell[,] cells,
            int[,] tokenIds,
            int col,
            List<TokenPiece> tokens,
            List<PestControlCellPulse> pulses,
            List<int> pulseSlots,
            List<PestControlCellPulseKind> pulseKinds,
            bool reverse)
        {
            for (int r = 0; r < GridSize; r++)
            {
                cells[r, col] = PestControlCell.Empty;
                tokenIds[r, col] = 0;
            }

            for (int i = 0; i < tokens.Count && i < GridSize; i++)
            {
                int row = reverse ? GridSize - 1 - i : i;
                var t = tokens[i];
                cells[row, col] = PestControlCell.Of(t.type, t.value);
                tokenIds[row, col] = t.tokenId;
            }

            AppendPulsesForLine(col, false, reverse, tokens, pulseSlots, pulseKinds, pulses);
        }

        private static void AppendPulsesForLine(
            int lineIndex,
            bool isRow,
            bool reverse,
            List<TokenPiece> tokens,
            List<int> pulseSlots,
            List<PestControlCellPulseKind> pulseKinds,
            List<PestControlCellPulse> pulses)
        {
            int tokenCount = tokens.Count;
            for (int i = 0; i < pulseSlots.Count; i++)
            {
                int slot = pulseSlots[i];
                if (slot < 0 || slot >= tokenCount)
                    continue;

                int index = reverse ? tokenCount - 1 - slot : slot;
                int row = isRow ? lineIndex : index;
                int col = isRow ? index : lineIndex;
                int tokenId = tokens[slot].tokenId;
                pulses.Add(new PestControlCellPulse
                {
                    row = row,
                    col = col,
                    tokenId = tokenId,
                    kind = pulseKinds[i],
                });
            }
        }

        private struct LineTokenResult
        {
            public List<TokenPiece> tokens;
            public List<int> pulseSlots;
            public List<PestControlCellPulseKind> pulseKinds;
            public List<int> eatConsumed;
            public List<int> mergeConsumed;
            public List<(int wolfId, int bugId)> wolfEatsBugPairs;
        }

        private static LineTokenResult ProcessLineTokens(List<TokenPiece> input)
        {
            var output = new List<TokenPiece>(input.Count);
            var pulseSlots = new List<int>();
            var pulseKinds = new List<PestControlCellPulseKind>();
            var eatConsumed = new List<int>();
            var mergeConsumed = new List<int>();
            var wolfEatsBugPairs = new List<(int wolfId, int bugId)>();
            int i = 0;
            while (i < input.Count)
            {
                if (i + 1 < input.Count)
                {
                    var a = input[i];
                    var b = input[i + 1];
                    if (a.type == b.type)
                    {
                        if (a.value == b.value)
                        {
                            output.Add(new TokenPiece
                            {
                                tokenId = a.tokenId,
                                type = a.type,
                                value = a.value * 2,
                            });
                            mergeConsumed.Add(b.tokenId);
                            pulseSlots.Add(output.Count - 1);
                            pulseKinds.Add(PestControlCellPulseKind.Merge);
                            i += 2;
                            continue;
                        }

                        output.Add(a);
                        i += 1;
                        continue;
                    }

                    if (TryResolvePredationTokens(a, b, out var merged, out var survivorId, out var blockedPair))
                    {
                        if (blockedPair)
                        {
                            output.Add(a);
                            output.Add(b);
                            i += 2;
                        }
                        else
                        {
                            output.Add(new TokenPiece
                            {
                                tokenId = survivorId,
                                type = merged.type,
                                value = merged.value,
                            });
                            int victimId = survivorId == a.tokenId ? b.tokenId : a.tokenId;
                            eatConsumed.Add(victimId);
                            pulseSlots.Add(output.Count - 1);
                            pulseKinds.Add(PestControlCellPulseKind.Eat);
                            if (merged.type == PestControlEntityType.Werewolf)
                                wolfEatsBugPairs.Add((survivorId, victimId));
                            i += 2;
                        }

                        continue;
                    }
                }

                output.Add(input[i]);
                i += 1;
            }

            return new LineTokenResult
            {
                tokens = output,
                pulseSlots = pulseSlots,
                pulseKinds = pulseKinds,
                eatConsumed = eatConsumed,
                mergeConsumed = mergeConsumed,
                wolfEatsBugPairs = wolfEatsBugPairs,
            };
        }

        private static bool TryResolvePredationTokens(
            TokenPiece a,
            TokenPiece b,
            out PestControlCell merged,
            out int survivorTokenId,
            out bool blockedPair)
        {
            merged = PestControlCell.Empty;
            survivorTokenId = 0;
            blockedPair = false;

            TokenPiece werewolf;
            TokenPiece bug;
            if (a.type == PestControlEntityType.Werewolf && b.type == PestControlEntityType.Bug)
            {
                werewolf = a;
                bug = b;
            }
            else if (a.type == PestControlEntityType.Bug && b.type == PestControlEntityType.Werewolf)
            {
                werewolf = b;
                bug = a;
            }
            else
            {
                return false;
            }

            if (werewolf.value > bug.value)
            {
                merged = PestControlCell.Of(PestControlEntityType.Werewolf, werewolf.value + bug.value);
                survivorTokenId = werewolf.tokenId;
                return true;
            }

            if (bug.value > werewolf.value)
            {
                merged = PestControlCell.Of(bug.type, bug.value);
                survivorTokenId = bug.tokenId;
                return true;
            }

            blockedPair = true;
            return true;
        }

        private void BuildSpawnLookup(IReadOnlyList<PestControlSpawnEntry> spawnConfig)
        {
            spawnByTurn.Clear();
            if (spawnConfig == null)
                return;

            for (int i = 0; i < spawnConfig.Count; i++)
            {
                var entry = spawnConfig[i];
                if (!spawnByTurn.TryGetValue(entry.turn, out var list))
                {
                    list = new List<PestControlSpawnEntry>();
                    spawnByTurn[entry.turn] = list;
                }

                list.Add(entry);
            }
        }

        private void ClearGrid()
        {
            for (int r = 0; r < GridSize; r++)
            {
                for (int c = 0; c < GridSize; c++)
                    grid[r, c] = PestControlCell.Empty;
            }
        }

        private void ApplySpawnForTurn(int turn)
        {
            if (!spawnByTurn.TryGetValue(turn, out var entries) || entries == null)
                return;

            for (int i = 0; i < entries.Count; i++)
                SpawnEntry(entries[i]);
        }

        private void SpawnEntry(PestControlSpawnEntry entry)
        {
            if (entry.spawnCount <= 0)
                return;

            var emptyCells = CollectEmptyCells();
            if (emptyCells.Count == 0)
            {
                if (entry.spawnCount > 0)
                    Debug.LogWarning($"[PestControlGameModel] turn={entry.turn} 无空格可生成 {entry.type}×{entry.spawnCount}。");
                return;
            }

            int toPlace = Math.Min(entry.spawnCount, emptyCells.Count);
            if (toPlace < entry.spawnCount)
            {
                Debug.LogWarning(
                    $"[PestControlGameModel] turn={entry.turn} 空格不足，仅放置 {toPlace}/{entry.spawnCount} 个 {entry.type}。");
            }

            Shuffle(emptyCells);
            for (int i = 0; i < toPlace; i++)
            {
                var (row, col) = emptyCells[i];
                grid[row, col] = PestControlCell.Of(entry.type, entry.value);
            }
        }

        private List<(int row, int col)> CollectEmptyCells()
        {
            var list = new List<(int, int)>(GridSize * GridSize);
            for (int r = 0; r < GridSize; r++)
            {
                for (int c = 0; c < GridSize; c++)
                {
                    if (grid[r, c].isEmpty)
                        list.Add((r, c));
                }
            }

            return list;
        }

        private void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private void EvaluateEndState()
        {
            if (CountEntity(PestControlEntityType.Werewolf) == 0)
            {
                Result = PestControlGameResult.Defeat;
                return;
            }

            if (RequiredKillScore > 0 && KillScore >= RequiredKillScore)
            {
                Result = PestControlGameResult.Victory;
                return;
            }

            if (CountEntity(PestControlEntityType.Bug) == 0 && Turn >= VictoryTurnThreshold)
                Result = PestControlGameResult.Victory;
            else
                Result = PestControlGameResult.Playing;
        }

#if UNITY_EDITOR
        public void EditorSetCell(int row, int col, PestControlCell cell)
        {
            grid[row, col] = cell;
            EvaluateEndState();
        }

        public void EditorSetTurn(int turn)
        {
            Turn = turn;
            EvaluateEndState();
        }
#endif

        private static PestControlCell[,] CloneGrid(PestControlCell[,] source)
        {
            var clone = new PestControlCell[GridSize, GridSize];
            for (int r = 0; r < GridSize; r++)
            {
                for (int c = 0; c < GridSize; c++)
                    clone[r, c] = source[r, c];
            }

            return clone;
        }

        private static bool GridsEqual(PestControlCell[,] a, PestControlCell[,] b)
        {
            for (int r = 0; r < GridSize; r++)
            {
                for (int c = 0; c < GridSize; c++)
                {
                    if (!a[r, c].EqualsCell(b[r, c]))
                        return false;
                }
            }

            return true;
        }
    }
}
