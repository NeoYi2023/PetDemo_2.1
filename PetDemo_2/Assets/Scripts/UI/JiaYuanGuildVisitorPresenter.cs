// SPEC §9.8.9.7 (v3.129)：公会跟随 NPC 进入家园来访。
// 玩家在公会让 NPC 跟随后"直接"切到家园 Tab 时，所有跟随中的 NPC 来访：在主角左侧 250px
// 生成村民 Spine 原地待机（多 NPC 依次错开），倒计时 5s 后朝左移动 1000px 并销毁。
// 触发严格限定"公会→家园"直接切换（经其它 Tab 中转则取消挂起）。
using System.Collections;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class JiaYuanGuildVisitorPresenter : MonoBehaviour
    {
        private const float LeftOffset = 250f;       // 主角左侧出生偏移
        private const float LeaveDistance = 1000f;   // 离场左移距离
        private const float IdleSeconds = 5f;        // 待机倒计时
        private const float MoveSpeed = 420f;        // 离场速度（与公会跟随一致）
        private const float StaggerX = 150f;         // 多 NPC 错开间距

        private RectTransform worldContentRt;
        private RectTransform playerRoleRt;
        private BottomNavBarView bottomNav;
        private string lastKey;
        private readonly List<GameObject> activeVisitors = new List<GameObject>();

        public void Build(
            RectTransform worldContent, RectTransform playerRoleRt, BottomNavBarView bottomNav)
        {
            this.worldContentRt = worldContent;
            this.playerRoleRt = playerRoleRt;

            if (this.bottomNav != null)
                this.bottomNav.OnOpenChanged -= OnBottomNavOpenChanged;
            this.bottomNav = bottomNav;
            if (this.bottomNav != null)
            {
                this.bottomNav.OnOpenChanged += OnBottomNavOpenChanged;
                lastKey = this.bottomNav.OpenKey;
            }
        }

        private void OnBottomNavOpenChanged(int index, string key)
        {
            bool toJiaYuan = string.Equals(key, JiaYuanHomeFeatureEntriesView.JiaYuanNavKey, System.StringComparison.Ordinal);
            bool fromGongHui = string.Equals(lastKey, GongHuiScreenView.GongHuiNavKey, System.StringComparison.Ordinal);

            if (toJiaYuan && fromGongHui && GuildHomeVisitState.HasPending)
            {
                // 仅"公会→家园"直接切换 + 存在跟随快照时触发来访。
                var ids = GuildHomeVisitState.Consume();
                SpawnVisitors(ids);
            }
            else
            {
                // 其它任何切换（含离开家园、经中转 Tab）取消挂起并清理在场来访者。
                GuildHomeVisitState.Clear();
                if (!toJiaYuan)
                    DestroyActiveVisitors();
            }

            lastKey = key;
        }

        private void SpawnVisitors(List<string> ids)
        {
            if (ids == null || ids.Count == 0 || worldContentRt == null || playerRoleRt == null)
                return;

            var playerPos = GuildSceneGeometry.PointInContentSpace(playerRoleRt, worldContentRt);
            for (int i = 0; i < ids.Count; i++)
            {
                var spawnPos = new Vector2(playerPos.x - LeftOffset - i * StaggerX, playerPos.y);
                var rt = GuildSpineCharacterBuilder.BuildVillager(
                    worldContentRt, "GuildVisitorNpc", spawnPos, out var skeleton);
                if (rt == null)
                    continue;

                // 原地待机，朝向主角（主角在右侧）。
                GuildSpineCharacterBuilder.PlayLoop(
                    skeleton, "exclusive_2", "standby_1", "animation", "idle");
                GuildSpineCharacterBuilder.SetFacing(rt, true);

                activeVisitors.Add(rt.gameObject);
                StartCoroutine(VisitRoutine(rt, skeleton));
                UnityEngine.Debug.Log("[JiaYuanVisitor] 公会 NPC 来访家园：" + ids[i]);
            }
        }

        private IEnumerator VisitRoutine(RectTransform rt, SkeletonGraphic skeleton)
        {
            yield return new WaitForSeconds(IdleSeconds);

            if (rt == null)
                yield break;

            // 朝左移动累计 1000px 后销毁。
            GuildSpineCharacterBuilder.SetFacing(rt, false);
            GuildSpineCharacterBuilder.PlayLoop(skeleton, "move_1", "move", "animation");

            float moved = 0f;
            while (moved < LeaveDistance)
            {
                if (rt == null)
                    yield break;
                float step = Mathf.Min(MoveSpeed * Time.deltaTime, LeaveDistance - moved);
                rt.anchoredPosition += new Vector2(-step, 0f);
                moved += step;
                yield return null;
            }

            if (rt != null)
            {
                activeVisitors.Remove(rt.gameObject);
                Destroy(rt.gameObject);
            }
        }

        private void DestroyActiveVisitors()
        {
            StopAllCoroutines();
            for (int i = 0; i < activeVisitors.Count; i++)
            {
                if (activeVisitors[i] != null)
                    Destroy(activeVisitors[i]);
            }
            activeVisitors.Clear();
        }

        private void OnDestroy()
        {
            if (bottomNav != null)
                bottomNav.OnOpenChanged -= OnBottomNavOpenChanged;
        }
    }
}
