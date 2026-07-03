// SPEC §12 / §12.6：怪物入侵系统的运行时服务。
// 职责：
//   1) 维护入口图标三阶段状态机（Countdown / Invading / InBattle）；
//   2) 倒计时驱动（首轮 10s，后续 180s）；
//   3) 战斗会话生命周期（OpenBattle / CloseBattle）与 BattleSession 持有；
//   4) 通过事件总线对 EntryView / BattleView 单向广播状态变化。
// v3.0：核心战斗流程不依赖 PlantingService；
// v3.1：仅在胜利结算时调用 PlantingService 奖励入包 API（GrantSeed/GrantFertilizer）。
// v3.30：kCountdownAutoInvadingEnabled=false 时冻结 Countdown，不因计时切入 Invading（SPEC §12.1）。
// v3.42：§9.8.13.5 / §12.6 — 仓库「开始」经 OpenBattleFromWarehouseHub 切入 Invading 再开战。
// v3.43：§12.9 — 开战扣体力 10、不足切主线；自动连战链 TryOpenBattleFromAutoChain。
// 战斗期间不暂停农场 Tick（首版约定）。
using System;
using System.Collections.Generic;
using PetDemo.Core;
using PetDemo.Farm;
using PetDemo.Save;
using PetDemo.UI;
using UnityEngine;

namespace PetDemo.Battle
{
    [DisallowMultipleComponent]
    public class InvasionService : MonoBehaviour, IBattleCombatDriver
    {
        public const float FirstCountdownSeconds = 10f;
        public const float NormalCountdownSeconds = 180f;

        /// <summary>
        /// false：Tick 不递减倒计时、不因归零切入 Invading（Demo 默认，SPEC §12.1 / v3.30）。
        /// </summary>
        public const bool kCountdownAutoInvadingEnabled = false;

        /// <summary>SPEC §12.9：每场战斗开战成功前扣除的体力。</summary>
        public const int BattleStaminaCostPerEncounter = 10;

        public static InvasionService Instance { get; private set; }

        public static InvasionService GetOrCreate(GameObject host)
        {
            if (Instance != null)
                return Instance;
            var existing = host != null ? host.GetComponent<InvasionService>() : null;
            if (existing != null)
                return existing;
            return host != null ? host.AddComponent<InvasionService>() : null;
        }

        public event Action<InvasionPhase> OnPhaseChanged;
        public event Action<float> OnCountdownTick;
        public event Action<BattleSession> OnBattleHpChanged;
        public event Action<bool> OnBattleEnded;
        /// <summary>§12.9：自动连战开下一场时体力不足（不切换主线 Tab）。</summary>
        public event Action OnAutoChainBattleStartDenied;

        private InvasionPhase phase = InvasionPhase.Countdown;
        private float countdownRemaining;
        private bool firstCycle = true;

        private List<InvasionUnitConfig> units;
        private List<InvasionRewardConfig> invasionVictoryRewards;
        private List<InvasionRewardConfig> activeVictoryRewards;
        private BattleSession session;

        private BottomNavBarView battleDeniedNavBar;
        private bool pendingAutoChainBattleOpen;
        private bool enteredBattleViaAutoChain;
        private bool pendingMainStoryLevelSelectBattleOpen;
        private bool enteredBattleViaMainStoryLevelSelect;
        private bool continueMainStoryProgressInAutoChain;
        private bool pendingFriendHomeBattleOpen;
        private bool enteredBattleViaFriendHome;

        /// <summary>本场战斗是否由胜利后自动连战链进入（§12.9）。</summary>
        public bool EnteredBattleViaAutoChain => enteredBattleViaAutoChain;

        /// <summary>SPEC §13.4：本场战斗是否由好友家园「驱赶」进入；CloseBattle 完成后复位。</summary>
        public bool EnteredBattleViaFriendHome => enteredBattleViaFriendHome;

        /// <summary>SPEC §9.8.8.7 (v3.104)：标记下一次 OpenBattle 为主线选择关卡进度战。</summary>
        public void RequestMainStoryLevelSelectBattle()
        {
            pendingMainStoryLevelSelectBattleOpen = true;
        }

        public InvasionPhase GetPhase() => phase;
        public float GetCountdownRemaining() => countdownRemaining;
        public BattleSession GetBattleSession() => session;
        public IReadOnlyList<InvasionUnitConfig> GetUnits() => units;
        public IReadOnlyList<InvasionRewardConfig> GetVictoryRewards() =>
            activeVictoryRewards ?? invasionVictoryRewards;

        /// <summary>SPEC §12.9：体力不足开战时切到底栏「主线」。</summary>
        public void SetBattleDeniedNavigation(BottomNavBarView barView)
        {
            battleDeniedNavBar = barView;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            units = InvasionConfigCatalog.LoadInvasionUnitsFromCsv();
            invasionVictoryRewards = InvasionConfigCatalog.LoadVictoryRewardsFromCsv();
            // 冻结模式下仍写入首轮剩余秒数，供 InvasionEntryView 静态展示（SPEC §12.1 v3.30）。
            StartCountdown(FirstCountdownSeconds);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void StartCountdown(float seconds)
        {
            countdownRemaining = Mathf.Max(0f, seconds);
            SetPhase(InvasionPhase.Countdown);
            OnCountdownTick?.Invoke(countdownRemaining);
        }

        public void Tick(float deltaSeconds)
        {
            if (phase != InvasionPhase.Countdown)
                return;
            if (deltaSeconds <= 0f)
                return;
            if (!kCountdownAutoInvadingEnabled)
                return;

            countdownRemaining -= deltaSeconds;
            if (countdownRemaining <= 0f)
            {
                countdownRemaining = 0f;
                OnCountdownTick?.Invoke(countdownRemaining);
                SetPhase(InvasionPhase.Invading);
                return;
            }
            OnCountdownTick?.Invoke(countdownRemaining);
        }

        public void OpenBattle()
        {
            if (phase != InvasionPhase.Invading)
            {
                UnityEngine.Debug.LogWarning("[InvasionService] OpenBattle 只能在 Invading 阶段调用，当前阶段：" + phase);
                return;
            }

            bool fromFriendHome = pendingFriendHomeBattleOpen;
            pendingFriendHomeBattleOpen = false;

            var planting = PlantingService.Instance;
            // SPEC §13.4：好友家园「驱赶」战斗不消耗体力（演示约定）。
            if (!fromFriendHome &&
                (planting == null || !planting.TryConsumeStamina(BattleStaminaCostPerEncounter)))
            {
                HandleBattleStartDenied();
                return;
            }

            enteredBattleViaFriendHome = fromFriendHome;

            bool fromAutoChain = pendingAutoChainBattleOpen;
            pendingAutoChainBattleOpen = false;
            enteredBattleViaAutoChain = fromAutoChain;

            bool fromMainStoryLevelSelect = pendingMainStoryLevelSelectBattleOpen;
            pendingMainStoryLevelSelectBattleOpen = false;
            enteredBattleViaMainStoryLevelSelect = fromMainStoryLevelSelect;
            ResolveActiveVictoryRewards(fromMainStoryLevelSelect, planting);

            if (planting != null)
            {
                var profile = planting.GetRestrictionProfile();
                if (profile == null || !profile.accepted || profile.fieldPetLimit <= 0)
                    UnityEngine.Debug.LogWarning("[InvasionService] OpenBattle: 上场数量限制未接受或上限为 0，仍按 P0 继续开战。");
            }

            session = BuildBattleSession();
            SetPhase(InvasionPhase.InBattle);
            OnBattleHpChanged?.Invoke(session);
        }

        /// <summary>
        /// SPEC §12.9：胜利结算 3 秒倒计时结束后调用；Countdown→Invading→OpenBattle（再扣体力）。
        /// 返回是否已进入 InBattle。
        /// </summary>
        public bool TryOpenBattleFromAutoChain()
        {
            if (phase == InvasionPhase.InBattle)
            {
                UnityEngine.Debug.LogWarning("[InvasionService] TryOpenBattleFromAutoChain: 已在战斗中，忽略。");
                return false;
            }

            pendingAutoChainBattleOpen = true;
            pendingMainStoryLevelSelectBattleOpen = continueMainStoryProgressInAutoChain;
            continueMainStoryProgressInAutoChain = false;
            if (phase == InvasionPhase.Countdown)
                SetPhase(InvasionPhase.Invading);
            OpenBattle();
            return phase == InvasionPhase.InBattle && session != null;
        }

        private void HandleBattleStartDenied()
        {
            bool wasAutoChain = pendingAutoChainBattleOpen;
            pendingAutoChainBattleOpen = false;
            enteredBattleViaAutoChain = false;
            pendingMainStoryLevelSelectBattleOpen = false;
            enteredBattleViaMainStoryLevelSelect = false;
            continueMainStoryProgressInAutoChain = false;
            pendingFriendHomeBattleOpen = false;
            enteredBattleViaFriendHome = false;
            if (phase == InvasionPhase.Invading)
                SetPhase(InvasionPhase.Countdown);

            if (wasAutoChain)
            {
                OnAutoChainBattleStartDenied?.Invoke();
                UnityEngine.Debug.LogWarning(
                    "[InvasionService] 自动连战开战失败：体力不足（需 ≥ " + BattleStaminaCostPerEncounter +
                    " 体力），已关闭自动推进并请求 HungryDialog。");
                return;
            }

            if (battleDeniedNavBar != null)
                battleDeniedNavBar.SetOpenKey(MainStoryLineScreenView.ZhuXianNavKey);
            else
                UnityEngine.Debug.LogWarning("[InvasionService] HandleBattleStartDenied: 未注入 BottomNavBarView，无法切主线。");
            UnityEngine.Debug.LogWarning("[InvasionService] 开战失败：体力不足或服务不可用（需 ≥ " + BattleStaminaCostPerEncounter + " 体力）。");
        }

        /// <summary>
        /// SPEC §9.8.13.5 / §12.6：统一仓库「开始」——在倒计时冻结默认下先切入 Invading，再复用 <see cref="OpenBattle"/>。
        /// </summary>
        public void OpenBattleFromWarehouseHub()
        {
            if (phase == InvasionPhase.InBattle)
            {
                UnityEngine.Debug.LogWarning("[InvasionService] OpenBattleFromWarehouseHub: 已在战斗中，忽略重复请求。");
                return;
            }
            if (phase == InvasionPhase.Countdown)
                SetPhase(InvasionPhase.Invading);
            OpenBattle();
        }

        /// <summary>
        /// SPEC §13.4：好友家园「驱赶」开战 — 免体力、不打主线标记、不推进主线进度；
        /// 敌人数据与主线第 5 关一致（当前实现全局共用 boss_langren，见 §12.5）。
        /// </summary>
        public void OpenBattleFromFriendHome()
        {
            if (phase == InvasionPhase.InBattle)
            {
                UnityEngine.Debug.LogWarning("[InvasionService] OpenBattleFromFriendHome: 已在战斗中，忽略重复请求。");
                return;
            }
            pendingFriendHomeBattleOpen = true;
            if (phase == InvasionPhase.Countdown)
                SetPhase(InvasionPhase.Invading);
            OpenBattle();
        }

        public void ApplyDamageToEnemy(int damage)
        {
            if (session == null || phase != InvasionPhase.InBattle)
                return;
            session.enemyHp = Mathf.Max(0, session.enemyHp - Mathf.Max(0, damage));
            OnBattleHpChanged?.Invoke(session);
            CheckBattleEnd();
        }

        public void ApplyDamageToPlayer(int damage)
        {
            if (session == null || phase != InvasionPhase.InBattle)
                return;
            session.playerHp = Mathf.Max(0, session.playerHp - Mathf.Max(0, damage));
            OnBattleHpChanged?.Invoke(session);
            CheckBattleEnd();
        }

        public void SetTurn(BattleTurn turn)
        {
            if (session == null)
                return;
            session.turn = turn;
        }

        public void CloseBattle(bool playerWon)
        {
            enteredBattleViaAutoChain = false;
            if (playerWon)
            {
                GrantVictoryRewards();
                if (enteredBattleViaMainStoryLevelSelect)
                    TryAdvanceMainStoryOnVictory();
            }

            continueMainStoryProgressInAutoChain =
                playerWon && enteredBattleViaMainStoryLevelSelect;
            enteredBattleViaMainStoryLevelSelect = false;

            session = null;
            firstCycle = false;
            OnBattleEnded?.Invoke(playerWon);
            // SPEC §13.4：在 OnBattleEnded 广播之后复位，订阅方（好友家园层）可在回调内读取该标记。
            enteredBattleViaFriendHome = false;
            // 冻结模式下仍重置剩余秒数；Tick 在 kCountdownAutoInvadingEnabled==false 时不递减（SPEC §12.1 v3.30）。
            StartCountdown(NormalCountdownSeconds);
        }

        private void TryAdvanceMainStoryOnVictory()
        {
            var planting = PlantingService.Instance;
            if (planting == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[InvasionService] TryAdvanceMainStoryOnVictory: PlantingService.Instance 为空，跳过主线推进。");
                return;
            }

            int highest = planting.GetMainStoryHighestClearedLevel();
            int levelToClear = highest + 1;
            var levelConfigs = MainStoryLevelConfigCatalog.LoadLevelConfigsFromCsv();
            int maxLevel = MainStoryLevelConfigCatalog.GetMaxLevelNumber(levelConfigs);
            if (maxLevel > 0 && levelToClear > maxLevel)
                return;

            if (!planting.TryRecordMainStoryLevelCleared(levelToClear))
                return;

            GameSaveCoordinator.TrySaveActiveSlot();
        }

        private void CheckBattleEnd()
        {
            if (session == null)
                return;
            if (session.enemyHp <= 0)
            {
                session.playerWon = true;
                session.turn = BattleTurn.Result;
            }
            else if (session.playerHp <= 0)
            {
                session.playerWon = false;
                session.turn = BattleTurn.Result;
            }
        }

        private BattleSession BuildBattleSession()
        {
            var player = InvasionConfigCatalog.FindById(units, InvasionConfigCatalog.PlayerUnitId);
            var enemy = InvasionConfigCatalog.FindById(units, InvasionConfigCatalog.DefaultEnemyUnitId);

            int pAtk = player != null ? player.attack : 12;
            int pHp = player != null ? player.maxHp : 80;
            int eAtk = enemy != null ? enemy.attack : 8;
            int eHp = enemy != null ? enemy.maxHp : 60;

            return new BattleSession
            {
                playerAttack = pAtk,
                playerMaxHp = pHp,
                playerHp = pHp,
                enemyAttack = eAtk,
                enemyMaxHp = eHp,
                enemyHp = eHp,
                turn = BattleTurn.Player,
                playerWon = false,
            };
        }

        private void SetPhase(InvasionPhase next)
        {
            if (phase == next)
                return;
            phase = next;
            OnPhaseChanged?.Invoke(phase);
        }

        // SPEC §12.8 / §9.8.8.8：只有战斗胜利才发放奖励；失败不发放。
        private void GrantVictoryRewards()
        {
            var planting = PlantingService.Instance;
            if (planting == null)
            {
                UnityEngine.Debug.LogWarning("[InvasionService] PlantingService.Instance 为空，跳过胜利奖励发放。");
                return;
            }

            var rewards = GetVictoryRewards();
            InvasionRewardGrantHelper.GrantAll(planting, rewards);
        }

        private void ResolveActiveVictoryRewards(bool fromMainStoryLevelSelect, PlantingService planting)
        {
            if (!fromMainStoryLevelSelect)
            {
                activeVictoryRewards = invasionVictoryRewards;
                return;
            }

            int highest = planting != null ? planting.GetMainStoryHighestClearedLevel() : 0;
            var levelConfigs = MainStoryLevelConfigCatalog.LoadLevelConfigsFromCsv();
            int battleLevel = MainStoryLevelConfigCatalog.ResolveMainStoryBattleLevelNumber(
                highest, levelConfigs);
            activeVictoryRewards = MainStoryLevelConfigCatalog.GetVictoryRewardsForLevel(
                levelConfigs, battleLevel);
        }

        public bool IsFirstCycle() => firstCycle;
    }
}
