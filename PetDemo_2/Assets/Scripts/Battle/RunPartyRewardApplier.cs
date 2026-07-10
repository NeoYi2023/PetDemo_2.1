// SPEC §12.14.12 / §12.14.12.1 / §12.14.8：局内事件奖励全员分发。
using System;
using System.Collections.Generic;

namespace PetDemo.Battle
{
    public static class RunPartyRewardApplier
    {
        public static void ApplyRewardToAllPartyMembers(
            RunPartyRoster roster, InvasionEventReward reward)
        {
            if (roster?.members == null || reward == null)
                return;

            switch (reward.kind)
            {
                case InvasionEventRewardKind.AttrPercent:
                    ApplyPercentStatToAllPartyMembers(roster, reward.target, reward.percent);
                    break;
            }
        }

        public static void ApplyPercentStatToAllPartyMembers(
            RunPartyRoster roster, string target, int percent)
        {
            if (roster?.members == null || string.IsNullOrEmpty(target))
                return;

            for (int i = 0; i < roster.members.Count; i++)
            {
                var entry = roster.members[i];
                if (entry?.stats != null)
                    ApplyPercentStatToMember(entry, target, percent);
            }
        }

        public static bool ApplyFlatStatToAllPartyMembers(
            RunPartyRoster roster, string attrId, int delta)
        {
            if (roster?.members == null || string.IsNullOrEmpty(attrId) || delta == 0)
                return false;
            if (!AttrEnhanceConfigCatalog.TryNormalizeAttrId(attrId, out string canonical))
                return false;

            bool any = false;
            for (int i = 0; i < roster.members.Count; i++)
            {
                var entry = roster.members[i];
                if (entry == null)
                    continue;
                if (ApplyFlatStatToMember(entry, canonical, delta))
                    any = true;
            }
            return any;
        }

        public static void AcquireSkillForAllPartyMembers(RunPartyRoster roster, string skillId)
        {
            if (roster?.members == null || string.IsNullOrEmpty(skillId))
                return;

            for (int i = 0; i < roster.members.Count; i++)
            {
                var entry = roster.members[i];
                if (entry?.acquiredSkillIds == null)
                    continue;
                if (!entry.acquiredSkillIds.Contains(skillId))
                    entry.acquiredSkillIds.Add(skillId);
            }
        }

        private static void ApplyPercentStatToMember(RunAllyEntry entry, string target, int percent)
        {
            float factor = 1f + percent / 100f;
            var stats = entry.stats;

            switch (target)
            {
                case "hp":
                    stats.maxHp = Math.Max(1, (int)Math.Round(stats.maxHp * factor));
                    if (percent >= 0)
                    {
                        stats.currentHp = ClampInt(
                            (int)Math.Round(stats.currentHp * factor), 0, stats.maxHp);
                    }
                    else
                    {
                        stats.currentHp = Math.Min(stats.currentHp, stats.maxHp);
                    }
                    break;
                case "atk":
                    stats.atk = Math.Max(0, (int)Math.Round(stats.atk * factor));
                    break;
                case "speed":
                    stats.agility = Math.Max(0, (int)Math.Round(stats.agility * factor));
                    break;
                case "def":
                    stats.def = Math.Max(0, (int)Math.Round(stats.def * factor));
                    break;
            }
        }

        private static bool ApplyFlatStatToMember(RunAllyEntry entry, string canonical, int delta)
        {
            var stats = entry.stats;
            var bonuses = entry.runEnhanceBonuses;
            if (stats == null)
                return false;

            switch (canonical)
            {
                case AttrEnhanceConfigCatalog.AttrLife:
                    stats.maxHp = Math.Max(1, stats.maxHp + delta);
                    stats.currentHp = ClampInt(stats.currentHp + delta, 0, stats.maxHp);
                    return true;
                case AttrEnhanceConfigCatalog.AttrAttack:
                    stats.atk = Math.Max(0, stats.atk + delta);
                    return true;
                case "def":
                    stats.def = Math.Max(0, stats.def + delta);
                    return true;
                case "speed":
                    stats.agility = Math.Max(0, stats.agility + delta);
                    return true;
                default:
                    if (!AttrEnhanceConfigCatalog.IsHexRadarAttr(canonical))
                        return false;
                    if (bonuses == null)
                        return false;
                    if (!bonuses.TryGetValue(canonical, out int cur))
                        cur = 0;
                    bonuses[canonical] = Math.Max(0, cur + delta);
                    return true;
            }
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
