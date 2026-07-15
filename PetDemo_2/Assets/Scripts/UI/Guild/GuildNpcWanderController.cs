// SPEC §9.8.9.15：公会场景 NPC 随机移动（游走）控制器 —
// 未跟随、未显示名牌的 GuildNpcMarker 自主执行「待机 → 选目标点游走 → 到达 → 再待机」循环：
// 速度 = 主角 moveSpeed × 0.85；沿途复用主角的分轴 AABB 贴墙滑动 + 世界边界钳位，并有卡住超时兜底；
// 跟随中（IsFollowing）跳过交由 GuildNpcFollowController；名牌显示（IsPlateVisible）时暂停发起新移动（finish 语义）。
// v3.229：每次进入待机先在 work_1~work_4 中随机单播一个动作，播完再回 standby 循环。
// v3.230：目标选择改为「专用目标点 GuildWaypointMarker」——从未占用目标点随机取一，选中即占用、改去别处才释放；
//         到达后停在目标点上持续占用；无可用目标点则直接进入待机。
// v3.261：选点排除落在 Obstacles 内的 Waypoint（脚底盒与沿途碰撞一致）。
using System.Collections.Generic;
using UnityEngine;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class GuildNpcWanderController : MonoBehaviour
    {
        private const float IdleMin = 2f;
        private const float IdleMax = 10f;
        private const float ArriveEps = 10f;
        private const float StuckTimeout = 1.5f;
        private const float StuckProgressEps = 0.5f;
        private const float FacingThreshold = 0.01f;

        // NPC 脚底碰撞盒（content 局部空间单位，可调）。
        private static readonly Vector2 NpcCollisionBoxSize = new Vector2(110f, 80f);
        private static readonly Vector2 NpcCollisionBoxOffset = new Vector2(0f, -40f);

        // SPEC §9.8.9.15 (v3.229)：进入待机时随机单播一个动作后回 standby。
        private static readonly string[] WorkClips = { "work_1", "work_2", "work_3", "work_4" };
        private static readonly string[] StandbyFallbacks = { "animation", "idle", "exclusive_2" };

        private enum WanderState { Idle, Moving }

        private sealed class WanderEntry
        {
            public GuildNpcMarker Marker;
            public WanderState State;
            public Vector2 Target;
            public GuildWaypointMarker Waypoint;   // v3.230：当前占用/前往的目标点（null=未占用）
            public float IdleTimer;
            public float StuckTimer;
            public float LastDistToTarget;
            public bool Moving;
        }

        private RectTransform worldContentRt;
        private readonly List<WanderEntry> entries = new List<WanderEntry>();
        private readonly List<Rect> obstacleRects = new List<Rect>();
        private readonly List<GuildWaypointMarker> waypoints = new List<GuildWaypointMarker>();
        private readonly HashSet<GuildWaypointMarker> occupied = new HashSet<GuildWaypointMarker>();
        private readonly List<GuildWaypointMarker> freePicks = new List<GuildWaypointMarker>();
        private float wanderSpeed;
        private bool initialized;

        public void Initialize(
            RectTransform worldContent,
            IList<GuildNpcMarker> npcs,
            IList<GuildObstacleArea> obstacles,
            IList<GuildWaypointMarker> waypointMarkers,
            float wanderMoveSpeed)
        {
            worldContentRt = worldContent;
            wanderSpeed = wanderMoveSpeed;

            entries.Clear();
            if (npcs != null)
            {
                for (int i = 0; i < npcs.Count; i++)
                {
                    if (npcs[i] == null)
                        continue;
                    entries.Add(new WanderEntry
                    {
                        Marker = npcs[i],
                        State = WanderState.Idle,
                        IdleTimer = Random.Range(IdleMin, IdleMax),
                        Moving = false,
                    });
                }
            }

            obstacleRects.Clear();
            if (obstacles != null && worldContentRt != null)
            {
                for (int i = 0; i < obstacles.Count; i++)
                {
                    if (obstacles[i] != null)
                        obstacleRects.Add(GuildSceneGeometry.RectInContentSpace(
                            obstacles[i].Rt, worldContentRt));
                }
            }

            waypoints.Clear();
            occupied.Clear();
            if (waypointMarkers != null)
            {
                for (int i = 0; i < waypointMarkers.Count; i++)
                {
                    if (waypointMarkers[i] != null)
                        waypoints.Add(waypointMarkers[i]);
                }
            }

            initialized = true;
        }

        private void Update()
        {
            if (!initialized || worldContentRt == null)
                return;

            float dt = Time.deltaTime;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Marker == null)
                    continue;

                // 跟随互斥：跟随中的 NPC 交由 GuildNpcFollowController，释放占用并复位游走内部状态。
                if (entry.Marker.IsFollowing)
                {
                    if (entry.State != WanderState.Idle || entry.Waypoint != null)
                    {
                        ReleaseWaypoint(entry);
                        entry.State = WanderState.Idle;
                        entry.Moving = false;
                        entry.IdleTimer = Random.Range(IdleMin, IdleMax);
                    }
                    continue;
                }

                if (entry.State == WanderState.Idle)
                    TickIdle(entry, dt);
                else
                    TickMoving(entry, dt);
            }
        }

        private void TickIdle(WanderEntry entry, float dt)
        {
            // 名牌显示时暂停「待机→选点出发」：计时不推进、不发起新移动（finish 语义）。
            if (entry.Marker.IsPlateVisible)
                return;

            entry.IdleTimer -= dt;
            if (entry.IdleTimer > 0f)
                return;

            if (TryPickWaypoint(entry, out var wp))
            {
                OccupyWaypoint(entry, wp);
                entry.Target = GuildSceneGeometry.PointInContentSpace(wp.Rt, worldContentRt);
                entry.State = WanderState.Moving;
                entry.StuckTimer = 0f;
                entry.LastDistToTarget =
                    (entry.Target - GuildSceneGeometry.PointInContentSpace(entry.Marker.Rt, worldContentRt)).magnitude;
                entry.Moving = true;
                GuildSpineCharacterBuilder.PlayLoop(entry.Marker.NpcSkeleton, "move_1", "move", "animation");
            }
            else
            {
                // SPEC §9.8.9.15 (v3.230)：无可用目标点 → 直接认定为到达、进入待机。
                EnterIdle(entry);
            }
        }

        private void TickMoving(WanderEntry entry, float dt)
        {
            var npcPos = GuildSceneGeometry.PointInContentSpace(entry.Marker.Rt, worldContentRt);
            var toTarget = entry.Target - npcPos;
            float dist = toTarget.magnitude;

            if (dist <= ArriveEps)
            {
                EnterIdle(entry);
                return;
            }

            float step = Mathf.Min(wanderSpeed * dt, dist);
            var dir = toTarget / dist;
            var desired = dir * step;

            var newPos = npcPos;
            newPos = TryMoveAxis(newPos, new Vector2(desired.x, 0f));
            newPos = TryMoveAxis(newPos, new Vector2(0f, desired.y));
            newPos = ClampToWorld(newPos);

            var actual = newPos - npcPos;
            // marker 父节点为拉伸容器，content 空间位移与 anchoredPosition 位移 1:1 对应。
            entry.Marker.Rt.anchoredPosition += actual;

            if (Mathf.Abs(actual.x) > FacingThreshold)
                GuildSpineCharacterBuilder.SetFacing(entry.Marker.SpineRt, actual.x > 0f);

            // 卡住超时：一段时间内到目标距离无明显推进则放弃当前目标回待机（仍持有目标点，下轮换点时释放）。
            float newDist = (entry.Target - newPos).magnitude;
            if (entry.LastDistToTarget - newDist < StuckProgressEps)
            {
                entry.StuckTimer += dt;
                if (entry.StuckTimer >= StuckTimeout)
                {
                    EnterIdle(entry);
                    return;
                }
            }
            else
            {
                entry.StuckTimer = 0f;
            }
            entry.LastDistToTarget = newDist;
        }

        private void EnterIdle(WanderEntry entry)
        {
            entry.State = WanderState.Idle;
            entry.IdleTimer = Random.Range(IdleMin, IdleMax);
            entry.StuckTimer = 0f;
            entry.Moving = false;
            // SPEC §9.8.9.15 (v3.229)：进入待机先随机单播一个 work 动作，播完队列衔接 standby 循环。
            string work = WorkClips[Random.Range(0, WorkClips.Length)];
            GuildSpineCharacterBuilder.PlayOnceThenLoop(
                entry.Marker.NpcSkeleton, work, "standby_1", StandbyFallbacks);
        }

        // SPEC §9.8.9.15 (v3.230；选点避障 v3.261)：从未被占用且不在障碍内的目标点中等概率随机取一。
        private bool TryPickWaypoint(WanderEntry entry, out GuildWaypointMarker chosen)
        {
            freePicks.Clear();
            for (int i = 0; i < waypoints.Count; i++)
            {
                var wp = waypoints[i];
                if (wp == null || occupied.Contains(wp))
                    continue;
                if (IsWaypointInsideObstacle(wp))
                    continue;
                freePicks.Add(wp);
            }

            if (freePicks.Count == 0)
            {
                chosen = null;
                return false;
            }

            chosen = freePicks[Random.Range(0, freePicks.Count)];
            return true;
        }

        /// <summary>
        /// SPEC §9.8.9.15（v3.261）：目标点脚底盒与任一障碍矩形相交则视为不可选。
        /// </summary>
        private bool IsWaypointInsideObstacle(GuildWaypointMarker wp)
        {
            if (wp == null || worldContentRt == null || obstacleRects.Count == 0)
                return false;

            var pos = GuildSceneGeometry.PointInContentSpace(wp.Rt, worldContentRt);
            var box = GuildSceneGeometry.CenteredRect(pos + NpcCollisionBoxOffset, NpcCollisionBoxSize);
            for (int i = 0; i < obstacleRects.Count; i++)
            {
                if (box.Overlaps(obstacleRects[i]))
                    return true;
            }

            return false;
        }

        // 占用新目标点：先释放旧点（开始前往另一个目标点时释放），再占用新点（开始前往即占用）。
        private void OccupyWaypoint(WanderEntry entry, GuildWaypointMarker wp)
        {
            ReleaseWaypoint(entry);
            entry.Waypoint = wp;
            if (wp != null)
                occupied.Add(wp);
        }

        private void ReleaseWaypoint(WanderEntry entry)
        {
            if (entry.Waypoint == null)
                return;
            occupied.Remove(entry.Waypoint);
            entry.Waypoint = null;
        }

        // 单轴尝试位移：候选脚底盒与任一障碍矩形相交则维持原位（另一轴不受影响 → 贴墙滑动）。
        private Vector2 TryMoveAxis(Vector2 pos, Vector2 axisDelta)
        {
            if (axisDelta.sqrMagnitude <= 0f)
                return pos;

            var candidate = pos + axisDelta;
            var box = GuildSceneGeometry.CenteredRect(candidate + NpcCollisionBoxOffset, NpcCollisionBoxSize);
            for (int i = 0; i < obstacleRects.Count; i++)
            {
                if (box.Overlaps(obstacleRects[i]))
                    return pos;
            }

            return candidate;
        }

        private Vector2 ClampToWorld(Vector2 pos)
        {
            if (worldContentRt == null)
                return pos;

            var half = worldContentRt.rect.size * 0.5f;
            var halfBox = NpcCollisionBoxSize * 0.5f;
            var boxCenter = pos + NpcCollisionBoxOffset;
            boxCenter.x = Mathf.Clamp(boxCenter.x, -half.x + halfBox.x, half.x - halfBox.x);
            boxCenter.y = Mathf.Clamp(boxCenter.y, -half.y + halfBox.y, half.y - halfBox.y);
            return boxCenter - NpcCollisionBoxOffset;
        }
    }
}
