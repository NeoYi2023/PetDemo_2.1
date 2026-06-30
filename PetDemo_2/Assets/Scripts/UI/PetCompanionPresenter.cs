// SPEC §9.5.1 / §9.5.1.3（v3.24/v3.26）：主界面精灵伴侣展示层。
// SPEC §4.1.12 (v3.70)：仅展示已上场精灵；仅 JiaYuan Tab 可见；主角左侧双槽布局。
// SPEC §9.5.2（v3.62）：精灵巡逻 — 待机 / 协助种植 FSM，与底部导航 JiaYuan 联动。
// SPEC §9.5.3（v3.89）：精灵互动 — 点击、喂食/抚摸 HUD、村民 work_3。
// SPEC §9.5.4（v3.91）：精灵拖动 — 按住移动、待机、缩小 30%。
using System;
using System.Collections;
using System.Collections.Generic;
using PetDemo.Core;
using PetDemo.Farm;
using PetDemo.UI.Farm;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PetDemo.UI
{
    public enum PetPatrolState
    {
        Idle,
        AssistPlanting,
    }

    public enum PetInteractionPhase
    {
        None,
        AwaitingChoice,
        Resolving,
    }

    [DisallowMultipleComponent]
    public class PetCompanionPresenter : MonoBehaviour
    {
        private const float IdleStateProbability = 0.65f;
        private const float InteractionChoiceTimeoutSec = 4f;
        private const float InteractionPatrolResumeDelaySec = 2f;
        private const float InteractionFavorIconDurationSec = 10f;
        private static readonly string[] InteractionIdleAnimCandidates = { "Idle", "idle" };
        private const string SkeletonGraphicShaderName = "Spine/SkeletonGraphic";
        private static readonly string[] AttackAnimFallbacks = { "Attack", "attack_1" };

        [Header("Pet Companion Layout (Main Menu, SPEC §9.5.1 v3.88)")]
        [SerializeField] private Vector2 rootAnchoredPosition = Vector2.zero;
        [SerializeField] private float petSpawnHorizontalOffset = 240f;
        [SerializeField] private float petLowerVerticalOffset = -80f;
        [SerializeField] private float petUpperVerticalOffset = 80f;
        [SerializeField] private Vector2 petGraphicSize = new Vector2(480f, 720f);
        [SerializeField] private Vector3 petLocalScale = new Vector3(0.30f, 0.30f, 1f);
        [SerializeField] private string idleAnimationName = "idle";
        [SerializeField] private string attackAnimationName = "attack";

        [Header("Pet Patrol (SPEC §9.5.2 v3.88)")]
        [SerializeField] private float moveDurationSec = 0.8571429f;
        [SerializeField] private float animationWaitTimeout = 8f;

        private bool _built;
        private RectTransform _root;
        private JiaYuanWorldDepthSorter _depthSorter;
        private IPlantingService _service;
        private BottomNavBarView _bottomNav;
        private readonly List<PetCompanionAgent> _agents = new List<PetCompanionAgent>();

        private Action<string, MutationKind, string> _harvestedHandler;
        private Action<int, string> _navChangedHandler;
        private Action _deploymentChangedHandler;

        private struct PrefabSkeletonSource
        {
            public SkeletonDataAsset DataAsset;
            public string InitialSkinName;
            public bool InitialFlipX;
            public bool InitialFlipY;
            public bool PmaVertexColors;
            public bool UseClipping;
        }

        private sealed class PetCompanionAgent
        {
            public RectTransform Rt;
            public SkeletonGraphic Sg;
            public PetConfig Config;
            public PetFieldSlot Slot;
            public PetPatrolState State = PetPatrolState.Idle;
            public PetInteractionPhase InteractionPhase = PetInteractionPhase.None;
            public bool HomePatrolEnabled;
            public bool BusyInAssist;
            public Coroutine Routine;
            public Coroutine InteractionRoutine;
            public PetInteractionHudView Hud = new PetInteractionHudView();
            public string ResolvedIdleAnim;
            public string ResolvedInteractionIdleAnim;
            public string ResolvedAttackAnim;
            public int AgentIndex;
            public bool IsDragging;
            public Vector3 PreDragScale;
            public Vector2 DragOffset;
            public bool DragArmed;
        }

        public bool IsAnyDragActive()
        {
            for (int i = 0; i < _agents.Count; i++)
            {
                if (_agents[i].IsDragging)
                    return true;
            }
            return false;
        }

        /// <summary>SPEC §9.1.4（v3.111）：绑定 Y 轴深度排序器。</summary>
        public void BindDepthSorter(JiaYuanWorldDepthSorter sorter)
        {
            _depthSorter = sorter;
            for (int i = 0; i < _agents.Count; i++)
                RegisterAgentDepthSort(_agents[i]);
        }

        public void Build(RectTransform canvasRect, IPlantingService service)
        {
            if (_built || canvasRect == null)
                return;
            _built = true;
            _service = service;

            _root = CreateChildRect(
                canvasRect, "PetCompanionRoot",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                rootAnchoredPosition, Vector2.zero);

            if (_service != null)
            {
                _harvestedHandler = OnMutationHarvested;
                _service.OnMutationHarvested += _harvestedHandler;
                _deploymentChangedHandler = RefreshDeploymentVisuals;
                _service.OnPetDeploymentChanged += _deploymentChangedHandler;
                RefreshDeploymentVisuals();
            }
        }

        public void BindBottomNavBar(BottomNavBarView barView)
        {
            if (_bottomNav != null && _navChangedHandler != null)
                _bottomNav.OnOpenChanged -= _navChangedHandler;

            _bottomNav = barView;
            if (_bottomNav == null)
                return;

            _navChangedHandler = OnBottomNavOpenChanged;
            _bottomNav.OnOpenChanged += _navChangedHandler;
            OnBottomNavOpenChanged(_bottomNav.OpenIndex, _bottomNav.OpenKey);
        }

        private void OnDestroy()
        {
            if (_service != null)
            {
                if (_harvestedHandler != null)
                    _service.OnMutationHarvested -= _harvestedHandler;
                if (_deploymentChangedHandler != null)
                    _service.OnPetDeploymentChanged -= _deploymentChangedHandler;
            }

            if (_bottomNav != null && _navChangedHandler != null)
                _bottomNav.OnOpenChanged -= _navChangedHandler;

            for (int i = 0; i < _agents.Count; i++)
                StopAgentRoutine(_agents[i]);
        }

        private void OnBottomNavOpenChanged(int index, string key)
        {
            bool isJiaYuan = !string.IsNullOrEmpty(key) &&
                string.Equals(key, JiaYuanHomeFeatureEntriesView.JiaYuanNavKey, StringComparison.Ordinal);

            if (_root != null)
                _root.gameObject.SetActive(isJiaYuan);

            if (!isJiaYuan)
            {
                ForceEndAllDrags();
                for (int i = 0; i < _agents.Count; i++)
                    CancelInteraction(_agents[i], resumePatrol: false);
                for (int i = 0; i < _agents.Count; i++)
                    FreezePatrolAtIdle(_agents[i]);
                return;
            }

            for (int i = 0; i < _agents.Count; i++)
            {
                var agent = _agents[i];
                agent.HomePatrolEnabled = true;
                RollAndEnterState(agent);
            }
        }

        private void OnMutationHarvested(string mutationId, MutationKind kind, string refId)
        {
            if (kind != MutationKind.Pet)
                return;
            RefreshDeploymentVisuals();
        }

        private void RefreshDeploymentVisuals()
        {
            if (_service == null || _root == null)
                return;

            for (int i = 0; i < _agents.Count; i++)
            {
                CancelInteraction(_agents[i], resumePatrol: false);
                StopAgentRoutine(_agents[i]);
                UnregisterAgentDepthSort(_agents[i]);
                if (_agents[i].Rt != null)
                    Destroy(_agents[i].Rt.gameObject);
            }
            _agents.Clear();

            TrySpawnDeployedCompanion(PetFieldSlot.LowerLeft);
            TrySpawnDeployedCompanion(PetFieldSlot.UpperLeft);

            bool showRoot = IsHomePatrolActive();
            if (_root != null)
                _root.gameObject.SetActive(showRoot);
        }

        private void TrySpawnDeployedCompanion(PetFieldSlot slot)
        {
            string configId = _service.GetDeployedPetConfigId(slot);
            if (string.IsNullOrEmpty(configId))
                return;

            var cfg = _service.GetPetConfig(configId);
            if (cfg == null)
            {
                UnityEngine.Debug.LogWarning("[PetCompanionPresenter] 上场精灵配置缺失：" + configId);
                return;
            }

            SpawnCompanion(cfg, slot);
        }

        private void SpawnCompanion(PetConfig petConfig, PetFieldSlot slot)
        {
            if (string.IsNullOrEmpty(petConfig.prefabResource))
            {
                UnityEngine.Debug.LogWarning("[PetCompanionPresenter] PetConfig.prefabResource 为空：" + petConfig.id);
                return;
            }

            var prefab = Resources.Load<GameObject>(petConfig.prefabResource);
            if (prefab == null)
            {
                UnityEngine.Debug.LogWarning("[PetCompanionPresenter] 加载预制体失败：" + petConfig.prefabResource);
                return;
            }

            if (!TryReadPrefabSkeletonSource(prefab, out var src))
                return;

            var shader = Shader.Find(SkeletonGraphicShaderName);
            if (shader == null)
            {
                UnityEngine.Debug.LogWarning("[PetCompanionPresenter] Shader 未找到：" + SkeletonGraphicShaderName + "，跳过本只精灵。");
                return;
            }

            var anchoredPos = ResolveInitialSpawnAnchoredPosition(slot);

            var rt = CreateChildRect(
                _root, "PetCompanion_" + petConfig.id + "_" + slot,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                anchoredPos, petGraphicSize);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            Canvas.ForceUpdateCanvases();
            bool spawnOnLeftHalf = FantaziaMonsterDisplay.IsSpawnOnScreenLeftHalf(rt);

            var uiMaterial = SkeletonGraphicUiMaterialFactory.CreateForPmaVertexColors(shader);
            var sg = rt.gameObject.AddComponent<SkeletonGraphic>();
            sg.material = uiMaterial;
            sg.skeletonDataAsset = src.DataAsset;
            sg.initialSkinName = src.InitialSkinName ?? string.Empty;
            sg.initialFlipX = spawnOnLeftHalf;
            sg.initialFlipY = src.InitialFlipY;
            sg.allowMultipleCanvasRenderers = NeedsMultipleCanvasRenderers(src.DataAsset);

            var meshGen = sg.MeshGenerator;
            var meshSettings = meshGen.settings;
            meshSettings.pmaVertexColors = src.PmaVertexColors;
            meshSettings.useClipping = src.UseClipping;
            meshGen.settings = meshSettings;

#if UNITY_2018_2_OR_NEWER
            var canvasRenderer = rt.gameObject.GetComponent<CanvasRenderer>();
            if (canvasRenderer != null)
                canvasRenderer.cullTransparentMesh = false;
#endif

            sg.Initialize(false);
            if (!sg.IsValid)
            {
                UnityEngine.Debug.LogWarning("[PetCompanionPresenter] SkeletonGraphic.Initialize 无效：" + petConfig.id);
                Destroy(rt.gameObject);
                return;
            }

            FantaziaMonsterDisplay.ConfigureFantaziaSkeletonGraphicUi(rt, sg, petLocalScale, rt);

            sg.raycastTarget = false;

            var agent = new PetCompanionAgent
            {
                Rt = rt,
                Sg = sg,
                Config = petConfig,
                Slot = slot,
                ResolvedIdleAnim = ResolveIdleAnimationName(sg, petConfig),
                ResolvedInteractionIdleAnim = ResolveInteractionIdleAnimationName(sg, petConfig),
                ResolvedAttackAnim = ResolveAttackAnimationName(sg),
                AgentIndex = _agents.Count,
            };

            if (string.IsNullOrEmpty(agent.ResolvedIdleAnim))
                UnityEngine.Debug.LogWarning("[PetCompanionPresenter] 精灵 " + petConfig.id + " 无可播放 idle，使用静默 Pose。");

            sg.AnimationState.Complete += entry => OnAgentSpineComplete(agent, entry);
            AttachClickHitbox(agent);

            _agents.Add(agent);
            RegisterAgentDepthSort(agent);

            if (IsHomePatrolActive())
            {
                agent.HomePatrolEnabled = true;
                RollAndEnterState(agent);
            }
            else
                FreezePatrolAtIdle(agent);
        }

        private bool IsHomePatrolActive()
        {
            return _bottomNav != null &&
                string.Equals(_bottomNav.OpenKey, JiaYuanHomeFeatureEntriesView.JiaYuanNavKey, StringComparison.Ordinal);
        }

        public void ForceEndAllDrags()
        {
            for (int i = 0; i < _agents.Count; i++)
                EndDrag(i);
        }

        public void CancelAllInteractionsForExternalDrag()
        {
            for (int i = 0; i < _agents.Count; i++)
                CancelInteraction(_agents[i], resumePatrol: false);
        }

        public void TryArmDrag(int agentIndex, PointerEventData eventData)
        {
            var agent = GetAgent(agentIndex);
            if (agent == null || agent.Rt == null || _root == null || !IsHomePatrolActive())
                return;
            if (!CanStartDrag(agent))
                return;

            agent.DragArmed = true;
            agent.DragOffset = HomeCharacterDragUtility.ComputeDragOffset(agent.Rt, _root, eventData);
        }

        public void CancelArmDrag(int agentIndex)
        {
            var agent = GetAgent(agentIndex);
            if (agent != null)
                agent.DragArmed = false;
        }

        public bool BeginDrag(int agentIndex, PointerEventData eventData)
        {
            var agent = GetAgent(agentIndex);
            if (agent == null || agent.Rt == null || _root == null || !IsHomePatrolActive())
                return false;
            if (agent.IsDragging || !CanStartDrag(agent))
                return false;

            CancelAllInteractionsForExternalDrag();
            var rolePresenter = GetComponent<MainRoleCunminPresenter>();
            rolePresenter?.ForceEndDragIfNeeded();

            StopAgentRoutine(agent);
            agent.BusyInAssist = false;
            agent.State = PetPatrolState.Idle;

            agent.PreDragScale = agent.Rt.localScale;
            agent.Rt.localScale = HomeCharacterDragUtility.ApplyScaleFactor(
                agent.PreDragScale, HomeCharacterDragUtility.DragScaleFactor);
            PlayInteractionIdleLoop(agent);

            agent.DragOffset = HomeCharacterDragUtility.ComputeDragOffset(agent.Rt, _root, eventData);

            agent.IsDragging = true;
            agent.DragArmed = false;
            UpdateDrag(agentIndex, eventData);
            return true;
        }

        public void UpdateDrag(int agentIndex, PointerEventData eventData)
        {
            var agent = GetAgent(agentIndex);
            if (agent == null || !agent.IsDragging || agent.Rt == null || _root == null || eventData == null)
                return;

            var fallback = agent.Rt.anchoredPosition;
            var pos = HomeCharacterDragUtility.ComputeAnchoredWithOffset(
                _root, eventData, agent.DragOffset, fallback);

            var boundsParent = HomeCharacterDragUtility.ResolveClampBoundsParent(agent.Rt) ?? _root;
            agent.Rt.anchoredPosition = HomeCharacterDragUtility.ClampAnchoredInBoundsParent(
                pos, agent.Rt, _root, boundsParent);
            _depthSorter?.MarkDirty();
        }

        public void EndDrag(int agentIndex)
        {
            var agent = GetAgent(agentIndex);
            if (agent == null)
                return;

            agent.DragArmed = false;
            if (!agent.IsDragging)
                return;

            agent.Rt.localScale = agent.PreDragScale;
            agent.IsDragging = false;
            _depthSorter?.MarkDirty();

            if (agent.HomePatrolEnabled)
                RollAndEnterState(agent);
            else
                PlayIdleLoop(agent);
        }

        private PetCompanionAgent GetAgent(int agentIndex)
        {
            if (agentIndex < 0 || agentIndex >= _agents.Count)
                return null;
            return _agents[agentIndex];
        }

        private bool CanStartDrag(PetCompanionAgent agent)
        {
            if (agent == null)
                return false;
            var rolePresenter = GetComponent<MainRoleCunminPresenter>();
            if (rolePresenter != null && rolePresenter.IsDragActive)
                return false;
            if (IsAnyDragActive())
                return false;

            for (int i = 0; i < _agents.Count; i++)
            {
                var other = _agents[i];
                if (other == agent)
                    continue;
                if (other.InteractionPhase != PetInteractionPhase.None)
                    return false;
            }

            return true;
        }

        private void OnAgentSpineComplete(PetCompanionAgent agent, TrackEntry entry)
        {
            if (entry == null || entry.TrackIndex != 0)
                return;
            if (agent.IsDragging)
                return;
            if (agent.InteractionPhase != PetInteractionPhase.None)
                return;
            if (agent.BusyInAssist)
                return;
            if (!agent.HomePatrolEnabled || agent.State != PetPatrolState.Idle)
                return;
            if (!entry.Loop)
                return;

            TryTransitionAfterIdleCycle(agent);
        }

        /// <summary>SPEC §9.5.3：由 <see cref="PetCompanionClickRelay"/> 转发。</summary>
        public void NotifyPetClicked(int agentIndex)
        {
            if (agentIndex < 0 || agentIndex >= _agents.Count)
                return;
            var agent = _agents[agentIndex];
            if (agent.IsDragging || IsAnyDragActive())
                return;
            BeginInteraction(agent);
        }

        private bool HasActiveInteraction()
        {
            if (IsAnyDragActive())
                return true;
            for (int i = 0; i < _agents.Count; i++)
            {
                if (_agents[i].InteractionPhase != PetInteractionPhase.None)
                    return true;
            }
            return false;
        }

        private void BeginInteraction(PetCompanionAgent agent)
        {
            if (agent == null || !IsHomePatrolActive())
                return;
            if (agent.IsDragging || IsAnyDragActive())
                return;
            var rolePresenter = GetComponent<MainRoleCunminPresenter>();
            if (rolePresenter != null && rolePresenter.IsDragActive)
                return;
            if (HasActiveInteraction() && agent.InteractionPhase == PetInteractionPhase.None)
                return;

            CancelInteraction(agent, resumePatrol: false);

            StopAgentRoutine(agent);
            agent.BusyInAssist = false;
            agent.State = PetPatrolState.Idle;
            agent.InteractionPhase = PetInteractionPhase.AwaitingChoice;
            PlayInteractionIdleLoop(agent);

            float headHalf = GetHeadHalfHeight();
            agent.Hud.ShowChoiceButtons(
                agent.Rt,
                headHalf,
                () => OnInteractionButtonClicked(agent),
                () => OnInteractionButtonClicked(agent));

            if (agent.InteractionRoutine != null)
                StopCoroutine(agent.InteractionRoutine);
            agent.InteractionRoutine = StartCoroutine(InteractionChoiceRoutine(agent));
        }

        private void OnInteractionButtonClicked(PetCompanionAgent agent)
        {
            if (agent == null || agent.InteractionPhase != PetInteractionPhase.AwaitingChoice)
                return;

            agent.InteractionPhase = PetInteractionPhase.Resolving;
            if (agent.InteractionRoutine != null)
            {
                StopCoroutine(agent.InteractionRoutine);
                agent.InteractionRoutine = null;
            }

            agent.Hud.HideAll();
            PlayInteractionIdleLoop(agent);

            var rolePresenter = GetComponent<MainRoleCunminPresenter>();
            if (rolePresenter != null && rolePresenter.VillagerRoleRoot != null && agent.Rt != null)
            {
                var world = agent.Rt.position;
                var local = rolePresenter.VillagerRoleRoot.InverseTransformPoint(world);
                var target = new Vector2(local.x + rolePresenter.PetInteractionOffsetX, local.y);
                rolePresenter.RunPetInteractionSequence(target);
            }

            float headHalf = GetHeadHalfHeight();
            agent.Hud.ShowFavorIcon(agent.Rt, headHalf);
            agent.InteractionRoutine = StartCoroutine(InteractionResolveRoutine(agent));
        }

        private IEnumerator InteractionChoiceRoutine(PetCompanionAgent agent)
        {
            yield return new WaitForSeconds(InteractionChoiceTimeoutSec);

            if (agent == null || agent.InteractionPhase != PetInteractionPhase.AwaitingChoice)
                yield break;

            EndInteractionResumePatrol(agent);
        }

        private IEnumerator InteractionResolveRoutine(PetCompanionAgent agent)
        {
            yield return new WaitForSeconds(InteractionPatrolResumeDelaySec);

            if (agent != null && agent.InteractionPhase == PetInteractionPhase.Resolving)
            {
                agent.InteractionPhase = PetInteractionPhase.None;
                if (agent.HomePatrolEnabled)
                    RollAndEnterState(agent);
            }

            yield return new WaitForSeconds(InteractionFavorIconDurationSec - InteractionPatrolResumeDelaySec);

            if (agent != null && agent.Rt != null)
            {
                agent.Hud.HideAll();
                agent.InteractionPhase = PetInteractionPhase.None;
                agent.InteractionRoutine = null;
            }
        }

        private void EndInteractionResumePatrol(PetCompanionAgent agent)
        {
            if (agent == null)
                return;

            CancelInteraction(agent, resumePatrol: true);
        }

        private void CancelInteraction(PetCompanionAgent agent, bool resumePatrol)
        {
            if (agent == null)
                return;

            if (agent.InteractionRoutine != null)
            {
                StopCoroutine(agent.InteractionRoutine);
                agent.InteractionRoutine = null;
            }

            agent.Hud.HideAll();
            var wasInteracting = agent.InteractionPhase != PetInteractionPhase.None;
            agent.InteractionPhase = PetInteractionPhase.None;

            if (resumePatrol && wasInteracting && agent.HomePatrolEnabled)
                RollAndEnterState(agent);
        }

        private void AttachClickHitbox(PetCompanionAgent agent)
        {
            if (agent?.Rt == null)
                return;

            var hitGo = new GameObject("PetClickHitbox");
            var hitRt = hitGo.AddComponent<RectTransform>();
            hitRt.SetParent(agent.Rt, false);
            hitRt.anchorMin = hitRt.anchorMax = new Vector2(0.5f, 0.5f);
            hitRt.pivot = new Vector2(0.5f, 0.5f);
            hitRt.anchoredPosition = Vector2.zero;
            hitRt.sizeDelta = petGraphicSize;

            var img = hitGo.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.004f);
            img.raycastTarget = true;

            var relay = hitGo.AddComponent<PetCompanionClickRelay>();
            relay.Initialize(this, agent.AgentIndex);
        }

        private float GetHeadHalfHeight()
        {
            return petGraphicSize.y * Mathf.Abs(petLocalScale.y) * 0.5f;
        }

        private void PlayInteractionIdleLoop(PetCompanionAgent agent)
        {
            if (agent.Sg == null || agent.Sg.AnimationState == null)
                return;

            var anim = !string.IsNullOrEmpty(agent.ResolvedInteractionIdleAnim)
                ? agent.ResolvedInteractionIdleAnim
                : agent.ResolvedIdleAnim;
            if (string.IsNullOrEmpty(anim))
                return;

            try
            {
                agent.Sg.AnimationState.SetAnimation(0, anim, true);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[PetCompanionPresenter] 互动 Idle 设置失败：" + e.Message);
            }
        }

        private string ResolveInteractionIdleAnimationName(SkeletonGraphic sg, PetConfig cfg)
        {
            var data = sg != null && sg.Skeleton != null ? sg.Skeleton.Data : null;
            if (data == null)
                return null;

            for (int i = 0; i < InteractionIdleAnimCandidates.Length; i++)
            {
                var a = FindAnimationCaseInsensitive(data, InteractionIdleAnimCandidates[i]);
                if (a != null)
                    return a.Name;
            }

            return ResolveIdleAnimationName(sg, cfg);
        }

        private void TryTransitionAfterIdleCycle(PetCompanionAgent agent)
        {
            if (!agent.HomePatrolEnabled || agent.State != PetPatrolState.Idle)
                return;

            if (!HasAnyPlantedTile())
                return;

            if (UnityEngine.Random.value < IdleStateProbability)
                return;

            BeginAssistSequence(agent);
        }

        private void RollAndEnterState(PetCompanionAgent agent)
        {
            StopAgentRoutine(agent);
            agent.BusyInAssist = false;
            agent.State = PetPatrolState.Idle;

            if (!agent.HomePatrolEnabled)
            {
                PlayIdleLoop(agent);
                return;
            }

            if (!HasAnyPlantedTile() || UnityEngine.Random.value < IdleStateProbability)
            {
                agent.State = PetPatrolState.Idle;
                PlayIdleLoop(agent);
                return;
            }

            BeginAssistSequence(agent);
        }

        private void FreezePatrolAtIdle(PetCompanionAgent agent)
        {
            StopAgentRoutine(agent);
            agent.HomePatrolEnabled = false;
            agent.BusyInAssist = false;
            agent.State = PetPatrolState.Idle;
            PlayIdleLoop(agent);
        }

        private void BeginAssistSequence(PetCompanionAgent agent)
        {
            StopAgentRoutine(agent);
            agent.State = PetPatrolState.AssistPlanting;
            agent.BusyInAssist = true;
            agent.Routine = StartCoroutine(AssistPlantingSequence(agent));
        }

        private IEnumerator AssistPlantingSequence(PetCompanionAgent agent)
        {
            string tileId = PickRandomPlantedTileId();
            if (string.IsNullOrEmpty(tileId))
            {
                agent.BusyInAssist = false;
                agent.State = PetPatrolState.Idle;
                PlayIdleLoop(agent);
                yield break;
            }

            if (TryGetTileLocalPosition(tileId, out var targetPos) && agent.Rt != null)
                yield return LerpAnchoredPosition(agent.Rt, targetPos, moveDurationSec);

            if (agent.Sg != null && agent.Rt != null)
                FantaziaMonsterDisplay.RefreshPetSkeletonGraphicFacing(agent.Sg, agent.Rt);

            for (int i = 0; i < 2; i++)
                yield return PlayAttackOnce(agent);

            ApplyAssistEffect(tileId);

            agent.BusyInAssist = false;
            agent.Routine = null;
            RollNextStateAfterAssist(agent);
        }

        private void RollNextStateAfterAssist(PetCompanionAgent agent)
        {
            if (!agent.HomePatrolEnabled)
            {
                agent.State = PetPatrolState.Idle;
                PlayIdleLoop(agent);
                return;
            }

            if (!HasAnyPlantedTile() || UnityEngine.Random.value < IdleStateProbability)
            {
                agent.State = PetPatrolState.Idle;
                PlayIdleLoop(agent);
                return;
            }

            BeginAssistSequence(agent);
        }

        private void ApplyAssistEffect(string tileId)
        {
            if (_service == null || string.IsNullOrEmpty(tileId))
                return;

            if (_service.TryHarvestTile(tileId))
                return;

            _service.TryWaterTile(tileId);
        }

        private bool HasAnyPlantedTile()
        {
            if (_service == null)
                return false;

            int count = _service.FarmTileCount;
            for (int i = 1; i <= count; i++)
            {
                var tile = _service.GetTileByOrder(i);
                if (IsPlantedTile(tile))
                    return true;
            }
            return false;
        }

        private static bool IsPlantedTile(CropTile tile)
        {
            return tile != null
                && tile.planting == PlantingFlag.Seeded
                && !string.IsNullOrEmpty(tile.plantInstanceId)
                && string.IsNullOrEmpty(tile.lockedByMutationId);
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

        private bool TryGetTileLocalPosition(string tileId, out Vector2 localPos)
        {
            localPos = Vector2.zero;
            if (_root == null || string.IsNullOrEmpty(tileId))
                return false;

            var grid = FarmGridView.Instance;
            if (grid == null)
                return false;

            return grid.TryGetTileLocalPositionIn(_root, tileId, out localPos);
        }

        private IEnumerator LerpAnchoredPosition(RectTransform rt, Vector2 target, float duration)
        {
            if (rt == null)
                yield break;

            var start = rt.anchoredPosition;
            if (duration <= 0f)
            {
                rt.anchoredPosition = target;
                _depthSorter?.MarkDirty();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rt.anchoredPosition = Vector2.Lerp(start, target, t);
                _depthSorter?.MarkDirty();
                yield return null;
            }

            rt.anchoredPosition = target;
            _depthSorter?.MarkDirty();
        }

        private IEnumerator PlayAttackOnce(PetCompanionAgent agent)
        {
            if (agent.Sg == null || agent.Sg.AnimationState == null)
                yield break;

            var anim = ResolveAttackClip(agent);
            if (anim == null)
            {
                yield return new WaitForSeconds(0.25f);
                yield break;
            }

            var entry = agent.Sg.AnimationState.SetAnimation(0, anim.Name, false);
            if (entry == null)
            {
                yield return new WaitForSeconds(0.25f);
                yield break;
            }

            float elapsed = 0f;
            float timeout = Mathf.Max(0.1f, animationWaitTimeout);
            while (!entry.IsComplete && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private Spine.Animation ResolveAttackClip(PetCompanionAgent agent)
        {
            if (agent.Sg == null || agent.Sg.Skeleton == null || agent.Sg.Skeleton.Data == null)
                return null;

            var data = agent.Sg.Skeleton.Data;
            if (!string.IsNullOrEmpty(agent.ResolvedAttackAnim))
            {
                var primary = FindAnimationCaseInsensitive(data, agent.ResolvedAttackAnim);
                if (primary != null)
                    return primary;
            }

            for (int i = 0; i < AttackAnimFallbacks.Length; i++)
            {
                var fb = FindAnimationCaseInsensitive(data, AttackAnimFallbacks[i]);
                if (fb != null)
                    return fb;
            }

            return null;
        }

        private void PlayIdleLoop(PetCompanionAgent agent)
        {
            if (agent.Sg == null || agent.Sg.AnimationState == null)
                return;

            if (string.IsNullOrEmpty(agent.ResolvedIdleAnim))
                return;

            try
            {
                agent.Sg.AnimationState.SetAnimation(0, agent.ResolvedIdleAnim, true);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[PetCompanionPresenter] idle 动画设置失败：" + e.Message);
            }
        }

        private void StopAgentRoutine(PetCompanionAgent agent)
        {
            if (agent == null || agent.Routine == null)
                return;
            StopCoroutine(agent.Routine);
            agent.Routine = null;
            agent.BusyInAssist = false;
        }

        private string ResolveAttackAnimationName(SkeletonGraphic sg)
        {
            var data = sg != null && sg.Skeleton != null ? sg.Skeleton.Data : null;
            if (data == null)
                return null;

            var byName = FindAnimationCaseInsensitive(data, attackAnimationName);
            if (byName != null)
                return byName.Name;

            for (int i = 0; i < AttackAnimFallbacks.Length; i++)
            {
                var fb = FindAnimationCaseInsensitive(data, AttackAnimFallbacks[i]);
                if (fb != null)
                    return fb.Name;
            }

            return null;
        }

        private string ResolveIdleAnimationName(SkeletonGraphic sg, PetConfig cfg)
        {
            var data = sg != null && sg.Skeleton != null ? sg.Skeleton.Data : null;
            if (data == null)
                return null;

            var byIdle = FindAnimationCaseInsensitive(data, idleAnimationName);
            if (byIdle != null)
                return byIdle.Name;

            if (cfg.randomAnimations != null)
            {
                for (int i = 0; i < cfg.randomAnimations.Count; i++)
                {
                    var n = cfg.randomAnimations[i];
                    var a = FindAnimationCaseInsensitive(data, n);
                    if (a != null)
                        return a.Name;
                }
            }

            if (data.Animations != null && data.Animations.Count > 0)
            {
                var first = data.Animations.Items[0];
                if (first != null)
                    return first.Name;
            }
            return null;
        }

        private static bool NeedsMultipleCanvasRenderers(SkeletonDataAsset asset)
        {
            if (asset?.atlasAssets == null || asset.atlasAssets.Length == 0)
                return false;
            if (asset.atlasAssets.Length > 1)
                return true;
            var first = asset.atlasAssets[0];
            return first != null && first.MaterialCount > 1;
        }

        private static bool TryReadPrefabSkeletonSource(GameObject prefab, out PrefabSkeletonSource src)
        {
            src = default;
            var probe = Instantiate(prefab);
            probe.SetActive(false);
            if (CountMissingMonoScripts(probe) > 0)
            {
                Destroy(probe);
                UnityEngine.Debug.LogWarning("[PetCompanionPresenter] 预制体存在缺失脚本：" + prefab.name);
                return false;
            }

            var anim = probe.GetComponent<SkeletonAnimation>()
                ?? probe.GetComponentInChildren<SkeletonAnimation>(true);
            if (anim == null || anim.skeletonDataAsset == null)
            {
                Destroy(probe);
                UnityEngine.Debug.LogWarning("[PetCompanionPresenter] 预制体缺少 SkeletonAnimation：" + prefab.name);
                return false;
            }

            src.DataAsset = anim.skeletonDataAsset;
            src.InitialSkinName = anim.initialSkinName;
            src.InitialFlipX = anim.initialFlipX;
            src.InitialFlipY = anim.initialFlipY;
            src.PmaVertexColors = anim.pmaVertexColors;
            src.UseClipping = anim.useClipping;
            Destroy(probe);
            return true;
        }

        private static Spine.Animation FindAnimationCaseInsensitive(SkeletonData data, string animationName)
        {
            if (data == null || string.IsNullOrEmpty(animationName))
                return null;
            var exact = data.FindAnimation(animationName);
            if (exact != null)
                return exact;
            var anims = data.Animations;
            if (anims == null)
                return null;
            for (int i = 0; i < anims.Count; i++)
            {
                var a = anims.Items[i];
                if (a != null && string.Equals(a.Name, animationName, StringComparison.OrdinalIgnoreCase))
                    return a;
            }
            return null;
        }

        private static int CountMissingMonoScripts(GameObject go)
        {
            var components = go.GetComponentsInChildren<MonoBehaviour>(true);
            int missing = 0;
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                    missing++;
            }
            return missing;
        }

        private Vector2 ResolveInitialSpawnAnchoredPosition(PetFieldSlot slot)
        {
            var villagerPos = ResolveVillagerPositionInPetRoot();
            float horizontal = slot == PetFieldSlot.LowerLeft
                ? -petSpawnHorizontalOffset
                : petSpawnHorizontalOffset;
            float vertical = slot == PetFieldSlot.LowerLeft
                ? petLowerVerticalOffset
                : petUpperVerticalOffset;
            return new Vector2(villagerPos.x + horizontal, villagerPos.y + vertical);
        }

        private Vector2 ResolveVillagerPositionInPetRoot()
        {
            var rolePresenter = GetComponent<MainRoleCunminPresenter>();
            var villagerRt = rolePresenter != null ? rolePresenter.VillagerRoleRectTransform : null;
            if (villagerRt == null || _root == null)
                return Vector2.zero;

            var localInRoot = _root.InverseTransformPoint(villagerRt.position);
            return new Vector2(localInRoot.x, localInRoot.y);
        }

        private void RegisterAgentDepthSort(PetCompanionAgent agent)
        {
            if (_depthSorter == null || agent == null || agent.Rt == null)
                return;

            _depthSorter.RegisterOrUpdate(new DepthSortEntry
            {
                Visual = agent.Rt,
                SortY = 0f,
                LayerOffset = JiaYuanWorldDepthLayer.Pet,
                Active = true,
                UseVisualCenterY = true,
            });
        }

        private void UnregisterAgentDepthSort(PetCompanionAgent agent)
        {
            if (_depthSorter == null || agent == null || agent.Rt == null)
                return;
            _depthSorter.Unregister(agent.Rt);
        }

        private static RectTransform CreateChildRect(
            RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }
    }
}
