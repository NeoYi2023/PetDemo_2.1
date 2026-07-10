// SPEC §12.14.16 阶段 4 验收自测（Editor 菜单，分支与 BuildEmbeddedGrid 前置条件）。
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using PetDemo.Battle;
using PetDemo.Core;
using PetDemo.EditorTools;
using UnityEditor;
using UnityEngine;

namespace PetDemo.UI.Battle.Editor
{
    public static class RunPartyRosterPhase4SelfTest
    {
        [MenuItem("Tools/PetDemo/Self-Test RunPartyRoster Phase4")]
        public static void Run()
        {
            int pass = 0;
            int fail = 0;

            void Assert(bool cond, string name)
            {
                if (cond)
                {
                    pass++;
                    Debug.Log("[Phase4SelfTest] PASS: " + name);
                }
                else
                {
                    fail++;
                    Debug.LogError("[Phase4SelfTest] FAIL: " + name);
                }
            }

            // --- pendingEventId：三类战斗均走九宫格（v3.220）---
            Assert(
                string.Equals(
                    GridEncounterBuilder.EventFightSmall2,
                    "evt_fight_small_2",
                    StringComparison.Ordinal),
                "evt_fight_small_2 常量");
            Assert(
                GridEncounterBuilder.IsGridPartyBattleEvent("evt_fight_small_1"),
                "evt_fight_small_1 走九宫格全队战");
            Assert(
                GridEncounterBuilder.IsGridPartyBattleEvent("evt_fight_boss"),
                "evt_fight_boss 走九宫格全队战");
            Assert(
                GridEncounterBuilder.IsGridPartyBattleEvent(GridEncounterBuilder.EventFightSmall2),
                "evt_fight_small_2 走九宫格全队战");

            // --- BuildEmbeddedGrid 工厂方法存在 ---
            var buildGrid = typeof(InvasionBattleView).GetMethod(
                "BuildEmbeddedGrid",
                BindingFlags.Public | BindingFlags.Static);
            Assert(buildGrid != null, "InvasionBattleView.BuildEmbeddedGrid 存在");

            // --- GridBattleField 预制体 ---
            if (Resources.Load<GameObject>(GridBattleConstants.GridBattleFieldPrefabPath) == null)
                GridBattleFieldPrefabGenerator.Generate();
            var fieldPrefab = Resources.Load<GameObject>(GridBattleConstants.GridBattleFieldPrefabPath);
            Assert(fieldPrefab != null, "GridBattleField.prefab 可加载");
            if (fieldPrefab != null)
            {
                Assert(fieldPrefab.GetComponent<GridBattleFieldLayout>() != null,
                    "GridBattleField 挂 GridBattleFieldLayout");
            }

            // --- 多单位战 session 可组装（LaunchEmbeddedGridBattle 前置）---
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
            };
            var roster3 = RunPartyRosterFactory.Build(
                baseline,
                new List<string> { "friend-01", "friend-02" },
                (string id, out string name, out string prefab, out bool found) =>
                {
                    name = id;
                    prefab = RunPartyRosterFactory.DefaultRoleSkeletonPrefab;
                    found = true;
                });

            var encounter = new GridEncounterBuilder(ResolveUnit);
            var enemies1 = encounter.BuildEnemies(GridEncounterBuilder.EventFightSmall1, 4001);
            Assert(enemies1.Count == 1, "evt_fight_small_1 刷 1 怪");

            int battleSeed = GridBattleSeedUtil.MixSeed(
                GridBattleSeedUtil.HashString(GridEncounterBuilder.EventFightSmall2),
                8,
                roster3.members.Count);

            var session = GridBattleSessionFactory.Create(
                roster3,
                GridEncounterBuilder.EventFightSmall2,
                battleSeed,
                ResolveUnit);
            Assert(session.allies.Count == 3, "多单位战我方 3 人");
            Assert(session.enemies.Count >= 2 && session.enemies.Count <= 3, "多单位战敌方 2~3");
            Assert(session.pendingEventId == GridEncounterBuilder.EventFightSmall2, "session.pendingEventId");

            var bossSession = GridBattleSessionFactory.Create(
                roster3,
                GridEncounterBuilder.EventFightBoss,
                battleSeed ^ 99,
                ResolveUnit);
            Assert(bossSession.allies.Count == 3, "BOSS 战我方仍 3 人");
            Assert(bossSession.enemies.Count == 1, "BOSS 战敌方 1");

            // --- headless 仍可跑完（规则与 UI 驱动共用 GridBattleDriver）---
            bool finished = GridBattleHeadlessRunner.RunToCompletion(session, out bool won);
            Assert(finished && session.finished, "组装后的 session headless 跑完");

            string summary = fail == 0
                ? "[Phase4SelfTest] 全部通过 (" + pass + ")"
                : "[Phase4SelfTest] 失败 " + fail + " / 通过 " + pass;
            if (fail == 0)
                Debug.Log(summary);
            else
                Debug.LogError(summary);
            EditorUtility.DisplayDialog("RunPartyRoster Phase4 Self-Test", summary, "OK");
        }
    }
}
#endif
