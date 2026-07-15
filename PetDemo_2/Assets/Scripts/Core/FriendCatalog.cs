// SPEC §9.14.2 / §9.14.3：创角界面静态好友目录与排序工具。
// 纯 C# 数据，零 Unity 依赖。
using System.Collections.Generic;

namespace PetDemo.Core
{
    public static class FriendCatalog
    {
        /// <summary>SPEC §9.14.3：创角亲密度阈值（达到即可创建主角）。</summary>
        public const int IntimacyThreshold = 80;

        /// <summary>SPEC §9.14.3：亲密度上限。</summary>
        public const int IntimacyMax = 100;

        // 头像循环引用 AirUI/WanJia_icon_1..10（缺图由 UI 层回退纯色）。
        private const int AvatarVariantCount = 10;

        /// <summary>
        /// SPEC §9.14.2：构建内置静态好友目录（混合在线/离线，亲密度跨越 80 阈值）。
        /// 缺 TopFriends.csv 时的回退；种子 gender/hasPartner 与演示 CSV 对齐（v3.258）。
        /// </summary>
        public static List<FriendProfile> BuildDefault()
        {
            // (displayName, online, intimacy, isFemale, hasPartner)
            var seed = new (string name, bool online, int intimacy, bool isFemale, bool hasPartner)[]
            {
                ("林小满", true, 92, true, false),
                ("苏晚晴", true, 88, true, true),
                ("陆既明", true, 81, false, false),
                ("江清欢", true, 67, true, false),
                ("顾行舟", true, 54, false, true),
                ("沈星河", true, 33, false, false),
                ("白鹿", true, 12, true, false),
                ("温野", false, 95, false, true),
                ("叶知秋", false, 83, true, false),
                ("陈默", false, 72, false, false),
                ("许嵩", false, 49, false, true),
                ("罗夏", false, 26, true, false),
                ("孟婆", false, 8, true, false),
            };

            var list = new List<FriendProfile>(seed.Length);
            for (int i = 0; i < seed.Length; i++)
            {
                int intimacy = Clamp(seed[i].intimacy, 0, IntimacyMax);
                list.Add(new FriendProfile
                {
                    id = "friend-" + (i + 1).ToString("D2"),
                    displayName = seed[i].name,
                    avatarResource = "AirUI/WanJia_icon_" + ((i % AvatarVariantCount) + 1),
                    online = seed[i].online,
                    intimacy = intimacy,
                    isFemale = seed[i].isFemale,
                    hasPartner = seed[i].hasPartner,
                });
            }

            return list;
        }

        /// <summary>
        /// SPEC §9.14.3：返回用于展示的排序结果（不修改入参）。
        /// 排序：在线好友按亲密度降序 > 离线好友按亲密度降序；同组内亲密度相同按 id 升序稳定。
        /// </summary>
        public static List<FriendProfile> SortForDisplay(IEnumerable<FriendProfile> friends)
        {
            var list = new List<FriendProfile>();
            if (friends != null)
            {
                foreach (var f in friends)
                {
                    if (f != null && !string.IsNullOrEmpty(f.id))
                        list.Add(f);
                }
            }

            list.Sort((a, b) =>
            {
                if (a.online != b.online)
                    return a.online ? -1 : 1;          // 在线优先
                if (a.intimacy != b.intimacy)
                    return b.intimacy.CompareTo(a.intimacy); // 亲密度降序
                return string.CompareOrdinal(a.id, b.id);    // 稳定兜底
            });

            return list;
        }

        private static int Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }
}
