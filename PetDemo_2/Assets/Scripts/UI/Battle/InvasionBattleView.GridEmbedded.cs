// SPEC §12.14.9 / §12.14.16 阶段 4：多单位阵型战嵌入 TopArea（GridBattleField + 按 turnQueue 行动动画）。
using System.Collections;
using System.Collections.Generic;
using PetDemo.Battle;
using PetDemo.UI;
using PetDemo.UI.Farm;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;
namespace PetDemo.UI.Battle
{
    public partial class InvasionBattleView
    {
        private const float GridCharacterScale = 0.30f * GridBattleConstants.BattleSpineDisplayScaleMultiplier;
        private static readonly Vector2 GridCharacterSize = new Vector2(360f, 600f);
        private static readonly Vector2 GridHpBarSize = new Vector2(140f, 16f);
        private const float GridHpBarOffsetY = -GridBattleConstants.GridHpBarOffsetBelowSpineCenterPx;

        private bool useGridBattle;
        private GridBattleSession gridSession;
        private GridBattleDriver gridDriver;
        private GridBattleTargetSelector gridTargetSelector;
        private GridBattleFieldLayout gridFieldLayout;
        private readonly Dictionary<string, GridUnitVisual> gridUnitVisuals =
            new Dictionary<string, GridUnitVisual>();

        private sealed class GridUnitVisual
        {
            public BattleUnitRuntime unit;
            public RectTransform slotRt;
            public RectTransform unitRt;
            public SkeletonGraphic skeleton;
            public Image hpFill;
            public Text hpText;
            public Vector2 homeAnchoredPos;
        }

        /// <summary>
        /// SPEC §12.14.9：嵌入九宫格多单位战——<paramref name="hostRect"/> 下实例化 GridBattleField，
        /// 按 <see cref="IGridBattleDriver"/> 驱动规则、依次播放行动动画，结算复用 EmbeddedResultOverlay。
        /// </summary>
        public static InvasionBattleView BuildEmbeddedGrid(
            RectTransform hostRect,
            GridBattleSession session,
            System.Action<bool> onEnded,
            RectTransform resultOverlayHost = null)
        {
            if (hostRect == null || session == null)
                return null;

            var modalRt = CreateChildRect(
                hostRect, "EmbeddedBattle",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            StretchFull(modalRt);

            var fieldPrefab = Resources.Load<GameObject>(GridBattleConstants.GridBattleFieldPrefabPath);
            if (fieldPrefab == null)
            {
                UnityEngine.Debug.LogError(
                    "[InvasionBattleView] 缺少 GridBattleField 预制体 Resources/"
                    + GridBattleConstants.GridBattleFieldPrefabPath
                    + "，请执行 Tools/PetDemo/Generate Grid Battle Field Prefab。");
                Destroy(modalRt.gameObject);
                return null;
            }

            var fieldGo = Instantiate(fieldPrefab, modalRt, false);
            fieldGo.name = "GridBattleField";
            var fieldRt = fieldGo.GetComponent<RectTransform>();
            if (fieldRt != null)
            {
                StretchFull(fieldRt);
                // SPEC §12.14.9 (v3.224)：Top=200（offsetMax.y=-200）。
                fieldRt.offsetMax = new Vector2(
                    fieldRt.offsetMax.x, -GridBattleConstants.GridBattleFieldTopInsetPx);
            }

            var layout = fieldGo.GetComponent<GridBattleFieldLayout>();
            if (layout == null)
            {
                UnityEngine.Debug.LogError("[InvasionBattleView] GridBattleField 缺少 GridBattleFieldLayout。");
                Destroy(modalRt.gameObject);
                return null;
            }
            layout.RebuildSlotIndex();

            var view = modalRt.gameObject.AddComponent<InvasionBattleView>();
            view.embedded = true;
            view.useGridBattle = true;
            view.modalRt = modalRt;
            view.onEmbeddedEnded = onEnded;
            view.gridSession = session;
            view.gridDriver = new GridBattleDriver(session);
            view.gridTargetSelector = new GridBattleTargetSelector();
            view.gridFieldLayout = layout;
            view.modalCanvasGroup = modalRt.gameObject.AddComponent<CanvasGroup>();
            view.modalCanvasGroup.alpha = 1f;
            view.modalCanvasGroup.interactable = true;
            view.modalCanvasGroup.blocksRaycasts = true;

            view.BuildGridUnitVisuals();
            if (resultOverlayHost != null)
                view.InstantiateEmbeddedResultOverlay(resultOverlayHost);
            else
                view.InstantiateResultDialog(modalRt);

            view.StartGridEmbeddedBattle();
            return view;
        }

        private void StartGridEmbeddedBattle()
        {
            if (modalRt == null || gridSession == null || gridDriver == null)
                return;

            modalRt.gameObject.SetActive(true);
            if (embeddedResultOverlayRt != null)
                embeddedResultOverlayRt.gameObject.SetActive(false);
            else if (resultDialogRt != null)
                resultDialogRt.gameObject.SetActive(false);

            RefreshAllGridHpBars();

            if (battleLoop != null)
                StopCoroutine(battleLoop);
            battleLoop = StartCoroutine(RunGridBattleLoop());
        }

        private void BuildGridUnitVisuals()
        {
            gridUnitVisuals.Clear();
            if (gridSession == null || gridFieldLayout == null)
                return;

            BuildSideVisuals(gridSession.allies);
            BuildSideVisuals(gridSession.enemies);
        }

        private void BuildSideVisuals(List<BattleUnitRuntime> units)
        {
            if (units == null)
                return;
            for (int i = 0; i < units.Count; i++)
                TryBuildGridUnitVisual(units[i]);
        }

        private void TryBuildGridUnitVisual(BattleUnitRuntime unit)
        {
            if (unit == null || string.IsNullOrEmpty(unit.instanceId))
                return;
            if (gridUnitVisuals.ContainsKey(unit.instanceId))
                return;

            var slotRt = gridFieldLayout.GetSlot(unit.side, unit.gridPos.row, unit.gridPos.col);
            if (slotRt == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[InvasionBattleView] 未找到槽位 side=" + unit.side
                    + " r" + unit.gridPos.row + "c" + unit.gridPos.col);
                return;
            }

            var unitRt = CreateChildRect(
                slotRt, "UnitAnchor",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, GridCharacterSize);

            bool isAlly = unit.side == BattleSide.Ally;
            unitRt.localScale = isAlly
                ? new Vector3(-GridCharacterScale, GridCharacterScale, 1f)
                : FantaziaMonsterDisplay.BoostedMirroredUniform(GridCharacterScale);

            // SPEC §12.14.9 (v3.223)：先挂嵌套 Canvas（含 Spine 通道与相对 sortingOrder），再构建 SkeletonGraphic。
            ApplyGridUnitDepthSorting(unitRt, unit.gridPos.row, unit.gridPos.col);

            string prefabPath = string.IsNullOrEmpty(unit.skeletonPrefab)
                ? (isAlly ? ResPlayerPrefab : ResEnemyPrefab)
                : unit.skeletonPrefab;
            var skeleton = TryBuildSkeletonGraphic(prefabPath, unitRt);

            var hpFill = BuildGridHpBar(slotRt, out var hpText);

            var visual = new GridUnitVisual
            {
                unit = unit,
                slotRt = slotRt,
                unitRt = unitRt,
                skeleton = skeleton,
                hpFill = hpFill,
                hpText = hpText,
                homeAnchoredPos = Vector2.zero,
            };
            gridUnitVisuals[unit.instanceId] = visual;
            UpdateGridUnitHp(visual);
        }

        /// <summary>
        /// SPEC §12.14.9 (v3.222 / v3.223)：按 Slot_r3 &gt; r2 &gt; r1 设置单位 Spine 绘制层级；
        /// sortingOrder 相对父 Canvas；补齐 Spine additionalShaderChannels 与 GraphicRaycaster。
        /// </summary>
        private static void ApplyGridUnitDepthSorting(RectTransform unitRt, int row, int col)
        {
            if (unitRt == null)
                return;

            int parentOrder = 0;
            AdditionalCanvasShaderChannels parentChannels = AdditionalCanvasShaderChannels.None;
            var ancestor = unitRt.parent;
            while (ancestor != null)
            {
                var parentCanvas = ancestor.GetComponent<Canvas>();
                if (parentCanvas != null)
                {
                    parentOrder = parentCanvas.sortingOrder;
                    parentChannels = parentCanvas.additionalShaderChannels;
                    break;
                }
                ancestor = ancestor.parent;
            }

            var canvas = unitRt.GetComponent<Canvas>();
            if (canvas == null)
                canvas = unitRt.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = parentOrder
                + GridBattleConstants.ComputeSlotDepthSortingOrder(row, col);
            canvas.additionalShaderChannels = parentChannels
                | AdditionalCanvasShaderChannels.TexCoord1
                | AdditionalCanvasShaderChannels.Normal
                | AdditionalCanvasShaderChannels.Tangent;

            MainHudLayerRoot.EnsureGraphicRaycaster(unitRt.gameObject);
        }

        private static Image BuildGridHpBar(RectTransform parent, out Text labelOut)
        {
            var bg = CreateChildRect(
                parent, "HpBar",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, GridHpBarOffsetY), GridHpBarSize);
            var bgImage = bg.gameObject.AddComponent<Image>();
            bgImage.sprite = GetFallbackWhiteSprite();
            bgImage.color = new Color(0.247f, 0.247f, 0.247f, 0.78f);
            bgImage.raycastTarget = false;

            var fillRt = CreateChildRect(bg, "Fill",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            StretchFull(fillRt);
            var fillImage = fillRt.gameObject.AddComponent<Image>();
            fillImage.sprite = GetFallbackWhiteSprite();
            fillImage.color = new Color(0.878f, 0.282f, 0.282f, 1f);
            fillImage.raycastTarget = false;

            var labelRt = CreateChildRect(bg, "HpText",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            StretchFull(labelRt);
            var label = labelRt.gameObject.AddComponent<Text>();
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.fontSize = 14;
            label.font = FarmGridView.LoadBuiltinFont();
            if (label.font == null)
                label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.raycastTarget = false;
            label.text = "";
            labelOut = label;
            return fillImage;
        }

        private IEnumerator RunGridBattleLoop()
        {
            yield return new WaitForSeconds(0.30f);
            gridDriver.BeginRound();

            const int maxSafetyTurns = 10000;
            int safety = maxSafetyTurns;

            while (!gridSession.finished && safety-- > 0)
            {
                var actor = gridDriver.GetCurrentActor();
                if (actor == null)
                {
                    if (!gridDriver.AdvanceTurn())
                        gridDriver.BeginRound();
                    continue;
                }

                var opponents = actor.side == BattleSide.Ally
                    ? gridSession.enemies
                    : (IReadOnlyList<BattleUnitRuntime>)gridSession.allies;

                var target = gridTargetSelector.PickNormalAttackTarget(
                    actor, opponents, gridSession.roundIndex, gridSession.battleSeed);
                if (target == null)
                {
                    if (!gridDriver.AdvanceTurn())
                        gridDriver.BeginRound();
                    continue;
                }

                yield return RunGridAttackTurn(actor, target);

                if (gridSession.finished)
                {
                    yield return PlayGridResultAnimations(gridSession.playerWon);
                    yield return ShowEmbeddedResultDialog(gridSession.playerWon);
                    yield break;
                }

                if (!gridDriver.AdvanceTurn())
                    gridDriver.BeginRound();
            }
        }

        private IEnumerator RunGridAttackTurn(BattleUnitRuntime attacker, BattleUnitRuntime defender)
        {
            if (!gridUnitVisuals.TryGetValue(attacker.instanceId, out var attackerVisual))
                yield break;
            if (!gridUnitVisuals.TryGetValue(defender.instanceId, out var defenderVisual))
                yield break;

            Vector2 approachPos = ComputeAttackApproachAnchoredPos(attackerVisual, defenderVisual);
            yield return MoveUnitAnchored(attackerVisual.unitRt, approachPos, MoveDuration);
            yield return PlayAnimationOnceAndWait(attackerVisual.skeleton, AttackAnimName, AnimationWaitTimeout);

            int damage = new GridBattleResolver().ResolveNormalAttackDamage(attacker, defender);
            gridDriver.ApplyNormalAttack(attacker, defender);

            UpdateGridUnitHp(defenderVisual);
            if (!GridBattleLiving.IsLiving(defender))
                SetGridUnitDeadVisual(defenderVisual, dead: true);

            SpawnGridDamageFloat(defenderVisual, damage);

            yield return MoveUnitAnchored(attackerVisual.unitRt, attackerVisual.homeAnchoredPos, MoveDuration);
            TryPlayFirstLoopAnimation(attackerVisual.skeleton);
        }

        private Vector2 ComputeAttackApproachAnchoredPos(GridUnitVisual attacker, GridUnitVisual defender)
        {
            if (attacker?.slotRt == null || defender?.slotRt == null || gridFieldLayout == null)
                return attacker != null ? attacker.homeAnchoredPos : Vector2.zero;

            var fieldRt = gridFieldLayout.transform as RectTransform;
            if (fieldRt == null)
                return attacker.homeAnchoredPos;

            Vector3 targetWorld = defender.slotRt.TransformPoint(Vector3.zero);
            Vector3 attackerWorld = attacker.slotRt.TransformPoint(Vector3.zero);
            Vector3 targetInField = fieldRt.InverseTransformPoint(targetWorld);
            Vector3 attackerInField = fieldRt.InverseTransformPoint(attackerWorld);

            float approachX = (attackerInField.x + targetInField.x) * 0.5f;
            var approachInField = new Vector3(approachX, targetInField.y, 0f);
            Vector3 approachWorld = fieldRt.TransformPoint(approachInField);
            Vector3 approachInSlot = attacker.slotRt.InverseTransformPoint(approachWorld);
            return new Vector2(approachInSlot.x, approachInSlot.y);
        }

        private static IEnumerator MoveUnitAnchored(RectTransform unitRt, Vector2 target, float duration)
        {
            if (unitRt == null)
                yield break;
            if (duration <= 0f)
            {
                unitRt.anchoredPosition = target;
                yield break;
            }

            Vector2 start = unitRt.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                unitRt.anchoredPosition = Vector2.Lerp(start, target, t);
                yield return null;
            }
            unitRt.anchoredPosition = target;
        }

        private void SpawnGridDamageFloat(GridUnitVisual defenderVisual, int damage)
        {
            if (modalRt == null || defenderVisual?.unitRt == null || damage <= 0)
                return;

            Vector2 startPos = defenderVisual.unitRt.anchoredPosition;
            var floatRt = CreateChildRect(
                defenderVisual.slotRt, "DamageFloat",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                startPos + new Vector2(0f, 40f), new Vector2(180f, 48f));
            floatRt.SetAsLastSibling();

            var label = floatRt.gameObject.AddComponent<Text>();
            label.text = "-" + damage;
            label.fontSize = DamageFloatFontSize;
            label.color = DamageFloatColor;
            label.alignment = TextAnchor.MiddleCenter;
            label.font = FarmGridView.LoadBuiltinFont();
            if (label.font == null)
                label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            StartCoroutine(AnimateGridDamageFloat(floatRt, floatRt.anchoredPosition));
        }

        private IEnumerator AnimateGridDamageFloat(RectTransform floatRt, Vector2 startPos)
        {
            if (floatRt == null)
                yield break;

            Vector2 endPos = startPos + new Vector2(0f, DamageFloatRiseDistance * 0.6f);
            float elapsed = 0f;
            while (elapsed < DamageFloatDuration)
            {
                if (floatRt == null)
                    yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / DamageFloatDuration);
                floatRt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                yield return null;
            }

            if (floatRt != null)
                Destroy(floatRt.gameObject);
        }

        private void RefreshAllGridHpBars()
        {
            foreach (var kv in gridUnitVisuals)
                UpdateGridUnitHp(kv.Value);
        }

        private static void UpdateGridUnitHp(GridUnitVisual visual)
        {
            if (visual?.unit?.stats == null)
                return;
            int hp = visual.unit.stats.currentHp;
            int maxHp = visual.unit.stats.maxHp;
            SetHpFill(visual.hpFill, hp, maxHp);
            if (visual.hpText != null)
                visual.hpText.text = hp + "/" + maxHp;
        }

        private static void SetGridUnitDeadVisual(GridUnitVisual visual, bool dead)
        {
            if (visual?.unitRt == null)
                return;
            float alpha = dead ? 0.35f : 1f;
            var cg = visual.unitRt.GetComponent<CanvasGroup>();
            if (cg == null && dead)
                cg = visual.unitRt.gameObject.AddComponent<CanvasGroup>();
            if (cg != null)
                cg.alpha = alpha;
        }

        private IEnumerator PlayGridResultAnimations(bool playerWon)
        {
            if (!playerWon)
                yield break;

            TrackEntry roleWinEntry = null;
            var deathEntries = new List<TrackEntry>();

            foreach (var kv in gridUnitVisuals)
            {
                var visual = kv.Value;
                if (visual?.unit == null || visual.skeleton == null)
                    continue;

                if (visual.unit.side == BattleSide.Ally && visual.unit.kind == BattleUnitKind.Role)
                {
                    if (GridBattleLiving.IsLiving(visual.unit))
                    {
                        var anim = FindAnimationByName(visual.skeleton, PlayerWinAnimName);
                        if (anim != null)
                            roleWinEntry = visual.skeleton.AnimationState.SetAnimation(0, anim.Name, false);
                    }
                }
                else if (visual.unit.side == BattleSide.Enemy && !GridBattleLiving.IsLiving(visual.unit))
                {
                    var anim = FindAnimationFirstMatch(
                        visual.skeleton, EnemyDeathAnimName, EnemyDeathAnimFallbacks);
                    if (anim != null)
                    {
                        var entry = visual.skeleton.AnimationState.SetAnimation(0, anim.Name, false);
                        if (entry != null)
                            deathEntries.Add(entry);
                    }
                }
            }

            float elapsed = 0f;
            float timeout = Mathf.Max(0.1f, AnimationWaitTimeout);
            while (elapsed < timeout)
            {
                bool roleDone = roleWinEntry == null || roleWinEntry.IsComplete;
                bool allDeathDone = true;
                for (int i = 0; i < deathEntries.Count; i++)
                {
                    if (deathEntries[i] != null && !deathEntries[i].IsComplete)
                    {
                        allDeathDone = false;
                        break;
                    }
                }
                if (roleDone && allDeathDone)
                    yield break;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator ShowEmbeddedResultDialog(bool playerWon)
        {
            if (resultDialogView == null || resultDialogRt == null)
            {
                onEmbeddedEnded?.Invoke(playerWon);
                yield break;
            }

            resultDialogView.SetTitle(playerWon ? "胜利！" : "失败...");
            resultDialogView.RebuildRewards(playerWon, null);
            resultDialogView.SetAutoAdvanceRowVisible(false);

            if (embeddedResultOverlayRt != null)
            {
                embeddedResultOverlayRt.gameObject.SetActive(true);
                embeddedResultOverlayRt.SetAsLastSibling();
            }
            else
            {
                resultDialogRt.gameObject.SetActive(true);
                resultDialogRt.SetAsLastSibling();
            }

            bool embeddedClicked = false;
            var embeddedBtn = resultDialogView.ClosePanelButton;
            if (embeddedBtn != null)
            {
                embeddedBtn.onClick.RemoveAllListeners();
                embeddedBtn.onClick.AddListener(() => embeddedClicked = true);
            }
            else
            {
                embeddedClicked = true;
            }

            while (!embeddedClicked)
                yield return null;

            if (embeddedResultOverlayRt != null)
                embeddedResultOverlayRt.gameObject.SetActive(false);
            else
                resultDialogRt.gameObject.SetActive(false);

            onEmbeddedEnded?.Invoke(playerWon);
        }
    }
}
