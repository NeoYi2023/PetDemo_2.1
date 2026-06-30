// SPEC §9.8.9.7 (v3.129)：公会跟随 NPC 进入家园来访 — 跨 Tab 跟随快照。
// 公会侧 GuildNpcFollowController.StartFollow 写入当前跟随 NPC 的 id 快照；
// 家园侧 JiaYuanGuildVisitorPresenter 在"公会→家园"直接切换时 Consume 取出并清空。
// 任何不满足触发的切换由家园侧 Clear() 取消挂起。纯静态，零 Unity 依赖。
using System.Collections.Generic;

namespace PetDemo.UI
{
    public static class GuildHomeVisitState
    {
        private static readonly List<string> followers = new List<string>();

        /// <summary>快照是否非空（存在挂起的跟随 NPC）。</summary>
        public static bool HasPending => followers.Count > 0;

        /// <summary>覆盖快照（公会 StartFollow 在跟随列表变化后调用）。</summary>
        public static void SetFollowers(IEnumerable<string> ids)
        {
            followers.Clear();
            if (ids == null)
                return;
            foreach (var id in ids)
            {
                if (!string.IsNullOrEmpty(id))
                    followers.Add(id);
            }
        }

        /// <summary>返回当前快照副本并清空（家园触发来访时调用）。</summary>
        public static List<string> Consume()
        {
            var copy = new List<string>(followers);
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
