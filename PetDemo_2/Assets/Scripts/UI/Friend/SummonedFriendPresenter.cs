// SPEC §13.8 (v3.132)：好友召唤系统 — 常驻组件，管理被召唤好友角色（村民 Spine + 头顶头像）。
// 点击角色弹「收获」(ZhaoHuan_1)/「守卫」(ZhaoHuan_2)：收获复用 §9.5.2 协助种植循环、守卫沿
// TileSlot_01→04→20→17→01 田中心点循环；各持续 600s 后左移 1000px 销毁。跨 Tab 保留并按
// Time.time 继续计时，允许同时召唤多个（presenter 始终 active，工作协程不随角色隐藏中断）。
using System;
using System.Collections;
using System.Collections.Generic;
using PetDemo.Core;
using PetDemo.Farm;
using PetDemo.UI.Farm;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Friend
{
    [DisallowMultipleComponent]
    public sealed class SummonedFriendPresenter : MonoBehaviour
    {
        private const float RightOffset = 200f;
        private const float WorkDurationSec = 600f;
        private const float LeaveDistance = 1000f;
        private const float MoveSpeed = 420f;
        private const float MoveDurationSec = 2.42f;
        private const float AvatarOffsetY = 360f;
        private const float GuardNodeDwellSec = 0.35f;
        private const float ActionAnimTimeoutSec = 4f;

        private static readonly int[] GuardOrderPath = { 1, 4, 20, 17 };
        private static readonly Color AvatarFallbackColor = new Color(0.3f, 0.36f, 0.46f, 1f);
        private static readonly Color ChoiceFallbackHarvest = new Color(0.3f, 0.7f, 0.35f, 1f);
        private static readonly Color ChoiceFallbackGuard = new Color(0.85f, 0.55f, 0.2f, 1f);

        private enum WorkMode { None, Harvest, Guard }

        private sealed class SummonAgent
        {
            public FriendProfile Friend;
            public RectTransform Container;   // 位置容器（不翻转）
            public RectTransform BodyRt;      // 村民 Spine（翻转朝向）
            public SkeletonGraphic Sg;
            public GameObject ChoiceButtons;
            public WorkMode Mode = WorkMode.None;
            public float WorkStartTime = -1f;
            public Coroutine Routine;
            public bool Leaving;
        }

        private bool _built;
        private RectTransform _roleParent;
        private IPlantingService _service;
        private MainRoleCunminPresenter _rolePresenter;
        private BottomNavBarView _bottomNav;
        private Action<int, string> _navHandler;
        private readonly List<SummonAgent> _agents = new List<SummonAgent>();

        public void Build(
            RectTransform roleParent,
            IPlantingService service,
            MainRoleCunminPresenter rolePresenter,
            BottomNavBarView bottomNav)
        {
            if (_built || roleParent == null || service == null)
                return;
            _built = true;
            _roleParent = roleParent;
            _service = service;
            _rolePresenter = rolePresenter;
            _bottomNav = bottomNav;

            if (_bottomNav != null)
            {
                _navHandler = OnBottomNavOpenChanged;
                _bottomNav.OnOpenChanged += _navHandler;
            }
        }

        /// <summary>SPEC §13.8：在主角右侧 200px 生成被召唤好友角色（村民 Spine + 头顶头像 + 点击交互）。</summary>
        public void Summon(FriendProfile friend)
        {
            if (!_built || friend == null || _roleParent == null)
                return;

            var spawnPos = ResolveSpawnPosition();

            var containerGo = new GameObject("SummonedFriend_" + friend.id, typeof(RectTransform));
            var container = containerGo.GetComponent<RectTransform>();
            container.SetParent(_roleParent, false);
            container.anchorMin = container.anchorMax = new Vector2(0.5f, 0.5f);
            container.pivot = new Vector2(0.5f, 0.5f);
            container.anchoredPosition = spawnPos;
            container.sizeDelta = Vector2.zero;
            container.localScale = Vector3.one;

            var bodyRt = GuildSpineCharacterBuilder.BuildVillager(container, "Body", Vector2.zero, out var sg);
            if (sg != null)
                GuildSpineCharacterBuilder.PlayLoop(sg, "exclusive_2", "standby_1", "animation", "idle");

            var agent = new SummonAgent
            {
                Friend = friend,
                Container = container,
                BodyRt = bodyRt,
                Sg = sg,
            };

            BuildAvatarOverhead(agent);
            BuildClickButton(agent);
            BuildChoiceButtons(agent);

            _agents.Add(agent);

            // 仅家园 Tab 可见。
            bool isJiaYuan = IsJiaYuanActive();
            container.gameObject.SetActive(isJiaYuan);

            UnityEngine.Debug.Log("[SummonedFriend] 召唤好友：" + friend.displayName);
        }

        private Vector2 ResolveSpawnPosition()
        {
            var villagerRt = _rolePresenter != null ? _rolePresenter.VillagerRoleRectTransform : null;
            if (villagerRt != null && _roleParent != null)
            {
                // 主角与召唤角色同挂 roleParent；取其 anchoredPosition 直接偏移。
                if (villagerRt.parent == _roleParent)
                    return villagerRt.anchoredPosition + new Vector2(RightOffset, 0f);

                var local = _roleParent.InverseTransformPoint(villagerRt.position);
                return new Vector2(local.x + RightOffset, local.y);
            }
            return new Vector2(RightOffset, 0f);
        }

        private void BuildAvatarOverhead(SummonAgent agent)
        {
            var go = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(agent.Container, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, AvatarOffsetY);
            rt.sizeDelta = new Vector2(80f, 80f);

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            var sprite = !string.IsNullOrEmpty(agent.Friend.avatarResource)
                ? Resources.Load<Sprite>(agent.Friend.avatarResource)
                : null;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
            }
            else
            {
                img.color = AvatarFallbackColor;
            }
        }

        private void BuildClickButton(SummonAgent agent)
        {
            var go = new GameObject("ClickArea", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(agent.Container, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 40f);
            rt.sizeDelta = new Vector2(360f, 640f);
            rt.SetAsFirstSibling(); // 在头像/按钮之下，避免遮挡选择按钮点击。

            var img = go.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f); // 透明但可接收射线。
            img.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;
            var captured = agent;
            btn.onClick.AddListener(() => OnAgentClicked(captured));
        }

        private void BuildChoiceButtons(SummonAgent agent)
        {
            var group = new GameObject("ChoiceButtons", typeof(RectTransform));
            var groupRt = group.GetComponent<RectTransform>();
            groupRt.SetParent(agent.Container, false);
            groupRt.anchorMin = groupRt.anchorMax = new Vector2(0.5f, 0.5f);
            groupRt.pivot = new Vector2(0.5f, 0.5f);
            groupRt.anchoredPosition = new Vector2(0f, AvatarOffsetY + 200f);
            groupRt.sizeDelta = new Vector2(380f, 160f);

            var captured = agent;
            CreateChoiceButton(groupRt, "HarvestButton", "收获", "AirUI/ZhaoHuan_1",
                new Vector2(-100f, 0f), ChoiceFallbackHarvest, () => OnChoiceSelected(captured, WorkMode.Harvest));
            CreateChoiceButton(groupRt, "GuardButton", "守卫", "AirUI/ZhaoHuan_2",
                new Vector2(100f, 0f), ChoiceFallbackGuard, () => OnChoiceSelected(captured, WorkMode.Guard));

            group.SetActive(false);
            agent.ChoiceButtons = group;
        }

        private static void CreateChoiceButton(
            RectTransform parent, string name, string label, string spriteRes,
            Vector2 anchoredPos, Color fallback, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(160f, 120f);

            var img = go.GetComponent<Image>();
            var sprite = Resources.Load<Sprite>(spriteRes);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = true;
            }
            else
            {
                img.color = fallback;
            }
            img.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;
            if (onClick != null)
                btn.onClick.AddListener(onClick);

            // 缺图时叠加文字标签。
            if (sprite == null)
            {
                var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
                var labelRt = labelGo.GetComponent<RectTransform>();
                labelRt.SetParent(rt, false);
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;
                var txt = labelGo.GetComponent<Text>();
                txt.text = label;
                txt.font = FarmGridView.LoadBuiltinFont();
                txt.fontSize = 36;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = Color.white;
                txt.raycastTarget = false;
            }
        }

        private void OnAgentClicked(SummonAgent agent)
        {
            if (agent == null || agent.Leaving)
                return;
            // 工作进行中再次点击不重复弹出。
            if (agent.Mode != WorkMode.None)
                return;
            if (agent.ChoiceButtons != null)
                agent.ChoiceButtons.SetActive(!agent.ChoiceButtons.activeSelf);
        }

        private void OnChoiceSelected(SummonAgent agent, WorkMode mode)
        {
            if (agent == null || agent.Leaving || agent.Mode != WorkMode.None)
                return;

            agent.Mode = mode;
            agent.WorkStartTime = Time.time;
            if (agent.ChoiceButtons != null)
                agent.ChoiceButtons.SetActive(false);

            if (agent.Routine != null)
                StopCoroutine(agent.Routine);
            agent.Routine = StartCoroutine(mode == WorkMode.Harvest ? HarvestRoutine(agent) : GuardRoutine(agent));

            UnityEngine.Debug.Log("[SummonedFriend] " + agent.Friend.displayName +
                " 选择：" + (mode == WorkMode.Harvest ? "收获" : "守卫"));
        }

        private bool WorkExpired(SummonAgent agent)
        {
            return agent.WorkStartTime >= 0f && (Time.time - agent.WorkStartTime) >= WorkDurationSec;
        }

        private IEnumerator HarvestRoutine(SummonAgent agent)
        {
            while (!WorkExpired(agent) && agent.Container != null)
            {
                string tileId = PickRandomPlantedTileId();
                if (string.IsNullOrEmpty(tileId))
                {
                    PlayIdle(agent);
                    yield return new WaitForSeconds(1f);
                    continue;
                }

                if (TryGetTileLocalPosition(tileId, out var target))
                    yield return MoveTo(agent, target);

                for (int i = 0; i < 2 && !WorkExpired(agent); i++)
                    yield return PlayActionOnce(agent);

                ApplyAssistEffect(tileId);
                PlayIdle(agent);
                yield return new WaitForSeconds(0.3f);
            }

            yield return LeaveAndDestroy(agent);
        }

        private IEnumerator GuardRoutine(SummonAgent agent)
        {
            // 解析守卫路径 4 点 tileId（TileSlot_01/04/20/17）。
            var pathTileIds = new List<string>();
            for (int i = 0; i < GuardOrderPath.Length; i++)
            {
                var tile = _service.GetTileByOrder(GuardOrderPath[i]);
                if (tile != null && !string.IsNullOrEmpty(tile.tileId))
                    pathTileIds.Add(tile.tileId);
            }

            if (pathTileIds.Count == 0)
            {
                // 无法解析路径时退化为原地待机直到时长结束。
                while (!WorkExpired(agent) && agent.Container != null)
                {
                    PlayIdle(agent);
                    yield return new WaitForSeconds(1f);
                }
                yield return LeaveAndDestroy(agent);
                yield break;
            }

            int idx = 0;
            while (!WorkExpired(agent) && agent.Container != null)
            {
                if (TryGetTileLocalPosition(pathTileIds[idx], out var target))
                {
                    PlayMove(agent);
                    yield return MoveTo(agent, target);
                    PlayIdle(agent);
                    yield return new WaitForSeconds(GuardNodeDwellSec);
                }
                else
                {
                    yield return null;
                }
                idx = (idx + 1) % pathTileIds.Count;
            }

            yield return LeaveAndDestroy(agent);
        }

        private IEnumerator LeaveAndDestroy(SummonAgent agent)
        {
            agent.Leaving = true;
            if (agent.ChoiceButtons != null)
                agent.ChoiceButtons.SetActive(false);

            SetFacing(agent, faceRight: false);
            PlayMove(agent);

            float moved = 0f;
            while (moved < LeaveDistance && agent.Container != null)
            {
                float step = Mathf.Min(MoveSpeed * Time.deltaTime, LeaveDistance - moved);
                agent.Container.anchoredPosition += new Vector2(-step, 0f);
                moved += step;
                yield return null;
            }

            _agents.Remove(agent);
            if (agent.Container != null)
                Destroy(agent.Container.gameObject);
            UnityEngine.Debug.Log("[SummonedFriend] " + agent.Friend.displayName + " 工作结束离场。");
        }

        private IEnumerator MoveTo(SummonAgent agent, Vector2 target)
        {
            if (agent.Container == null)
                yield break;

            var start = agent.Container.anchoredPosition;
            if (Mathf.Abs(target.x - start.x) > 0.01f)
                SetFacing(agent, faceRight: target.x > start.x);

            float elapsed = 0f;
            while (elapsed < MoveDurationSec && agent.Container != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / MoveDurationSec);
                agent.Container.anchoredPosition = Vector2.Lerp(start, target, t);
                yield return null;
            }
            if (agent.Container != null)
                agent.Container.anchoredPosition = target;
        }

        private IEnumerator PlayActionOnce(SummonAgent agent)
        {
            var sg = agent.Sg;
            if (sg == null || sg.Skeleton == null || sg.Skeleton.Data == null)
            {
                yield return new WaitForSeconds(0.6f);
                yield break;
            }

            string clip = ResolveClip(sg, "work_1", "work", "attack", "attack_1");
            if (string.IsNullOrEmpty(clip))
            {
                yield return new WaitForSeconds(0.6f);
                yield break;
            }

            var entry = sg.AnimationState.SetAnimation(0, clip, false);
            float elapsed = 0f;
            while (entry != null && !entry.IsComplete && elapsed < ActionAnimTimeoutSec)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void PlayIdle(SummonAgent agent)
        {
            if (agent.Sg != null)
                GuildSpineCharacterBuilder.PlayLoop(agent.Sg, "exclusive_2", "standby_1", "animation", "idle");
        }

        private void PlayMove(SummonAgent agent)
        {
            if (agent.Sg != null)
                GuildSpineCharacterBuilder.PlayLoop(agent.Sg, "move_1", "move", "animation");
        }

        private static void SetFacing(SummonAgent agent, bool faceRight)
        {
            if (agent.BodyRt != null)
                GuildSpineCharacterBuilder.SetFacing(agent.BodyRt, faceRight);
        }

        private static string ResolveClip(SkeletonGraphic sg, params string[] candidates)
        {
            if (sg == null || sg.Skeleton == null || sg.Skeleton.Data == null)
                return null;
            if (candidates != null)
            {
                for (int i = 0; i < candidates.Length; i++)
                {
                    if (!string.IsNullOrEmpty(candidates[i]) &&
                        sg.Skeleton.Data.FindAnimation(candidates[i]) != null)
                        return candidates[i];
                }
            }
            var anims = sg.Skeleton.Data.Animations;
            if (anims != null && anims.Count > 0 && anims.Items[0] != null)
                return anims.Items[0].Name;
            return null;
        }

        private void ApplyAssistEffect(string tileId)
        {
            if (_service == null || string.IsNullOrEmpty(tileId))
                return;
            if (_service.TryHarvestTile(tileId))
                return;
            _service.TryWaterTile(tileId);
        }

        private string PickRandomPlantedTileId()
        {
            if (_service == null)
                return null;

            var candidates = new List<string>();
            int count = _service.FarmTileCount;
            for (int i = 1; i <= count; i++)
            {
                var tile = _service.GetTileByOrder(i);
                if (IsPlantedTile(tile))
                    candidates.Add(tile.tileId);
            }
            if (candidates.Count == 0)
                return null;
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        private static bool IsPlantedTile(CropTile tile)
        {
            return tile != null
                && tile.planting == PlantingFlag.Seeded
                && !string.IsNullOrEmpty(tile.plantInstanceId)
                && string.IsNullOrEmpty(tile.lockedByMutationId);
        }

        private bool TryGetTileLocalPosition(string tileId, out Vector2 localPos)
        {
            localPos = Vector2.zero;
            if (_roleParent == null || string.IsNullOrEmpty(tileId))
                return false;
            var grid = FarmGridView.Instance;
            if (grid == null)
                return false;
            return grid.TryGetTileLocalPositionIn(_roleParent, tileId, out localPos);
        }

        private bool IsJiaYuanActive()
        {
            return _bottomNav == null ||
                string.Equals(_bottomNav.OpenKey, JiaYuanHomeFeatureEntriesView.JiaYuanNavKey, StringComparison.Ordinal);
        }

        private void OnBottomNavOpenChanged(int index, string key)
        {
            bool isJiaYuan = string.Equals(key, JiaYuanHomeFeatureEntriesView.JiaYuanNavKey, StringComparison.Ordinal);
            for (int i = 0; i < _agents.Count; i++)
            {
                if (_agents[i].Container != null)
                    _agents[i].Container.gameObject.SetActive(isJiaYuan);
            }
        }

        private void OnDestroy()
        {
            if (_bottomNav != null && _navHandler != null)
                _bottomNav.OnOpenChanged -= _navHandler;
        }
    }
}
