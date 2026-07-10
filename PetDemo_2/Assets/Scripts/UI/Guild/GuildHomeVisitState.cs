// SPEC §9.8.9.7 (v3.129 / v3.220 / v3.222)：公会跟随 NPC 跨 Tab 快照。
// 公会侧 GuildNpcFollowController.StartFollow 写入当前跟随 NPC 的 id + skeletonPrefab 快照；
// 家园侧 JiaYuanGuildVisitorPresenter：公会→家园 Consume；非公会→家园 Clear；
// 公会→主线等非家园 Tab 保留快照（供冒险 Peek 读队）。
// 战斗侧 InvasionBattleModal2View.Show() 经 PeekFollowers 非消费式读队（§12.14.1.1）。
// 纯静态，零 Unity 依赖。
using System.Collections.Generic;

namespace PetDemo.UI
{
    /// <summary>跟随 NPC 快照条目（v3.222：含骨骼预制体路径）。</summary>
    public struct GuildFollowerSnapshot
    {
        public string npcId;
        public string skeletonPrefab;
    }

    public static class GuildHomeVisitState
    {
        private static readonly List<GuildFollowerSnapshot> followers = new List<GuildFollowerSnapshot>();

        /// <summary>快照是否非空（存在挂起的跟随 NPC）。</summary>
        public static bool HasPending => followers.Count > 0;

        /// <summary>SPEC §9.8.9.7 (v3.222)：覆盖快照（含骨骼路径）。</summary>
        public static void SetFollowers(IEnumerable<GuildFollowerSnapshot> entries)
        {
            followers.Clear();
            if (entries == null)
                return;
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.npcId))
                    continue;
                followers.Add(new GuildFollowerSnapshot
                {
                    npcId = entry.npcId,
                    skeletonPrefab = entry.skeletonPrefab,
                });
            }
        }

        /// <summary>兼容：仅写入 id，骨骼由读队方回退 LangRen。</summary>
        public static void SetFollowers(IEnumerable<string> ids)
        {
            followers.Clear();
            if (ids == null)
                return;
            foreach (var id in ids)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    followers.Add(new GuildFollowerSnapshot
                    {
                        npcId = id,
                        skeletonPrefab = null,
                    });
                }
            }
        }

        /// <summary>
        /// SPEC §9.8.9.7 / §12.14.1.1 (v3.213)：返回 id 列表副本，不清空。
        /// </summary>
        public static IReadOnlyList<string> PeekFollowers()
        {
            var ids = new List<string>(followers.Count);
            for (int i = 0; i < followers.Count; i++)
                ids.Add(followers[i].npcId);
            return ids;
        }

        /// <summary>SPEC §9.8.9.7 (v3.222)：返回完整快照副本，不清空。</summary>
        public static IReadOnlyList<GuildFollowerSnapshot> PeekFollowerSnapshots()
        {
            return new List<GuildFollowerSnapshot>(followers);
        }

        /// <summary>SPEC §12.14.1.1 (v3.222)：按 npcId 查询快照中的骨骼预制体路径。</summary>
        public static bool TryGetSkeletonPrefab(string npcId, out string skeletonPrefab)
        {
            skeletonPrefab = null;
            if (string.IsNullOrEmpty(npcId))
                return false;
            for (int i = 0; i < followers.Count; i++)
            {
                if (followers[i].npcId == npcId
                    && !string.IsNullOrEmpty(followers[i].skeletonPrefab))
                {
                    skeletonPrefab = followers[i].skeletonPrefab;
                    return true;
                }
            }
            return false;
        }

        /// <summary>返回当前快照 id 列表并清空（家园触发来访时调用）。</summary>
        public static List<string> Consume()
        {
            var copy = new List<string>(followers.Count);
            for (int i = 0; i < followers.Count; i++)
                copy.Add(followers[i].npcId);
            followers.Clear();
            return copy;
        }

        /// <summary>取消挂起（不满足"直接切换"条件时调用）。</summary>
        public static void Clear()
        {
            followers.Clear();
        }
    }
}
