// SPEC §12.14.16 阶段 1 验收自测（Editor 菜单，不依赖 Play Mode）。
#if UNITY_EDITOR
using System.Collections.Generic;
using PetDemo.Battle;
using PetDemo.Core;
using PetDemo.UI;
using UnityEditor;
using UnityEngine;

namespace PetDemo.UI.Battle.Editor
{
    public static class RunPartyRosterPhase1SelfTest
    {
        [MenuItem("Tools/PetDemo/Self-Test RunPartyRoster Phase1")]
        public static void Run()
        {
            int pass = 0;
            int fail = 0;

            void Assert(bool cond, string name)
            {
                if (cond)
                {
                    pass++;
                    Debug.Log("[Phase1SelfTest] PASS: " + name);
                }
                else
                {
                    fail++;
                    Debug.LogError("[Phase1SelfTest] FAIL: " + name);
                }
            }

            // --- PeekFollowers 非消费 ---
            GuildHomeVisitState.Clear();
            GuildHomeVisitState.SetFollowers(new[] { "friend-01", "friend-02" });
            var peek1 = GuildHomeVisitState.PeekFollowers();
            Assert(peek1 != null && peek1.Count == 2, "PeekFollowers 返回 2 人");
            Assert(GuildHomeVisitState.HasPending, "Peek 后快照仍挂起");
            var peek2 = GuildHomeVisitState.PeekFollowers();
            Assert(peek2 != null && peek2.Count == 2, "再次 Peek 仍为 2 人");
            var consumed = GuildHomeVisitState.Consume();
            Assert(consumed != null && consumed.Count == 2, "Consume 返回 2 人");
            Assert(!GuildHomeVisitState.HasPending, "Consume 后快照清空");
            Assert(GuildHomeVisitState.PeekFollowers().Count == 0, "Consume 后 Peek 为空");

            // --- 名册：2 跟随 → 3 人；runStats 别名 ---
            var baseline = new RoleStats
            {
                displayName = "Role",
                atk = 12,
                def = 5,
                maxHp = 80,
                currentHp = 80,
                agility = 4,
                criticalHit = 3,
                combo = 6,
                counterattack = 12,
                stun = 2,
                evasion = 4,
                lifeSteal = 8,
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
            Assert(roster3.members.Count == 3, "带 2 NPC → 名册 3 人");
            Assert(roster3.members[0].kind == BattleUnitKind.Role, "首位为 Role");
            Assert(roster3.members[1].kind == BattleUnitKind.FollowerNpc, "第 2 为 Follower");
            RoleStats alias = roster3.members[0].stats;
            alias.atk = 99;
            Assert(roster3.members[0].stats.atk == 99, "runStats 别名与 members[0].stats 同引用");
            Assert(!ReferenceEquals(roster3.members[0].stats, roster3.members[1].stats),
                "队友 stats 为独立副本");
            Assert(roster3.members[1].stats.atk == baseline.atk, "队友开局 atk 与基线一致");

            // --- 零跟随 ---
            var roster1 = RunPartyRosterFactory.Build(baseline, new List<string>(), null);
            Assert(roster1.members.Count == 1, "无跟随 → 仅 Role");

            // --- 去重 + 截断 9 ---
            var many = new List<string>();
            for (int i = 1; i <= 12; i++)
                many.Add("friend-" + i.ToString("D2"));
            many.Insert(2, "friend-01"); // 重复
            var warnings = new List<string>();
            var rosterCap = RunPartyRosterFactory.Build(
                baseline, many,
                (string id, out string name, out string prefab, out bool found) =>
                {
                    name = id;
                    prefab = RunPartyRosterFactory.DefaultRoleSkeletonPrefab;
                    found = true;
                },
                w => warnings.Add(w));
            Assert(rosterCap.members.Count == RunPartyRosterFactory.MaxPartySize,
                "超过 9 人截断至 9");
            Assert(warnings.Count >= 1, "重复 id 产生 LogWarning");

            // --- Deduplicate 工具 ---
            var dedup = RunPartyRosterFactory.DeduplicateNpcIds(
                new List<string> { "a", "b", "a", "c" });
            Assert(dedup.Count == 3 && dedup[0] == "a" && dedup[1] == "b" && dedup[2] == "c",
                "Deduplicate 保留首次顺序");

            string summary = fail == 0
                ? "[Phase1SelfTest] 全部通过 (" + pass + ")"
                : "[Phase1SelfTest] 失败 " + fail + " / 通过 " + pass;
            if (fail == 0)
                Debug.Log(summary);
            else
                Debug.LogError(summary);
            EditorUtility.DisplayDialog(
                "RunPartyRoster Phase1 Self-Test",
                summary,
                "OK");
        }
    }
}
#endif
