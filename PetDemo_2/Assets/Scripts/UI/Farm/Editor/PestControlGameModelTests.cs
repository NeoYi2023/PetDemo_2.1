#if UNITY_EDITOR
using PetDemo.Farm;
using UnityEditor;
using UnityEngine;

namespace PetDemo.UI.Farm.Editor
{
    public static class PestControlGameModelTests
    {
        [MenuItem("Tools/PetDemo/Run Pest Control Game Model Tests")]
        public static void RunAll()
        {
            int passed = 0;
            int failed = 0;
            Run("MergeSameTypeSameValue", TestMergeSameTypeSameValue, ref passed, ref failed);
            Run("PredationWerewolfEatsBug", TestPredationWerewolfEatsBug, ref passed, ref failed);
            Run("PredationBugEatsWerewolf", TestPredationBugEatsWerewolf, ref passed, ref failed);
            Run("PredationEqualBlocks", TestPredationEqualBlocks, ref passed, ref failed);
            Run("InvalidSwipeDoesNotAdvanceTurn", TestInvalidSwipeDoesNotAdvanceTurn, ref passed, ref failed);
            Run("PrepareSwipeDoesNotAdvanceTurn", TestPrepareSwipeDoesNotAdvanceTurn, ref passed, ref failed);
            Run("CommitAfterPrepareAdvancesTurn", TestCommitAfterPrepareAdvancesTurn, ref passed, ref failed);
            Run("PrepareSwipeTagsEatConsumed", TestPrepareSwipeTagsEatConsumed, ref passed, ref failed);
            Run("PrepareSwipeTagsMergeConsumed", TestPrepareSwipeTagsMergeConsumed, ref passed, ref failed);
            Run("PrepareSwipeOrdersMovesFromLeftEdge", TestPrepareSwipeOrdersMovesFromLeftEdge, ref passed, ref failed);
            Run("PrepareSwipeOrdersMovesFromRightEdge", TestPrepareSwipeOrdersMovesFromRightEdge, ref passed, ref failed);
            Run("PrepareSwipeOrdersPulsesFromTargetEdge", TestPrepareSwipeOrdersPulsesFromTargetEdge, ref passed, ref failed);
            Run("VictoryRequiresTurn20AndNoBugs", TestVictoryRequiresTurn20AndNoBugs, ref passed, ref failed);
            Run("DefeatWhenNoWerewolves", TestDefeatWhenNoWerewolves, ref passed, ref failed);
            Run("KillScoreIncrementsByBugValueOnWolfEat", TestKillScoreIncrementsByBugValueOnWolfEat, ref passed, ref failed);
            Run("KillScoreVictoryBeforeTurn20WithBugsRemaining", TestKillScoreVictoryBeforeTurn20WithBugsRemaining, ref passed, ref failed);
            Run("KillScoreVictoryTakesPriority", TestKillScoreVictoryTakesPriority, ref passed, ref failed);
            Debug.Log($"[PestControlGameModelTests] 完成：{passed} 通过，{failed} 失败。");
        }

        private static void Run(string name, System.Func<bool> test, ref int passed, ref int failed)
        {
            if (test())
            {
                passed++;
                Debug.Log($"[PestControlGameModelTests] PASS {name}");
            }
            else
            {
                failed++;
                Debug.LogError($"[PestControlGameModelTests] FAIL {name}");
            }
        }

        private static bool TestMergeSameTypeSameValue()
        {
            var model = CreateModelWithGrid(new[,]
            {
                { E(PestControlEntityType.Bug, 2), E(PestControlEntityType.Bug, 2), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
            });

            if (!model.TrySwipe(PestControlSwipeDirection.Left))
                return false;
            var cell = model.GetCell(0, 0);
            return !cell.isEmpty && cell.type == PestControlEntityType.Bug && cell.value == 4
                   && model.CountEntity(PestControlEntityType.Bug) == 1;
        }

        private static bool TestPredationWerewolfEatsBug()
        {
            var model = CreateModelWithGrid(new[,]
            {
                { E(PestControlEntityType.Werewolf, 4), E(PestControlEntityType.Bug, 2), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
            });

            if (!model.TrySwipe(PestControlSwipeDirection.Left))
                return false;
            var cell = model.GetCell(0, 0);
            return !cell.isEmpty && cell.type == PestControlEntityType.Werewolf && cell.value == 6
                   && model.CountEntity(PestControlEntityType.Bug) == 0
                   && model.KillScore == 2;
        }

        private static bool TestPredationBugEatsWerewolf()
        {
            var model = CreateModelWithGrid(new[,]
            {
                { E(PestControlEntityType.Bug, 4), E(PestControlEntityType.Werewolf, 2), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
            });

            if (!model.TrySwipe(PestControlSwipeDirection.Left))
                return false;
            var cell = model.GetCell(0, 0);
            return !cell.isEmpty && cell.type == PestControlEntityType.Bug && cell.value == 4
                   && model.CountEntity(PestControlEntityType.Werewolf) == 0
                   && model.Result == PestControlGameResult.Defeat;
        }

        private static bool TestPredationEqualBlocks()
        {
            var model = CreateModelWithGrid(new[,]
            {
                { E(PestControlEntityType.Werewolf, 2), E(PestControlEntityType.Bug, 2), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
            });

            if (!model.TrySwipe(PestControlSwipeDirection.Left))
                return false;
            return model.CountEntity(PestControlEntityType.Werewolf) == 1
                   && model.CountEntity(PestControlEntityType.Bug) == 1;
        }

        private static bool TestPrepareSwipeDoesNotAdvanceTurn()
        {
            var model = CreateModelWithGrid(new[,]
            {
                { E(PestControlEntityType.Bug, 2), E(PestControlEntityType.Bug, 2), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
            });

            int turnBefore = model.Turn;
            if (!model.TryPrepareSwipe(PestControlSwipeDirection.Left, out var plan))
                return false;
            if (!plan.changed)
                return false;
            return model.Turn == turnBefore;
        }

        private static bool TestCommitAfterPrepareAdvancesTurn()
        {
            var model = CreateModelWithGrid(new[,]
            {
                { E(PestControlEntityType.Bug, 2), E(PestControlEntityType.Bug, 2), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
            });

            int turnBefore = model.Turn;
            if (!model.TryPrepareSwipe(PestControlSwipeDirection.Left, out var plan))
                return false;
            model.CommitSwipeAndSpawn(plan);
            return model.Turn == turnBefore + 1;
        }

        private static bool TestPrepareSwipeTagsEatConsumed()
        {
            var model = CreateModelWithGrid(new[,]
            {
                { E(PestControlEntityType.Werewolf, 4), E(PestControlEntityType.Bug, 2), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
            });

            if (!model.TryPrepareSwipe(PestControlSwipeDirection.Left, out var plan))
                return false;
            return plan.eatConsumedTokenIds.Count == 1
                   && plan.mergeConsumedTokenIds.Count == 0
                   && plan.pulses.Exists(p => p.kind == PestControlCellPulseKind.Eat);
        }

        private static bool TestPrepareSwipeTagsMergeConsumed()
        {
            var model = CreateModelWithGrid(new[,]
            {
                { E(PestControlEntityType.Bug, 2), E(PestControlEntityType.Bug, 2), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
            });

            if (!model.TryPrepareSwipe(PestControlSwipeDirection.Left, out var plan))
                return false;
            return plan.mergeConsumedTokenIds.Count == 1
                   && plan.eatConsumedTokenIds.Count == 0
                   && plan.pulses.Exists(p => p.kind == PestControlCellPulseKind.Merge);
        }

        private static bool TestPrepareSwipeOrdersMovesFromLeftEdge()
        {
            var model = CreateModelWithGrid(new[,]
            {
                { E(), E(PestControlEntityType.Bug, 2), E(), E(PestControlEntityType.Bug, 4), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
            });

            if (!model.TryPrepareSwipe(PestControlSwipeDirection.Left, out var plan))
                return false;
            return plan.moves.Count == 2
                   && plan.moves[0].actionOrder == 1
                   && plan.moves[1].actionOrder == 3
                   && plan.moves[0].fromCol < plan.moves[1].fromCol;
        }

        private static bool TestPrepareSwipeOrdersMovesFromRightEdge()
        {
            var model = CreateModelWithGrid(new[,]
            {
                { E(), E(PestControlEntityType.Bug, 2), E(), E(PestControlEntityType.Bug, 4), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
            });

            if (!model.TryPrepareSwipe(PestControlSwipeDirection.Right, out var plan))
                return false;
            return plan.moves.Count == 2
                   && plan.moves[0].actionOrder == 1
                   && plan.moves[1].actionOrder == 3
                   && plan.moves[0].fromCol > plan.moves[1].fromCol;
        }

        private static bool TestPrepareSwipeOrdersPulsesFromTargetEdge()
        {
            var model = CreateModelWithGrid(new[,]
            {
                { E(PestControlEntityType.Bug, 2), E(PestControlEntityType.Bug, 2), E(), E(PestControlEntityType.Bug, 4), E(PestControlEntityType.Bug, 4) },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
            });

            if (!model.TryPrepareSwipe(PestControlSwipeDirection.Left, out var plan))
                return false;
            return plan.pulses.Count == 2
                   && plan.pulses[0].actionOrder == 0
                   && plan.pulses[1].actionOrder == 1
                   && plan.pulses[0].col < plan.pulses[1].col;
        }

        private static bool TestInvalidSwipeDoesNotAdvanceTurn()
        {
            var model = CreateModelWithGrid(new[,]
            {
                { E(PestControlEntityType.Werewolf, 2), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
            });

            int turnBefore = model.Turn;
            if (model.TrySwipe(PestControlSwipeDirection.Up))
                return false;
            return model.Turn == turnBefore;
        }

        private static bool TestVictoryRequiresTurn20AndNoBugs()
        {
            var model = new PestControlGameModel(12345);
            model.Reset(System.Array.Empty<PestControlSpawnEntry>());
            SeedGrid(model, new[,]
            {
                { E(PestControlEntityType.Werewolf, 2), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
            });

            model.EditorSetTurn(19);
            if (model.Result == PestControlGameResult.Victory)
                return false;

            model.EditorSetTurn(20);
            return model.Result == PestControlGameResult.Victory
                   && model.CountEntity(PestControlEntityType.Bug) == 0;
        }

        private static bool TestDefeatWhenNoWerewolves()
        {
            var model = CreateModelWithGrid(new[,]
            {
                { E(PestControlEntityType.Bug, 8), E(PestControlEntityType.Werewolf, 2), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
            });

            model.TrySwipe(PestControlSwipeDirection.Left);
            return model.Result == PestControlGameResult.Defeat;
        }

        private static bool TestKillScoreIncrementsByBugValueOnWolfEat()
        {
            var model = CreateModelWithGrid(new[,]
            {
                { E(PestControlEntityType.Werewolf, 4), E(PestControlEntityType.Bug, 2), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
            }, requiredKillScore: 999);

            if (!model.TrySwipe(PestControlSwipeDirection.Left))
                return false;
            if (model.KillScore != 2)
                return false;

            SeedGrid(model, new[,]
            {
                { E(PestControlEntityType.Werewolf, 6), E(PestControlEntityType.Bug, 4), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
            });

            if (!model.TrySwipe(PestControlSwipeDirection.Left))
                return false;
            return model.KillScore == 6;
        }

        private static bool TestKillScoreVictoryBeforeTurn20WithBugsRemaining()
        {
            var model = CreateModelWithGrid(new[,]
            {
                { E(PestControlEntityType.Werewolf, 8), E(PestControlEntityType.Bug, 2), E(PestControlEntityType.Bug, 4), E(), E() },
                { E(PestControlEntityType.Bug, 2), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
            }, requiredKillScore: 6);

            if (!model.TrySwipe(PestControlSwipeDirection.Left))
                return false;
            return model.Result == PestControlGameResult.Victory
                   && model.Turn < PestControlGameModel.VictoryTurnThreshold
                   && model.KillScore >= 6
                   && model.CountEntity(PestControlEntityType.Bug) > 0;
        }

        private static bool TestKillScoreVictoryTakesPriority()
        {
            var model = new PestControlGameModel(99);
            model.Reset(System.Array.Empty<PestControlSpawnEntry>(), requiredKillScore: 2);
            SeedGrid(model, new[,]
            {
                { E(PestControlEntityType.Werewolf, 4), E(PestControlEntityType.Bug, 2), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
                { E(), E(), E(), E(), E() },
            });

            model.EditorSetTurn(20);
            if (!model.TrySwipe(PestControlSwipeDirection.Left))
                return false;
            return model.Result == PestControlGameResult.Victory
                   && model.KillScore == 2
                   && model.Turn == 21;
        }

        private static PestControlCell E(PestControlEntityType type = PestControlEntityType.Bug, int value = 0)
        {
            if (value <= 0)
                return PestControlCell.Empty;
            return PestControlCell.Of(type, value);
        }

        private static PestControlGameModel CreateModelWithGrid(PestControlCell[,] cells, int requiredKillScore = 0)
        {
            var model = new PestControlGameModel(42);
            model.Reset(System.Array.Empty<PestControlSpawnEntry>(), requiredKillScore);
            SeedGrid(model, cells);
            return model;
        }

        private static void SeedGrid(PestControlGameModel model, PestControlCell[,] cells)
        {
            for (int r = 0; r < PestControlGameModel.GridSize; r++)
            {
                for (int c = 0; c < PestControlGameModel.GridSize; c++)
                    model.EditorSetCell(r, c, cells[r, c]);
            }
        }
    }
}
#endif
