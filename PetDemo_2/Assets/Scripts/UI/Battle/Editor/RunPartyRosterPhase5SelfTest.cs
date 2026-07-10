// SPEC §12.14.16 阶段 5 验收自测（Editor 菜单，§12.14.14 全 9 条）。

#if UNITY_EDITOR

using System;

using System.Collections.Generic;

using System.Reflection;

using PetDemo.Battle;

using PetDemo.Core;

using UnityEditor;

using UnityEngine;



namespace PetDemo.UI.Battle.Editor

{

    public static class RunPartyRosterPhase5SelfTest

    {

        [MenuItem("Tools/PetDemo/Self-Test RunPartyRoster Phase5")]

        public static void Run()

        {

            int pass = 0;

            int fail = 0;



            void Assert(bool cond, string name)

            {

                if (cond)

                {

                    pass++;

                    Debug.Log("[Phase5SelfTest] PASS: " + name);

                }

                else

                {

                    fail++;

                    Debug.LogError("[Phase5SelfTest] FAIL: " + name);

                }

            }



            var baseline = new RoleStats

            {

                displayName = "Role",

                atk = 100,

                def = 5,

                maxHp = 80,

                currentHp = 80,

                agility = 10,

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



            // --- SyncRosterHpAfterBattle 存在 ---

            var syncMethod = typeof(RosterBattleSync).GetMethod(

                "SyncRosterHpAfterBattle",

                BindingFlags.Public | BindingFlags.Static);

            Assert(syncMethod != null, "RosterBattleSync.SyncRosterHpAfterBattle 存在");



            // --- §12.14.14 #1：名册 3 人 + Role r2c2 + 队友 col2 ---

            Assert(BuildRoster3().members.Count == 3, "#1 名册 3 人");

            const int placementSeed = 424242;

            var assembler = new BattlePartyAssembler();

            var alliesPlacement = assembler.BuildAllies(BuildRoster3());

            assembler.AssignAllyGridPositions(BuildRoster3(), alliesPlacement, placementSeed);

            BattleUnitRuntime roleUnit = null;

            for (int i = 0; i < alliesPlacement.Count; i++)

            {

                if (alliesPlacement[i].kind == BattleUnitKind.Role)

                    roleUnit = alliesPlacement[i];

            }

            Assert(roleUnit != null && roleUnit.gridPos.row == 2 && roleUnit.gridPos.col == 2,

                "#1 Role 固定 r2c2");

            int col2Followers = 0;

            for (int i = 0; i < alliesPlacement.Count; i++)

            {

                if (alliesPlacement[i].kind == BattleUnitKind.FollowerNpc

                    && alliesPlacement[i].gridPos.col == 2)

                    col2Followers++;

            }

            Assert(col2Followers == 2, "#1 2 名队友落 col2");



            // --- §12.14.14 #2：evt_boost_atk +10% ---

            var rosterAtk = BuildRoster3();

            RunPartyRewardApplier.ApplyPercentStatToAllPartyMembers(rosterAtk, "atk", 10);

            Assert(rosterAtk.members[0].stats.atk == 110 && rosterAtk.members[1].stats.atk == 110,

                "#2 evt_boost_atk 全员 +10% atk");



            // --- §12.14.14 #3：老虎机 +5 atk ---

            var rosterFlat = BuildRoster3();

            RunPartyRewardApplier.ApplyFlatStatToAllPartyMembers(rosterFlat, "atk", 5);

            Assert(rosterFlat.members[2].stats.atk == 105, "#3 老虎机 +5 atk 全员");



            // --- §12.14.14 #4：三选一 skill ---

            var rosterSkill = BuildRoster3();

            const string skillId = "skill_phase5_test";

            RunPartyRewardApplier.AcquireSkillForAllPartyMembers(rosterSkill, skillId);

            Assert(rosterSkill.members[0].acquiredSkillIds.Contains(skillId)

                && rosterSkill.members[2].acquiredSkillIds.Contains(skillId),

                "#4 三选一 skill 全员 acquiredSkillIds");



            // --- §12.14.14 #5：NPC 阵亡、Role 存活并胜 → 30% 复活 + 存活者战后 HP ---

            var roster5 = BuildRoster3();

            roster5.members[0].stats.maxHp = 100;

            roster5.members[0].stats.currentHp = 100;

            roster5.members[1].stats.maxHp = 100;

            roster5.members[1].stats.currentHp = 100;

            var session5 = new GridBattleSession { battleSeed = 1 };

            var roleBattle = new BattleUnitRuntime

            {

                rosterId = RunPartyRosterFactory.RoleRosterId,

                side = BattleSide.Ally,

                kind = BattleUnitKind.Role,

                stats = new RoleStats { maxHp = 100, currentHp = 37 },

            };

            var deadNpc = new BattleUnitRuntime

            {

                rosterId = "follower_friend-01",

                side = BattleSide.Ally,

                kind = BattleUnitKind.FollowerNpc,

                stats = new RoleStats { maxHp = 100, currentHp = 0 },

                isBattleDead = true,

            };

            session5.allies.Add(roleBattle);

            session5.allies.Add(deadNpc);

            RosterBattleSync.SyncRosterHpAfterBattle(roster5, session5, true);

            Assert(roster5.members[0].stats.currentHp == 37, "#5 Role 保留战后 HP");

            Assert(roster5.members[1].stats.currentHp == 30,

                "#5 阵亡 NPC 复活为 max(1,floor(maxHp*0.3))=30");



            // --- §12.14.14 #6：我方全灭 → 判负（负不写回） ---

            var roster6 = BuildRoster3();

            int hpBeforeLoss = roster6.members[0].stats.currentHp;

            var lossSession = new GridBattleSession { battleSeed = 2 };

            var deadAlly = new BattleUnitRuntime

            {

                rosterId = RunPartyRosterFactory.RoleRosterId,

                side = BattleSide.Ally,

                stats = new RoleStats { maxHp = 80, currentHp = 0 },

                isBattleDead = true,

            };

            var livingEnemy = new BattleUnitRuntime

            {

                side = BattleSide.Enemy,

                stats = new RoleStats { maxHp = 40, currentHp = 10 },

            };

            lossSession.allies.Add(deadAlly);

            lossSession.enemies.Add(livingEnemy);

            var lossDriver = new GridBattleDriver(lossSession);

            lossDriver.IsBattleFinished(out bool playerWon6);
            Assert(lossSession.finished && !playerWon6, "#6 我方全灭判负");

            RosterBattleSync.SyncRosterHpAfterBattle(roster6, lossSession, false);

            Assert(roster6.members[0].stats.currentHp == hpBeforeLoss,

                "#6 负局不写回名册 HP");



            // --- §12.14.14 #7：敌方全灭 → 判胜 ---

            var winSession = new GridBattleSession { battleSeed = 3 };

            winSession.allies.Add(new BattleUnitRuntime

            {

                side = BattleSide.Ally,

                stats = new RoleStats { maxHp = 50, currentHp = 50, agility = 10 },

            });

            winSession.enemies.Add(new BattleUnitRuntime

            {

                side = BattleSide.Enemy,

                stats = new RoleStats { maxHp = 5, currentHp = 5, agility = 1 },

            });

            var winDriver = new GridBattleDriver(winSession);

            winDriver.BeginRound();

            winDriver.ApplyNormalAttack(winSession.allies[0], winSession.enemies[0]);

            Assert(winSession.finished && winSession.playerWon, "#7 敌方全灭立即胜");



            // --- §12.14.14 #8：当前行动者击杀末敌 → 胜（敌方先判） ---

            var dualSession = new GridBattleSession { battleSeed = 4 };

            var lastAlly = new BattleUnitRuntime

            {

                instanceId = "ally_last",

                side = BattleSide.Ally,

                stats = new RoleStats { maxHp = 10, currentHp = 1, agility = 99 },

            };

            var lastEnemy = new BattleUnitRuntime

            {

                instanceId = "enemy_last",

                side = BattleSide.Enemy,

                stats = new RoleStats { maxHp = 3, currentHp = 3, agility = 1 },

            };

            dualSession.allies.Add(lastAlly);

            dualSession.enemies.Add(lastEnemy);

            var dualDriver = new GridBattleDriver(dualSession);

            dualDriver.BeginRound();

            dualDriver.ApplyNormalAttack(lastAlly, lastEnemy);

            Assert(dualSession.playerWon, "#8 当前行动者击杀末敌→胜");



            // --- §12.14.14 #9：pendingEventId 分支（v3.220 全队九宫格）---

            Assert(string.Equals(

                GridEncounterBuilder.EventFightSmall2, "evt_fight_small_2", StringComparison.Ordinal),

                "#9 evt_fight_small_2 常量");

            Assert(GridEncounterBuilder.IsGridPartyBattleEvent("evt_fight_small_1"),

                "#9 evt_fight_small_1 走九宫格全队战");

            Assert(GridEncounterBuilder.IsGridPartyBattleEvent(GridEncounterBuilder.EventFightBoss),

                "#9 evt_fight_boss 走九宫格全队战");



            // --- 胜后回写 + §12.14.12.1 hp 边界回归 ---

            Assert(RosterBattleSync.ComputeRevivalHp(3) == 1, "maxHp=3 复活至少 1");

            var rosterAfterWin = BuildRoster3();

            RunPartyRewardApplier.ApplyPercentStatToAllPartyMembers(rosterAfterWin, "hp", 10);

            var winSyncSession = new GridBattleSession();

            winSyncSession.allies.Add(new BattleUnitRuntime

            {

                rosterId = RunPartyRosterFactory.RoleRosterId,

                side = BattleSide.Ally,

                stats = new RoleStats { maxHp = 88, currentHp = 0 },

                isBattleDead = true,

            });

            RosterBattleSync.SyncRosterHpAfterBattle(rosterAfterWin, winSyncSession, true);

            Assert(rosterAfterWin.members[0].stats.currentHp

                == RosterBattleSync.ComputeRevivalHp(88),

                "胜后复活基于名册 maxHp（含事件加成）");



            // --- OnEmbeddedBattleEnded 接线 ---

            var gridField = typeof(InvasionBattleModal2View).GetField(

                "activeGridBattleSession",

                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert(gridField != null && gridField.FieldType == typeof(GridBattleSession),

                "InvasionBattleModal2View.activeGridBattleSession 字段");



            var rebuild = typeof(InvasionBattleModal2View).GetMethod(

                "RebuildPartyStandVisuals",

                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert(rebuild != null, "RebuildPartyStandVisuals 存在");



            string summary = fail == 0

                ? "[Phase5SelfTest] §12.14.14 全部 9 条通过 (" + pass + ")"

                : "[Phase5SelfTest] 失败 " + fail + " / 通过 " + pass;

            if (fail == 0)

                Debug.Log(summary);

            else

                Debug.LogError(summary);

            EditorUtility.DisplayDialog("RunPartyRoster Phase5 Self-Test", summary, "OK");

        }

    }

}

#endif


