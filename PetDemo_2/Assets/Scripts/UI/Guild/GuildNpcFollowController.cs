// SPEC §9.8.9 (v3.124)：公会场景 NPC 跟随控制器 — 点击名牌互动按钮后该 NPC 进入跟随主角状态：
// content 局部空间距主角 > 40px 时以 420px/s 直线靠近（55px 起步滞回防抖、单帧钳制防过冲），
// ≤ 40px 停步待机；无碰撞/边界检测；离开公会界面（OnDisable）时全部复位回出生点并恢复待机。
// SPEC §9.8.9.8：StartFollow 时切换 marker 为头顶 Avatar 模式；OnDisable 时退出跟随 UI 模式。
// SPEC §9.8.15.1 (v3.159)：StartFollow 成功后触发 FollowerAdded，供 TopDingBar 左下角登记头像。
// SPEC §9.8.9.7 (v3.257)：跟随快照 skeletonPrefab 优先写 TopFriends.csv spinePrefab。
using System;
using System.Collections.Generic;
using PetDemo.Core;
using Spine.Unity;
using UnityEngine;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class GuildNpcFollowController : MonoBehaviour
    {
        private const float FollowDistance = 100f;
        private const float ResumeDistance = 175f;
        private const float MoveSpeed = 420f;

        private sealed class FollowEntry
        {
            public GuildNpcMarker Marker;
            public Vector2 SpawnAnchoredPos;
            public bool Moving;
        }

        private RectTransform playerRt;
        private RectTransform worldContentRt;
        private readonly List<FollowEntry> entries = new List<FollowEntry>();
        private readonly HashSet<GuildNpcMarker> actionLockedMarkers = new HashSet<GuildNpcMarker>();
        private bool initialized;

        /// <summary>SPEC §9.8.15.1：成功加入跟随列表后触发（重复拉手不触发）。</summary>
        public event Action<GuildNpcMarker> FollowerAdded;

        /// <summary>当前跟随中的 NPC 数量（拉手顺序）。</summary>
        public int FollowerCount => entries.Count;

        public void Initialize(RectTransform player, RectTransform worldContent)
        {
            playerRt = player;
            worldContentRt = worldContent;
            initialized = true;
        }

        /// <summary>
        /// SPEC §12.14.1.1：按拉手顺序返回当前跟随 NPC 的 NpcId 列表（副本）。
        /// GongHui 激活时供 InvasionBattleModal2View.Show() 读队。
        /// </summary>
        public List<string> GetFollowerNpcIds()
        {
            var ids = new List<string>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Marker != null && !string.IsNullOrEmpty(entries[i].Marker.NpcId))
                    ids.Add(entries[i].Marker.NpcId);
            }
            return ids;
        }

        /// <summary>按拉手顺序返回当前跟随中的 GuildNpcMarker（跳过空引用）。</summary>
        public List<GuildNpcMarker> GetFollowerMarkers()
        {
            var list = new List<GuildNpcMarker>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Marker != null)
                    list.Add(entries[i].Marker);
            }
            return list;
        }

        /// <summary>将 NPC 加入跟随列表（已在列表则忽略），记录出生点供离开界面时复位。</summary>
        public void StartFollow(GuildNpcMarker marker)
        {
            if (marker == null)
                return;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Marker == marker)
                    return;
            }

            entries.Add(new FollowEntry
            {
                Marker = marker,
                SpawnAnchoredPos = marker.Rt.anchoredPosition,
                Moving = false,
            });
            marker.SetFollowing(true);
            FollowerAdded?.Invoke(marker);
            UnityEngine.Debug.Log("[GongHuiScreen] NPC 开始跟随主角：" + marker.NpcId);

            // SPEC §9.8.9.7 (v3.129)：发布当前跟随快照，供"公会→家园"直接切换时家园来访消费。
            PublishVisitSnapshot();
        }

        /// <summary>v3.156：work_2 编排期间暂停该 NPC 的跟随动画切换。</summary>
        public void SetActionAnimationLocked(GuildNpcMarker marker, bool locked)
        {
            if (marker == null)
                return;
            if (locked)
                actionLockedMarkers.Add(marker);
            else
                actionLockedMarkers.Remove(marker);
        }

        /// <summary>v3.156：work_2 结束后按跟随状态恢复 NPC 动画。</summary>
        public void RefreshMarkerAnimation(GuildNpcMarker marker)
        {
            if (marker == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Marker != marker)
                    continue;
                PlayAnimation(marker.NpcSkeleton, entries[i].Moving);
                return;
            }

            PlayAnimation(marker.NpcSkeleton, false);
        }

        // SPEC §9.8.9.7 (v3.222 / v3.257)：用当前 entries 重建跨 Tab 跟随快照（id + skeletonPrefab）。
        private void PublishVisitSnapshot()
        {
            var snapshots = new List<GuildFollowerSnapshot>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                var marker = entries[i].Marker;
                if (marker == null || string.IsNullOrEmpty(marker.NpcId))
                    continue;
                snapshots.Add(new GuildFollowerSnapshot
                {
                    npcId = marker.NpcId,
                    skeletonPrefab = ResolveSkeletonPrefabForMarker(marker),
                });
            }
            GuildHomeVisitState.SetFollowers(snapshots);
        }

        /// <summary>优先 CSV spinePrefab；空则回退 skeletonKind 探针路径。</summary>
        private static string ResolveSkeletonPrefabForMarker(GuildNpcMarker marker)
        {
            if (marker != null
                && TopFriendCatalog.TryGetById(marker.NpcId, out var profile)
                && profile != null
                && !string.IsNullOrEmpty(profile.spinePrefabPath))
            {
                return profile.spinePrefabPath;
            }

            return ResolveSkeletonPrefabForKind(marker != null ? marker.SkeletonKind : GuildNpcSkeletonKind.LangRen);
        }

        private static string ResolveSkeletonPrefabForKind(GuildNpcSkeletonKind kind)
        {
            return kind == GuildNpcSkeletonKind.LangMeiRen
                ? GuildSpineCharacterBuilder.LangMeiRenResourcesPrefabPath
                : GuildSpineCharacterBuilder.LangRenResourcesPrefabPath;
        }

        private void Update()
        {
            if (!initialized || playerRt == null || worldContentRt == null)
                return;

            var playerPos = GuildSceneGeometry.PointInContentSpace(playerRt, worldContentRt);
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Marker == null)
                    continue;

                var npcPos = GuildSceneGeometry.PointInContentSpace(entry.Marker.Rt, worldContentRt);
                var toPlayer = playerPos - npcPos;
                float dist = toPlayer.magnitude;

                // 滞回：停步后须超过 ResumeDistance 才重新起步，避免主角微动导致走/停抖动。
                bool wantMove = entry.Moving ? dist > FollowDistance : dist > ResumeDistance;

                if (wantMove && dist > Mathf.Epsilon)
                {
                    // 单帧位移钳制到"距主角 FollowDistance"的目标点，防止过冲往返。
                    float step = Mathf.Min(MoveSpeed * Time.deltaTime, dist - FollowDistance);
                    var dir = toPlayer / dist;
                    // marker 父节点为拉伸容器，content 空间位移与 anchoredPosition 位移 1:1 对应。
                    entry.Marker.Rt.anchoredPosition += dir * step;

                    if (Mathf.Abs(dir.x) > 0.01f)
                        GuildSpineCharacterBuilder.SetFacing(entry.Marker.SpineRt, dir.x > 0f);
                }

                if (wantMove != entry.Moving)
                {
                    entry.Moving = wantMove;
                    if (!actionLockedMarkers.Contains(entry.Marker))
                        PlayAnimation(entry.Marker.NpcSkeleton, wantMove);
                }
            }
        }

        private void OnDisable()
        {
            // 离开公会 Tab 时全部复位回出生点并恢复待机（模式同 GuildProximityController 隐藏名牌）。
            // SPEC §9.8.9.7：此处刻意不清空 GuildHomeVisitState，把跟随快照留给家园来访消费；
            // 取消挂起的职责交由 JiaYuanGuildVisitorPresenter 按"直接切换"规则处理。
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Marker == null)
                    continue;
                entry.Marker.Rt.anchoredPosition = entry.SpawnAnchoredPos;
                entry.Marker.SetFollowing(false);
                PlayAnimation(entry.Marker.NpcSkeleton, false);
            }
            entries.Clear();
            actionLockedMarkers.Clear();
        }

        private static void PlayAnimation(SkeletonGraphic skeleton, bool moving)
        {
            if (moving)
                GuildSpineCharacterBuilder.PlayLoop(skeleton, "move_1", "move", "animation");
            else
                GuildSpineCharacterBuilder.PlayLoop(
                    skeleton, "standby_1", "animation", "idle", "exclusive_2");
        }
    }
}
