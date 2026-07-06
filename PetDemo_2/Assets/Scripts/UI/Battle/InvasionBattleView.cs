// SPEC §12.3 / §12.4 / §12.9 / §12.9.1：怪物入侵全屏战斗界面（含开战体力、自动推进关卡与返回家园最小化）。
// SPEC §13.4：好友家园驱赶战敌方同玩家模型（右侧镜像）且结算禁用自动推进关卡。
// SPEC §12.3 (v3.93)：命中扣血时在受击角色中心播放红色伤害飘字（`-{damage}`，上飘）。
// SPEC §4.1.12 (v3.70)：上场精灵显示于玩家左上下槽，偶数我方行动轮参与攻击。
// 职责：
//   1) 在主 Canvas 内构建可显隐的全屏面板：背景 ZhanDou_1 + 玩家（左，左右翻转）+ 敌人（右）+ 双血条 + 结果弹窗；
//   2) 玩家与敌人通过 SkeletonGraphic 显示，与 §9.5 MainRoleCunminPresenter 同方法；玩家预制体为 Resources `Hero_Role_cunmin`（v3.48+ 内嵌 LangRen Role_cslangren 骨骼）；敌人为 Resources `Pets/Monster_1_Salamander`；
//   3) 驱动回合循环（我方先 → 敌方 → 我方 → ...），攻击者在 0.25s 内移到画面中心、停 0.15s 施加伤害、再 0.25s 移回；
//   4) 一方 HP 归 0 后弹出结果弹窗，玩家点击关闭后通知 InvasionService.CloseBattle 重启 180s 倒计时。
using System.Collections;
using PetDemo.Battle;
using PetDemo.Core;
using PetDemo.Farm;
using PetDemo.UI;
using PetDemo.UI.Farm;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Battle
{
    [DisallowMultipleComponent]
    public class InvasionBattleView : MonoBehaviour
    {
        public const string ResBattleBackground = "AirUI/ZhanDou_1";
        public const string ResBattleOngoingEntry = "AirUI/ZhanDouZhong";

        private const float BattleOngoingEntryWidth = 280f;
        private const float BattleOngoingEntryHeight = 177f;
        private const float BattleOngoingEntryTopInset = 105f;
        public const string ResPlayerPrefab = "Prefabs/Air/Hero_Role_cunmin";
        public const string ResEnemyPrefab = "Pets/Monster_1_Salamander";
        public const string SkeletonGraphicShaderName = "Spine/SkeletonGraphic";

        // SPEC §12.3：摆位常量。
        private static readonly Vector2 PlayerHomePos = new Vector2(-280f, -120f);
        private static readonly Vector2 EnemyHomePos = new Vector2(280f, -120f);
        private static readonly Vector2 PetLowerLeftHomePos = new Vector2(-430f, -200f);
        private static readonly Vector2 PetUpperLeftHomePos = new Vector2(-430f, -40f);
        private static readonly Vector2 ScreenCenterPos = new Vector2(0f, -120f);
        private static readonly Vector2 CharacterSize = new Vector2(720f, 1200f);
        private static readonly Vector2 PetCharacterSize = new Vector2(480f, 720f);
        private const float CharacterScale = 0.53f;
        private const float PetCharacterScale = 0.40f;
        // SPEC §12.11.10 (v3.172)：嵌入 TopArea 区域比全屏小，整体缩放使角色与血条收进该区域（可按实际显示微调）。
        private const float EmbeddedScale = 0.75f;
        // SPEC §12.11.10.1 (v3.174)：嵌入结算弹窗全屏居中尺寸与遮罩色。
        private static readonly Vector2 EmbeddedResultDialogSize = new Vector2(856f, 883f);
        private static readonly Color EmbeddedResultBackdropColor = new Color(0f, 0f, 0f, 0.72f);
        // SPEC §12.11.10.1 (v3.176)：嵌入结算 ResultDialog 内文本排版（运行时覆写，不影响 §12.3 全屏 prefab）。
        private const float EmbeddedResultTextPosY = 175f;
        private const int EmbeddedResultTextFontSize = 64;
        private const float EmbeddedHintTextPosY = -340f;
        private const int EmbeddedHintTextFontSize = 40;
        private const string EmbeddedResultOverlayName = "EmbeddedResultOverlay";
        private const string EmbeddedResultBackdropName = "DimBackdrop";
        private const string PetAttackAnimName = "attack";
        private static readonly string[] PetAttackAnimFallbacks = { "Attack", "attack_1" };

        // SPEC §12.4：回合时序常量。
        private const float MoveDuration = 0.25f;
        private const float AnimationWaitTimeout = 2.5f;
        private const int DamageFloatFontSize = 36;
        private const float DamageFloatDuration = 0.9f;
        private const float DamageFloatRiseDistance = 100f;
        private static readonly Color DamageFloatColor = new Color(0.878f, 0.282f, 0.282f, 1f);
        private const string AttackAnimName = "attack_1";
        private const string PlayerWinAnimName = "exclusive_2";
        private const string EnemyDeathAnimName = "death";
        // Fantazia 等第三方骨骼常用 Attack / Dead 命名，与 Boss_langren 的 attack_1 / death 并存。
        private static readonly string[] AttackAnimFallbacks = { "Attack" };
        private static readonly string[] EnemyDeathAnimFallbacks = { "Dead" };

        private InvasionService service;
        private RectTransform modalRt;
        private RectTransform playerRt;
        private RectTransform enemyRt;
        private SkeletonGraphic playerSkeleton;
        private SkeletonGraphic enemySkeleton;
        private RectTransform petLowerLeftRt;
        private RectTransform petUpperLeftRt;
        private SkeletonGraphic petLowerLeftSkeleton;
        private SkeletonGraphic petUpperLeftSkeleton;
        private int playerTurnCounter;
        private Image playerHpFill;
        private Image enemyHpFill;
        private Text playerHpText;
        private Text enemyHpText;
        private InvasionBattleResultDialogView resultDialogView;
        private RectTransform resultDialogRt;
        private Toggle autoAdvanceWinToggle;
        private Coroutine battleLoop;
        private RectTransform countdownHostRt;
        private Text countdownText;
        private RectTransform inBattleAutoRowRt;
        private Toggle inBattleAutoToggle;
        private RectTransform returnHomeRowRt;
        private Button returnHomeButton;
        private RectTransform battleOngoingEntryRt;
        private CanvasGroup modalCanvasGroup;
        private BottomNavBarView bottomNav;
        private JiaYuanWorldScreenView jiaYuanWorld;
        private bool autoAdvanceLevels;
        private bool battleModalMinimized;
        /// <summary>§12.9.1：自动连战开下一场时是否保留「返回家园」最小化（不自动弹回战斗全屏）。</summary>
        private bool preserveBattleModalMinimized;
        private bool suppressAutoToggleSync;
        private static Sprite fallbackWhiteSprite;

        // SPEC §12.11.10 (v3.172)：嵌入模式——由 InvasionBattleModal_2 复用本战斗模拟，
        // 本地驱动、无 InvasionService 副作用、关闭背景图、结算走 onEmbeddedEnded 回调。
        private bool embedded;
        private IBattleCombatDriver combatDriver;
        private System.Action<bool> onEmbeddedEnded;
        private string embeddedEnemyPrefab;
        private RectTransform embeddedResultOverlayRt;

        public static InvasionBattleView BuildInto(
            RectTransform canvasRect,
            InvasionService svc,
            BottomNavBarView navBar = null,
            JiaYuanWorldScreenView jiaYuan = null)
        {
            if (canvasRect == null || svc == null)
                return null;

            var modalRt = CreateChildRect(
                canvasRect, "InvasionBattleModal",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            StretchFull(modalRt);
            modalRt.gameObject.SetActive(false);

            var bgRt = CreateChildRect(
                modalRt, "BattleBackground",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            StretchFull(bgRt);
            var bgImage = bgRt.gameObject.AddComponent<Image>();
            var bgSprite = Resources.Load<Sprite>(ResBattleBackground);
            if (bgSprite != null)
            {
                bgImage.sprite = bgSprite;
                bgImage.preserveAspect = false;
            }
            else
            {
                bgImage.color = new Color(0.06f, 0.05f, 0.10f, 1f);
                UnityEngine.Debug.LogWarning("[InvasionBattleView] 缺少战斗背景资源 ZhanDou_1，回退为深色背景。");
            }
            bgImage.raycastTarget = true;

            var view = modalRt.gameObject.AddComponent<InvasionBattleView>();
            view.service = svc;
            view.combatDriver = svc;
            view.modalRt = modalRt;
            view.bottomNav = navBar;
            view.jiaYuanWorld = jiaYuan;
            view.modalCanvasGroup = modalRt.gameObject.AddComponent<CanvasGroup>();
            view.modalCanvasGroup.alpha = 1f;
            view.modalCanvasGroup.interactable = true;
            view.modalCanvasGroup.blocksRaycasts = true;

            view.BuildPlayerSlot(modalRt);
            view.BuildEnemySlot(modalRt);
            view.BuildHpBars(modalRt);
            view.InstantiateResultDialog(modalRt);
            view.BuildAutoAdvanceCountdownHost(modalRt);
            view.BuildInBattleAutoAdvanceRow(modalRt);
            view.BuildReturnHomeButton(modalRt);
            view.BuildBattleOngoingEntry(jiaYuan);

            view.SubscribeEvents();
            return view;
        }

        /// <summary>
        /// SPEC §12.11.10 (v3.172)：嵌入模式工厂——把完整战斗模拟渲染进 <paramref name="hostRect"/>
        /// （InvasionBattleModal_2 的 TopArea），关闭战斗背景图，用本地 <see cref="LocalBattleCombatDriver"/>
        /// 驱动（不经 InvasionService），结算通过 <paramref name="onEnded"/> 回调（true=玩家胜）。
        /// </summary>
        public static InvasionBattleView BuildEmbedded(
            RectTransform hostRect,
            BattleSession session,
            string enemyPrefab,
            System.Action<bool> onEnded,
            RectTransform resultOverlayHost = null)
        {
            if (hostRect == null || session == null)
                return null;

            var modalRt = CreateChildRect(
                hostRect, "EmbeddedBattle",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            StretchFull(modalRt);
            // 整体缩放使全屏摆位的角色/血条收进较小的 TopArea 区域。
            modalRt.localScale = new Vector3(EmbeddedScale, EmbeddedScale, 1f);

            // 嵌入模式：不创建 BattleBackground（关闭战斗背景图 ZhanDou_1），透出 Modal_2 自身背景。
            var view = modalRt.gameObject.AddComponent<InvasionBattleView>();
            view.embedded = true;
            view.modalRt = modalRt;
            view.onEmbeddedEnded = onEnded;
            view.combatDriver = new LocalBattleCombatDriver(session);
            view.embeddedEnemyPrefab = enemyPrefab;
            view.modalCanvasGroup = modalRt.gameObject.AddComponent<CanvasGroup>();
            view.modalCanvasGroup.alpha = 1f;
            view.modalCanvasGroup.interactable = true;
            view.modalCanvasGroup.blocksRaycasts = true;

            view.BuildPlayerSlot(modalRt);
            view.BuildEnemySlot(modalRt);
            view.BuildHpBars(modalRt);
            if (resultOverlayHost != null)
                view.InstantiateEmbeddedResultOverlay(resultOverlayHost);
            else
                view.InstantiateResultDialog(modalRt);

            view.StartEmbeddedBattle();
            return view;
        }

        private void StartEmbeddedBattle()
        {
            if (modalRt == null)
                return;
            modalRt.gameObject.SetActive(true);
            if (embeddedResultOverlayRt != null)
                embeddedResultOverlayRt.gameObject.SetActive(false);
            else if (resultDialogRt != null)
                resultDialogRt.gameObject.SetActive(false);

            playerTurnCounter = 0;
            ResetCharacterPositions();
            ResetCharacterAnimations();
            var sess = combatDriver != null ? combatDriver.GetBattleSession() : null;
            if (sess != null)
                UpdateHpDisplay(sess);

            if (battleLoop != null)
                StopCoroutine(battleLoop);
            battleLoop = StartCoroutine(RunBattleLoop());
        }

        private void SubscribeEvents()
        {
            if (service == null)
                return;
            service.OnPhaseChanged += OnPhaseChanged;
            service.OnBattleHpChanged += OnBattleHpChanged;
        }

        private void OnDestroy()
        {
            UnbindAutoAdvanceListeners();
            if (embeddedResultOverlayRt != null)
            {
                Destroy(embeddedResultOverlayRt.gameObject);
                embeddedResultOverlayRt = null;
            }
            if (service == null)
                return;
            service.OnPhaseChanged -= OnPhaseChanged;
            service.OnBattleHpChanged -= OnBattleHpChanged;
        }

        private void OnPhaseChanged(InvasionPhase phase)
        {
            if (phase == InvasionPhase.InBattle)
                OpenBattlePanel();
            else if (phase == InvasionPhase.Countdown)
                CloseBattlePanel();
        }

        private void OnBattleHpChanged(BattleSession sess)
        {
            if (sess == null)
                return;
            UpdateHpDisplay(sess);
        }

        private void OpenBattlePanel()
        {
            if (modalRt == null)
                return;
            modalRt.SetAsLastSibling();
            modalRt.gameObject.SetActive(true);
            if (resultDialogRt != null)
                resultDialogRt.gameObject.SetActive(false);

            playerTurnCounter = 0;
            ResetCharacterPositions();
            RebuildEnemyVisualForCurrentBattle();
            ResetCharacterAnimations();
            RebuildBattlePetSlots();
            var sess = service != null ? service.GetBattleSession() : null;
            if (sess != null)
                UpdateHpDisplay(sess);

            if (battleLoop != null)
                StopCoroutine(battleLoop);
            battleLoop = StartCoroutine(RunBattleLoop());
            SetupInBattleAutoRowForOpen();
            if (battleModalMinimized)
            {
                ApplyBattleModalMinimized(true);
                SetBattleOngoingEntryVisible(true);
            }
        }

        private void CloseBattlePanel()
        {
            UnbindAutoAdvanceListeners();
            bool preserveMinimized = preserveBattleModalMinimized;
            preserveBattleModalMinimized = false;
            if (!preserveMinimized)
            {
                battleModalMinimized = false;
                ApplyBattleModalMinimized(false);
                SetBattleOngoingEntryVisible(false);
            }
            if (battleLoop != null)
            {
                StopCoroutine(battleLoop);
                battleLoop = null;
            }
            if (modalRt != null)
                modalRt.gameObject.SetActive(false);
            ClearDamageFloats();
            ClearBattlePetSlots();
        }

        private IEnumerator RunBattleLoop()
        {
            yield return new WaitForSeconds(0.30f);

            while (true)
            {
                if (combatDriver == null)
                    yield break;
                var sess = combatDriver.GetBattleSession();
                if (sess == null)
                    yield break;

                if (sess.turn == BattleTurn.Result || sess.playerHp <= 0 || sess.enemyHp <= 0)
                {
                    yield return PlayResultAnimations(sess);
                    yield return ShowResultDialog(sess);
                    yield break;
                }

                if (sess.turn == BattleTurn.Player)
                {
                    playerTurnCounter += 1;
                    bool evenPlayerRound = (playerTurnCounter % 2) == 0;
                    int petDamage = Mathf.Max(1, sess.playerAttack / 2);

                    if (evenPlayerRound)
                    {
                        if (petLowerLeftRt != null && petLowerLeftSkeleton != null)
                        {
                            yield return RunSingleTurn(
                                petLowerLeftRt, PetLowerLeftHomePos, isPlayerAttacking: true, petDamage,
                                petLowerLeftSkeleton);
                            sess = combatDriver.GetBattleSession();
                            if (sess == null) yield break;
                            if (sess.enemyHp <= 0)
                            {
                                yield return PlayResultAnimations(sess);
                                yield return ShowResultDialog(sess);
                                yield break;
                            }
                        }

                        if (petUpperLeftRt != null && petUpperLeftSkeleton != null)
                        {
                            yield return RunSingleTurn(
                                petUpperLeftRt, PetUpperLeftHomePos, isPlayerAttacking: true, petDamage,
                                petUpperLeftSkeleton);
                            sess = combatDriver.GetBattleSession();
                            if (sess == null) yield break;
                            if (sess.enemyHp <= 0)
                            {
                                yield return PlayResultAnimations(sess);
                                yield return ShowResultDialog(sess);
                                yield break;
                            }
                        }
                    }

                    sess = combatDriver.GetBattleSession();
                    if (sess == null) yield break;

                    yield return RunSingleTurn(
                        playerRt, PlayerHomePos, isPlayerAttacking: true, sess.playerAttack, playerSkeleton);
                    sess = combatDriver.GetBattleSession();
                    if (sess == null) yield break;
                    if (sess.enemyHp <= 0)
                    {
                        yield return PlayResultAnimations(sess);
                        yield return ShowResultDialog(sess);
                        yield break;
                    }
                    combatDriver.SetTurn(BattleTurn.Enemy);
                }
                else if (sess.turn == BattleTurn.Enemy)
                {
                    yield return RunSingleTurn(
                        enemyRt, EnemyHomePos, isPlayerAttacking: false, sess.enemyAttack, enemySkeleton);
                    sess = combatDriver.GetBattleSession();
                    if (sess == null) yield break;
                    if (sess.playerHp <= 0)
                    {
                        yield return PlayResultAnimations(sess);
                        yield return ShowResultDialog(sess);
                        yield break;
                    }
                    combatDriver.SetTurn(BattleTurn.Player);
                }
                else
                {
                    yield break;
                }
            }
        }

        private IEnumerator RunSingleTurn(
            RectTransform attackerRt, Vector2 attackerHome, bool isPlayerAttacking, int damage,
            SkeletonGraphic attackerSkeletonOverride = null)
        {
            if (attackerRt == null)
                yield break;

            yield return MoveTo(attackerRt, ScreenCenterPos, MoveDuration);
            var attackerSkeleton = attackerSkeletonOverride
                ?? (isPlayerAttacking ? playerSkeleton : enemySkeleton);
            if (attackerSkeleton == petLowerLeftSkeleton || attackerSkeleton == petUpperLeftSkeleton)
                yield return PlayPetAttackOnceAndWait(attackerSkeleton);
            else
                yield return PlayAnimationOnceAndWait(attackerSkeleton, AttackAnimName, AnimationWaitTimeout);

            if (combatDriver != null)
            {
                if (isPlayerAttacking)
                    combatDriver.ApplyDamageToEnemy(damage);
                else
                    combatDriver.ApplyDamageToPlayer(damage);

                // 嵌入模式无 InvasionService 的 OnBattleHpChanged 事件驱动，需主动刷新血条；
                // 全屏模式此处为幂等重复刷新，无副作用。
                var s = combatDriver.GetBattleSession();
                if (s != null)
                    UpdateHpDisplay(s);
            }

            var defenderRt = isPlayerAttacking ? enemyRt : playerRt;
            SpawnDamageFloat(defenderRt, damage);

            yield return MoveTo(attackerRt, attackerHome, MoveDuration);
            TryPlayFirstLoopAnimation(attackerSkeleton);
        }

        private IEnumerator PlayResultAnimations(BattleSession sess)
        {
            if (sess == null)
                yield break;
            bool playerWon = sess.enemyHp <= 0 && sess.playerHp > 0;
            if (!playerWon)
                yield break;

            // SPEC §12.4（v3.9）：胜利动画与敌方死亡同一帧开播，各一遍；弹窗仍以二者均结束为准。
            yield return PlayPlayerWinAndEnemyDeathTogether();
        }

        /// <summary>
        /// 同一帧触发我方 exclusive_2 与敌方 death（均 loop=false），随后等待较晚结束的一条。
        /// </summary>
        private IEnumerator PlayPlayerWinAndEnemyDeathTogether()
        {
            TrackEntry playerEntry = null;
            TrackEntry enemyEntry = null;

            if (playerSkeleton != null && playerSkeleton.AnimationState != null
                && playerSkeleton.Skeleton != null && playerSkeleton.Skeleton.Data != null)
            {
                var pa = FindAnimationByName(playerSkeleton, PlayerWinAnimName);
                if (pa != null)
                    playerEntry = playerSkeleton.AnimationState.SetAnimation(0, pa.Name, false);
                else
                {
                    UnityEngine.Debug.LogWarning("[InvasionBattleView] 动画不存在，回退为待机：" + PlayerWinAnimName);
                    TryPlayFirstLoopAnimation(playerSkeleton);
                }
            }

            if (enemySkeleton != null && enemySkeleton.AnimationState != null
                && enemySkeleton.Skeleton != null && enemySkeleton.Skeleton.Data != null)
            {
                var ea = FindAnimationFirstMatch(enemySkeleton, EnemyDeathAnimName, EnemyDeathAnimFallbacks);
                if (ea != null)
                    enemyEntry = enemySkeleton.AnimationState.SetAnimation(0, ea.Name, false);
                else
                {
                    UnityEngine.Debug.LogWarning("[InvasionBattleView] 动画不存在，回退为待机：" + EnemyDeathAnimName);
                    TryPlayFirstLoopAnimation(enemySkeleton);
                }
            }

            float elapsed = 0f;
            float timeout = Mathf.Max(0.1f, AnimationWaitTimeout);
            while (elapsed < timeout)
            {
                bool playerDone = playerEntry == null || playerEntry.IsComplete;
                bool enemyDone = enemyEntry == null || enemyEntry.IsComplete;
                if (playerDone && enemyDone)
                    yield break;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private static IEnumerator MoveTo(RectTransform rt, Vector2 target, float duration)
        {
            if (rt == null || duration <= 0f)
            {
                if (rt != null) rt.anchoredPosition = target;
                yield break;
            }
            Vector2 start = rt.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rt.anchoredPosition = Vector2.Lerp(start, target, t);
                yield return null;
            }
            rt.anchoredPosition = target;
        }

        private void SpawnDamageFloat(RectTransform defenderSlot, int damage)
        {
            if (modalRt == null || defenderSlot == null || damage <= 0)
                return;

            var startPos = defenderSlot.anchoredPosition;
            var floatRt = CreateChildRect(
                modalRt, "DamageFloat",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                startPos, new Vector2(240f, 64f));
            floatRt.SetSiblingIndex(defenderSlot.GetSiblingIndex() + 1);

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

            StartCoroutine(AnimateDamageFloat(floatRt, startPos));
        }

        private IEnumerator AnimateDamageFloat(RectTransform floatRt, Vector2 startPos)
        {
            if (floatRt == null)
                yield break;

            Vector2 endPos = startPos + new Vector2(0f, DamageFloatRiseDistance);
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

        private void ClearDamageFloats()
        {
            if (modalRt == null)
                return;
            for (int i = modalRt.childCount - 1; i >= 0; i--)
            {
                var child = modalRt.GetChild(i);
                if (child != null && child.name == "DamageFloat")
                    Destroy(child.gameObject);
            }
        }

        private IEnumerator ShowResultDialog(BattleSession sess)
        {
            if (resultDialogView == null || resultDialogRt == null || sess == null)
                yield break;

            bool playerWon = sess.enemyHp <= 0 && sess.playerHp > 0;

            // SPEC §12.11.10：嵌入模式——简化结算：展示胜/负弹窗（无战利品、无自动连战），
            // 玩家点关闭后回调 onEmbeddedEnded，交由 InvasionBattleModal_2 处理胜负流转。
            if (embedded)
            {
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
                yield break;
            }

            bool isFriendHomeBattle = service != null && service.EnteredBattleViaFriendHome;
            resultDialogView.SetTitle(playerWon ? "胜利！" : "失败...");
            resultDialogView.RebuildRewards(playerWon, service);

            if (!playerWon || isFriendHomeBattle)
                autoAdvanceLevels = false;

            if (autoAdvanceWinToggle != null)
            {
                resultDialogView.SetAutoAdvanceRowVisible(playerWon && !isFriendHomeBattle);
                if (playerWon && !isFriendHomeBattle)
                {
                    suppressAutoToggleSync = true;
                    try
                    {
                        autoAdvanceWinToggle.isOn = autoAdvanceLevels;
                        if (inBattleAutoToggle != null && inBattleAutoRowRt != null && inBattleAutoRowRt.gameObject.activeSelf)
                            inBattleAutoToggle.isOn = autoAdvanceLevels;
                    }
                    finally
                    {
                        suppressAutoToggleSync = false;
                    }
                }
            }

            if (countdownHostRt != null)
                countdownHostRt.gameObject.SetActive(false);

            resultDialogRt.gameObject.SetActive(true);
            resultDialogRt.SetAsLastSibling();
            if (countdownHostRt != null)
            {
                countdownHostRt.SetAsLastSibling();
                countdownHostRt.gameObject.SetActive(false);
            }

            if (playerWon && !isFriendHomeBattle)
                BindAutoAdvanceListeners();

            bool clicked = false;
            void OnClick() { clicked = true; }
            var btn = resultDialogView.ClosePanelButton;
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OnClick);
            }
            else
            {
                clicked = true;
            }

            bool countdownChain = false;
            float cdLeft = 0f;
            bool hadAutoWindow = false;

            if (playerWon && !isFriendHomeBattle)
            {
                while (!clicked)
                {
                    if (autoAdvanceLevels)
                    {
                        if (!hadAutoWindow)
                        {
                            cdLeft = 3f;
                            hadAutoWindow = true;
                        }
                        cdLeft -= Time.deltaTime;
                        if (countdownHostRt != null && countdownText != null)
                        {
                            countdownHostRt.gameObject.SetActive(true);
                            int show = Mathf.CeilToInt(Mathf.Max(0f, cdLeft));
                            countdownText.text = show + " 秒后自动开战";
                        }
                        if (cdLeft <= 0f && autoAdvanceLevels)
                        {
                            countdownChain = true;
                            break;
                        }
                    }
                    else
                    {
                        hadAutoWindow = false;
                        if (countdownHostRt != null)
                            countdownHostRt.gameObject.SetActive(false);
                    }
                    yield return null;
                }
            }
            else
            {
                while (!clicked)
                    yield return null;
            }

            UnbindAutoAdvanceListeners();
            resultDialogRt.gameObject.SetActive(false);
            if (countdownHostRt != null)
                countdownHostRt.gameObject.SetActive(false);

            if (playerWon && countdownChain && autoAdvanceLevels && service != null)
            {
                preserveBattleModalMinimized = battleModalMinimized;
                service.CloseBattle(true);
                if (!service.TryOpenBattleFromAutoChain())
                {
                    ResetBattleModalMinimizedUi();
                    ClearAutoAdvanceLevelsUi();
                }
                yield break;
            }

            // SPEC §13.4：好友家园「驱赶」战斗关闭后不弹 §12.10 主角升级弹窗（标记在 CloseBattle 内复位，先读取）。
            if (service != null)
                service.CloseBattle(playerWon);
            if (!playerWon)
                autoAdvanceLevels = false;

            if (!isFriendHomeBattle)
                ProtagonistLevelUpDialogView.RequestShowAfterBattleClose();
        }

        private void UpdateHpDisplay(BattleSession sess)
        {
            SetHpFill(playerHpFill, sess.playerHp, sess.playerMaxHp);
            SetHpFill(enemyHpFill, sess.enemyHp, sess.enemyMaxHp);
            if (playerHpText != null)
                playerHpText.text = sess.playerHp + "/" + sess.playerMaxHp;
            if (enemyHpText != null)
                enemyHpText.text = sess.enemyHp + "/" + sess.enemyMaxHp;
        }

        private static void SetHpFill(Image fillImage, int currentHp, int maxHp)
        {
            if (fillImage == null)
                return;
            // 固定从左向右填充：当 fillAmount 下降时，视觉上从右侧向左端收缩。
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = maxHp > 0
                ? Mathf.Clamp01((float)currentHp / maxHp)
                : 0f;
        }

        private void ResetCharacterPositions()
        {
            if (playerRt != null) playerRt.anchoredPosition = PlayerHomePos;
            if (enemyRt != null) enemyRt.anchoredPosition = EnemyHomePos;
            if (petLowerLeftRt != null) petLowerLeftRt.anchoredPosition = PetLowerLeftHomePos;
            if (petUpperLeftRt != null) petUpperLeftRt.anchoredPosition = PetUpperLeftHomePos;
        }

        private void ResetCharacterAnimations()
        {
            TryPlayFirstLoopAnimation(playerSkeleton);
            TryPlayFirstLoopAnimation(enemySkeleton);
            TryPlayFirstLoopAnimation(petLowerLeftSkeleton);
            TryPlayFirstLoopAnimation(petUpperLeftSkeleton);
        }

        private void RebuildBattlePetSlots()
        {
            ClearBattlePetSlots();
            var planting = PlantingService.Instance;
            if (planting == null || modalRt == null)
                return;

            petLowerLeftRt = TryBuildBattlePetSlot(
                modalRt, planting, PetFieldSlot.LowerLeft, "PetLowerLeftSlot", PetLowerLeftHomePos,
                out petLowerLeftSkeleton);
            petUpperLeftRt = TryBuildBattlePetSlot(
                modalRt, planting, PetFieldSlot.UpperLeft, "PetUpperLeftSlot", PetUpperLeftHomePos,
                out petUpperLeftSkeleton);
            ApplyBattleCharacterLayering();
        }

        /// <summary>
        /// SPEC §12.3（v3.80）：左上上场精灵须在主角身后；左下槽紧随主角之后、敌方之前。
        /// </summary>
        private void ApplyBattleCharacterLayering()
        {
            if (playerRt == null)
                return;

            if (petUpperLeftRt != null)
                petUpperLeftRt.SetSiblingIndex(playerRt.GetSiblingIndex());

            if (petLowerLeftRt != null)
                petLowerLeftRt.SetSiblingIndex(playerRt.GetSiblingIndex() + 1);
        }

        /// <summary>
        /// 与 §9.5.1 / §9.10.5 一致：复用 <see cref="PetBagSpineGraphicBuilder"/> 构建 Fantazia 多材质 Spine。
        /// </summary>
        private static RectTransform TryBuildBattlePetSlot(
            RectTransform parent, IPlantingService planting, PetFieldSlot slot, string slotName,
            Vector2 homePos, out SkeletonGraphic skeletonOut)
        {
            skeletonOut = null;
            if (parent == null)
                return null;

            string configId = planting.GetDeployedPetConfigId(slot);
            if (string.IsNullOrEmpty(configId))
                return null;

            var cfg = planting.GetPetConfig(configId);
            if (cfg == null || string.IsNullOrEmpty(cfg.prefabResource))
            {
                UnityEngine.Debug.LogWarning("[InvasionBattleView] 上场精灵配置或 prefabResource 缺失：" + configId);
                return null;
            }

            var slotRt = CreateChildRect(
                parent, slotName,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                homePos, PetCharacterSize);
            // SPEC §12.3（v3.77）：外层槽 scale=1 仅负责位移；镜像在 PetBattleVisual 子节点（同 PetCompanionPresenter）。
            slotRt.localScale = Vector3.one;

            skeletonOut = PetBagSpineGraphicBuilder.TryBuildBattleDeployedVisual(
                slotRt, cfg, PetCharacterSize, new Vector3(PetCharacterScale, PetCharacterScale, 1f));
            if (skeletonOut == null)
            {
                UnityEngine.Debug.LogWarning("[InvasionBattleView] 上场精灵 Spine 构建失败：" + cfg.id
                    + " slot=" + slot);
                Destroy(slotRt.gameObject);
                return null;
            }

            return slotRt;
        }

        private void ClearBattlePetSlots()
        {
            if (petLowerLeftRt != null)
            {
                Destroy(petLowerLeftRt.gameObject);
                petLowerLeftRt = null;
                petLowerLeftSkeleton = null;
            }

            if (petUpperLeftRt != null)
            {
                Destroy(petUpperLeftRt.gameObject);
                petUpperLeftRt = null;
                petUpperLeftSkeleton = null;
            }
        }

        private IEnumerator PlayPetAttackOnceAndWait(SkeletonGraphic skel)
        {
            if (skel == null || skel.AnimationState == null || skel.Skeleton == null || skel.Skeleton.Data == null)
                yield break;

            var anim = FindAnimationFirstMatch(skel, PetAttackAnimName, PetAttackAnimFallbacks);
            if (anim == null)
            {
                yield return PlayAnimationOnceAndWait(skel, AttackAnimName, AnimationWaitTimeout);
                yield break;
            }

            TrackEntry entry = skel.AnimationState.SetAnimation(0, anim.Name, false);
            if (entry == null)
            {
                TryPlayFirstLoopAnimation(skel);
                yield break;
            }

            float elapsed = 0f;
            while (!entry.IsComplete && elapsed < AnimationWaitTimeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void BuildPlayerSlot(RectTransform parent)
        {
            playerRt = CreateChildRect(
                parent, "PlayerSlot",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                PlayerHomePos, CharacterSize);
            playerRt.localScale = new Vector3(-CharacterScale, CharacterScale, 1f);
            playerSkeleton = TryBuildSkeletonGraphic(ResPlayerPrefab, playerRt);
        }

        private void BuildEnemySlot(RectTransform parent)
        {
            enemyRt = CreateChildRect(
                parent, "EnemySlot",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                EnemyHomePos, CharacterSize);

            // SPEC §12.11.10：嵌入模式按事件单位切换敌人骨骼；用玩家同款模型时右侧镜像朝向（同 §13.4）。
            string enemyPrefabPath = (embedded && !string.IsNullOrEmpty(embeddedEnemyPrefab))
                ? embeddedEnemyPrefab
                : ResEnemyPrefab;
            enemyRt.localScale = enemyPrefabPath == ResPlayerPrefab
                ? new Vector3(CharacterScale, CharacterScale, 1f)
                : FantaziaMonsterDisplay.BoostedMirroredUniform(CharacterScale);
            enemySkeleton = TryBuildSkeletonGraphic(enemyPrefabPath, enemyRt);
        }

        /// <summary>
        /// SPEC §13.4：好友家园驱赶战敌方与玩家同模型（右侧镜像朝向）；普通入侵战恢复 §12.3 Salamander。
        /// </summary>
        private void RebuildEnemyVisualForCurrentBattle()
        {
            if (enemyRt == null)
                return;

            bool friendHome = service != null && service.EnteredBattleViaFriendHome;
            if (friendHome)
                autoAdvanceLevels = false;

            for (int i = enemyRt.childCount - 1; i >= 0; i--)
            {
                var child = enemyRt.GetChild(i);
                if (child != null)
                    Destroy(child.gameObject);
            }

            string prefab = friendHome ? ResPlayerPrefab : ResEnemyPrefab;
            enemyRt.localScale = friendHome
                ? new Vector3(CharacterScale, CharacterScale, 1f)
                : FantaziaMonsterDisplay.BoostedMirroredUniform(CharacterScale);
            enemySkeleton = TryBuildSkeletonGraphic(prefab, enemyRt);
        }

        // SPEC §9.5 / §12.7：与 MainRoleCunminPresenter 一致的 SkeletonGraphic 构建路径——
        // 实例化预制体探针读取 SkeletonDataAsset，再在 Overlay Canvas 下创建 SkeletonGraphic。
        private SkeletonGraphic TryBuildSkeletonGraphic(string resourcesPath, RectTransform parent)
        {
            var prefab = Resources.Load<GameObject>(resourcesPath);
            if (prefab == null)
            {
                UnityEngine.Debug.LogWarning("[InvasionBattleView] 缺少角色预制体：Resources/" + resourcesPath
                    + "。回退为占位色块。");
                BuildFallbackBlock(parent);
                return null;
            }

            var probe = Instantiate(prefab);
            probe.SetActive(false);
            var srcAnim = probe.GetComponent<SkeletonAnimation>()
                ?? probe.GetComponentInChildren<SkeletonAnimation>(true);
            var dataAsset = srcAnim != null ? srcAnim.skeletonDataAsset : null;
            Destroy(probe);

            if (dataAsset == null)
            {
                UnityEngine.Debug.LogWarning("[InvasionBattleView] 预制体 " + resourcesPath
                    + " 未找到 SkeletonDataAsset，回退为占位色块。");
                BuildFallbackBlock(parent);
                return null;
            }

            var shader = Shader.Find(SkeletonGraphicShaderName);
            if (shader == null)
            {
                UnityEngine.Debug.LogError("[InvasionBattleView] 未找到 Shader：" + SkeletonGraphicShaderName);
                BuildFallbackBlock(parent);
                return null;
            }

            var uiMaterial = SkeletonGraphicUiMaterialFactory.CreateForPmaVertexColors(shader);
            var roleGo = new GameObject("Skeleton");
            var rt = roleGo.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = CharacterSize;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;

            var skel = SkeletonGraphic.AddSkeletonGraphicComponent(roleGo, dataAsset, uiMaterial);
            if (skel == null || !skel.IsValid)
            {
                UnityEngine.Debug.LogWarning("[InvasionBattleView] SkeletonGraphic 构建失败，回退为占位色块。");
                Destroy(roleGo);
                BuildFallbackBlock(parent);
                return null;
            }
            skel.raycastTarget = false;
            TryPlayFirstLoopAnimation(skel);
            return skel;
        }

        private static void TryPlayFirstLoopAnimation(SkeletonGraphic skel)
        {
            if (skel == null || skel.Skeleton == null || skel.Skeleton.Data == null)
                return;
            var animations = skel.Skeleton.Data.Animations;
            if (animations == null || animations.Count == 0)
                return;
            // 优先使用 standby/idle 命名的动画，否则取第 0 条循环播放。
            Spine.Animation chosen = null;
            for (int i = 0; i < animations.Count; i++)
            {
                var a = animations.Items[i];
                if (a == null || string.IsNullOrEmpty(a.Name)) continue;
                string n = a.Name.ToLowerInvariant();
                if (n.Contains("standby") || n.Contains("idle") || n.StartsWith("exclusive"))
                {
                    chosen = a;
                    break;
                }
            }
            if (chosen == null)
                chosen = animations.Items[0];
            if (chosen != null)
                skel.AnimationState.SetAnimation(0, chosen, true);
        }

        private static IEnumerator PlayAnimationOnceAndWait(
            SkeletonGraphic skel, string animName, float timeoutSeconds)
        {
            if (skel == null || skel.AnimationState == null || skel.Skeleton == null || skel.Skeleton.Data == null)
                yield break;

            var anim = animName == AttackAnimName
                ? FindAnimationFirstMatch(skel, AttackAnimName, AttackAnimFallbacks)
                : FindAnimationByName(skel, animName);
            if (anim == null)
            {
                UnityEngine.Debug.LogWarning("[InvasionBattleView] 动画不存在，回退为待机：" + animName);
                TryPlayFirstLoopAnimation(skel);
                yield break;
            }

            TrackEntry entry = skel.AnimationState.SetAnimation(0, anim.Name, false);
            if (entry == null)
            {
                TryPlayFirstLoopAnimation(skel);
                yield break;
            }

            float elapsed = 0f;
            while (!entry.IsComplete && elapsed < Mathf.Max(0.1f, timeoutSeconds))
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private static Spine.Animation FindAnimationByName(SkeletonGraphic skel, string animName)
        {
            if (skel == null || skel.Skeleton == null || skel.Skeleton.Data == null || string.IsNullOrEmpty(animName))
                return null;
            var animations = skel.Skeleton.Data.Animations;
            if (animations == null || animations.Count == 0)
                return null;
            for (int i = 0; i < animations.Count; i++)
            {
                var a = animations.Items[i];
                if (a == null || string.IsNullOrEmpty(a.Name))
                    continue;
                if (string.Equals(a.Name, animName, System.StringComparison.OrdinalIgnoreCase))
                    return a;
            }
            return null;
        }

        /// <summary>依次尝试首选名与若干备选名（大小写不敏感精确匹配）。</summary>
        private static Spine.Animation FindAnimationFirstMatch(
            SkeletonGraphic skel, string primaryName, string[] additionalNames)
        {
            var a = FindAnimationByName(skel, primaryName);
            if (a != null)
                return a;
            if (additionalNames == null)
                return null;
            for (int i = 0; i < additionalNames.Length; i++)
            {
                a = FindAnimationByName(skel, additionalNames[i]);
                if (a != null)
                    return a;
            }
            return null;
        }

        private static void BuildFallbackBlock(RectTransform parent)
        {
            var go = new GameObject("FallbackBlock");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(180f, 280f);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.30f, 0.55f, 0.85f, 0.92f);
            img.raycastTarget = false;
        }

        private void BuildHpBars(RectTransform parent)
        {
            playerHpFill = BuildHpBar(parent, "PlayerHpBar", new Vector2(-280f, -460f), out playerHpText);
            enemyHpFill = BuildHpBar(parent, "EnemyHpBar", new Vector2(280f, -460f), out enemyHpText);
        }

        private static Image BuildHpBar(RectTransform parent, string name, Vector2 anchored, out Text labelOut)
        {
            var bg = CreateChildRect(parent, name,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                anchored, new Vector2(240f, 24f));
            var bgImage = bg.gameObject.AddComponent<Image>();
            bgImage.sprite = LoadBuiltinUiSprite();
            bgImage.color = new Color(0.247f, 0.247f, 0.247f, 0.78f);
            bgImage.type = Image.Type.Sliced;
            bgImage.raycastTarget = false;

            var fillRt = CreateChildRect(bg, "Fill",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            StretchFull(fillRt);
            var fillImage = fillRt.gameObject.AddComponent<Image>();
            fillImage.sprite = LoadBuiltinUiSprite();
            fillImage.color = new Color(0.878f, 0.282f, 0.282f, 1f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 1f;
            fillImage.raycastTarget = false;

            var labelRt = CreateChildRect(bg, "HpText",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            StretchFull(labelRt);
            var label = labelRt.gameObject.AddComponent<Text>();
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.fontSize = 22;
            label.font = FarmGridView.LoadBuiltinFont();
            if (label.font == null)
                label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.raycastTarget = false;
            label.text = "";
            labelOut = label;
            return fillImage;
        }

        private static Sprite LoadBuiltinUiSprite()
        {
            var sprite = GetFallbackWhiteSprite();
            return sprite;
        }

        private static Sprite GetFallbackWhiteSprite()
        {
            if (fallbackWhiteSprite != null)
                return fallbackWhiteSprite;
            var tex = Texture2D.whiteTexture;
            if (tex == null)
                return null;
            fallbackWhiteSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
            return fallbackWhiteSprite;
        }


        private void InstantiateResultDialog(RectTransform parent)
        {
            var resPath = InvasionBattleResultDialogView.ResPrefabPath;
            var prefab = Resources.Load<GameObject>(resPath);
            if (prefab == null)
            {
                UnityEngine.Debug.LogError(
                    "[InvasionBattleView] 缺少预制体 Resources/" + resPath
                    + "，请在编辑器执行 Tools/PetDemo/Generate Invasion Battle Result Dialog Prefab。");
                return;
            }

            var go = Instantiate(prefab, parent, false);
            go.name = "ResultDialog";
            resultDialogView = go.GetComponent<InvasionBattleResultDialogView>();
            if (resultDialogView == null)
            {
                UnityEngine.Debug.LogError(
                    "[InvasionBattleView] 预制体缺少 InvasionBattleResultDialogView 组件，请重新执行生成菜单。");
                Destroy(go);
                return;
            }

            resultDialogRt = resultDialogView.RootRt;
            autoAdvanceWinToggle = resultDialogView.AutoAdvanceToggle;
            resultDialogRt.gameObject.SetActive(false);
        }

        /// <summary>
        /// SPEC §12.11.10.1 (v3.174)：于 Modal_2 根节点创建全屏结算遮罩 + 居中放大 ResultDialog。
        /// </summary>
        private void InstantiateEmbeddedResultOverlay(RectTransform host)
        {
            if (host == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[InvasionBattleView] resultOverlayHost 为空，回退为 EmbeddedBattle 内实例化 ResultDialog。");
                InstantiateResultDialog(modalRt);
                return;
            }

            embeddedResultOverlayRt = CreateChildRect(
                host, EmbeddedResultOverlayName,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            StretchFull(embeddedResultOverlayRt);
            embeddedResultOverlayRt.gameObject.SetActive(false);

            var backdropRt = CreateChildRect(
                embeddedResultOverlayRt, EmbeddedResultBackdropName,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            StretchFull(backdropRt);
            var backdropImg = backdropRt.gameObject.AddComponent<Image>();
            backdropImg.color = EmbeddedResultBackdropColor;
            backdropImg.raycastTarget = true;

            var resPath = InvasionBattleResultDialogView.ResPrefabPath;
            var prefab = Resources.Load<GameObject>(resPath);
            if (prefab == null)
            {
                UnityEngine.Debug.LogError(
                    "[InvasionBattleView] 缺少预制体 Resources/" + resPath
                    + "，请在编辑器执行 Tools/PetDemo/Generate Invasion Battle Result Dialog Prefab。");
                return;
            }

            var go = Instantiate(prefab, embeddedResultOverlayRt, false);
            go.name = "ResultDialog";
            resultDialogView = go.GetComponent<InvasionBattleResultDialogView>();
            if (resultDialogView == null)
            {
                UnityEngine.Debug.LogError(
                    "[InvasionBattleView] 预制体缺少 InvasionBattleResultDialogView 组件，请重新执行生成菜单。");
                Destroy(go);
                return;
            }

            resultDialogRt = resultDialogView.RootRt;
            autoAdvanceWinToggle = resultDialogView.AutoAdvanceToggle;
            resultDialogRt.anchorMin = new Vector2(0.5f, 0.5f);
            resultDialogRt.anchorMax = new Vector2(0.5f, 0.5f);
            resultDialogRt.pivot = new Vector2(0.5f, 0.5f);
            resultDialogRt.anchoredPosition = Vector2.zero;
            resultDialogRt.sizeDelta = EmbeddedResultDialogSize;
            resultDialogRt.localScale = Vector3.one;
            ApplyEmbeddedResultDialogTextLayout(resultDialogView);
        }

        /// <summary>SPEC §12.11.10.1 (v3.176)：嵌入结算弹窗内 ResultText / HintText 排版覆写。</summary>
        private static void ApplyEmbeddedResultDialogTextLayout(InvasionBattleResultDialogView dialogView)
        {
            if (dialogView == null)
                return;

            var titleText = dialogView.TitleText;
            if (titleText != null)
            {
                var titleRt = titleText.rectTransform;
                var titlePos = titleRt.anchoredPosition;
                titleRt.anchoredPosition = new Vector2(titlePos.x, EmbeddedResultTextPosY);
                titleText.fontSize = EmbeddedResultTextFontSize;
                titleText.fontStyle = FontStyle.Bold;
            }

            var hintTransform = dialogView.transform.Find("HintText");
            if (hintTransform == null)
                return;

            if (hintTransform is RectTransform hintRt)
            {
                var hintPos = hintRt.anchoredPosition;
                hintRt.anchoredPosition = new Vector2(hintPos.x, EmbeddedHintTextPosY);
            }

            var hintText = hintTransform.GetComponent<Text>();
            if (hintText == null)
                return;

            hintText.fontSize = EmbeddedHintTextFontSize;
            hintText.fontStyle = FontStyle.Bold;
        }

        private static Toggle BuildLabeledToggleOnRow(RectTransform rowRt, string labelText)
        {
            var bgRt = CreateChildRect(rowRt, "Background",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(28f, 0f), new Vector2(48f, 48f));
            var bgImg = bgRt.gameObject.AddComponent<Image>();
            bgImg.sprite = GetFallbackWhiteSprite();
            bgImg.color = new Color(0.22f, 0.22f, 0.28f, 1f);
            bgImg.raycastTarget = true;

            var checkRt = CreateChildRect(bgRt, "Checkmark",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(36f, 36f));
            var checkImg = checkRt.gameObject.AddComponent<Image>();
            checkImg.sprite = GetFallbackWhiteSprite();
            checkImg.color = new Color(0.32f, 0.82f, 0.45f, 1f);
            checkImg.raycastTarget = false;

            var labelRt = CreateChildRect(rowRt, "Label",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(207f, 0f), new Vector2(300f, 48f));
            var label = labelRt.gameObject.AddComponent<Text>();
            label.text = labelText;
            label.font = FarmGridView.LoadBuiltinFont();
            if (label.font == null)
                label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.fontSize = 28;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = Color.white;
            label.raycastTarget = false;

            var toggle = rowRt.gameObject.AddComponent<Toggle>();
            toggle.transition = Selectable.Transition.None;
            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;
            toggle.isOn = false;
            return toggle;
        }

        private void BuildAutoAdvanceCountdownHost(RectTransform parent)
        {
            countdownHostRt = CreateChildRect(parent, "AutoAdvanceCountdownHost",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -507f), new Vector2(720f, 52f));
            countdownText = countdownHostRt.gameObject.AddComponent<Text>();
            countdownText.alignment = TextAnchor.MiddleCenter;
            countdownText.color = new Color(1f, 0.92f, 0.65f, 1f);
            countdownText.fontSize = 32;
            countdownText.font = FarmGridView.LoadBuiltinFont();
            if (countdownText.font == null)
                countdownText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            countdownText.raycastTarget = false;
            countdownText.text = "";
            countdownHostRt.gameObject.SetActive(false);
        }

        private void BuildInBattleAutoAdvanceRow(RectTransform parent)
        {
            inBattleAutoRowRt = CreateChildRect(parent, "InBattleAutoAdvanceRow",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 132f), new Vector2(700f, 56f));
            inBattleAutoRowRt.pivot = new Vector2(0.5f, 0f);
            inBattleAutoToggle = BuildLabeledToggleOnRow(inBattleAutoRowRt, "自动推进关卡");
            inBattleAutoRowRt.gameObject.SetActive(false);
        }

        private void BuildReturnHomeButton(RectTransform parent)
        {
            returnHomeRowRt = CreateChildRect(parent, "ReturnHomeButtonRow",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 68f), new Vector2(200f, 48f));
            returnHomeRowRt.pivot = new Vector2(0.5f, 0f);

            var bgImg = returnHomeRowRt.gameObject.AddComponent<Image>();
            bgImg.sprite = GetFallbackWhiteSprite();
            bgImg.type = Image.Type.Sliced;
            bgImg.color = new Color(0.18f, 0.22f, 0.32f, 0.92f);
            bgImg.raycastTarget = true;

            var labelRt = CreateChildRect(returnHomeRowRt, "Label",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero);
            StretchFull(labelRt);
            var label = labelRt.gameObject.AddComponent<Text>();
            label.text = "返回家园";
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.fontSize = 28;
            label.font = FarmGridView.LoadBuiltinFont();
            if (label.font == null)
                label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.raycastTarget = false;

            returnHomeButton = returnHomeRowRt.gameObject.AddComponent<Button>();
            returnHomeButton.transition = Selectable.Transition.ColorTint;
            returnHomeButton.targetGraphic = bgImg;
            returnHomeButton.onClick.AddListener(OnReturnHomeClicked);
            returnHomeRowRt.gameObject.SetActive(false);
        }

        private void BuildBattleOngoingEntry(JiaYuanWorldScreenView jiaYuan)
        {
            if (jiaYuan == null)
                return;
            var parent = jiaYuan.transform as RectTransform;
            if (parent == null)
                return;

            battleOngoingEntryRt = CreateChildRect(parent, "BattleOngoingEntry",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -BattleOngoingEntryTopInset),
                new Vector2(BattleOngoingEntryWidth, BattleOngoingEntryHeight));
            battleOngoingEntryRt.pivot = new Vector2(0f, 1f);

            var entryImg = battleOngoingEntryRt.gameObject.AddComponent<Image>();
            var entrySprite = Resources.Load<Sprite>(ResBattleOngoingEntry);
            if (entrySprite != null)
            {
                entryImg.sprite = entrySprite;
                entryImg.preserveAspect = true;
            }
            else
            {
                entryImg.sprite = GetFallbackWhiteSprite();
                entryImg.color = new Color(0.85f, 0.25f, 0.2f, 0.92f);
                UnityEngine.Debug.LogWarning(
                    "[InvasionBattleView] 缺少战斗中入口图 Resources/" + ResBattleOngoingEntry + "，已使用占位。");
            }
            entryImg.raycastTarget = true;

            var entryBtn = battleOngoingEntryRt.gameObject.AddComponent<Button>();
            entryBtn.transition = Selectable.Transition.None;
            entryBtn.targetGraphic = entryImg;
            entryBtn.onClick.AddListener(OnBattleOngoingEntryClicked);
            battleOngoingEntryRt.gameObject.SetActive(false);
        }

        private void ResetBattleModalMinimizedUi()
        {
            battleModalMinimized = false;
            ApplyBattleModalMinimized(false);
            SetBattleOngoingEntryVisible(false);
        }

        private void OnReturnHomeClicked()
        {
            if (service == null || service.GetPhase() != InvasionPhase.InBattle)
                return;
            battleModalMinimized = true;
            ApplyBattleModalMinimized(true);
            if (bottomNav != null)
                bottomNav.SetOpenKey(JiaYuanHomeFeatureEntriesView.JiaYuanNavKey);
            SetBattleOngoingEntryVisible(true);
        }

        private void OnBattleOngoingEntryClicked()
        {
            if (!battleModalMinimized)
                return;
            ResetBattleModalMinimizedUi();
            if (modalRt != null)
                modalRt.SetAsLastSibling();
        }

        private void ApplyBattleModalMinimized(bool minimized)
        {
            if (modalCanvasGroup == null)
                return;
            if (minimized)
            {
                modalCanvasGroup.alpha = 0f;
                modalCanvasGroup.interactable = false;
                modalCanvasGroup.blocksRaycasts = false;
            }
            else
            {
                modalCanvasGroup.alpha = 1f;
                modalCanvasGroup.interactable = true;
                modalCanvasGroup.blocksRaycasts = true;
            }
        }

        private void SetBattleOngoingEntryVisible(bool visible)
        {
            if (battleOngoingEntryRt == null)
                return;
            bool show = visible && battleModalMinimized &&
                        service != null && service.GetPhase() == InvasionPhase.InBattle;
            battleOngoingEntryRt.gameObject.SetActive(show);
            if (show)
                battleOngoingEntryRt.SetAsLastSibling();
        }

        private void RefreshReturnHomeRowVisibility()
        {
            if (returnHomeRowRt == null)
                return;
            bool show = autoAdvanceLevels &&
                        service != null &&
                        service.EnteredBattleViaAutoChain &&
                        service.GetPhase() == InvasionPhase.InBattle;
            returnHomeRowRt.gameObject.SetActive(show);
        }

        /// <summary>§12.9：自动连战因体力不足失败时，关闭勾选并同步 UI。</summary>
        private void ClearAutoAdvanceLevelsUi()
        {
            autoAdvanceLevels = false;
            suppressAutoToggleSync = true;
            try
            {
                if (autoAdvanceWinToggle != null)
                    autoAdvanceWinToggle.isOn = false;
                if (inBattleAutoToggle != null)
                    inBattleAutoToggle.isOn = false;
            }
            finally
            {
                suppressAutoToggleSync = false;
            }
            RefreshReturnHomeRowVisibility();
        }

        private void OnAutoAdvanceToggleChanged(bool v)
        {
            if (suppressAutoToggleSync)
                return;
            suppressAutoToggleSync = true;
            try
            {
                autoAdvanceLevels = v;
                if (autoAdvanceWinToggle != null && autoAdvanceWinToggle.isOn != v)
                    autoAdvanceWinToggle.isOn = v;
                if (inBattleAutoToggle != null && inBattleAutoToggle.isOn != v)
                    inBattleAutoToggle.isOn = v;
            }
            finally
            {
                suppressAutoToggleSync = false;
            }
            RefreshReturnHomeRowVisibility();
        }

        private void UnbindAutoAdvanceListeners()
        {
            if (autoAdvanceWinToggle != null)
                autoAdvanceWinToggle.onValueChanged.RemoveListener(OnAutoAdvanceToggleChanged);
            if (inBattleAutoToggle != null)
                inBattleAutoToggle.onValueChanged.RemoveListener(OnAutoAdvanceToggleChanged);
        }

        private void BindAutoAdvanceListeners()
        {
            UnbindAutoAdvanceListeners();
            if (autoAdvanceWinToggle != null && autoAdvanceWinToggle.gameObject.activeSelf)
                autoAdvanceWinToggle.onValueChanged.AddListener(OnAutoAdvanceToggleChanged);
            if (inBattleAutoToggle != null && inBattleAutoRowRt != null && inBattleAutoRowRt.gameObject.activeSelf)
                inBattleAutoToggle.onValueChanged.AddListener(OnAutoAdvanceToggleChanged);
        }

        private void SetupInBattleAutoRowForOpen()
        {
            if (inBattleAutoRowRt == null)
                return;
            UnbindAutoAdvanceListeners();
            bool show = service != null && service.EnteredBattleViaAutoChain;
            inBattleAutoRowRt.gameObject.SetActive(show);
            RefreshReturnHomeRowVisibility();
            if (!show)
                return;
            suppressAutoToggleSync = true;
            try
            {
                if (inBattleAutoToggle != null)
                    inBattleAutoToggle.isOn = autoAdvanceLevels;
                if (autoAdvanceWinToggle != null)
                    autoAdvanceWinToggle.isOn = autoAdvanceLevels;
            }
            finally
            {
                suppressAutoToggleSync = false;
            }
            BindAutoAdvanceListeners();
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
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
