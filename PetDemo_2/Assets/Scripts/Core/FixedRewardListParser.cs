// SPEC §9.8.8.8 / §12.8：固定产出奖励串解析（kind:id:count，多条以 ; 分隔）。
using System.Collections.Generic;

namespace PetDemo.Core
{
    public static class FixedRewardListParser
    {
        public static List<InvasionRewardConfig> Parse(string encoded)
        {
            var list = new List<InvasionRewardConfig>();
            if (string.IsNullOrWhiteSpace(encoded))
                return list;

            var entries = encoded.Split(';');
            for (int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i]?.Trim();
                if (string.IsNullOrEmpty(entry))
                    continue;

                var parts = entry.Split(':');
                if (parts.Length < 3)
                {
                    UnityEngine.Debug.LogWarning(
                        "[FixedRewardListParser] 奖励条目格式非法（须 kind:id:count）：" + entry);
                    continue;
                }

                string kindRaw = parts[0].Trim();
                string id = parts[1].Trim();
                string countRaw = parts[2].Trim();
                if (string.IsNullOrEmpty(kindRaw) || string.IsNullOrEmpty(id))
                {
                    UnityEngine.Debug.LogWarning(
                        "[FixedRewardListParser] 奖励条目缺少 kind 或 id：" + entry);
                    continue;
                }

                if (!System.Enum.TryParse<InvasionRewardKind>(kindRaw, false, out var kind))
                {
                    UnityEngine.Debug.LogWarning(
                        "[FixedRewardListParser] kind 非法（仅 Seed/Fertilizer/SeedPack）：" + kindRaw);
                    continue;
                }

                if (!int.TryParse(countRaw, out int count) || count <= 0)
                {
                    UnityEngine.Debug.LogWarning(
                        "[FixedRewardListParser] count 非法（须 > 0）：" + countRaw);
                    continue;
                }

                list.Add(new InvasionRewardConfig
                {
                    kind = kind,
                    id = id,
                    count = count,
                });
            }

            return list;
        }
    }
}
