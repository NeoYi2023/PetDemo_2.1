// SPEC §12.14.1.1：局内名册工厂——从 RoleStats 基线 + 跟随 NPC id 列表构建 RunPartyRoster。
// 纯 C#（零 Unity 依赖），便于阶段 1 验收自测。
using System;
using System.Collections.Generic;
using PetDemo.Core;

namespace PetDemo.Battle
{
    /// <summary>解析跟随 NPC 的展示名与骨骼预制体路径。</summary>
    public delegate void ResolveFollowerPresentation(
        string npcId, out string displayName, out string skeletonPrefab, out bool found);

    public static class RunPartyRosterFactory
    {
        public const int MaxPartySize = 9;
        public const int MaxFollowerCount = MaxPartySize - 1;
        public const string RoleRosterId = "role";
        public const string DefaultRoleSkeletonPrefab = "Prefabs/Air/Hero_Role_cunmin";

        /// <summary>
        /// 构建局内名册：Role 首位深拷贝 <paramref name="globalRoleStats"/>；
        /// 跟随者按顺序追加（去重、截断至 8 名），各深拷贝同一开局基线。
        /// </summary>
        public static RunPartyRoster Build(
            RoleStats globalRoleStats,
            IReadOnlyList<string> followerNpcIds,
            ResolveFollowerPresentation resolveFollower,
            Action<string> logWarning = null)
        {
            var roster = new RunPartyRoster();
            var baseline = CloneRoleStats(globalRoleStats) ?? RoleStats.CreateDefault();

            var role = new RunAllyEntry
            {
                rosterId = RoleRosterId,
                kind = BattleUnitKind.Role,
                sourceNpcId = null,
                stats = baseline,
                displayName = string.IsNullOrEmpty(baseline.displayName) ? "Role" : baseline.displayName,
                skeletonPrefab = DefaultRoleSkeletonPrefab,
            };
            AttrEnhanceConfigCatalog.SeedHexBonusesFromRole(role.stats, role.runEnhanceBonuses);
            role.acquiredSkillIds.Clear();
            roster.members.Add(role);

            if (followerNpcIds == null || followerNpcIds.Count == 0)
                return roster;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            int followerAdded = 0;
            for (int i = 0; i < followerNpcIds.Count; i++)
            {
                string npcId = followerNpcIds[i];
                if (string.IsNullOrEmpty(npcId))
                    continue;
                if (!seen.Add(npcId))
                {
                    logWarning?.Invoke(
                        "[RunPartyRoster] 重复 NpcId 已跳过：" + npcId);
                    continue;
                }
                if (followerAdded >= MaxFollowerCount)
                    break;

                string displayName = npcId;
                string skeletonPrefab = DefaultRoleSkeletonPrefab;
                bool found = false;
                if (resolveFollower != null)
                    resolveFollower(npcId, out displayName, out skeletonPrefab, out found);
                if (!found)
                {
                    logWarning?.Invoke(
                        "[RunPartyRoster] 未找到 NPC 配置，使用回退外观：" + npcId);
                    if (string.IsNullOrEmpty(displayName))
                        displayName = npcId;
                    if (string.IsNullOrEmpty(skeletonPrefab))
                        skeletonPrefab = DefaultRoleSkeletonPrefab;
                }

                var followerStats = CloneRoleStats(baseline);
                var entry = new RunAllyEntry
                {
                    rosterId = "follower_" + npcId,
                    kind = BattleUnitKind.FollowerNpc,
                    sourceNpcId = npcId,
                    stats = followerStats,
                    displayName = displayName,
                    skeletonPrefab = skeletonPrefab,
                };
                AttrEnhanceConfigCatalog.SeedHexBonusesFromRole(entry.stats, entry.runEnhanceBonuses);
                entry.acquiredSkillIds.Clear();
                roster.members.Add(entry);
                followerAdded++;
            }

            return roster;
        }

        /// <summary>按拉手顺序去重（保留首次），供读队层预处理。</summary>
        public static List<string> DeduplicateNpcIds(IReadOnlyList<string> ids, Action<string> logWarning = null)
        {
            var result = new List<string>();
            if (ids == null)
                return result;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                if (string.IsNullOrEmpty(id))
                    continue;
                if (!seen.Add(id))
                {
                    logWarning?.Invoke("[RunPartyRoster] 重复 NpcId 已跳过：" + id);
                    continue;
                }
                result.Add(id);
            }
            return result;
        }

        public static RoleStats CloneRoleStats(RoleStats src)
        {
            if (src == null)
                return null;
            return new RoleStats
            {
                displayName = src.displayName,
                atk = src.atk,
                def = src.def,
                maxHp = src.maxHp,
                currentHp = src.currentHp,
                agility = src.agility,
                stamina = src.stamina,
                staminaMax = src.staminaMax,
                criticalHit = src.criticalHit,
                combo = src.combo,
                counterattack = src.counterattack,
                stun = src.stun,
                evasion = src.evasion,
                lifeSteal = src.lifeSteal,
                intelligence = src.intelligence,
                memory = src.memory,
                imagination = src.imagination,
                physique = src.physique,
                charm = src.charm,
                emotionalIntelligence = src.emotionalIntelligence,
                level = src.level,
                currentExp = src.currentExp,
                expToNextLevel = src.expToNextLevel,
            };
        }
    }
}
