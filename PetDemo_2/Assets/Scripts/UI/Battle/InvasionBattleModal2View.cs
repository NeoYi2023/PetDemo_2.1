// SPEC §12.11：新战斗界面 InvasionBattleModal_2（预制体化 + 「下一天」事件玩法）。
// 职责：
//   1) 预制体优先 / 代码回退地构建全屏 modal：上(角色展示)/中(属性)/下(事件日志+下一天)三段，共用背景 AirUI/ZhanDou_0；
//   2) 上部运行时以 SkeletonGraphic 构建玩家 Role_cslangren（与 §9.5 / §12.7 同方法）；
//   3) 中部读取「玩法局内属性副本」（Show() 时克隆 RoleStats；事件奖励只改副本、不写回存档）；
//   4) 点击「下一天」→ day+1 → 天数表加权随机抽 1 个 eventId → 事件表解析明细 → 事件日志按 /n 拆多条九宫格卡展示；
//      期间「下一天」按钮灰置直到展示完成，随后按事件类型切换按钮态（战斗/抽奖换素材换字，其余恢复常态）。
using System;
using System.Collections;
using System.Collections.Generic;
using PetDemo.Battle;
using PetDemo.Core;
using PetDemo.Farm;
using PetDemo.UI;
using PetDemo.UI.Farm;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Battle
{
    [DisallowMultipleComponent]
    public sealed class InvasionBattleModal2View : MonoBehaviour
    {
        public const string ResPrefabPath = "Prefabs/Battle/InvasionBattleModal_2";
        public const string ResBackground = "AirUI/ZhanDou_0";
        public const string ResNextDayButton = "AirUI/InvasionBattleModal_2_Button_1";
        public const string ResBattleButton = "AirUI/InvasionBattleModal_2_Button_2";
        public const string ResLotteryButton = "AirUI/InvasionBattleModal_2_Button_3";
        public const string ResDetailAttrButton = "AirUI/JiNengLiebiao";
        public const string ResEventFramePrefix = "AirUI/ShiJian_";
        public const string ResPlayerPrefab = "Prefabs/Air/Hero_Role_cunmin";
        public const string SkeletonGraphicShaderName = "Spine/SkeletonGraphic";

        public const string PanelObjectName = "InvasionBattleModal_2";
        public const string TopAreaName = "TopArea";
        public const string MiddleAreaName = "MiddleArea";
        public const string DetailAttrButtonName = "DetailAttrButton";
        public const string BottomAreaName = "BottomArea";
        public const string PlayerSlotName = "PlayerSlot";
        public const string EventScrollName = "EventScroll";
        public const string EventContentName = "EventContent";

        private static readonly Vector2 CharacterSize = new Vector2(720f, 1200f);
        private const float CharacterScale = 0.53f;
        private static readonly Vector2 NextDayButtonSize = new Vector2(360f, 140f);
        private static readonly Vector2 CloseButtonSize = new Vector2(96f, 96f);
        private static readonly Vector2 DetailAttrButtonSize = new Vector2(96f, 96f);

        private static readonly Color ButtonGreyTint = new Color(0.55f, 0.55f, 0.55f, 1f);
        private const float RevealSegmentInterval = 0.15f;

        // SPEC §12.11.4 / §12.11.5（v3.173）：PlayerSlot 待机/移动动画候选链与「下一天」移动过场时长。
        private const float PlayerMoveAnimDurationSec = 1f;
        private static readonly string[] MoveAnimCandidates = { "move_1", "move", "animation" };
        private static readonly string[] IdleAnimCandidates = { "standby_1", "standby", "idle", "exclusive_2", "animation" };

        private static InvasionBattleModal2View instance;

        [SerializeField] private RectTransform playerSlot;
        [SerializeField] private Text hpText;
        [SerializeField] private Text atkText;
        [SerializeField] private Text speedText;
        [SerializeField] private ScrollRect eventScrollRect;
        [SerializeField] private RectTransform eventContent;
        [SerializeField] private Text dayLabel;
        [SerializeField] private Button nextDayButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button detailAttrButton;

        private RectTransform panelRt;
        private RectTransform canvasRectCache;
        private IPlantingService service;

        private Dictionary<string, InvasionEventConfig> eventDict;
        private List<InvasionEventDayEntry> dayEntries;
        private int currentDay;
        private bool wired;
        private bool roleStatsSubscribed;
        private bool playerBuilt;
        private SkeletonGraphic playerSkeleton;

        private RoleStats runStats;
        private bool runStatsDirty;
        private readonly Dictionary<string, int> runEnhanceBonuses = new Dictionary<string, int>();

        private Image nextDayButtonImage;
        private Text nextDayLabel;
        private NextDayButtonMode buttonMode = NextDayButtonMode.Normal;
        private Coroutine revealRoutine;

        private readonly Dictionary<int, Sprite> frameSpriteCache = new Dictionary<int, Sprite>();

        // SPEC §12.11.9：三选一技能事件（领悟/顿悟）——技能池、本局已获得、左上角技能条。
        private List<BattleSkillConfig> skillCatalog;
        private readonly HashSet<string> acquiredSkillIds = new HashSet<string>();
        private RectTransform skillStrip;
        private int skillIconCount;

        // SPEC §12.12 / §B.19：属性增强表（老虎机抽奖候选与产出）、待开启的老虎机轴数。
        private List<AttrEnhanceConfig> attrEnhanceCatalog;
        private int pendingSlotReelCount;

        // SPEC §12.11.10 (v3.172)：小战斗/BOSS 战——待触发类型、单位表缓存、嵌入战斗实例、TopArea 缓存。
        private InvasionEventRewardKind pendingBattleKind = InvasionEventRewardKind.BattleSmall;
        private List<InvasionUnitConfig> battleUnits;
        private InvasionBattleView embeddedBattle;
        private RectTransform topArea;

        // 左上角技能条布局（SPEC §12.11.9）
        private static readonly Vector2 SkillStripFirstPos = new Vector2(-480f, 765f);
        private const float SkillIconTargetSize = 96f;
        private const float SkillIconStartSize = 200f;
        private const float SkillIconGap = 10f;
        private const int SkillIconsPerRow = 5;
        private const float SkillIconRowStep = 50f;
        private const float SkillIconShrinkDuration = 0.4f;

        private enum NextDayButtonMode
        {
            Normal,     // 常态「下一天」，可点击推进
            Revealing,  // 灰置，事件展示中
            Battle,     // 「战斗」态（本期点击无响应）
            Lottery,    // 「打开」态（本期点击无响应）
        }

        public bool IsShown => gameObject != null && gameObject.activeSelf;

        public static InvasionBattleModal2View GetOrCreate(RectTransform canvasRect)
        {
            if (canvasRect == null)
            {
                UnityEngine.Debug.LogWarning("[InvasionBattleModal2View] GetOrCreate: canvasRect 为空");
                return null;
            }

            if (instance != null && instance.panelRt != null)
            {
                instance.canvasRectCache = canvasRect;
                return instance;
            }

            var existing = canvasRect.Find(PanelObjectName);
            if (existing != null)
            {
                var existView = existing.GetComponent<InvasionBattleModal2View>();
                if (existView == null)
                    existView = existing.gameObject.AddComponent<InvasionBattleModal2View>();
                existView.panelRt = existing as RectTransform;
                existView.canvasRectCache = canvasRect;
                instance = existView;
                return existView;
            }

            var prefab = Resources.Load<GameObject>(ResPrefabPath);
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, canvasRect, false);
            }
            else
            {
                UnityEngine.Debug.LogWarning(
                    "[InvasionBattleModal2View] 缺少预制体 Resources/" + ResPrefabPath +
                    "，使用运行时回退 UI；请在编辑器执行 Tools/PetDemo/Generate Invasion Battle Modal 2 Prefab。");
                go = BuildRuntimeFallback(canvasRect);
                if (go == null)
                    return null;
            }

            go.name = PanelObjectName;
            go.SetActive(false);
            var rt = go.transform as RectTransform;
            if (rt != null)
                BottomNavAttachedScreenLayout.StretchFull(rt);

            var view = go.GetComponent<InvasionBattleModal2View>();
            if (view == null)
                view = go.AddComponent<InvasionBattleModal2View>();
            view.panelRt = rt;
            view.canvasRectCache = canvasRect;
            instance = view;
            return view;
        }

        public void Show(IPlantingService plantingService = null)
        {
            service = plantingService ?? PlantingService.Instance;

            EnsureFieldsFromHierarchy();
            EnsureEventsLoaded();
            EnsureEventLog();
            WireButtonsOnce();
            EnsureButtonRefs();
            EnsurePlayerBuilt();
            ResetSkillState();

            // SPEC §12.11.10：新一局清理残留的嵌入战斗并确保站立阿狼可见。
            if (embeddedBattle != null)
            {
                Destroy(embeddedBattle.gameObject);
                embeddedBattle = null;
            }
            SetStandingPlayerVisible(true);

            currentDay = 0;
            RefreshDayLabel();

            runStats = CloneRoleStats(service != null ? service.GetRoleStats() : null);
            runStatsDirty = false;
            InitRunEnhanceBonusesFromStats();
            RefreshRoleStats();
            EnsureRoleStatsSubscription();
            EnsureDetailAttrButton();

            ClearEventLog();
            SetNextDayButtonMode(NextDayButtonMode.Normal);
            AppendEventCard("点击「下一天」开始探索。", InvasionEventConfigCatalog.MinBackgroundIndex);
            ScrollEventLogToBottom();

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            if (revealRoutine != null)
            {
                StopCoroutine(revealRoutine);
                revealRoutine = null;
            }
            // SPEC §12.11.10：关闭时清理进行中的嵌入战斗并恢复站立阿狼。
            if (embeddedBattle != null)
            {
                Destroy(embeddedBattle.gameObject);
                embeddedBattle = null;
                SetStandingPlayerVisible(true);
            }
            SkillPickThreeModalView.HideIfAny();
            SlotMachineModalView.HideIfAny();
            DetailAttributeModalView.HideIfAny();
            gameObject.SetActive(false);
        }

        public static void HideIfAny()
        {
            if (instance != null && instance.IsShown)
                instance.Hide();
        }

        private void EnsureEventsLoaded()
        {
            if (eventDict == null || eventDict.Count == 0)
                eventDict = InvasionEventConfigCatalog.LoadEventsFromCsv();
            if (dayEntries == null || dayEntries.Count == 0)
                dayEntries = InvasionEventConfigCatalog.LoadDayTableFromCsv();
            if (skillCatalog == null || skillCatalog.Count == 0)
                skillCatalog = SkillConfigCatalog.LoadSkillsFromCsv();
            if (attrEnhanceCatalog == null || attrEnhanceCatalog.Count == 0)
                attrEnhanceCatalog = AttrEnhanceConfigCatalog.LoadFromCsv();
        }

        private void WireButtonsOnce()
        {
            if (wired)
                return;

            if (nextDayButton != null)
            {
                nextDayButton.onClick.RemoveAllListeners();
                nextDayButton.onClick.AddListener(OnNextDayClicked);
            }
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(OnCloseClicked);
            }
            if (detailAttrButton != null)
            {
                detailAttrButton.onClick.RemoveAllListeners();
                detailAttrButton.onClick.AddListener(OnDetailAttrClicked);
            }
            wired = true;
        }

        /// <summary>SPEC §12.11.5：缓存「下一天」按钮的图片与文字标签引用（兼容缺 Label 的旧预制体，运行时补建）。</summary>
        private void EnsureButtonRefs()
        {
            if (nextDayButton == null)
                return;

            if (nextDayButtonImage == null)
                nextDayButtonImage = nextDayButton.targetGraphic as Image ?? nextDayButton.GetComponent<Image>();

            var btnRt = nextDayButton.transform as RectTransform;
            if (btnRt == null)
                return;
            var labelT = btnRt.Find("Label");
            nextDayLabel = labelT != null ? labelT.GetComponent<Text>() : null;
            if (nextDayLabel == null)
            {
                var labelRt = BottomNavAttachedScreenLayout.CreateChildRect(
                    btnRt, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                BottomNavAttachedScreenLayout.StretchFull(labelRt);
                nextDayLabel = labelRt.gameObject.AddComponent<Text>();
                nextDayLabel.font = FarmGridView.LoadBuiltinFont();
                nextDayLabel.fontSize = 44;
                nextDayLabel.alignment = TextAnchor.MiddleCenter;
                nextDayLabel.raycastTarget = false;
                nextDayLabel.color = Color.black;
                nextDayLabel.fontStyle = FontStyle.Bold;
            }
        }

        // ============================================================
        // 「下一天」玩法（SPEC §12.11.5）
        // ============================================================
        private void OnNextDayClicked()
        {
            // SPEC §12.12：「抽奖 Lottery」态「打开」→ 打开老虎机抽奖界面。
            if (buttonMode == NextDayButtonMode.Lottery)
            {
                OpenSlotMachine(pendingSlotReelCount);
                return;
            }

            // SPEC §12.11.10：「战斗 Battle」态「战斗」→ 嵌入复用关卡战斗模拟。
            if (buttonMode == NextDayButtonMode.Battle)
            {
                LaunchEmbeddedBattle();
                return;
            }

            // 灰置中 → 忽略
            if (buttonMode != NextDayButtonMode.Normal)
                return;

            currentDay += 1;
            RefreshDayLabel();

            EnsureEventsLoaded();
            string eventId = InvasionEventConfigCatalog.PickWeightedByDay(dayEntries, currentDay);
            InvasionEventConfig cfg = null;
            if (!string.IsNullOrEmpty(eventId) && eventDict != null)
                eventDict.TryGetValue(eventId, out cfg);

            if (revealRoutine != null)
                StopCoroutine(revealRoutine);
            revealRoutine = StartCoroutine(RevealEventRoutine(cfg));
        }

        private IEnumerator RevealEventRoutine(InvasionEventConfig cfg)
        {
            SetNextDayButtonMode(NextDayButtonMode.Revealing);

            // SPEC §12.11.5（v3.173）：移动过场——角色播放移动动画 1 秒，期间 BottomArea 暂停不更新事件；随后恢复待机再展示。
            PlayPlayerMoveLoop();
            yield return new WaitForSeconds(PlayerMoveAnimDurationSec);
            PlayPlayerIdleLoop();

            if (cfg == null)
            {
                AppendEventCard("今日无事发生。", InvasionEventConfigCatalog.MinBackgroundIndex);
                ScrollEventLogToBottom();
                yield return null;
                SetNextDayButtonMode(NextDayButtonMode.Normal);
                revealRoutine = null;
                yield break;
            }

            var segments = cfg.textSegments;
            if (segments == null || segments.Count == 0)
            {
                AppendEventCard(cfg.eventId, cfg.backgroundIndex);
                ScrollEventLogToBottom();
                yield return new WaitForSeconds(RevealSegmentInterval);
            }
            else
            {
                for (int i = 0; i < segments.Count; i++)
                {
                    AppendEventCard(segments[i], cfg.backgroundIndex);
                    ScrollEventLogToBottom();
                    yield return new WaitForSeconds(RevealSegmentInterval);
                }
            }

            ApplyRewards(cfg.rewards);

            // SPEC §12.11.9：领悟/顿悟（奇遇 + pick3:*）——展示完成后保持灰置并打开三选一，
            // 由玩家「确定」获取技能后再恢复常态。
            var pick = FindPickThreeReward(cfg.rewards);
            if (pick != null)
            {
                revealRoutine = null;
                OpenSkillPickThree(pick.skillQuality);
                yield break;
            }

            switch (cfg.eventType)
            {
                case InvasionEventType.Battle:
                    pendingBattleKind = FindBattleKind(cfg.rewards);
                    SetNextDayButtonMode(NextDayButtonMode.Battle);
                    break;
                case InvasionEventType.Lottery:
                    pendingSlotReelCount = FindSlotReelCount(cfg.rewards);
                    SetNextDayButtonMode(NextDayButtonMode.Lottery);
                    break;
                default:
                    SetNextDayButtonMode(NextDayButtonMode.Normal);
                    break;
            }
            revealRoutine = null;
        }

        private static InvasionEventReward FindPickThreeReward(List<InvasionEventReward> rewards)
        {
            if (rewards == null)
                return null;
            for (int i = 0; i < rewards.Count; i++)
            {
                if (rewards[i] != null && rewards[i].kind == InvasionEventRewardKind.PickThree)
                    return rewards[i];
            }
            return null;
        }

        private void SetNextDayButtonMode(NextDayButtonMode mode)
        {
            buttonMode = mode;
            EnsureButtonRefs();

            string label;
            string spritePath;
            bool interactable;
            Color tint;

            switch (mode)
            {
                case NextDayButtonMode.Revealing:
                    label = "下一天";
                    spritePath = ResNextDayButton;
                    interactable = false;
                    tint = ButtonGreyTint;
                    break;
                case NextDayButtonMode.Battle:
                    label = "战斗";
                    spritePath = ResBattleButton;
                    interactable = true;
                    tint = Color.white;
                    break;
                case NextDayButtonMode.Lottery:
                    label = "打开";
                    spritePath = ResLotteryButton;
                    interactable = true;
                    tint = Color.white;
                    break;
                default:
                    label = "下一天";
                    spritePath = ResNextDayButton;
                    interactable = true;
                    tint = Color.white;
                    break;
            }

            if (nextDayButton != null)
                nextDayButton.interactable = interactable;
            if (nextDayLabel != null)
                nextDayLabel.text = label;
            if (nextDayButtonImage != null)
            {
                var sprite = Resources.Load<Sprite>(spritePath);
                if (sprite != null)
                    nextDayButtonImage.sprite = sprite;
                nextDayButtonImage.color = tint;
            }
        }

        // ============================================================
        // 事件奖励（SPEC §12.11.5 / §B.17.2）——仅玩法局内生效
        // ============================================================
        private void ApplyRewards(List<InvasionEventReward> rewards)
        {
            if (rewards == null || rewards.Count == 0 || runStats == null)
                return;

            bool changed = false;
            for (int i = 0; i < rewards.Count; i++)
            {
                var r = rewards[i];
                if (r == null)
                    continue;
                switch (r.kind)
                {
                    case InvasionEventRewardKind.AttrPercent:
                        ApplyAttrPercent(r.target, r.percent);
                        changed = true;
                        break;
                    case InvasionEventRewardKind.PickThree:
                        // SPEC §12.11.9：三选一在展示完成后单独处理（打开三选一界面），此处不作占位告警。
                        break;
                    default:
                        UnityEngine.Debug.LogWarning(
                            "[InvasionBattleModal2View] 事件奖励占位（本期无效果，效果 TBD）：" + r.kind);
                        break;
                }
            }

            if (changed)
            {
                runStatsDirty = true;
                RefreshRoleStats();
            }
        }

        private void ApplyAttrPercent(string target, int percent)
        {
            if (runStats == null || string.IsNullOrEmpty(target))
                return;
            float factor = 1f + percent / 100f;
            switch (target)
            {
                case "hp":
                    runStats.maxHp = Mathf.Max(1, Mathf.RoundToInt(runStats.maxHp * factor));
                    runStats.currentHp = Mathf.Clamp(Mathf.RoundToInt(runStats.currentHp * factor), 0, runStats.maxHp);
                    break;
                case "atk":
                    runStats.atk = Mathf.Max(0, Mathf.RoundToInt(runStats.atk * factor));
                    break;
                case "speed":
                    runStats.agility = Mathf.Max(0, Mathf.RoundToInt(runStats.agility * factor));
                    break;
            }
        }

        private static RoleStats CloneRoleStats(RoleStats src)
        {
            if (src == null)
                return null;
            return new RoleStats
            {
                displayName = src.displayName,
                atk = src.atk,
                def = src.def,
                maxHp = src.maxHp,
                currentHp = src.currentHp,
                agility = src.agility,
                criticalHit = src.criticalHit,
                combo = src.combo,
                counterattack = src.counterattack,
                stun = src.stun,
                evasion = src.evasion,
                lifeSteal = src.lifeSteal,
            };
        }

        private void InitRunEnhanceBonusesFromStats()
        {
            AttrEnhanceConfigCatalog.SeedHexBonusesFromRole(runStats, runEnhanceBonuses);
        }

        // ============================================================
        // 小战斗 / BOSS 战（SPEC §12.11.10：嵌入复用关卡战斗模拟）
        // ============================================================
        /// <summary>从事件奖励解析战斗类型：优先 BOSS，其次小怪；无则默认小怪。</summary>
        private static InvasionEventRewardKind FindBattleKind(List<InvasionEventReward> rewards)
        {
            if (rewards != null)
            {
                for (int i = 0; i < rewards.Count; i++)
                {
                    if (rewards[i] == null)
                        continue;
                    if (rewards[i].kind == InvasionEventRewardKind.BattleBoss)
                        return InvasionEventRewardKind.BattleBoss;
                    if (rewards[i].kind == InvasionEventRewardKind.BattleSmall)
                        return InvasionEventRewardKind.BattleSmall;
                }
            }
            return InvasionEventRewardKind.BattleSmall;
        }

        private void LaunchEmbeddedBattle()
        {
            if (embeddedBattle != null)
                return;

            EnsureFieldsFromHierarchy();
            if (topArea == null)
            {
                UnityEngine.Debug.LogWarning("[InvasionBattleModal2View] 未找到 TopArea，无法启动嵌入战斗，恢复常态。");
                SetNextDayButtonMode(NextDayButtonMode.Normal);
                return;
            }

            if (battleUnits == null || battleUnits.Count == 0)
                battleUnits = InvasionConfigCatalog.LoadInvasionUnitsFromCsv();

            string enemyUnitId = pendingBattleKind == InvasionEventRewardKind.BattleBoss
                ? InvasionConfigCatalog.BossEnemyUnitId
                : InvasionConfigCatalog.SmallEnemyUnitId;
            var enemyUnit = InvasionConfigCatalog.FindById(battleUnits, enemyUnitId);
            int eAtk = enemyUnit != null ? enemyUnit.attack : 6;
            int eHp = enemyUnit != null ? enemyUnit.maxHp : 40;
            string enemyPrefab = enemyUnit != null ? enemyUnit.skeletonPrefab : null;

            // SPEC §12.11.10：玩家侧数值取玩法局内属性副本 runStats。
            int pAtk = runStats != null ? Mathf.Max(0, runStats.atk) : 12;
            int pHp = runStats != null ? Mathf.Max(1, runStats.maxHp) : 80;

            var session = new BattleSession
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

            SetStandingPlayerVisible(false);
            // 战斗期间灰置底部按钮，避免重复触发。
            SetNextDayButtonMode(NextDayButtonMode.Revealing);

            EnsureFieldsFromHierarchy();
            if (panelRt == null)
                panelRt = transform as RectTransform;

            embeddedBattle = InvasionBattleView.BuildEmbedded(
                topArea, session, enemyPrefab, OnEmbeddedBattleEnded, panelRt);
            if (embeddedBattle == null)
            {
                UnityEngine.Debug.LogWarning("[InvasionBattleModal2View] 嵌入战斗构建失败，恢复常态。");
                SetStandingPlayerVisible(true);
                SetNextDayButtonMode(NextDayButtonMode.Normal);
            }
        }

        private void OnEmbeddedBattleEnded(bool playerWon)
        {
            if (embeddedBattle != null)
            {
                Destroy(embeddedBattle.gameObject);
                embeddedBattle = null;
            }
            SetStandingPlayerVisible(true);

            if (playerWon)
            {
                // SPEC §12.11.10 (v3.181)：BOSS 胜 → 关闭探索界面并返回关卡选择；小怪胜 → 继续「下一天」。
                if (pendingBattleKind == InvasionEventRewardKind.BattleBoss)
                {
                    Hide();
                    MainStoryLineScreenView.ShowLevelSelectPanel();
                }
                else
                {
                    SetNextDayButtonMode(NextDayButtonMode.Normal);
                }
            }
            else
            {
                // 负：本局结束，关闭 InvasionBattleModal_2。
                Hide();
            }
        }

        /// <summary>显隐 TopArea/PlayerSlot 下运行时构建的站立阿狼（含骨骼或占位块）。</summary>
        private void SetStandingPlayerVisible(bool visible)
        {
            if (playerSlot == null)
                return;
            for (int i = 0; i < playerSlot.childCount; i++)
            {
                var child = playerSlot.GetChild(i);
                if (child != null)
                    child.gameObject.SetActive(visible);
            }
        }

        // ============================================================
        // 三选一技能事件（SPEC §12.11.9：领悟/顿悟）
        // ============================================================
        private void ResetSkillState()
        {
            acquiredSkillIds.Clear();
            skillIconCount = 0;
            EnsureSkillStrip();
            if (skillStrip != null)
            {
                for (int i = skillStrip.childCount - 1; i >= 0; i--)
                    Destroy(skillStrip.GetChild(i).gameObject);
            }
        }

        private void EnsureSkillStrip()
        {
            if (skillStrip != null)
                return;
            if (panelRt == null)
                panelRt = transform as RectTransform;
            var existing = FindDescendantRect("SkillStrip");
            if (existing != null)
            {
                skillStrip = existing;
                return;
            }
            skillStrip = BottomNavAttachedScreenLayout.CreateChildRect(
                panelRt, "SkillStrip",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            skillStrip.SetAsLastSibling();
        }

        private void OpenSkillPickThree(SkillQuality quality)
        {
            EnsureEventsLoaded();
            var options = SkillConfigCatalog.PickThreeByQuality(skillCatalog, quality, acquiredSkillIds);
            if (options == null || options.Count == 0)
            {
                string msg = quality == SkillQuality.Legendary
                    ? "你已顿悟所有传说奥义。"
                    : "你已领悟所有招式。";
                AppendEventCard(msg, InvasionEventConfigCatalog.MinBackgroundIndex);
                ScrollEventLogToBottom();
                SetNextDayButtonMode(NextDayButtonMode.Normal);
                return;
            }

            var picker = SkillPickThreeModalView.GetOrCreate(canvasRectCache);
            if (picker == null)
            {
                UnityEngine.Debug.LogWarning("[InvasionBattleModal2View] 三选一界面创建失败，跳过并恢复常态。");
                SetNextDayButtonMode(NextDayButtonMode.Normal);
                return;
            }
            picker.Show(quality, options, OnSkillPicked);
        }

        private void OnSkillPicked(BattleSkillConfig skill)
        {
            if (skill != null && !string.IsNullOrEmpty(skill.skillId))
            {
                if (acquiredSkillIds.Add(skill.skillId))
                    AddSkillIconToStrip(skill);
            }
            SetNextDayButtonMode(NextDayButtonMode.Normal);
        }

        // ============================================================
        // 老虎机抽奖事件（SPEC §12.12：三轴 slot3 / 五轴 slot5）
        // ============================================================
        /// <summary>从事件奖励解析老虎机轴数：Slot5→5，Slot3→3，无则默认 3。</summary>
        private static int FindSlotReelCount(List<InvasionEventReward> rewards)
        {
            if (rewards != null)
            {
                for (int i = 0; i < rewards.Count; i++)
                {
                    if (rewards[i] == null)
                        continue;
                    if (rewards[i].kind == InvasionEventRewardKind.Slot5)
                        return 5;
                    if (rewards[i].kind == InvasionEventRewardKind.Slot3)
                        return 3;
                }
            }
            return 3;
        }

        private void OpenSlotMachine(int reelCount)
        {
            EnsureEventsLoaded();
            int rc = reelCount == 5 ? 5 : 3;

            var slot = SlotMachineModalView.GetOrCreate(canvasRectCache, rc);
            if (slot == null)
            {
                UnityEngine.Debug.LogWarning("[InvasionBattleModal2View] 老虎机界面创建失败，跳过并恢复常态。");
                SetNextDayButtonMode(NextDayButtonMode.Normal);
                return;
            }
            var flyTargetRt = detailAttrButton != null
                ? detailAttrButton.transform as RectTransform
                : null;
            slot.Show(rc, attrEnhanceCatalog, OnSlotComplete, flyTargetRt);
        }

        private void OnSlotComplete(List<SlotMachineResultItem> results)
        {
            if (results != null && results.Count > 0)
            {
                var summary = SlotMachineResultText.FormatResultSummary(results);
                bool anyApplied = false;
                for (int i = 0; i < results.Count; i++)
                {
                    var item = results[i];
                    if (item == null || item.cfg == null || item.gain == 0)
                        continue;
                    if (ApplyFlatStat(item.cfg.attrId, item.gain))
                        anyApplied = true;
                }

                if (anyApplied)
                {
                    runStatsDirty = true;
                    RefreshRoleStats();
                    AppendEventCard(summary, InvasionEventConfigCatalog.MinBackgroundIndex);
                }
                else
                {
                    AppendEventCard(SlotMachineResultText.EmptyFallback,
                        InvasionEventConfigCatalog.MinBackgroundIndex);
                }
                ScrollEventLogToBottom();
            }

            SetNextDayButtonMode(NextDayButtonMode.Normal);
        }

        /// <summary>SPEC §12.12.3 / §12.13：把老虎机固定增加值累加到局内属性副本（Life/Attack→runStats，六宫项→runEnhanceBonuses）。</summary>
        private bool ApplyFlatStat(string attrId, int delta)
        {
            if (string.IsNullOrEmpty(attrId) || delta == 0)
                return false;

            if (!AttrEnhanceConfigCatalog.TryNormalizeAttrId(attrId, out string canonical))
            {
                UnityEngine.Debug.LogWarning(
                    "[InvasionBattleModal2View] 属性增强项 attrId 未映射，仅展示不加值：" + attrId);
                return false;
            }

            switch (canonical)
            {
                case AttrEnhanceConfigCatalog.AttrLife:
                    if (runStats == null)
                        return false;
                    runStats.maxHp = Mathf.Max(1, runStats.maxHp + delta);
                    runStats.currentHp = Mathf.Clamp(runStats.currentHp + delta, 0, runStats.maxHp);
                    return true;
                case AttrEnhanceConfigCatalog.AttrAttack:
                    if (runStats == null)
                        return false;
                    runStats.atk = Mathf.Max(0, runStats.atk + delta);
                    return true;
                case "def":
                    if (runStats == null)
                        return false;
                    runStats.def = Mathf.Max(0, runStats.def + delta);
                    return true;
                case "speed":
                    if (runStats == null)
                        return false;
                    runStats.agility = Mathf.Max(0, runStats.agility + delta);
                    return true;
                default:
                    if (!AttrEnhanceConfigCatalog.IsHexRadarAttr(canonical))
                        return false;
                    if (!runEnhanceBonuses.TryGetValue(canonical, out int cur))
                        cur = 0;
                    runEnhanceBonuses[canonical] = Mathf.Max(0, cur + delta);
                    return true;
            }
        }

        /// <summary>SPEC §12.13：读取六宫属性局内累加值（局外初始 0）。</summary>
        public int GetRunEnhanceValue(string attrId)
        {
            if (!AttrEnhanceConfigCatalog.TryNormalizeAttrId(attrId, out string canonical))
                return 0;
            return runEnhanceBonuses.TryGetValue(canonical, out int v) ? v : 0;
        }

        public IReadOnlyDictionary<string, int> GetRunEnhanceBonuses() => runEnhanceBonuses;

        private void OnDetailAttrClicked()
        {
            if (canvasRectCache == null)
                canvasRectCache = panelRt != null ? panelRt.parent as RectTransform : null;
            var modal = DetailAttributeModalView.GetOrCreate(canvasRectCache);
            if (modal == null)
                return;
            modal.Show(runStats, runEnhanceBonuses);
        }

        /// <summary>SPEC §12.13：兼容缺 DetailAttrButton 的旧预制体，运行时补建。</summary>
        private void EnsureDetailAttrButton()
        {
            if (detailAttrButton != null)
                return;

            var midArea = FindDescendantRect(MiddleAreaName);
            if (midArea == null)
                return;

            detailAttrButton = CreateDetailAttrButton(midArea);
            if (detailAttrButton != null && wired)
            {
                detailAttrButton.onClick.RemoveAllListeners();
                detailAttrButton.onClick.AddListener(OnDetailAttrClicked);
            }
        }

        private static Button CreateDetailAttrButton(RectTransform midArea)
        {
            var rt = BottomNavAttachedScreenLayout.CreateChildRect(
                midArea, DetailAttrButtonName,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-60f, 0f), DetailAttrButtonSize);

            var img = rt.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = true;
            var sprite = Resources.Load<Sprite>(ResDetailAttrButton);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
            }
            else
            {
                img.color = new Color(0.5f, 0.55f, 0.7f, 1f);
            }

            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;

            var labelRt = BottomNavAttachedScreenLayout.CreateChildRect(
                rt, "Label", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, -52f), new Vector2(120f, 36f));
            var txt = labelRt.gameObject.AddComponent<Text>();
            txt.text = "详细属性";
            txt.font = FarmGridView.LoadBuiltinFont();
            txt.fontSize = 22;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.raycastTarget = false;

            return btn;
        }

        private void AddSkillIconToStrip(BattleSkillConfig skill)
        {
            EnsureSkillStrip();
            if (skillStrip == null || skill == null)
                return;

            int index = skillIconCount;
            int col = index % SkillIconsPerRow;
            int row = index / SkillIconsPerRow;
            float x = SkillStripFirstPos.x + col * (SkillIconTargetSize + SkillIconGap);
            float y = SkillStripFirstPos.y - row * SkillIconRowStep;

            var iconRt = BottomNavAttachedScreenLayout.CreateChildRect(
                skillStrip, "SkillIcon_" + skill.skillId,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, y), new Vector2(SkillIconStartSize, SkillIconStartSize));

            var img = iconRt.gameObject.AddComponent<Image>();
            var sprite = LoadSkillIcon(skill.iconName);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.preserveAspect = true;
                img.color = Color.white;
            }
            else
            {
                img.color = new Color(0.85f, 0.8f, 0.5f, 1f);
            }
            img.raycastTarget = false;

            skillIconCount += 1;
            StartCoroutine(ShrinkIconRoutine(iconRt,
                new Vector2(SkillIconStartSize, SkillIconStartSize),
                new Vector2(SkillIconTargetSize, SkillIconTargetSize),
                SkillIconShrinkDuration));
        }

        private static IEnumerator ShrinkIconRoutine(RectTransform rt, Vector2 from, Vector2 to, float duration)
        {
            if (rt == null)
                yield break;
            float elapsed = 0f;
            rt.sizeDelta = from;
            while (elapsed < duration && rt != null)
            {
                elapsed += Time.deltaTime;
                float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                float eased = 1f - (1f - t) * (1f - t); // ease-out
                rt.sizeDelta = Vector2.Lerp(from, to, eased);
                yield return null;
            }
            if (rt != null)
                rt.sizeDelta = to;
        }

        private Sprite LoadSkillIcon(string iconName)
        {
            string path = SkillConfigCatalog.IconResourcePath(iconName);
            return string.IsNullOrEmpty(path) ? null : Resources.Load<Sprite>(path);
        }

        private void OnCloseClicked()
        {
            Hide();
        }

        private void RefreshDayLabel()
        {
            if (dayLabel != null)
                dayLabel.text = $"第 {currentDay} 天";
        }

        // ============================================================
        // 事件日志（SPEC §12.11.5：滚动 + /n 多条 + 九宫格卡）
        // ============================================================
        private void ClearEventLog()
        {
            if (eventContent == null)
                return;
            for (int i = eventContent.childCount - 1; i >= 0; i--)
                Destroy(eventContent.GetChild(i).gameObject);
        }

        private void AppendEventCard(string richText, int backgroundIndex)
        {
            if (eventContent == null)
                return;

            var cardGo = new GameObject("EventCard");
            var cardRt = cardGo.AddComponent<RectTransform>();
            cardRt.SetParent(eventContent, false);

            var img = cardGo.AddComponent<Image>();
            var frame = LoadFrameSprite(backgroundIndex);
            if (frame != null)
            {
                img.sprite = frame;
                img.type = Image.Type.Sliced;
                img.fillCenter = true;
                img.color = Color.white;
            }
            else
            {
                img.color = new Color(0f, 0f, 0f, 0.35f);
            }
            img.raycastTarget = false;

            var vlg = cardGo.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(48, 48, 30, 30);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = cardGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textGo = new GameObject("Text");
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.SetParent(cardRt, false);
            var txt = textGo.AddComponent<Text>();
            txt.font = FarmGridView.LoadBuiltinFont();
            txt.fontSize = 34;
            txt.color = Color.black;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.supportRichText = true;
            txt.raycastTarget = false;
            txt.text = richText;
        }

        private Sprite LoadFrameSprite(int index)
        {
            if (frameSpriteCache.TryGetValue(index, out var cached))
                return cached;
            var sprite = Resources.Load<Sprite>(ResEventFramePrefix + index);
            frameSpriteCache[index] = sprite;
            return sprite;
        }

        private void ScrollEventLogToBottom()
        {
            if (eventScrollRect == null)
                return;
            Canvas.ForceUpdateCanvases();
            eventScrollRect.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
        }

        private void EnsureEventLog()
        {
            if (eventContent != null)
                return;
            var bottom = FindDescendantRect(BottomAreaName);
            if (bottom == null)
                return;
            var legacy = FindDescendantByName(transform, "EventLabel");
            if (legacy != null)
                Destroy(legacy.gameObject);
            BuildEventLogInto(bottom);
        }

        /// <summary>SPEC §12.11.5：在 BottomArea 内构建可上下滑动的事件日志（Viewport/Content，老在上、新在下）。</summary>
        private void BuildEventLogInto(RectTransform bottomArea)
        {
            var scrollRt = BottomNavAttachedScreenLayout.CreateChildRect(
                bottomArea, EventScrollName, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(40f, 230f);
            scrollRt.offsetMax = new Vector2(-40f, -90f);

            var scrollBg = scrollRt.gameObject.AddComponent<Image>();
            scrollBg.color = new Color(0f, 0f, 0f, 0.18f);
            scrollBg.raycastTarget = true;

            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;

            var viewportRt = BottomNavAttachedScreenLayout.CreateChildRect(
                scrollRt, "Viewport", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(viewportRt);
            var viewportImg = viewportRt.gameObject.AddComponent<Image>();
            viewportImg.color = new Color(1f, 1f, 1f, 0.01f);
            viewportRt.gameObject.AddComponent<RectMask2D>();

            var contentRt = BottomNavAttachedScreenLayout.CreateChildRect(
                viewportRt, EventContentName,
                new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = new Vector2(0f, contentRt.offsetMin.y);
            contentRt.offsetMax = new Vector2(0f, contentRt.offsetMax.y);

            var vlg = contentRt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.spacing = 12f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var contentFitter = contentRt.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRt;
            scroll.content = contentRt;

            eventScrollRect = scroll;
            eventContent = contentRt;
        }

        // ============================================================
        // 中部属性区（读取玩法局内属性副本 runStats）
        // ============================================================
        private void EnsureRoleStatsSubscription()
        {
            if (roleStatsSubscribed || service == null)
                return;
            service.OnRoleStatsChanged += OnServiceRoleStatsChanged;
            roleStatsSubscribed = true;
        }

        private void OnServiceRoleStatsChanged()
        {
            // 未发生玩法内奖励改动时，跟随全局基线刷新；否则保留局内改动不被覆盖。
            if (runStatsDirty)
                return;
            runStats = CloneRoleStats(service != null ? service.GetRoleStats() : null);
            InitRunEnhanceBonusesFromStats();
            RefreshRoleStats();
        }

        private void RefreshRoleStats()
        {
            RoleStats role = runStats;
            if (role == null)
            {
                if (hpText != null) hpText.text = "-- / --";
                if (atkText != null) atkText.text = "--";
                if (speedText != null) speedText.text = "--";
                return;
            }

            if (hpText != null) hpText.text = $"{role.currentHp} / {role.maxHp}";
            if (atkText != null) atkText.text = $"{role.atk}";
            if (speedText != null) speedText.text = $"{role.agility}";
        }

        // ============================================================
        // 上部玩家角色（SkeletonGraphic 运行时构建）
        // ============================================================
        private void EnsurePlayerBuilt()
        {
            if (playerBuilt || playerSlot == null)
                return;
            TryBuildSkeletonGraphic(ResPlayerPrefab, playerSlot);
            playerBuilt = true;
        }

        private void TryBuildSkeletonGraphic(string resourcesPath, RectTransform parent)
        {
            var prefab = Resources.Load<GameObject>(resourcesPath);
            if (prefab == null)
            {
                UnityEngine.Debug.LogWarning("[InvasionBattleModal2View] 缺少角色预制体：Resources/" + resourcesPath
                    + "。回退为占位色块。");
                BuildFallbackBlock(parent);
                return;
            }

            var probe = Instantiate(prefab);
            probe.SetActive(false);
            var srcAnim = probe.GetComponent<SkeletonAnimation>()
                ?? probe.GetComponentInChildren<SkeletonAnimation>(true);
            var dataAsset = srcAnim != null ? srcAnim.skeletonDataAsset : null;
            Destroy(probe);

            if (dataAsset == null)
            {
                UnityEngine.Debug.LogWarning("[InvasionBattleModal2View] 预制体 " + resourcesPath
                    + " 未找到 SkeletonDataAsset，回退为占位色块。");
                BuildFallbackBlock(parent);
                return;
            }

            var shader = Shader.Find(SkeletonGraphicShaderName);
            if (shader == null)
            {
                UnityEngine.Debug.LogError("[InvasionBattleModal2View] 未找到 Shader：" + SkeletonGraphicShaderName);
                BuildFallbackBlock(parent);
                return;
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
            // SPEC §12.11.4（v3.173）：默认水平镜像 1 次（同 §12.3 朝向路径）。
            rt.localScale = new Vector3(-1f, 1f, 1f);

            var skel = SkeletonGraphic.AddSkeletonGraphicComponent(roleGo, dataAsset, uiMaterial);
            if (skel == null || !skel.IsValid)
            {
                UnityEngine.Debug.LogWarning("[InvasionBattleModal2View] SkeletonGraphic 构建失败，回退为占位色块。");
                Destroy(roleGo);
                BuildFallbackBlock(parent);
                return;
            }
            skel.raycastTarget = false;
            playerSkeleton = skel;
            TryPlayFirstLoopAnimation(skel);
        }

        private static void TryPlayFirstLoopAnimation(SkeletonGraphic skel)
        {
            if (skel == null || skel.Skeleton == null || skel.Skeleton.Data == null)
                return;
            var animations = skel.Skeleton.Data.Animations;
            if (animations == null || animations.Count == 0)
                return;
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

        // SPEC §12.11.5（v3.173）：切换玩家角色动画（移动/待机），按候选链解析，找不到回退首条。
        private void PlayPlayerMoveLoop()
        {
            PlayPlayerLoopByCandidates(MoveAnimCandidates);
        }

        private void PlayPlayerIdleLoop()
        {
            PlayPlayerLoopByCandidates(IdleAnimCandidates);
        }

        private void PlayPlayerLoopByCandidates(string[] candidates)
        {
            var skel = playerSkeleton;
            if (skel == null || !skel.IsValid || skel.AnimationState == null
                || skel.Skeleton == null || skel.Skeleton.Data == null)
                return;

            var data = skel.Skeleton.Data;
            Spine.Animation chosen = null;
            if (candidates != null)
            {
                for (int i = 0; i < candidates.Length && chosen == null; i++)
                    chosen = FindAnimationCaseInsensitive(data, candidates[i]);
            }
            if (chosen == null)
            {
                var animations = data.Animations;
                if (animations != null && animations.Count > 0)
                    chosen = animations.Items[0];
            }
            if (chosen != null)
                skel.AnimationState.SetAnimation(0, chosen, true);
        }

        private static Spine.Animation FindAnimationCaseInsensitive(Spine.SkeletonData data, string animationName)
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

        private static void BuildFallbackBlock(RectTransform parent)
        {
            var blockRt = BottomNavAttachedScreenLayout.CreateChildRect(
                parent, "PlayerFallback",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(240f, 480f));
            var img = blockRt.gameObject.AddComponent<Image>();
            img.color = new Color(0.35f, 0.45f, 0.6f, 0.9f);
            img.raycastTarget = false;
        }

        // ============================================================
        // 字段回填（预制体路径按名查找）
        // ============================================================
        private void EnsureFieldsFromHierarchy()
        {
            if (panelRt == null)
                panelRt = transform as RectTransform;
            if (topArea == null)
                topArea = FindDescendantRect(TopAreaName);
            if (playerSlot == null)
                playerSlot = FindDescendantRect(PlayerSlotName);
            if (hpText == null)
                hpText = FindDescendantText("HpText");
            if (atkText == null)
                atkText = FindDescendantText("AtkText");
            if (speedText == null)
                speedText = FindDescendantText("SpeedText");
            if (eventScrollRect == null)
            {
                var t = FindDescendantByName(transform, EventScrollName);
                if (t != null)
                    eventScrollRect = t.GetComponent<ScrollRect>();
            }
            if (eventContent == null)
                eventContent = FindDescendantRect(EventContentName);
            if (dayLabel == null)
                dayLabel = FindDescendantText("DayLabel");
            if (nextDayButton == null)
                nextDayButton = FindDescendantButton("NextDayButton");
            if (closeButton == null)
                closeButton = FindDescendantButton("CloseButton");
            if (detailAttrButton == null)
                detailAttrButton = FindDescendantButton(DetailAttrButtonName);
        }

        private RectTransform FindDescendantRect(string nodeName)
        {
            var t = FindDescendantByName(transform, nodeName);
            return t as RectTransform;
        }

        private Text FindDescendantText(string nodeName)
        {
            var t = FindDescendantByName(transform, nodeName);
            return t != null ? t.GetComponent<Text>() : null;
        }

        private Button FindDescendantButton(string nodeName)
        {
            var t = FindDescendantByName(transform, nodeName);
            return t != null ? t.GetComponent<Button>() : null;
        }

        private static Transform FindDescendantByName(Transform root, string name)
        {
            if (root == null)
                return null;
            if (string.Equals(root.name, name, StringComparison.Ordinal))
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDescendantByName(root.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }

        // ============================================================
        // 运行时代码回退（缺预制体时，与生成器布局对齐）
        // ============================================================
        private static GameObject BuildRuntimeFallback(RectTransform canvasRect)
        {
            var rootGo = new GameObject(PanelObjectName, typeof(RectTransform));
            var rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.SetParent(canvasRect, false);
            BottomNavAttachedScreenLayout.StretchFull(rootRt);

            var view = rootGo.AddComponent<InvasionBattleModal2View>();
            BottomNavAttachedScreenLayout.AddStretchedResourcesBackground(
                rootRt, ResBackground, nameof(InvasionBattleModal2View));

            // 上部：角色展示区 + PlayerSlot 挂载点
            var topArea = CreateArea(rootRt, TopAreaName, new Vector2(0f, 0.55f), new Vector2(1f, 1f));
            view.playerSlot = BottomNavAttachedScreenLayout.CreateChildRect(
                topArea, PlayerSlotName,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, CharacterSize);
            view.playerSlot.localScale = new Vector3(CharacterScale, CharacterScale, 1f);

            // 中部：属性区
            var midArea = CreateArea(rootRt, MiddleAreaName, new Vector2(0f, 0.30f), new Vector2(1f, 0.55f));
            view.hpText = CreateAreaText(midArea, "HpText", new Vector2(0f, 40f), new Vector2(900f, 60f),
                "-- / --", 40, TextAnchor.MiddleCenter);
            view.atkText = CreateAreaText(midArea, "AtkText", new Vector2(-200f, -40f), new Vector2(400f, 60f),
                "--", 40, TextAnchor.MiddleCenter);
            view.speedText = CreateAreaText(midArea, "SpeedText", new Vector2(200f, -40f), new Vector2(400f, 60f),
                "--", 40, TextAnchor.MiddleCenter);
            view.detailAttrButton = CreateDetailAttrButton(midArea);

            // 下部：事件日志 + 天数 + 下一天按钮
            var bottomArea = CreateArea(rootRt, BottomAreaName, new Vector2(0f, 0f), new Vector2(1f, 0.30f));
            view.dayLabel = CreateAnchoredText(bottomArea, "DayLabel",
                new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(600f, 60f),
                "第 0 天", 40, TextAnchor.UpperCenter);
            view.BuildEventLogInto(bottomArea);
            view.nextDayButton = CreateNextDayButton(bottomArea);

            // 关闭按钮（右上角，挂根节点）
            view.closeButton = CreateCloseButton(rootRt);

            return rootGo;
        }

        private static RectTransform CreateArea(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rt = BottomNavAttachedScreenLayout.CreateChildRect(
                parent, name, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static Text CreateAreaText(RectTransform parent, string name, Vector2 pos, Vector2 size,
            string content, int fontSize, TextAnchor anchor)
        {
            return CreateAnchoredText(parent, name, new Vector2(0.5f, 0.5f), pos, size, content, fontSize, anchor);
        }

        private static Text CreateAnchoredText(RectTransform parent, string name, Vector2 anchorPivot,
            Vector2 pos, Vector2 size, string content, int fontSize, TextAnchor anchor)
        {
            var rt = BottomNavAttachedScreenLayout.CreateChildRect(parent, name, anchorPivot, anchorPivot, pos, size);
            rt.pivot = anchorPivot;
            var txt = rt.gameObject.AddComponent<Text>();
            txt.text = content;
            txt.font = FarmGridView.LoadBuiltinFont();
            txt.fontSize = fontSize;
            txt.alignment = anchor;
            txt.color = Color.white;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            return txt;
        }

        private static Button CreateNextDayButton(RectTransform parent)
        {
            var rt = BottomNavAttachedScreenLayout.CreateChildRect(
                parent, "NextDayButton",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 130f), NextDayButtonSize);
            rt.pivot = new Vector2(0.5f, 0f);

            var img = rt.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = true;
            var sprite = Resources.Load<Sprite>(ResNextDayButton);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
            }
            else
            {
                UnityEngine.Debug.LogWarning("[InvasionBattleModal2View] 缺失精灵：" + ResNextDayButton);
                img.color = new Color(0.9f, 0.55f, 0.2f, 0.95f);
            }

            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;

            var labelRt = BottomNavAttachedScreenLayout.CreateChildRect(
                rt, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(labelRt);
            var txt = labelRt.gameObject.AddComponent<Text>();
            txt.font = FarmGridView.LoadBuiltinFont();
            txt.fontSize = 44;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.black;
            txt.fontStyle = FontStyle.Bold;
            txt.raycastTarget = false;
            txt.text = "下一天";

            return btn;
        }

        private static Button CreateCloseButton(RectTransform parent)
        {
            var rt = BottomNavAttachedScreenLayout.CreateChildRect(
                parent, "CloseButton",
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-40f, -40f), CloseButtonSize);
            rt.pivot = new Vector2(1f, 1f);

            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.5f);
            img.raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;

            var labelRt = BottomNavAttachedScreenLayout.CreateChildRect(
                rt, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(labelRt);
            var txt = labelRt.gameObject.AddComponent<Text>();
            txt.text = "X";
            txt.font = FarmGridView.LoadBuiltinFont();
            txt.fontSize = 48;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.raycastTarget = false;

            return btn;
        }

        private void OnDestroy()
        {
            if (roleStatsSubscribed && service != null)
            {
                service.OnRoleStatsChanged -= OnServiceRoleStatsChanged;
                roleStatsSubscribed = false;
            }
            if (instance == this)
                instance = null;
        }
    }
}
