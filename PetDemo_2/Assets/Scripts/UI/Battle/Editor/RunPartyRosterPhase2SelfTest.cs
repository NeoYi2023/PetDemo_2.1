// SPEC §12.14.16 阶段 2 验收自测（Editor 菜单，不依赖 Play Mode）。
#if UNITY_EDITOR
using System.Collections.Generic;
using PetDemo.Battle;
using PetDemo.Core;
using UnityEditor;
using UnityEngine;

namespace PetDemo.UI.Battle.Editor
{
    public static class RunPartyRosterPhase2SelfTest
    {
        [MenuItem("Tools/PetDemo/Self-Test RunPartyRoster Phase2")]
        public static void Run()
        {
            int pass = 0;
            int fail = 0;

            void Assert(bool cond, string name)
            {
                if (cond)
                {
                    pass++;
                    Debug.Log("[Phase2SelfTest] PASS: " + name);
                }
                else
                {
                    fail++;
                    Debug.LogError("[Phase2SelfTest] FAIL: " + name);
                }
            }

            var baseline = new RoleStats
            {
                displayName = "Role",
                atk = 100,
                def = 5,
                maxHp = 80,
                currentHp = 60,
                agility = 10,
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

            // --- §12.14.14 #2：evt_boost_atk +10% → 3 人各 +10% ---
            var rosterAtk = BuildRoster3();
            RunPartyRewardApplier.ApplyPercentStatToAllPartyMembers(rosterAtk, "atk", 10);
            Assert(rosterAtk.members[0].stats.atk == 110, "Role atk +10%");
            Assert(rosterAtk.members[1].stats.atk == 110, "Follower1 atk +10%");
            Assert(rosterAtk.members[2].stats.atk == 110, "Follower2 atk +10%");

            // --- §12.14.14 #3：老虎机 +5 atk → 3 人各 +5 ---
            var rosterFlat = BuildRoster3();
            bool flatApplied = RunPartyRewardApplier.ApplyFlatStatToAllPartyMembers(rosterFlat, "atk", 5);
            Assert(flatApplied, "flat atk 应用成功");
            Assert(rosterFlat.members[0].stats.atk == 105, "Role atk +5 flat");
            Assert(rosterFlat.members[1].stats.atk == 105, "Follower1 atk +5 flat");
            Assert(rosterFlat.members[2].stats.atk == 105, "Follower2 atk +5 flat");

            // --- §12.14.14 #4：三选一 skill → 3 人 acquiredSkillIds ---
            var rosterSkill = BuildRoster3();
            const string skillId = "skill_test_01";
            RunPartyRewardApplier.AcquireSkillForAllPartyMembers(rosterSkill, skillId);
            Assert(rosterSkill.members[0].acquiredSkillIds.Contains(skillId), "Role 获得技能");
            Assert(rosterSkill.members[1].acquiredSkillIds.Contains(skillId), "Follower1 获得技能");
            Assert(rosterSkill.members[2].acquiredSkillIds.Contains(skillId), "Follower2 获得技能");

            // --- §12.14.12.1：hp 百分比增/减边界（每名独立） ---
            var rosterHpUp = BuildRoster3();
            int hp0Before = rosterHpUp.members[0].stats.currentHp;
            RunPartyRewardApplier.ApplyPercentStatToAllPartyMembers(rosterHpUp, "hp", 10);
            Assert(rosterHpUp.members[0].stats.maxHp == 88, "Role maxHp +10%");
            Assert(rosterHpUp.members[0].stats.currentHp > hp0Before, "Role currentHp 同比增");

            var rosterHpDown = BuildRoster3();
            RunPartyRewardApplier.ApplyPercentStatToAllPartyMembers(rosterHpDown, "hp", -50);
            Assert(rosterHpDown.members[0].stats.maxHp == 40, "Role maxHp -50%");
            Assert(rosterHpDown.members[0].stats.currentHp == 40,
                "Role currentHp 减 max 后不抬血（min(current, max)）");
            Assert(rosterHpDown.members[0].stats.currentHp <= rosterHpDown.members[0].stats.maxHp,
                "currentHp <= maxHp");

            // --- 单人局等价 legacy ---
            var roster1 = RunPartyRosterFactory.Build(baseline, new List<string>(), null);
            RunPartyRewardApplier.ApplyPercentStatToAllPartyMembers(roster1, "atk", 10);
            Assert(roster1.members.Count == 1 && roster1.members[0].stats.atk == 110,
                "单人局 attr 等价 runStats");

            string summary = fail == 0
                ? "[Phase2SelfTest] 全部通过 (" + pass + ")"
                : "[Phase2SelfTest] 失败 " + fail + " / 通过 " + pass;
            if (fail == 0)
                Debug.Log(summary);
            else
                Debug.LogError(summary);
            EditorUtility.DisplayDialog(
                "RunPartyRoster Phase2 Self-Test",
                summary,
                "OK");
        }
    }
}
#endif
