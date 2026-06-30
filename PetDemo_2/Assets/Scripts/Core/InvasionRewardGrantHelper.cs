// SPEC §12.8 / §9.8.8.8：战斗胜利固定掉落入包。
using System.Collections.Generic;
using PetDemo.Farm;

namespace PetDemo.Core
{
    public static class InvasionRewardGrantHelper
    {
        public static void GrantAll(IPlantingService planting, IReadOnlyList<InvasionRewardConfig> rewards)
        {
            if (planting == null || rewards == null || rewards.Count == 0)
                return;

            for (int i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                if (reward == null || reward.count <= 0 || string.IsNullOrEmpty(reward.id))
                    continue;

                switch (reward.kind)
                {
                    case InvasionRewardKind.Seed:
                        planting.GrantSeed(reward.id, reward.count);
                        break;
                    case InvasionRewardKind.Fertilizer:
                        planting.GrantFertilizer(reward.id, reward.count);
                        break;
                    case InvasionRewardKind.SeedPack:
                        if (System.Enum.TryParse<SeedPackQuality>(reward.id, false, out var quality))
                            planting.GrantSeedPack(quality, reward.count);
                        else
                            UnityEngine.Debug.LogWarning(
                                "[InvasionRewardGrantHelper] SeedPack 奖励 quality 非法: " + reward.id);
                        break;
                    default:
                        UnityEngine.Debug.LogWarning(
                            "[InvasionRewardGrantHelper] 未支持的奖励类型: " + reward.kind);
                        break;
                }
            }
        }
    }
}
