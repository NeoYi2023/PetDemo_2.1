// SPEC §12.14.16 阶段 3 验收自测（Editor 菜单，headless 战斗纯逻辑）。
#if UNITY_EDITOR
using System.Collections.Generic;
using PetDemo.Battle;
using PetDemo.Core;
using PetDemo.EditorTools;
using UnityEditor;
using UnityEngine;

namespace PetDemo.UI.Battle.Editor
{
    public static class RunPartyRosterPhase3SelfTest
    {
        [MenuItem("Tools/PetDemo/Self-Test RunPartyRoster Phase3")]
        public static void Run()
        {
            int pass = 0;
            int fail = 0;

            void Assert(bool cond, string name)
            {
                if (cond)
                {
                    pass++;
                    Debug.Log("[Phase3SelfTest] PASS: " + name);
                }
                else
                {
                    fail++;
                    Debug.LogError("[Phase3SelfTest] FAIL: " + name);
                }
            }

            var units = InvasionConfigCatalog.BuildDefaultInvasionUnits();
            InvasionUnitConfig ResolveUnit(string unitId) =>
                InvasionConfigCatalog.FindById(units, unitId);

            var baseline = new RoleStats
            {
                displayName = "Role",
                atk = 12,
                def = 5,
                maxHp = 80,
                currentHp = 80,
                agility = 6,
                criticalHit = 3,
                combo = 6,
                counterattack = 12,
                stun = 2,
                evasion = 4,
                lifeSteal = 8,
            };

            RunPartyRoster BuildRoster3()
            {
                return RunPartyRosterFactory.Build(
                    baseline,
                    new List<string> { "friend-01", "friend-02" },
                    (string id, out string name, out string prefab, out bool found) =>
                    {
                        name = id;
                        prefab = RunPartyRosterFactory.DefaultRoleSkeletonPrefab;
                        found = true;
                    });
            }

            // --- kGridBattlePetsEnabled ---
            Assert(!GridBattleConstants.kGridBattlePetsEnabled, "kGridBattlePetsEnabled=false");

            // --- 预制体存在 ---
            if (Resources.Load<GameObject>(GridBattleFieldPrefabGenerator.PrefabResourcePath) == null)
                GridBattleFieldPrefabGenerator.Generate();
            var prefab = Resources.Load<GameObject>(GridBattleFieldPrefabGenerator.PrefabResourcePath);
            Assert(prefab != null, "GridBattleField.prefab 可加载");
            if (prefab != null)
            {
                var layout = prefab.GetComponent<GridBattleFieldLayout>();
                Assert(layout != null, "GridBattleField 挂 GridBattleFieldLayout");
                int markerCount = prefab.GetComponentsInChildren<BattleGridSlotMarker>(true).Length;
                Assert(markerCount == 18, "九宫格共 18 槽（Ally+Enemy）");
            }

            // --- 站位：Role r2c2，队友 col2 优先 ---
            const int placementSeed = 424242;
            var roster3 = BuildRoster3();
            var assembler = new BattlePartyAssembler();
            var allies = assembler.BuildAllies(roster3);
            assembler.AssignAllyGridPositions(roster3, allies, placementSeed);
            BattleUnitRuntime role = null;
            for (int i = 0; i < allies.Count; i++)
            {
                if (allies[i].kind == BattleUnitKind.Role)
                    role = allies[i];
            }
            Assert(role != null && role.gridPos.row == 2 && role.gridPos.col == 2, "Role 固定 r2c2");
            int col2Followers = 0;
            for (int i = 0; i < allies.Count; i++)
            {
                if (allies[i].kind == BattleUnitKind.FollowerNpc && allies[i].gridPos.col == 2)
                    col2Followers++;
            }
            Assert(col2Followers == 2, "2 名队友优先落 col2（r1c2/r3c2）");

            // --- 遭遇：small_1 / small_2 / boss ---
            var encounter = new GridEncounterBuilder(ResolveUnit);
            var enemies1 = encounter.BuildEnemies(GridEncounterBuilder.EventFightSmall1, 3003);
            Assert(enemies1.Count == 1, "evt_fight_small_1 刷 1 怪");
            var enemies2 = encounter.BuildEnemies(GridEncounterBuilder.EventFightSmall2, 1001);
            var enemies3 = encounter.BuildEnemies(GridEncounterBuilder.EventFightSmall2, 2002);
            Assert(enemies2.Count >= 2 && enemies2.Count <= 3, "遭遇敌人数 2~3（seed=1001）");
            Assert(enemies3.Count >= 2 && enemies3.Count <= 3, "遭遇敌人数 2~3（seed=2002）");
            var enemiesBoss = encounter.BuildEnemies(GridEncounterBuilder.EventFightBoss, 4004);
            Assert(enemiesBoss.Count == 1, "evt_fight_boss 刷 1 BOSS");

            var slotSet = new HashSet<string>();
            for (int i = 0; i < enemies2.Count; i++)
            {
                var p = enemies2[i].gridPos;
                Assert(slotSet.Add(p.row + "," + p.col), "敌方槽位无重复");
                Assert(enemies2[i].stats.agility == GridEncounterBuilder.DefaultEnemyAgility, "敌方 agility=2");
                Assert(enemies2[i].stats.def == 0, "敌方 def=0");
            }

            // --- headless：3 人 vs 2~3 怪跑完 ---
            const int battleSeed = 777888;
            var session3vN = GridBattleSessionFactory.Create(
                BuildRoster3(),
                GridEncounterBuilder.EventFightSmall2,
                battleSeed,
                ResolveUnit);
            Assert(session3vN.allies.Count == 3, "开战我方 3 人");
            Assert(session3vN.enemies.Count >= 2 && session3vN.enemies.Count <= 3, "开战敌方 2~3");

            bool finished3 = GridBattleHeadlessRunner.RunToCompletion(session3vN, out bool won3);
            Assert(finished3, "3人队 headless 战斗跑完");
            Assert(session3vN.finished, "session.finished=true");
            Debug.Log("[Phase3SelfTest] 3vN 结果 playerWon=" + won3
                + " rounds=" + session3vN.roundIndex
                + " enemies=" + session3vN.enemies.Count);

            // --- 仅 Role 亦可开战 ---
            var roster1 = RunPartyRosterFactory.Build(baseline, new List<string>(), null);
            var session1vN = GridBattleSessionFactory.Create(
                roster1,
                GridEncounterBuilder.EventFightSmall2,
                13579,
                ResolveUnit);
            bool finished1 = GridBattleHeadlessRunner.RunToCompletion(session1vN, out _);
            Assert(finished1 && session1vN.allies.Count == 1, "仅 Role headless 跑完");

            // --- §12.14.6.1.1 即时胜负：敌方全灭后立即胜利 ---
            var instantWinSession = new GridBattleSession { battleSeed = 1, roundIndex = 1 };
            var allyWin = new BattleUnitRuntime
            {
                instanceId = "ally_role",
                side = BattleSide.Ally,
                stats = new RoleStats { atk = 99, def = 0, maxHp = 50, currentHp = 50, agility = 10 },
            };
            var enemyLast = new BattleUnitRuntime
            {
                instanceId = "enemy_0",
                side = BattleSide.Enemy,
                stats = new RoleStats { atk = 1, def = 0, maxHp = 5, currentHp = 5, agility = 1 },
            };
            instantWinSession.allies.Add(allyWin);
            instantWinSession.enemies.Add(enemyLast);
            var driver = new GridBattleDriver(instantWinSession);
            driver.BeginRound();
            driver.ApplyNormalAttack(allyWin, enemyLast);
            Assert(instantWinSession.finished && instantWinSession.playerWon, "最后一击敌方全灭→立即胜");

            // --- 暂死标记 ---
            var deadEnemy = new BattleUnitRuntime
            {
                instanceId = "enemy_dead",
                side = BattleSide.Enemy,
                stats = new RoleStats { atk = 1, def = 0, maxHp = 10, currentHp = 0, agility = 1 },
                isBattleDead = true,
            };
            Assert(!GridBattleLiving.IsLiving(deadEnemy), "HP<=0 且 isBattleDead 不算存活");

            // --- 目标选择：col1 优先于 col2，同行优先 ---
            var selector = new GridBattleTargetSelector();
            var attacker = new BattleUnitRuntime
            {
                instanceId = "atk",
                side = BattleSide.Ally,
                gridPos = new BattleGridPos(2, 1),
                stats = new RoleStats { agility = 5 },
            };
            var sameRow = new BattleUnitRuntime
            {
                instanceId = "e_same",
                side = BattleSide.Enemy,
                gridPos = new BattleGridPos(2, 1),
                stats = new RoleStats { currentHp = 10, maxHp = 10, agility = 1 },
            };
            var otherRow = new BattleUnitRuntime
            {
                instanceId = "e_other",
                side = BattleSide.Enemy,
                gridPos = new BattleGridPos(1, 1),
                stats = new RoleStats { currentHp = 10, maxHp = 10, agility = 1 },
            };
            var col2Target = new BattleUnitRuntime
            {
                instanceId = "e_col2",
                side = BattleSide.Enemy,
                gridPos = new BattleGridPos(2, 2),
                stats = new RoleStats { currentHp = 10, maxHp = 10, agility = 1 },
            };
            var picked = selector.PickNormalAttackTarget(
                attacker,
                new List<BattleUnitRuntime> { otherRow, sameRow, col2Target },
                1,
                99);
            Assert(picked == sameRow, "col1 同行目标优先于异行与 col2");

            string summary = fail == 0
                ? "[Phase3SelfTest] 全部通过 (" + pass + ")"
                : "[Phase3SelfTest] 失败 " + fail + " / 通过 " + pass;
            if (fail == 0)
                Debug.Log(summary);
            else
                Debug.LogError(summary);
            EditorUtility.DisplayDialog("RunPartyRoster Phase3 Self-Test", summary, "OK");
        }
    }
}
#endif
