// 主界面主角 Spine 展示与动画（SPEC §9.5 / §9.5.3 / §9.5.4 v3.92 / §9.8.14）；预制体默认骨骼为 LangRen `Role_cslangren`。
// 使用 SkeletonGraphic：Screen Space Overlay 下 MeshRenderer 的 SkeletonAnimation 会被全屏 UI 盖住，无法看见。
using System;
using System.Collections;
using System.Collections.Generic;
using PetDemo.Farm;
using PetDemo.UI.Farm;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public class MainRoleCunminPresenter : MonoBehaviour
    {
        private const string ResourcesPrefabPath = "Prefabs/Air/Hero_Role_cunmin";
        private const string SkeletonGraphicShaderName = "Spine/SkeletonGraphic";

        private enum VillagerCommandKind
        {
            Water,
            FertilizeAttack,
            Wait,
        }

        private struct VillagerCommand
        {
            public VillagerCommandKind Kind;
            public string TileId;
            public string FertilizerId;
        }

        [Header("Hero Spine (Main Menu)")]
        [SerializeField] private GameObject villagerPrefabOverride;
        [SerializeField] private Vector2 villagerAnchoredPosition = Vector2.zero;
        [SerializeField] private Vector3 villagerLocalScale = new Vector3(0.53f, 0.53f, 1f);
        [SerializeField] private Vector2 villagerGraphicSize = new Vector2(720f, 1200f);

        [Header("Hero Drag Hitbox (SPEC §9.5.4 v3.92)")]
        [SerializeField] private Vector2 dragHitboxSize = new Vector2(322f, 622f);
        [SerializeField] private float dragHitboxPosY = 290f;

        [SerializeField] private string idleAnimationName = "exclusive_2";
        [SerializeField] private string moveAnimationName = "move_1";
        [SerializeField] private string workAnimationName = "work_1";
        // Legacy Inspector field; fertilize uses workAnimationName (SPEC §9.5 v3.82, same as water).
        [SerializeField] private string waterFertilizeAnimationName = "attack_3";
        [SerializeField] private string harvestGrowthAnimationName = "wait_3";

        [Header("Water/Fertilize Move (SPEC §9.5 v3.80, offset v3.85–v3.86)")]
        [SerializeField] private float moveDurationSec = 0.6f;
        [SerializeField] private Vector2 tileApproachOffsetFromLeft = new Vector2(-160f, 40f);
        [SerializeField] private Vector2 tileApproachOffsetFromRight = new Vector2(160f, 40f);
        [SerializeField] private float workEffectDelaySec = 0.5f;
        [SerializeField] private float animationWaitTimeout = 8f;

        [Header("Pet Interaction (SPEC §9.5.3 v3.89)")]
        [SerializeField] private string petInteractionWorkAnimationName = "work_3";
        [SerializeField] private float petInteractionOffsetX = 40f;

        /// <summary>
        /// SPEC §9.5.1：主角实际 anchoredPosition，供 <see cref="PetCompanionPresenter"/> 同步伴侣 Y 坐标。
        /// </summary>
        public Vector2 VillagerAnchoredPosition => villagerAnchoredPosition;

        public RectTransform VillagerRoleRectTransform => _villagerRoleRt;

        public RectTransform VillagerRoleRoot => _villagerRootRt;

        public float PetInteractionOffsetX => petInteractionOffsetX;

        public bool IsDragActive => _dragActive;

        private bool _built;
        private IPlantingService _service;
        private JiaYuanWorldScreenView _jiaYuanWorld;
        private RectTransform _villagerRootRt;
        private RectTransform _villagerRoleRt;
        private SkeletonGraphic _skeletonGraphic;
        private readonly Queue<VillagerCommand> _actionQueue = new Queue<VillagerCommand>();
        private Coroutine _queueRoutine;
        private bool _queueProcessing;
        private VillagerCommand _inFlightCommand;
        private bool _hasInFlightCommand;
        private VillagerCommand _suspendedCommand;
        private bool _hasSuspendedCommand;
        private Coroutine _petInteractionRoutine;
        private bool _petInteractionRunning;

        private JiaYuanWorldDepthSorter _depthSorter;
        private BottomNavBarView _bottomNav;
        private Action<int, string> _navChangedHandler;
        private bool _dragActive;
        private bool _dragArmed;
        private Vector2 _dragOffset;
        private Vector3 _preDragScale;

        private Action<string, ActionType> _unifiedHandler;
        private Action _roleStatsHandler;
        private Action<string, string> _fertilizeAppliedHandler;
        private Action _appearanceHandler;

        public void Build(RectTransform parent, IPlantingService service)
        {
            if (_built || parent == null)
                return;
            _built = true;
            _service = service;

            var villagerPrefab = ResolveVillagerPrefab();
            _villagerRootRt = CreateChildRect(
                parent, "VillagerRoleRoot",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

            if (!TryBuildSkeletonGraphic(_villagerRootRt) &&
                (villagerPrefab == null || !TryBuildSkeletonGraphicFromPrefab(villagerPrefab, _villagerRootRt)))
            {
                Destroy(_villagerRootRt.gameObject);
                _villagerRootRt = null;
                BuildFallback(parent);
                return;
            }

            _skeletonGraphic.raycastTarget = false;
            PlayIdleLoop();

            if (_service != null)
            {
                _unifiedHandler = OnUnifiedActionExecuted;
                _roleStatsHandler = OnRoleStatsChanged;
                _fertilizeAppliedHandler = OnFertilizeApplied;
                _appearanceHandler = OnPlayerAppearanceChanged;
                _service.OnUnifiedActionExecuted += _unifiedHandler;
                _service.OnRoleStatsChanged += _roleStatsHandler;
                _service.OnFertilizeApplied += _fertilizeAppliedHandler;
                _service.OnPlayerAppearanceChanged += _appearanceHandler;
            }

            AttachDragHitbox();
            RegisterVillagerDepthSort();
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

        /// <summary>绑定家园视口，供一次性 Spine 片段期间冻结镜头跟随（SPEC §9.8.14 v3.80.2）。</summary>
        public void BindJiaYuanWorld(JiaYuanWorldScreenView worldView)
        {
            _jiaYuanWorld = worldView;
        }

        /// <summary>SPEC §9.1.4（v3.111）：绑定 Y 轴深度排序器。</summary>
        public void BindDepthSorter(JiaYuanWorldDepthSorter sorter)
        {
            _depthSorter = sorter;
            RegisterVillagerDepthSort();
        }

        /// <summary>SPEC §9.5.4：底栏离开家园或销毁时结束拖动。</summary>
        public void ForceEndDragIfNeeded()
        {
            if (_dragActive || _dragArmed)
                EndDrag();
        }

        public void TryArmDrag(PointerEventData eventData)
        {
            if (!_built || _villagerRoleRt == null || _villagerRootRt == null || !IsJiaYuanActive())
                return;
            if (!CanStartDrag())
                return;

            _dragArmed = true;
            _dragOffset = HomeCharacterDragUtility.ComputeDragOffset(
                _villagerRoleRt, _villagerRootRt, eventData);
        }

        public void CancelArmDrag()
        {
            _dragArmed = false;
        }

        public bool BeginDrag(PointerEventData eventData)
        {
            if (!_built || _villagerRoleRt == null || _villagerRootRt == null || !IsJiaYuanActive())
                return false;
            if (_dragActive || !CanStartDrag())
                return false;

            CancelPetInteractionsForDrag();

            if (_petInteractionRoutine != null)
            {
                StopCoroutine(_petInteractionRoutine);
                _petInteractionRoutine = null;
                _petInteractionRunning = false;
            }

            _preDragScale = _villagerRoleRt.localScale;
            _villagerRoleRt.localScale = HomeCharacterDragUtility.ApplyScaleFactor(
                _preDragScale, HomeCharacterDragUtility.DragScaleFactor);

            SuspendQueueForInteraction();
            PlayIdleLoop();
            _jiaYuanWorld?.SetFollowFrozen(true);

            _dragOffset = HomeCharacterDragUtility.ComputeDragOffset(
                _villagerRoleRt, _villagerRootRt, eventData);

            _dragActive = true;
            _dragArmed = false;
            UpdateDrag(eventData);
            return true;
        }

        public void UpdateDrag(PointerEventData eventData)
        {
            if (!_dragActive || _villagerRoleRt == null || _villagerRootRt == null || eventData == null)
                return;

            var fallback = _villagerRoleRt.anchoredPosition;
            var pos = HomeCharacterDragUtility.ComputeAnchoredWithOffset(
                _villagerRootRt, eventData, _dragOffset, fallback);

            var boundsParent = HomeCharacterDragUtility.ResolveClampBoundsParent(_villagerRoleRt)
                ?? _villagerRootRt;
            _villagerRoleRt.anchoredPosition = HomeCharacterDragUtility.ClampAnchoredInBoundsParent(
                pos, _villagerRoleRt, _villagerRootRt, boundsParent);
            _depthSorter?.MarkDirty();
        }

        public void EndDrag()
        {
            if (!_dragActive && !_dragArmed)
                return;

            _dragArmed = false;

            if (_villagerRoleRt != null && _dragActive)
            {
                _villagerRoleRt.localScale = _preDragScale;
                villagerAnchoredPosition = _villagerRoleRt.anchoredPosition;
                _depthSorter?.MarkDirty();
            }

            if (_dragActive)
            {
                _dragActive = false;
                _jiaYuanWorld?.SetFollowFrozen(false);
                TryResumeActionQueue();
                if (!_queueProcessing)
                    PlayIdleLoop();
            }
        }

        /// <summary>SPEC §9.5.3：走到精灵偏右位置并播放 work_3×2，挂起并恢复浇水/施肥队列。</summary>
        public void RunPetInteractionSequence(Vector2 targetInVillagerRoot)
        {
            if (!_built || _villagerRoleRt == null || _petInteractionRunning || _dragActive)
                return;
            if (_petInteractionRoutine != null)
                StopCoroutine(_petInteractionRoutine);
            _petInteractionRoutine = StartCoroutine(PetInteractionSequenceRoutine(targetInVillagerRoot));
        }

        private void OnDestroy()
        {
            if (_bottomNav != null && _navChangedHandler != null)
                _bottomNav.OnOpenChanged -= _navChangedHandler;

            ForceEndDragIfNeeded();

            if (_depthSorter != null && _villagerRoleRt != null)
                _depthSorter.Unregister(_villagerRoleRt);

            if (_queueRoutine != null)
                StopCoroutine(_queueRoutine);
            if (_petInteractionRoutine != null)
                StopCoroutine(_petInteractionRoutine);

            _jiaYuanWorld?.SetFollowFrozen(false);

            if (_service == null)
                return;
            if (_unifiedHandler != null) _service.OnUnifiedActionExecuted -= _unifiedHandler;
            if (_roleStatsHandler != null) _service.OnRoleStatsChanged -= _roleStatsHandler;
            if (_fertilizeAppliedHandler != null) _service.OnFertilizeApplied -= _fertilizeAppliedHandler;
            if (_appearanceHandler != null) _service.OnPlayerAppearanceChanged -= _appearanceHandler;
        }

        private void OnPlayerAppearanceChanged()
        {
            RebuildVillagerAppearance();
        }

        /// <summary>SPEC §9.14.9（v3.244）：装备 Spine 变更后重建家园主角。</summary>
        private void RebuildVillagerAppearance()
        {
            if (!_built || _villagerRootRt == null)
                return;

            if (_villagerRoleRt != null)
            {
                Destroy(_villagerRoleRt.gameObject);
                _villagerRoleRt = null;
            }
            _skeletonGraphic = null;

            if (!TryBuildSkeletonGraphic(_villagerRootRt))
            {
                var prefab = ResolveVillagerPrefab();
                if (prefab == null || !TryBuildSkeletonGraphicFromPrefab(prefab, _villagerRootRt))
                {
                    UnityEngine.Debug.LogWarning("[MainRoleCunminPresenter] 外观重建失败。");
                    return;
                }
            }

            if (_skeletonGraphic != null)
            {
                _skeletonGraphic.raycastTarget = false;
                PlayIdleLoop();
            }
        }

        private GameObject ResolveVillagerPrefab()
        {
            if (villagerPrefabOverride != null)
                return villagerPrefabOverride;

            var fromResources = Resources.Load<GameObject>(ResourcesPrefabPath);
            if (fromResources != null)
                return fromResources;

#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Scenes/Air/Role/Hero_Role_cunmin.prefab");
#else
            return null;
#endif
        }

        /// <summary>SPEC §9.14.9（v3.244）：优先从会话装备路径解析 SkeletonData 并构建。</summary>
        private bool TryBuildSkeletonGraphic(RectTransform parent)
        {
            string equipped = _service != null ? _service.GetEquippedPlayerSpineResource() : null;
            var dataAsset = PetDemo.Core.PlayerSpineAppearanceResolver.Resolve(equipped);
            if (dataAsset == null)
                return false;
            return BuildSkeletonGraphicFromData(dataAsset, parent);
        }

        private bool TryBuildSkeletonGraphicFromPrefab(GameObject prefab, RectTransform parent)
        {
            GameObject probe = Instantiate(prefab);
            probe.SetActive(false);
            if (CountMissingMonoScripts(probe) > 0)
            {
                Destroy(probe);
                UnityEngine.Debug.LogWarning("MainRoleCunminPresenter: villager prefab has missing scripts.");
                return false;
            }

            var srcAnim = probe.GetComponent<SkeletonAnimation>()
                ?? probe.GetComponentInChildren<SkeletonAnimation>(true);
            var dataAsset = srcAnim != null ? srcAnim.skeletonDataAsset : null;
            Destroy(probe);

            if (dataAsset == null)
            {
                UnityEngine.Debug.LogWarning("MainRoleCunminPresenter: could not resolve SkeletonDataAsset from villager prefab.");
                return false;
            }

            return BuildSkeletonGraphicFromData(dataAsset, parent);
        }

        private bool BuildSkeletonGraphicFromData(SkeletonDataAsset dataAsset, RectTransform parent)
        {
            if (dataAsset == null || parent == null)
                return false;

            var shader = Shader.Find(SkeletonGraphicShaderName);
            if (shader == null)
            {
                UnityEngine.Debug.LogError("MainRoleCunminPresenter: shader '" + SkeletonGraphicShaderName + "' not found.");
                return false;
            }

            var uiMaterial = SkeletonGraphicUiMaterialFactory.CreateForPmaVertexColors(shader);
            var roleGo = new GameObject("VillagerRole");
            var rt = roleGo.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = villagerAnchoredPosition;
            rt.sizeDelta = villagerGraphicSize;
            rt.localRotation = Quaternion.identity;
            rt.localScale = villagerLocalScale;

            _villagerRoleRt = rt;
            _skeletonGraphic = SkeletonGraphic.AddSkeletonGraphicComponent(roleGo, dataAsset, uiMaterial);
            return _skeletonGraphic != null && _skeletonGraphic.IsValid;
        }

        private void OnUnifiedActionExecuted(string tileId, ActionType action)
        {
            if (action != ActionType.Water)
                return;
            EnqueueCommand(new VillagerCommand
            {
                Kind = VillagerCommandKind.Water,
                TileId = tileId,
            });
        }

        private void OnFertilizeApplied(string tileId, string fertilizerId)
        {
            EnqueueCommand(new VillagerCommand
            {
                Kind = VillagerCommandKind.FertilizeAttack,
                TileId = tileId,
                FertilizerId = fertilizerId,
            });
        }

        private void OnRoleStatsChanged()
        {
            EnqueueCommand(new VillagerCommand { Kind = VillagerCommandKind.Wait });
        }

        private void EnqueueCommand(VillagerCommand command)
        {
            _actionQueue.Enqueue(command);
            if (!_queueProcessing && !_dragActive && isActiveAndEnabled)
                _queueRoutine = StartCoroutine(ProcessActionQueue());
        }

        private IEnumerator ProcessActionQueue()
        {
            _queueProcessing = true;

            while (_actionQueue.Count > 0)
            {
                var cmd = _actionQueue.Dequeue();
                _inFlightCommand = cmd;
                _hasInFlightCommand = true;

                switch (cmd.Kind)
                {
                    case VillagerCommandKind.Water:
                        yield return RunWorkSequence(cmd);
                        break;
                    case VillagerCommandKind.FertilizeAttack:
                        yield return RunWorkSequence(cmd);
                        break;
                    case VillagerCommandKind.Wait:
                        yield return PlayClipOnceCoroutine(
                            ResolveClipName(harvestGrowthAnimationName, "wait_3", "wait_1", "wait", "animation"));
                        break;
                }

                _hasInFlightCommand = false;
            }

            PlayIdleLoop();
            _queueProcessing = false;
            _queueRoutine = null;
        }

        private void SuspendQueueForPetInteraction()
        {
            SuspendQueueForInteraction();
        }

        private void SuspendQueueForInteraction()
        {
            if (_queueRoutine != null)
            {
                StopCoroutine(_queueRoutine);
                _queueRoutine = null;
            }

            if (_hasInFlightCommand)
            {
                _suspendedCommand = _inFlightCommand;
                _hasSuspendedCommand = true;
                _hasInFlightCommand = false;
            }

            _queueProcessing = false;
            if (!_dragActive)
                _jiaYuanWorld?.SetFollowFrozen(false);
        }

        private void OnBottomNavOpenChanged(int index, string key)
        {
            bool isJiaYuan = !string.IsNullOrEmpty(key) &&
                string.Equals(key, JiaYuanHomeFeatureEntriesView.JiaYuanNavKey, StringComparison.Ordinal);
            if (!isJiaYuan)
                ForceEndDragIfNeeded();
        }

        private bool IsJiaYuanActive()
        {
            return _bottomNav != null &&
                string.Equals(_bottomNav.OpenKey, JiaYuanHomeFeatureEntriesView.JiaYuanNavKey, StringComparison.Ordinal);
        }

        private bool CanStartDrag()
        {
            var petPresenter = GetComponent<PetCompanionPresenter>();
            if (petPresenter != null && petPresenter.IsAnyDragActive())
                return false;
            return true;
        }

        private void CancelPetInteractionsForDrag()
        {
            var petPresenter = GetComponent<PetCompanionPresenter>();
            petPresenter?.CancelAllInteractionsForExternalDrag();
        }

        private void AttachDragHitbox()
        {
            if (_villagerRoleRt == null)
                return;

            var hitGo = new GameObject("VillagerDragHitbox");
            var hitRt = hitGo.AddComponent<RectTransform>();
            hitRt.SetParent(_villagerRoleRt, false);
            hitRt.anchorMin = hitRt.anchorMax = new Vector2(0.5f, 0.5f);
            hitRt.pivot = new Vector2(0.5f, 0.5f);
            hitRt.anchoredPosition = new Vector2(0f, dragHitboxPosY);
            hitRt.sizeDelta = dragHitboxSize;

            var img = hitGo.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.004f);
            img.raycastTarget = true;

            var relay = hitGo.AddComponent<VillagerDragRelay>();
            relay.Initialize(this);
        }

        private void PrependSuspendedCommandToQueue()
        {
            if (!_hasSuspendedCommand)
                return;

            var pending = new List<VillagerCommand> { _suspendedCommand };
            while (_actionQueue.Count > 0)
                pending.Add(_actionQueue.Dequeue());

            for (int i = 0; i < pending.Count; i++)
                _actionQueue.Enqueue(pending[i]);

            _hasSuspendedCommand = false;
        }

        private void TryResumeActionQueue()
        {
            PrependSuspendedCommandToQueue();
            if (_actionQueue.Count > 0 && !_queueProcessing && isActiveAndEnabled)
                _queueRoutine = StartCoroutine(ProcessActionQueue());
        }

        private IEnumerator PetInteractionSequenceRoutine(Vector2 targetInVillagerRoot)
        {
            _petInteractionRunning = true;
            SuspendQueueForPetInteraction();

            try
            {
                if (_villagerRoleRt != null)
                {
                    PlayMoveLoop();
                    yield return LerpAnchoredPosition(_villagerRoleRt, targetInVillagerRoot, moveDurationSec);
                }

                var workClip = ResolveClipName(petInteractionWorkAnimationName, "work_3", "work_1", "work", "animation");
                for (int i = 0; i < 2; i++)
                    yield return PlayClipOnceCoroutine(workClip);
            }
            finally
            {
                _petInteractionRunning = false;
                _petInteractionRoutine = null;
                TryResumeActionQueue();
                if (!_queueProcessing)
                    PlayIdleLoop();
            }
        }

        private IEnumerator RunWorkSequence(VillagerCommand cmd)
        {
            if (_villagerRoleRt == null)
                yield break;

            var tileId = cmd.TileId;
            var grid = FarmGridView.Instance;
            if (grid != null && !string.IsNullOrEmpty(tileId) &&
                grid.TryGetTileLocalPositionIn(_villagerRootRt, tileId, out var tileCenterPos))
            {
                var targetPos = tileCenterPos + ResolveTileApproachOffset(tileCenterPos);
                PlayMoveLoop();
                yield return LerpAnchoredPosition(_villagerRoleRt, targetPos, moveDurationSec);
            }

            var workClip = ResolveClipName(workAnimationName, "work_1", "work", "animation");
            yield return PlayWorkClipWithDelayedCommit(workClip, cmd);
        }

        // SPEC §9.5 (v3.87)：work_1 开始后 workEffectDelaySec 再提交浇水/施肥数据。
        private IEnumerator PlayWorkClipWithDelayedCommit(string clipName, VillagerCommand cmd)
        {
            _jiaYuanWorld?.SetFollowFrozen(true);
            try
            {
                if (_skeletonGraphic == null || string.IsNullOrEmpty(clipName))
                {
                    yield return new WaitForSeconds(Mathf.Max(0.1f, workEffectDelaySec));
                    CommitWorkEffect(cmd);
                    yield break;
                }

                var entry = _skeletonGraphic.AnimationState.SetAnimation(0, clipName, false);
                if (entry == null)
                {
                    yield return new WaitForSeconds(Mathf.Max(0.1f, workEffectDelaySec));
                    CommitWorkEffect(cmd);
                    yield break;
                }

                float delay = Mathf.Max(0f, workEffectDelaySec);
                if (delay > 0f)
                    yield return new WaitForSeconds(delay);

                CommitWorkEffect(cmd);

                float elapsed = 0f;
                float timeout = Mathf.Max(0.1f, animationWaitTimeout);
                while (!entry.IsComplete && elapsed < timeout)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
            finally
            {
                _jiaYuanWorld?.SetFollowFrozen(false);
            }
        }

        private void CommitWorkEffect(VillagerCommand cmd)
        {
            if (_service == null || string.IsNullOrEmpty(cmd.TileId))
                return;

            switch (cmd.Kind)
            {
                case VillagerCommandKind.Water:
                    _service.CommitWaterTile(cmd.TileId);
                    break;
                case VillagerCommandKind.FertilizeAttack:
                    _service.CommitFertilizeToTile(cmd.TileId, cmd.FertilizerId);
                    break;
            }
        }

        private IEnumerator PlayClipOnceCoroutine(string clipName)
        {
            _jiaYuanWorld?.SetFollowFrozen(true);
            try
            {
                if (_skeletonGraphic == null || string.IsNullOrEmpty(clipName))
                {
                    yield return new WaitForSeconds(0.25f);
                    yield break;
                }

                var entry = _skeletonGraphic.AnimationState.SetAnimation(0, clipName, false);
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
            finally
            {
                _jiaYuanWorld?.SetFollowFrozen(false);
            }
        }

        private IEnumerator LerpAnchoredPosition(RectTransform rt, Vector2 target, float duration)
        {
            if (rt == null)
                yield break;

            var start = rt.anchoredPosition;
            ApplyMoveHorizontalFacing(start, target);

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

        private void RegisterVillagerDepthSort()
        {
            if (_depthSorter == null || _villagerRoleRt == null)
                return;

            _depthSorter.RegisterOrUpdate(new DepthSortEntry
            {
                Visual = _villagerRoleRt,
                SortY = 0f,
                LayerOffset = JiaYuanWorldDepthLayer.Villager,
                Active = true,
                UseVisualCenterY = true,
            });
        }

        /// <summary>
        /// SPEC §9.5 (v3.86)：村民在田格中心左侧时用 <see cref="tileApproachOffsetFromLeft"/>，右侧用 <see cref="tileApproachOffsetFromRight"/>；同 X 默认左侧偏移。
        /// </summary>
        private Vector2 ResolveTileApproachOffset(Vector2 tileCenterInRootSpace)
        {
            if (_villagerRoleRt == null)
                return tileApproachOffsetFromLeft;

            float villagerX = _villagerRoleRt.anchoredPosition.x;
            if (villagerX < tileCenterInRootSpace.x)
                return tileApproachOffsetFromLeft;
            if (villagerX > tileCenterInRootSpace.x)
                return tileApproachOffsetFromRight;
            return tileApproachOffsetFromLeft;
        }

        /// <summary>
        /// SPEC §9.5 (v3.80.6)：默认朝左（正 scale.x）；目标 X 大于当前 X 时水平镜像朝右。
        /// </summary>
        private void ApplyMoveHorizontalFacing(Vector2 from, Vector2 to)
        {
            if (_villagerRoleRt == null)
                return;

            const float axisEpsilon = 0.01f;
            if (to.x > from.x + axisEpsilon)
                SetHeroFacingRight();
            else if (to.x < from.x - axisEpsilon)
                SetHeroFacingLeft();
        }

        private void SetHeroFacingLeft()
        {
            var s = villagerLocalScale;
            _villagerRoleRt.localScale = new Vector3(Mathf.Abs(s.x), s.y, s.z);
        }

        private void SetHeroFacingRight()
        {
            _villagerRoleRt.localScale = FantaziaMonsterDisplay.HorizontallyMirroredScale(villagerLocalScale);
        }

        private void PlayMoveLoop()
        {
            if (_skeletonGraphic == null)
                return;

            var moveClip = ResolveClipName(moveAnimationName, "move_1", "move", "animation");
            if (string.IsNullOrEmpty(moveClip))
                return;

            _skeletonGraphic.AnimationState.SetAnimation(0, moveClip, true);
        }

        private void PlayIdleLoop()
        {
            if (_skeletonGraphic == null)
                return;

            var idleClip = ResolveClipName(
                idleAnimationName, "exclusive_2", "standby_1", "animation", "idle");
            if (string.IsNullOrEmpty(idleClip))
            {
                UnityEngine.Debug.LogWarning("MainRoleCunminPresenter: no usable idle Spine clip.");
                return;
            }

            _skeletonGraphic.AnimationState.SetAnimation(0, idleClip, true);
        }

        private bool HasAnimation(string animationName)
        {
            return _skeletonGraphic != null
                && _skeletonGraphic.Skeleton != null
                && _skeletonGraphic.Skeleton.Data != null
                && _skeletonGraphic.Skeleton.Data.FindAnimation(animationName) != null;
        }

        private string ResolveClipName(string configured, params string[] fallbacks)
        {
            if (_skeletonGraphic?.Skeleton?.Data == null)
                return null;
            if (!string.IsNullOrEmpty(configured) && HasAnimation(configured))
                return configured;
            if (fallbacks != null)
            {
                for (var i = 0; i < fallbacks.Length; i++)
                {
                    var n = fallbacks[i];
                    if (!string.IsNullOrEmpty(n) && HasAnimation(n))
                        return n;
                }
            }

            var anims = _skeletonGraphic.Skeleton.Data.Animations;
            if (anims != null && anims.Count > 0 && anims.Items[0] != null)
                return anims.Items[0].Name;
            return null;
        }

        private void BuildFallback(RectTransform parent)
        {
            var fallbackRoot = CreateChildRect(
                parent, "VillagerRoleFallback",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                villagerAnchoredPosition, new Vector2(220f, 280f));

            var body = fallbackRoot.gameObject.AddComponent<Image>();
            body.color = new Color(0.30f, 0.55f, 0.85f, 0.95f);

            var labelRt = CreateChildRect(
                fallbackRoot, "Label",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(180f, 64f));
            var label = labelRt.gameObject.AddComponent<Text>();
            label.text = "Role";
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.fontSize = 42;
            label.font = FarmGridView.LoadBuiltinFont();
            label.raycastTarget = false;
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

        private static int CountMissingMonoScripts(GameObject go)
        {
            var components = go.GetComponentsInChildren<MonoBehaviour>(true);
            var missing = 0;
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                    missing++;
            }

            return missing;
        }
    }
}
