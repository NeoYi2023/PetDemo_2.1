// PlantingService：SPEC §4.1 / §6 / §10 的运行时实现，纯 C# 不依赖 Unity（除 UnityEngine.Debug.Log 通过 ILogger 间接）。
// 这里直接使用 UnityEngine.Debug 以方便 Editor 期看 Warning，但不依赖具体 GameObject。
using System;
using System.Collections.Generic;
using PetDemo.Core;
using PetDemo.Save;
using UnityEngine;

namespace PetDemo.Farm
{
    public class PlantingService : IPlantingService
    {
        public const int FarmTileTotal = 20;

        // SPEC §4.1.10 产品配置（v3.27）：为 false 时不检测/触发同行四田四格变异。
        private const bool kRowMutationTriggerEnabled = false;

        // SPEC §9.5 (v3.87)：关闭玩家浇水/施肥的即时数据写入；保留路径供回滚。
        private const bool UseImmediateWaterFertilizeEffects = false;

        // SPEC §9.1 (v3.71)：统一按钮已受理、尚未 CommitWaterTile 的浇水次数（1..3）。
        private readonly Dictionary<string, int> _pendingWaterCounts = new Dictionary<string, int>();
        private readonly HashSet<string> _pendingFertilizeTiles = new HashSet<string>();

        // SPEC §B.8 开局引导性农田预置：构造期硬编码两条「已待收获」预置，
        // 与 §10.1 / §10.2 优先级链联通，让玩家进入主界面立即可执行 Harvest。
        // 选取 lajiao（afterHarvest=Wilt） + fanqie（afterHarvest=Regrow），
        // 让开局两次收获分别覆盖 §4.1.5 的两条结算分支。
        private static readonly (int orderIndex, string plantConfigId)[] kInitialGuidancePresets =
        {
            (2, "lajiao"),
            (3, "fanqie"),
        };

        public static PlantingService Instance { get; private set; }

        private readonly GameSession session;
        private readonly Dictionary<string, CropTile> tileById;
        private readonly Dictionary<string, PlantInstance> plantById;
        private readonly Dictionary<string, PlantConfig> configById;
        private readonly Dictionary<string, FertilizerType> fertilizerTypeById;
        // SPEC §4.1.10 (v3.18)：变异机制相关索引。
        private readonly Dictionary<string, MutationPlant> mutationById;
        private readonly Dictionary<string, PetConfig> petConfigById;
        private readonly Dictionary<string, SkillConfig> skillConfigById;
        private string focusTileId;
        private int plantInstanceSeq;
        private int mutationInstanceSeq;
        private int petInstanceSeq;
        private readonly Dictionary<string, PetInstance> petById;

        // SPEC §9.1 (v3.70)：统一按钮浇水 pending 增删。
        public event Action<string> OnWaterPendingChanged;
        public event Action<string> OnTileFlagsChanged;
        public event Action<string, PlantState> OnPlantStateChanged;
        public event Action<string, int> OnAppearanceNodeChanged;
        public event Action<string> OnPlantTileInteracted;
        public event Action<string> OnFocusChanged;
        public event Action<string, ActionType> OnUnifiedActionExecuted;
        public event Action OnSeedBagChanged;
        public event Action<string, SeedPackQuality, string> OnSeedRolledFromPack;
        public event Action<string> OnPestEventTriggered;
        public event Action OnRoleStatsChanged;
        public event Action OnFruitBagChanged;
        public event Action<string, string, int> OnHarvestFruitReady;
        public event Action OnFertilizerBagChanged;
        public event Action<string, string> OnFertilizeApplied;
        public event Action<string> OnMutationCreated;
        public event Action<string, MutationKind, string> OnMutationHarvested;
        public event Action OnPetBagChanged;
        public event Action OnPetDeploymentChanged;
        // SPEC §6 / §9.8.12 (v3.40)：食物 / 体力。
        public event Action OnFoodBagChanged;
        public event Action<int, int> OnStaminaChanged;

        // SPEC §9.8.12 (v3.40)：食物配置索引（与 fertilizerTypeById 对齐）。
        private Dictionary<string, FoodConfig> foodConfigById;

        public int FarmTileCount => session.farmTiles.Count;

        public PlantingService(
            List<PlantConfig> plantConfigs,
            List<SeedPackContents> packContents,
            InitialInventory initialInventory = null,
            List<FertilizerType> fertilizerTypes = null,
            List<PetConfig> petConfigs = null,
            List<SkillConfig> skillConfigs = null,
            List<FoodConfig> foodConfigs = null,
            GameSaveSnapshot loadSnapshot = null)
        {
            // SPEC §9.8.12 (v3.40)：foodConfigs 缺省 → 使用 Demo 默认 2 种食物。
            var effectiveFoodConfigs = (foodConfigs != null && foodConfigs.Count > 0)
                ? foodConfigs
                : PlantConfigCatalog.BuildDefaultFoodConfigs();

            session = new GameSession
            {
                plantConfigs = plantConfigs ?? new List<PlantConfig>(),
                packContents = packContents ?? new List<SeedPackContents>(),
                fertilizerTypes = fertilizerTypes ?? new List<FertilizerType>(),
                petConfigs = petConfigs ?? new List<PetConfig>(),
                skillConfigs = skillConfigs ?? new List<SkillConfig>(),
                foodConfigs = effectiveFoodConfigs,
                petBag = new PlayerPetBag(),
                petDeployment = new PetDeployment(),
                restrictionProfile = new RestrictionProfile
                {
                    fieldPetLimit = PetDeploymentRules.DefaultFieldPetLimit,
                    accepted = true,
                },
            };

            if (loadSnapshot != null)
            {
                GameSaveSnapshot.ApplyToSession(loadSnapshot, session);
                plantInstanceSeq = loadSnapshot.plantInstanceSeq;
                mutationInstanceSeq = loadSnapshot.mutationInstanceSeq;
                petInstanceSeq = loadSnapshot.petInstanceSeq;
                focusTileId = loadSnapshot.focusTileId;
            }
            else
            {
                for (int i = 1; i <= FarmTileTotal; i++)
                {
                    session.farmTiles.Add(new CropTile
                    {
                        tileId = "tile-" + i.ToString("D2"),
                        orderIndex = i,
                        planting = PlantingFlag.AwaitingSeed,
                        fertilizer = FertilizerFlag.None,
                        water = WaterStage.Empty,
                        pest = PestFlag.None,
                        moleTheft = MoleTheftFlag.None,
                        harvest = HarvestFlag.None,
                        plantInstanceId = null,
                    });
                }
            }

            ClampSessionFarmTiles();

            tileById = new Dictionary<string, CropTile>();
            plantById = new Dictionary<string, PlantInstance>();
            configById = new Dictionary<string, PlantConfig>();
            fertilizerTypeById = new Dictionary<string, FertilizerType>();
            mutationById = new Dictionary<string, MutationPlant>();
            petConfigById = new Dictionary<string, PetConfig>();
            petById = new Dictionary<string, PetInstance>();
            skillConfigById = new Dictionary<string, SkillConfig>();
            foodConfigById = new Dictionary<string, FoodConfig>();
            PopulateRuntimeIndexes();

            if (loadSnapshot == null)
            {
                // SPEC §B.5：初始仓库装载点。未传 initialInventory 时回退到内置默认值。
                var inv = initialInventory ?? PlantConfigCatalog.BuildDefaultInitialInventory();
                if (inv.seeds != null)
                {
                    for (int i = 0; i < inv.seeds.Count; i++)
                    {
                        var s = inv.seeds[i];
                        if (s == null || string.IsNullOrEmpty(s.plantConfigId) || s.count <= 0)
                            continue;
                        session.seedBag.seeds.Add(new SeedStack { plantConfigId = s.plantConfigId, count = s.count });
                    }
                }
                if (inv.packs != null)
                {
                    for (int i = 0; i < inv.packs.Count; i++)
                    {
                        var p = inv.packs[i];
                        if (p == null || p.count <= 0)
                            continue;
                        session.seedBag.seedPacks.Add(new SeedPackStack { quality = p.quality, count = p.count });
                    }
                }
                session.seedBag.active = null;

                // SPEC §B.5 / §9.7：肥料初始堆叠装载（P0 默认空）。
                if (inv.fertilizers != null)
                {
                    for (int i = 0; i < inv.fertilizers.Count; i++)
                    {
                        var f = inv.fertilizers[i];
                        if (f == null || string.IsNullOrEmpty(f.fertilizerId) || f.count <= 0)
                            continue;
                        session.fertilizerBag.stacks.Add(new FertilizerStack
                        {
                            fertilizerId = f.fertilizerId,
                            count = f.count,
                        });
                    }
                }
                session.fertilizerBag.activeId = null;

                session.foodBag = PlantConfigCatalog.BuildDefaultFoodBag(inv);

                // SPEC §9.14：新存档初始化创角好友目录；创角状态默认未创建。
                session.friends = FriendCatalog.BuildDefault();
                session.characterCreation = new CharacterCreationState();

                // SPEC §B.8：开局引导性农田预置。必须在 Instance 暴露之前完成写入，
                // 因为本路径不触发任何 §6 事件，UI 后续通过 RefreshAllSlots() 主动拉取快照。
                ApplyInitialGuidanceTiles();
            }

            Instance = this;
        }

        /// <summary>SPEC §13：导出当前会话快照。</summary>
        public GameSaveSnapshot ExportSnapshot() => GameSaveSnapshot.FromPlantingService(this);

        /// <summary>SPEC §9.8.16：主线竞技场入口是否已永久解锁。</summary>
        public bool IsMainStoryArenaEntryUnlocked() =>
            session?.uiProgress != null && session.uiProgress.mainStoryArenaEntryUnlocked;

        /// <summary>SPEC §9.8.16：写入解锁并随 §13 自动存档。</summary>
        public void SetMainStoryArenaEntryUnlocked(bool unlocked)
        {
            if (session == null)
                return;
            if (session.uiProgress == null)
                session.uiProgress = new UiProgress();
            session.uiProgress.mainStoryArenaEntryUnlocked = unlocked;
        }

        /// <summary>SPEC §9.8.8.7：已挑战胜利的最高主线关卡编号（0 = 未通关任何关）。</summary>
        public int GetMainStoryHighestClearedLevel() =>
            session?.uiProgress?.mainStoryHighestClearedLevel ?? 0;

        /// <summary>SPEC §9.8.8.7：记录主线关卡胜利，仅当 levelNumber 大于当前最高通关关时推进。</summary>
        public bool TryRecordMainStoryLevelCleared(int levelNumber)
        {
            if (session == null || levelNumber <= 0)
                return false;
            if (session.uiProgress == null)
                session.uiProgress = new UiProgress();
            int current = session.uiProgress.mainStoryHighestClearedLevel;
            if (levelNumber <= current)
                return false;
            session.uiProgress.mainStoryHighestClearedLevel = levelNumber;
            return true;
        }

        internal GameSession GetSessionForSave() => session;

        internal int GetPlantInstanceSeqForSave() => plantInstanceSeq;

        internal int GetMutationInstanceSeqForSave() => mutationInstanceSeq;

        internal int GetPetInstanceSeqForSave() => petInstanceSeq;

        internal string GetFocusTileIdForSave() => focusTileId;

        // SPEC §9.1（v3.109）：农田规模 20；读档时剔除 orderIndex>20 的遗留 24 格存档数据。
        private void ClampSessionFarmTiles()
        {
            var removedTileIds = new HashSet<string>();
            for (int i = session.farmTiles.Count - 1; i >= 0; i--)
            {
                var tile = session.farmTiles[i];
                if (tile == null || tile.orderIndex <= FarmTileTotal)
                    continue;
                removedTileIds.Add(tile.tileId);
                session.farmTiles.RemoveAt(i);
            }

            if (removedTileIds.Count == 0)
                return;

            for (int i = session.plants.Count - 1; i >= 0; i--)
            {
                var plant = session.plants[i];
                if (plant != null && removedTileIds.Contains(plant.tileId))
                    session.plants.RemoveAt(i);
            }

            for (int i = session.mutations.Count - 1; i >= 0; i--)
            {
                var mutation = session.mutations[i];
                if (mutation?.tileIds == null)
                    continue;
                for (int j = 0; j < mutation.tileIds.Count; j++)
                {
                    if (!removedTileIds.Contains(mutation.tileIds[j]))
                        continue;
                    session.mutations.RemoveAt(i);
                    break;
                }
            }

            if (!string.IsNullOrEmpty(focusTileId) && removedTileIds.Contains(focusTileId))
                focusTileId = null;
        }

        private void PopulateRuntimeIndexes()
        {
            tileById.Clear();
            foreach (var t in session.farmTiles)
            {
                if (t != null && !string.IsNullOrEmpty(t.tileId))
                    tileById[t.tileId] = t;
            }

            plantById.Clear();
            if (session.plants != null)
            {
                for (int i = 0; i < session.plants.Count; i++)
                {
                    var p = session.plants[i];
                    if (p != null && !string.IsNullOrEmpty(p.instanceId))
                        plantById[p.instanceId] = p;
                }
            }

            configById.Clear();
            foreach (var c in session.plantConfigs)
                if (c != null && !string.IsNullOrEmpty(c.id))
                    configById[c.id] = c;

            fertilizerTypeById.Clear();
            foreach (var ft in session.fertilizerTypes)
                if (ft != null && !string.IsNullOrEmpty(ft.id))
                    fertilizerTypeById[ft.id] = ft;

            mutationById.Clear();
            if (session.mutations != null)
            {
                for (int i = 0; i < session.mutations.Count; i++)
                {
                    var m = session.mutations[i];
                    if (m != null && !string.IsNullOrEmpty(m.instanceId))
                        mutationById[m.instanceId] = m;
                }
            }

            petConfigById.Clear();
            foreach (var p in session.petConfigs)
                if (p != null && !string.IsNullOrEmpty(p.id))
                    petConfigById[p.id] = p;

            petById.Clear();
            if (session.petBag?.owned != null)
            {
                for (int i = 0; i < session.petBag.owned.Count; i++)
                {
                    var inst = session.petBag.owned[i];
                    if (inst != null && !string.IsNullOrEmpty(inst.instanceId))
                        petById[inst.instanceId] = inst;
                }
            }

            skillConfigById.Clear();
            foreach (var s in session.skillConfigs)
                if (s != null && !string.IsNullOrEmpty(s.id))
                    skillConfigById[s.id] = s;

            foodConfigById.Clear();
            for (int i = 0; i < session.foodConfigs.Count; i++)
            {
                var fc = session.foodConfigs[i];
                if (fc != null && !string.IsNullOrEmpty(fc.id))
                    foodConfigById[fc.id] = fc;
            }
        }

        // ============================================================
        // SPEC §6 接口
        // ============================================================
        public void SelectActive(ActiveKind kind, string id)
        {
            session.seedBag.active = new ActiveSelection { kind = kind, id = id };
            OnSeedBagChanged?.Invoke();
        }

        public ActiveSelection GetActive() => session.seedBag.active;

        public string RollSeedPack(SeedPackQuality quality)
        {
            SeedPackContents target = null;
            for (int i = 0; i < session.packContents.Count; i++)
            {
                var c = session.packContents[i];
                if (c != null && c.quality == quality)
                {
                    target = c;
                    break;
                }
            }

            if (target == null || target.entries == null || target.entries.Count == 0)
            {
                UnityEngine.Debug.LogWarning("RollSeedPack: 未找到品质权重表，quality=" + quality);
                return FallbackPlantConfigId();
            }

            float totalWeight = 0f;
            for (int i = 0; i < target.entries.Count; i++)
            {
                var e = target.entries[i];
                if (e == null || string.IsNullOrEmpty(e.plantConfigId))
                    continue;
                if (!configById.ContainsKey(e.plantConfigId))
                    continue;
                if (e.weight <= 0f)
                    continue;
                totalWeight += e.weight;
            }

            if (totalWeight <= 0f)
            {
                UnityEngine.Debug.LogWarning("RollSeedPack: 权重和 <= 0，quality=" + quality);
                return FallbackPlantConfigId();
            }

            float r = UnityEngine.Random.Range(0f, totalWeight);
            float acc = 0f;
            for (int i = 0; i < target.entries.Count; i++)
            {
                var e = target.entries[i];
                if (e == null || string.IsNullOrEmpty(e.plantConfigId))
                    continue;
                if (!configById.ContainsKey(e.plantConfigId))
                    continue;
                if (e.weight <= 0f)
                    continue;

                acc += e.weight;
                if (r <= acc)
                    return e.plantConfigId;
            }

            // 浮点边界兜底：返回最后一个合法条目。
            for (int i = target.entries.Count - 1; i >= 0; i--)
            {
                var e = target.entries[i];
                if (e == null || string.IsNullOrEmpty(e.plantConfigId))
                    continue;
                if (!configById.ContainsKey(e.plantConfigId))
                    continue;
                if (e.weight <= 0f)
                    continue;
                return e.plantConfigId;
            }

            return FallbackPlantConfigId();
        }

        public bool ExecuteUnifiedAction()
        {
            var preview = FindNextAction();
            if (preview.HasValue && tileById.TryGetValue(preview.Value.tileId, out var tile))
            {
                SetFocusTile(tile.tileId);
                if (preview.Value.action == ActionType.Water && !UseImmediateWaterFertilizeEffects)
                {
                    if (!CanQueueWaterRequest(tile))
                        return false;
                    _pendingWaterCounts[tile.tileId] = GetPendingWaterCount(tile.tileId) + 1;
                    NotifyWaterPendingChanged(tile.tileId);
                    OnUnifiedActionExecuted?.Invoke(tile.tileId, preview.Value.action);
                    return true;
                }

                Apply(preview.Value.action, tile);
                OnUnifiedActionExecuted?.Invoke(tile.tileId, preview.Value.action);
                return true;
            }

            SetFocusTile(null);
            return false;
        }

        // SPEC §9.5 (v3.87)：work_1 开始后 0.5s 由 MainRoleCunminPresenter 调用。
        public bool CommitWaterTile(string tileId)
        {
            if (!TryDecrementPendingWater(tileId))
            {
                UnityEngine.Debug.LogWarning("CommitWaterTile: 无待提交浇水次数，tileId=" + tileId);
                return false;
            }

            NotifyWaterPendingChanged(tileId);
            if (string.IsNullOrEmpty(tileId) || !tileById.TryGetValue(tileId, out var tile))
            {
                UnityEngine.Debug.LogWarning("CommitWaterTile: 未找到 tileId=" + tileId);
                return false;
            }

            if (!IsWaterActionableForCommit(tile))
            {
                UnityEngine.Debug.LogWarning("CommitWaterTile: 田格已不可浇水，tileId=" + tileId);
                return false;
            }

            ApplyWater(tile);
            OnPlantTileInteracted?.Invoke(tileId);
            return true;
        }

        // SPEC §9.5 (v3.87)：work_1 开始后 0.5s 由 MainRoleCunminPresenter 调用。
        public bool CommitFertilizeToTile(string tileId, string fertilizerId)
        {
            _pendingFertilizeTiles.Remove(tileId);
            if (string.IsNullOrEmpty(tileId) || !tileById.TryGetValue(tileId, out var tile))
            {
                UnityEngine.Debug.LogWarning("CommitFertilizeToTile: 未找到 tileId=" + tileId);
                return false;
            }

            if (!IsFertilizeActionableForCommit(tile))
            {
                UnityEngine.Debug.LogWarning("CommitFertilizeToTile: 田格已不可施肥，tileId=" + tileId);
                return false;
            }

            if (string.IsNullOrEmpty(fertilizerId) || !fertilizerTypeById.TryGetValue(fertilizerId, out var ftype))
            {
                UnityEngine.Debug.LogWarning("CommitFertilizeToTile: 未知 fertilizerId=" + fertilizerId);
                return false;
            }

            var stack = FindFertilizerStack(fertilizerId);
            if (stack == null || stack.count <= 0)
            {
                UnityEngine.Debug.LogWarning("CommitFertilizeToTile: 肥料库存不足，id=" + fertilizerId);
                return false;
            }

            stack.count -= 1;
            if (stack.count == 0)
            {
                session.fertilizerBag.stacks.Remove(stack);
                if (session.fertilizerBag.activeId == fertilizerId)
                    session.fertilizerBag.activeId = null;
            }

            if (!string.IsNullOrEmpty(tile.plantInstanceId)
                && plantById.TryGetValue(tile.plantInstanceId, out var plant))
            {
                plant.appliedFertilizerSpeedMul = ftype.speedMul;
            }

            ApplyFertilize(tile);
            OnFertilizerBagChanged?.Invoke();
            OnPlantTileInteracted?.Invoke(tileId);
            return true;
        }

        // SPEC §6 / §9.4.6：仓库内按钮 + 手势直接驱动的播种入口。
        // 内部沿用 §4.1.4 第 1 步与 ApplySeed 路径，保持 Seed/Pack 双分支语义与既有事件链。
        public bool TrySeedTile(string tileId)
        {
            if (string.IsNullOrEmpty(tileId))
                return false;
            if (!tileById.TryGetValue(tileId, out var tile))
                return false;
            if (!IsSeedActionable(tile))
                return false;

            string plantInstanceBefore = tile.plantInstanceId;
            ApplySeed(tile);

            // ApplySeed 在失败分支仅打印 Warning 不抛异常；
            // 通过 plantInstanceId 是否新建来判断本次是否真的成功。
            return tile.planting == PlantingFlag.Seeded
                && !string.IsNullOrEmpty(tile.plantInstanceId)
                && tile.plantInstanceId != plantInstanceBefore;
        }

        public bool TryHarvestTile(string tileId)
        {
            if (string.IsNullOrEmpty(tileId))
                return false;
            if (!tileById.TryGetValue(tileId, out var tile))
                return false;
            if (!IsHarvestActionable(tile))
                return false;
            ApplyHarvest(tile);
            return true;
        }

        // SPEC §9.1 (v3.71)：待浇水次数；0 表示无叠层。
        public int GetPendingWaterDisplayTier(string tileId)
        {
            return GetPendingWaterCount(tileId);
        }

        private int GetPendingWaterCount(string tileId)
        {
            if (string.IsNullOrEmpty(tileId))
                return 0;
            return _pendingWaterCounts.TryGetValue(tileId, out var count) ? count : 0;
        }

        private static int GetMaxQueueableWaterCount(CropTile tile)
        {
            if (tile == null)
                return 0;
            switch (tile.water)
            {
                case WaterStage.Empty: return 3;
                case WaterStage.W1: return 2;
                case WaterStage.W2: return 1;
                default: return 0;
            }
        }

        private bool CanQueueWaterRequest(CropTile tile)
        {
            if (IsLockedByMutation(tile))
                return false;
            if (tile.planting != PlantingFlag.Seeded)
                return false;
            return GetPendingWaterCount(tile.tileId) < GetMaxQueueableWaterCount(tile);
        }

        private bool TryDecrementPendingWater(string tileId)
        {
            if (string.IsNullOrEmpty(tileId) || !_pendingWaterCounts.TryGetValue(tileId, out var count))
                return false;
            count--;
            if (count <= 0)
                _pendingWaterCounts.Remove(tileId);
            else
                _pendingWaterCounts[tileId] = count;
            return true;
        }

        private void NotifyWaterPendingChanged(string tileId)
        {
            OnWaterPendingChanged?.Invoke(tileId);
        }

        // SPEC §9.5.2 (v3.62)：精灵巡逻协助浇水；不触发 OnUnifiedActionExecuted、不进入 pending。
        public bool TryWaterTile(string tileId)
        {
            if (string.IsNullOrEmpty(tileId))
                return false;
            if (!tileById.TryGetValue(tileId, out var tile))
                return false;
            if (!IsWaterImmediatelyActionable(tile))
                return false;
            ApplyWater(tile);
            OnPlantTileInteracted?.Invoke(tileId);
            return true;
        }

        private bool IsWaterImmediatelyActionable(CropTile tile)
        {
            if (IsLockedByMutation(tile))
                return false;
            if (tile.planting != PlantingFlag.Seeded)
                return false;
            return tile.water == WaterStage.Empty
                || tile.water == WaterStage.W1
                || tile.water == WaterStage.W2;
        }

        public void ApplyHarvestRoleReward(RoleStatType statType, int amount)
        {
            if (amount <= 0)
                return;
            if (session.role == null)
                session.role = RoleStats.CreateDefault();
            switch (statType)
            {
                case RoleStatType.Atk:
                    session.role.atk += amount;
                    break;
                case RoleStatType.Def:
                    session.role.def += amount;
                    break;
                case RoleStatType.MaxHp:
                    session.role.maxHp += amount;
                    session.role.currentHp += amount;
                    break;
                case RoleStatType.Agility:
                    session.role.agility += amount;
                    break;
            }
            OnRoleStatsChanged?.Invoke();
        }

        public RoleStats GetRoleStats()
        {
            if (session.role == null)
                session.role = RoleStats.CreateDefault();
            return session.role;
        }

        // ---- SPEC §9.14：创角界面好友与创角状态 ----

        public IReadOnlyList<FriendProfile> GetFriends()
        {
            if (session.friends == null)
                session.friends = FriendCatalog.BuildDefault();
            return session.friends;
        }

        public CharacterCreationState GetCharacterCreation()
        {
            if (session.characterCreation == null)
                session.characterCreation = new CharacterCreationState();
            return session.characterCreation;
        }

        public bool AddFriendFavor(string friendId, int amount = 10)
        {
            if (string.IsNullOrEmpty(friendId) || amount <= 0)
                return false;

            var friend = FindFriend(friendId);
            if (friend == null)
                return false;

            int next = friend.intimacy + amount;
            if (next > FriendCatalog.IntimacyMax)
                next = FriendCatalog.IntimacyMax;
            friend.intimacy = next;
            return true;
        }

        public bool CreateCharacterWith(string friendId)
        {
            if (string.IsNullOrEmpty(friendId))
                return false;

            var friend = FindFriend(friendId);
            if (friend == null || friend.intimacy < FriendCatalog.IntimacyThreshold)
                return false;

            var state = GetCharacterCreation();
            state.created = true;
            state.partnerFriendId = friendId;
            return true;
        }

        public bool CreateCharacterDirect()
        {
            // SPEC §9.14.1（v3.117）：加号无伙伴直接创角。
            var state = GetCharacterCreation();
            state.created = true;
            state.partnerFriendId = string.Empty;
            return true;
        }

        private FriendProfile FindFriend(string friendId)
        {
            var friends = GetFriends();
            for (int i = 0; i < friends.Count; i++)
            {
                if (friends[i] != null && string.Equals(friends[i].id, friendId, StringComparison.Ordinal))
                    return friends[i];
            }
            return null;
        }

        public CropTile GetTile(int orderIndex) => GetTileByOrder(orderIndex);

        public ActionType? GetActionableActionOf(string tileId)
        {
            if (!tileById.TryGetValue(tileId, out var tile))
                return null;
            return HighestPriorityActionOf(tile);
        }

        public string GetCurrentFocusTileId() => focusTileId;

        public void TickGrowth(float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
                return;

            // SPEC §4.1.10：变异会同帧内删除 4 株 PlantInstance，破坏 session.plants 的索引连续性。
            // 一旦发生变异，立即跳出本帧 Tick；剩余植物在下一帧继续推进，避免索引越界 / 漏 tick。
            for (int i = 0; i < session.plants.Count; i++)
            {
                var plant = session.plants[i];
                if (plant.state != PlantState.Growing)
                    continue;

                if (!tileById.TryGetValue(plant.tileId, out var tile))
                    continue;
                if (!configById.TryGetValue(plant.plantConfigId, out var cfg))
                    continue;

                // SPEC §4.1.6 (v3.50)：虫灾期间暂停生长，等同缺水暂停。
                if (tile.pest == PestFlag.AwaitingPestControl)
                {
                    if (plant.state == PlantState.Growing)
                        SetPlantState(plant, PlantState.Paused);
                    continue;
                }

                // SPEC §4.1.6.1 (v3.52)：地鼠偷窃期间暂停生长。
                if (tile.moleTheft == MoleTheftFlag.AwaitingMoleTheft)
                {
                    if (plant.state == PlantState.Growing)
                        SetPlantState(plant, PlantState.Paused);
                    continue;
                }

                if (tile.water == WaterStage.Empty)
                {
                    SetPlantState(plant, PlantState.Paused);
                    continue;
                }

                // SPEC §9.7：当 tile.fertilizer == Fertilized 且 PlantInstance 已写入活跃肥料倍率时，
                // 使用本周期肥料倍率覆盖 PlantConfig.fertilizerSpeedMul；否则回退到 PlantConfig 默认。
                float fertMul = plant.appliedFertilizerSpeedMul > 0f
                    ? plant.appliedFertilizerSpeedMul
                    : cfg.fertilizerSpeedMul;
                float speedMul = (tile.fertilizer == FertilizerFlag.Fertilized) ? fertMul : 1f;
                float effectiveDt = deltaSeconds * speedMul;

                bool mutatedThisTick = false;
                // 一帧内可能跨越多个阶段，循环消耗到 dt 用尽或植物状态切换。
                while (effectiveDt > 0f && plant.state == PlantState.Growing)
                {
                    if (plant.currentStageRemainingSec > effectiveDt)
                    {
                        plant.currentStageRemainingSec -= effectiveDt;
                        effectiveDt = 0f;
                        break;
                    }

                    effectiveDt -= plant.currentStageRemainingSec;
                    plant.currentStageRemainingSec = 0f;
                    if (AdvanceOneStage(tile, plant, cfg))
                    {
                        mutatedThisTick = true;
                        break;
                    }

                    if (plant.state != PlantState.Growing)
                        break;
                    // 仍 Growing：water 阶仍 ≥ W1，继续消耗剩余 effectiveDt。
                }

                if (mutatedThisTick)
                    break;
            }
        }

        public PreviewActionResult? PreviewNextAction()
        {
            var preview = FindNextAction();
            if (preview.HasValue)
            {
                if (focusTileId != preview.Value.tileId)
                {
                    focusTileId = preview.Value.tileId;
                    OnFocusChanged?.Invoke(focusTileId);
                }
                return preview;
            }

            if (focusTileId != null)
            {
                focusTileId = null;
                OnFocusChanged?.Invoke(null);
            }
            return null;
        }

        public CropTile GetTileByOrder(int orderIndex)
        {
            if (orderIndex < 1 || orderIndex > session.farmTiles.Count)
                return null;
            return session.farmTiles[orderIndex - 1];
        }

        public PlantInstance GetPlant(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return null;
            plantById.TryGetValue(instanceId, out var p);
            return p;
        }

        public PlantConfig GetPlantConfig(string plantConfigId)
        {
            if (string.IsNullOrEmpty(plantConfigId))
                return null;
            configById.TryGetValue(plantConfigId, out var c);
            return c;
        }

        public PlayerSeedBag GetSeedBag() => session.seedBag;

        public PlayerFruitBag GetFruitBag() => session.fruitBag;

        public int GetHarvestablePlantCount()
        {
            int count = 0;
            for (int i = 0; i < session.farmTiles.Count; i++)
            {
                if (IsHarvestActionable(session.farmTiles[i]))
                    count++;
            }

            for (int i = 0; i < session.mutations.Count; i++)
            {
                if (session.mutations[i].state == PlantState.AwaitingHarvest)
                    count++;
            }

            return count;
        }

        public PlayerFertilizerBag GetFertilizerBag() => session.fertilizerBag;

        public FertilizerType GetFertilizerType(string fertilizerId)
        {
            if (string.IsNullOrEmpty(fertilizerId))
                return null;
            fertilizerTypeById.TryGetValue(fertilizerId, out var ft);
            return ft;
        }

        // ============================================================
        // SPEC §6 / §9.7：施肥三段式 API（自 v2.10 起）
        // ============================================================
        public void SelectActiveFertilizer(string fertilizerId)
        {
            if (string.IsNullOrEmpty(fertilizerId))
            {
                if (session.fertilizerBag.activeId != null)
                {
                    session.fertilizerBag.activeId = null;
                    OnFertilizerBagChanged?.Invoke();
                }
                return;
            }
            if (!fertilizerTypeById.ContainsKey(fertilizerId))
            {
                UnityEngine.Debug.LogWarning("SelectActiveFertilizer: 未知 fertilizerId=" + fertilizerId + "，保持原值。");
                return;
            }
            if (session.fertilizerBag.activeId == fertilizerId)
                return;
            session.fertilizerBag.activeId = fertilizerId;
            OnFertilizerBagChanged?.Invoke();
        }

        public string GetActiveFertilizer() => session.fertilizerBag.activeId;

        public bool ApplyFertilizerToTile(string tileId)
        {
            if (string.IsNullOrEmpty(tileId))
                return false;
            if (!tileById.TryGetValue(tileId, out var tile))
                return false;
            if (!IsFertilizeActionable(tile))
                return false;

            var activeId = session.fertilizerBag.activeId;
            if (string.IsNullOrEmpty(activeId))
                return false;
            if (!fertilizerTypeById.TryGetValue(activeId, out var ftype))
                return false;

            var stack = FindFertilizerStack(activeId);
            if (stack == null || stack.count <= 0)
                return false;

            if (UseImmediateWaterFertilizeEffects)
            {
                ApplyFertilizerToTileImmediate(tile, activeId, ftype, stack);
                return true;
            }

            if (!_pendingFertilizeTiles.Add(tile.tileId))
                return false;

            OnFertilizeApplied?.Invoke(tile.tileId, activeId);
            return true;
        }

        // 保留：点击即生效的施肥路径（UseImmediateWaterFertilizeEffects=true 时使用）。
        private void ApplyFertilizerToTileImmediate(
            CropTile tile, string activeId, FertilizerType ftype, FertilizerStack stack)
        {
            stack.count -= 1;
            if (stack.count == 0)
            {
                session.fertilizerBag.stacks.Remove(stack);
                if (session.fertilizerBag.activeId == activeId)
                    session.fertilizerBag.activeId = null;
            }

            if (!string.IsNullOrEmpty(tile.plantInstanceId)
                && plantById.TryGetValue(tile.plantInstanceId, out var plant))
            {
                plant.appliedFertilizerSpeedMul = ftype.speedMul;
            }

            ApplyFertilize(tile);
            OnFertilizerBagChanged?.Invoke();
            OnFertilizeApplied?.Invoke(tile.tileId, activeId);
        }

        // SPEC §9.7 / §6（v3.22）：「全部施肥」按钮的一键批量施肥入口。
        // 按 orderIndex 1..20 升序扫描，对每块满足 IsFertilizeActionable 的田调用 ApplyFertilizerToTile；
        // 当 activeId 为空 / 不在 fertilizerTypes / 当前堆叠库存归零时立即停止。返回成功施肥的田数。
        public int ApplyFertilizerToAllAwaitingTiles()
        {
            int successCount = 0;
            for (int i = 0; i < session.farmTiles.Count; i++)
            {
                var activeId = session.fertilizerBag.activeId;
                if (string.IsNullOrEmpty(activeId))
                    break;
                if (!fertilizerTypeById.ContainsKey(activeId))
                    break;
                var stack = FindFertilizerStack(activeId);
                if (stack == null || stack.count <= 0)
                    break;

                var tile = session.farmTiles[i];
                if (!IsFertilizeActionable(tile))
                    continue;

                if (ApplyFertilizerToTile(tile.tileId))
                    successCount += 1;
            }
            return successCount;
        }

        // SPEC §12.8 / §B.10：战斗胜利掉落入包（Seed/Fertilizer/SeedPack）。
        public bool GrantSeed(string plantConfigId, int count)
        {
            if (string.IsNullOrEmpty(plantConfigId) || count <= 0)
            {
                UnityEngine.Debug.LogWarning("GrantSeed: 参数非法，plantConfigId=" + plantConfigId + ", count=" + count);
                return false;
            }
            if (!configById.ContainsKey(plantConfigId))
            {
                UnityEngine.Debug.LogWarning("GrantSeed: 未知 plantConfigId=" + plantConfigId);
                return false;
            }

            var stack = FindSeedStack(plantConfigId);
            if (stack == null)
            {
                stack = new SeedStack { plantConfigId = plantConfigId, count = 0 };
                session.seedBag.seeds.Add(stack);
            }
            stack.count += count;
            OnSeedBagChanged?.Invoke();
            return true;
        }

        public bool GrantFertilizer(string fertilizerId, int count)
        {
            if (string.IsNullOrEmpty(fertilizerId) || count <= 0)
            {
                UnityEngine.Debug.LogWarning("GrantFertilizer: 参数非法，fertilizerId=" + fertilizerId + ", count=" + count);
                return false;
            }
            if (!fertilizerTypeById.ContainsKey(fertilizerId))
            {
                UnityEngine.Debug.LogWarning("GrantFertilizer: 未知 fertilizerId=" + fertilizerId);
                return false;
            }

            var stack = FindFertilizerStack(fertilizerId);
            if (stack == null)
            {
                stack = new FertilizerStack { fertilizerId = fertilizerId, count = 0 };
                session.fertilizerBag.stacks.Add(stack);
            }
            stack.count += count;
            OnFertilizerBagChanged?.Invoke();
            return true;
        }

        public bool GrantSeedPack(SeedPackQuality quality, int count)
        {
            if (count <= 0)
            {
                UnityEngine.Debug.LogWarning("GrantSeedPack: 参数非法，quality=" + quality + ", count=" + count);
                return false;
            }

            var stack = FindPackStack(quality);
            if (stack == null)
            {
                stack = new SeedPackStack { quality = quality, count = 0 };
                session.seedBag.seedPacks.Add(stack);
            }
            stack.count += count;
            OnSeedBagChanged?.Invoke();
            return true;
        }

        // ============================================================
        // SPEC §10.1 优先级判定
        // 自 v2.9 起 Seed 已从该链中下线，改由 TrySeedTile 直接驱动（§9.4.6）；
        // 自 v2.10 起 Fertilize 也从该链中下线，改由 ApplyFertilizerToTile 直接驱动（§9.7）。
        // ============================================================
        private PreviewActionResult? FindNextAction()
        {
            PreviewActionResult result;
            if (TryFindFirst(ActionType.Harvest, IsHarvestActionable, out result))
                return result;
            if (TryFindFirst(ActionType.Water, IsWaterStage1Actionable, out result))
                return result;
            if (TryFindFirst(ActionType.Water, IsWaterStage2Actionable, out result))
                return result;
            if (TryFindFirst(ActionType.Water, IsWaterStage3Actionable, out result))
                return result;
            return null;
        }

        private bool TryFindFirst(ActionType action, Predicate<CropTile> matcher, out PreviewActionResult result)
        {
            for (int i = 0; i < session.farmTiles.Count; i++)
            {
                var tile = session.farmTiles[i];
                if (!matcher(tile))
                    continue;

                result = new PreviewActionResult { tileId = tile.tileId, action = action };
                return true;
            }

            result = default(PreviewActionResult);
            return false;
        }

        private ActionType? HighestPriorityActionOf(CropTile tile)
        {
            // 自 v2.9 起 Seed 不再参与统一按钮的"焦点田"扫描；
            // IsSeedActionable 仍然保留供 TrySeedTile 在仓库手势侧使用。
            // 自 v2.10 起 Fertilize 也不再参与统一按钮扫描；
            // IsFertilizeActionable 仍然保留供 ApplyFertilizerToTile 在 §9.7 三段式中使用。

            // 1. Harvest
            if (IsHarvestActionable(tile))
                return ActionType.Harvest;

            // 2. Water 1：Empty -> W1
            if (IsWaterStage1Actionable(tile))
                return ActionType.Water;

            // 3. Water 2：W1 -> W2
            if (IsWaterStage2Actionable(tile))
                return ActionType.Water;

            // 4. Water 3：W2 -> W3
            if (IsWaterStage3Actionable(tile))
                return ActionType.Water;

            return null;
        }

        // SPEC §4.1.10.3：被变异锁定的 4 田对所有 5 类操作短路返回 false。
        private static bool IsLockedByMutation(CropTile tile)
        {
            return tile != null && !string.IsNullOrEmpty(tile.lockedByMutationId);
        }

        private bool IsHarvestActionable(CropTile tile)
        {
            if (IsLockedByMutation(tile)) return false;
            // SPEC §4.1.6.1 (v3.52)：地鼠偷窃期间不可收获。
            if (tile.moleTheft == MoleTheftFlag.AwaitingMoleTheft)
                return false;
            return tile.harvest == HarvestFlag.AwaitingHarvest;
        }

        private bool IsSeedActionable(CropTile tile)
        {
            if (IsLockedByMutation(tile)) return false;
            return tile.planting == PlantingFlag.AwaitingSeed && CanSeedNow();
        }

        private bool IsWaterStage1Actionable(CropTile tile)
        {
            if (IsLockedByMutation(tile)) return false;
            return tile.planting == PlantingFlag.Seeded
                && tile.water == WaterStage.Empty
                && CanQueueWaterRequest(tile);
        }

        private bool IsFertilizeActionable(CropTile tile)
        {
            if (IsLockedByMutation(tile)) return false;
            if (_pendingFertilizeTiles.Contains(tile.tileId)) return false;
            return tile.planting == PlantingFlag.Seeded && tile.fertilizer == FertilizerFlag.AwaitingFertilizer;
        }

        private bool IsWaterStage2Actionable(CropTile tile)
        {
            if (IsLockedByMutation(tile)) return false;
            return tile.planting == PlantingFlag.Seeded
                && tile.water == WaterStage.W1
                && CanQueueWaterRequest(tile);
        }

        private bool IsWaterStage3Actionable(CropTile tile)
        {
            if (IsLockedByMutation(tile)) return false;
            return tile.planting == PlantingFlag.Seeded
                && tile.water == WaterStage.W2
                && CanQueueWaterRequest(tile);
        }

        private bool IsWaterActionableForCommit(CropTile tile)
        {
            if (IsLockedByMutation(tile)) return false;
            return tile.planting == PlantingFlag.Seeded
                && (tile.water == WaterStage.Empty
                    || tile.water == WaterStage.W1
                    || tile.water == WaterStage.W2);
        }

        private bool IsFertilizeActionableForCommit(CropTile tile)
        {
            if (IsLockedByMutation(tile)) return false;
            return tile.planting == PlantingFlag.Seeded
                && tile.fertilizer == FertilizerFlag.AwaitingFertilizer;
        }

        private bool CanSeedNow()
        {
            var active = session.seedBag.active;
            if (active == null)
                return false;
            if (active.kind == ActiveKind.Seed)
            {
                var stack = FindSeedStack(active.id);
                return stack != null && stack.count > 0;
            }
            // Pack 分支：按品质堆查找。
            if (Enum.TryParse<SeedPackQuality>(active.id, out var q))
            {
                var ps = FindPackStack(q);
                return ps != null && ps.count > 0;
            }
            return false;
        }

        // ============================================================
        // §10.2 应用操作
        // ============================================================
        private void Apply(ActionType act, CropTile tile)
        {
            switch (act)
            {
                case ActionType.Seed:
                    ApplySeed(tile);
                    break;
                case ActionType.Water:
                    ApplyWater(tile);
                    break;
                case ActionType.Fertilize:
                    ApplyFertilize(tile);
                    break;
                case ActionType.PestControl:
                    ApplyPestControl(tile);
                    break;
                case ActionType.Harvest:
                    ApplyHarvest(tile);
                    break;
            }
        }

        private void ApplySeed(CropTile tile)
        {
            var active = session.seedBag.active;
            if (active == null)
            {
                UnityEngine.Debug.LogWarning("ApplySeed: 当前无活跃道具，跳过。");
                return;
            }

            string plantConfigId;
            if (active.kind == ActiveKind.Seed)
            {
                var stack = FindSeedStack(active.id);
                if (stack == null || stack.count <= 0)
                {
                    UnityEngine.Debug.LogWarning("ApplySeed: 种子堆不足，跳过。id=" + active.id);
                    return;
                }
                stack.count -= 1;
                if (stack.count == 0)
                    session.seedBag.seeds.Remove(stack);
                plantConfigId = active.id;
                OnSeedBagChanged?.Invoke();
            }
            else // ActiveKind.Pack
            {
                if (!Enum.TryParse<SeedPackQuality>(active.id, out var quality))
                {
                    UnityEngine.Debug.LogWarning("ApplySeed(Pack): 非法 active.id=" + active.id);
                    return;
                }
                var ps = FindPackStack(quality);
                if (ps == null || ps.count <= 0)
                {
                    UnityEngine.Debug.LogWarning("ApplySeed(Pack): 种子包堆不足，跳过。quality=" + quality);
                    return;
                }
                plantConfigId = RollSeedPack(quality);
                ps.count -= 1;
                if (ps.count == 0)
                    session.seedBag.seedPacks.Remove(ps);
                OnSeedRolledFromPack?.Invoke(tile.tileId, quality, plantConfigId);
                OnSeedBagChanged?.Invoke();
            }

            if (!configById.TryGetValue(plantConfigId, out var cfg))
            {
                UnityEngine.Debug.LogWarning("ApplySeed: 未找到 PlantConfig id=" + plantConfigId);
                return;
            }

            tile.planting = PlantingFlag.Seeded;
            tile.fertilizer = FertilizerFlag.AwaitingFertilizer;
            tile.water = WaterStage.Empty;
            tile.pest = PestFlag.PestControlled;
            tile.moleTheft = MoleTheftFlag.None;
            // 一致性修正（SPEC §11 v0.8）：SPEC §4.1.4 第 1 步原写 AwaitingHarvest，但与 §10.1 Rank 1
            // 矛盾（会让刚播种的田立即被自动收获）；本实现把播种瞬间 harvest 维度置 None，等
            // waterConsumed==5 时才推到 AwaitingHarvest，使 §10.1 优先级链能闭合。
            tile.harvest = HarvestFlag.None;

            plantInstanceSeq += 1;
            var plant = new PlantInstance
            {
                instanceId = "plant-" + plantInstanceSeq.ToString("D4"),
                plantConfigId = plantConfigId,
                tileId = tile.tileId,
                state = PlantState.Growing,
                waterConsumed = 0,
                appearanceNode = 1,
                currentStageRemainingSec = cfg.baseStageSeconds,
                pestEventConsumed = false,
                pestSpriteRollMask = 0,
                moleTheftEventConsumed = false,
                moleSpriteRollMask = 0,
            };
            session.plants.Add(plant);
            plantById[plant.instanceId] = plant;
            tile.plantInstanceId = plant.instanceId;

            OnTileFlagsChanged?.Invoke(tile.tileId);
            OnPlantStateChanged?.Invoke(plant.instanceId, plant.state);
            OnAppearanceNodeChanged?.Invoke(plant.instanceId, plant.appearanceNode);
        }

        private void ApplyWater(CropTile tile)
        {
            var prev = tile.water;
            switch (tile.water)
            {
                case WaterStage.Empty: tile.water = WaterStage.W1; break;
                case WaterStage.W1: tile.water = WaterStage.W2; break;
                case WaterStage.W2: tile.water = WaterStage.W3; break;
                case WaterStage.W3: return; // 上限 W3，不再增加
            }

            if (prev == WaterStage.Empty && !string.IsNullOrEmpty(tile.plantInstanceId))
            {
                var plant = plantById[tile.plantInstanceId];
                if (plant.state == PlantState.Paused
                    && tile.pest != PestFlag.AwaitingPestControl
                    && tile.moleTheft != MoleTheftFlag.AwaitingMoleTheft)
                    SetPlantState(plant, PlantState.Growing);
            }

            OnTileFlagsChanged?.Invoke(tile.tileId);
        }

        private void ApplyFertilize(CropTile tile)
        {
            tile.fertilizer = FertilizerFlag.Fertilized;
            OnTileFlagsChanged?.Invoke(tile.tileId);
        }

        private void ApplyPestControl(CropTile tile)
        {
            tile.pest = PestFlag.PestControlled;
            OnTileFlagsChanged?.Invoke(tile.tileId);
        }

        // SPEC §4.1.6 (v3.50)
        private void TryRollPestSpriteEvent(CropTile tile, PlantInstance plant, PlantConfig cfg, int node)
        {
            // 本株已触发过虫灾，不再重复。
            if (plant.pestEventConsumed)
                return;
            // 检查该节点是否已做过抽取（bit0 = node2, bit1 = node3）。
            int bit = (node == 2) ? 1 : 2;
            if ((plant.pestSpriteRollMask & bit) != 0)
                return;
            // 无论成败，标记该节点已抽。
            plant.pestSpriteRollMask |= bit;

            if (UnityEngine.Random.value < cfg.pestSpriteProb)
            {
                plant.pestEventConsumed = true;
                tile.pest = PestFlag.AwaitingPestControl;
                SetPlantState(plant, PlantState.Paused);
                OnTileFlagsChanged?.Invoke(tile.tileId);
                OnPestEventTriggered?.Invoke(tile.tileId);
            }
        }

        // SPEC §9.11.4 (v3.50)：玩家在打虫子界面点击「胜利」后调用。
        public bool CompletePestControl(string tileId)
        {
            if (!tileById.TryGetValue(tileId, out var tile))
            {
                UnityEngine.Debug.LogWarning("CompletePestControl: 未找到 tileId=" + tileId);
                return false;
            }
            if (tile.pest != PestFlag.AwaitingPestControl)
            {
                UnityEngine.Debug.LogWarning("CompletePestControl: tile.pest 不是 AwaitingPestControl，tileId=" + tileId);
                return false;
            }
            tile.pest = PestFlag.PestControlled;
            // 若有植物且处于暂停（虫灾导致），且有水则恢复生长。
            if (!string.IsNullOrEmpty(tile.plantInstanceId)
                && plantById.TryGetValue(tile.plantInstanceId, out var plant)
                && plant.state == PlantState.Paused
                && tile.water != WaterStage.Empty)
            {
                SetPlantState(plant, PlantState.Growing);
            }
            OnTileFlagsChanged?.Invoke(tile.tileId);
            return true;
        }

        // SPEC §9.11.4 (v3.80)：一次胜利清除农田内所有 AwaitingPestControl 田格。
        public int CompleteAllPestControl()
        {
            int cleared = 0;
            foreach (var tile in tileById.Values)
            {
                if (tile == null || tile.pest != PestFlag.AwaitingPestControl)
                    continue;
                tile.pest = PestFlag.PestControlled;
                // 与 CompletePestControl 一致：因虫灾暂停且有水的植物恢复生长。
                if (!string.IsNullOrEmpty(tile.plantInstanceId)
                    && plantById.TryGetValue(tile.plantInstanceId, out var plant)
                    && plant.state == PlantState.Paused
                    && tile.water != WaterStage.Empty)
                {
                    SetPlantState(plant, PlantState.Growing);
                }
                OnTileFlagsChanged?.Invoke(tile.tileId);
                cleared++;
            }
            return cleared;
        }

        // SPEC §9.11.4 (v3.81)
        public int CountAwaitingPestControlTiles()
        {
            int count = 0;
            foreach (var tile in tileById.Values)
            {
                if (tile != null && tile.pest == PestFlag.AwaitingPestControl)
                    count++;
            }

            return count;
        }

        // SPEC §4.1.6.1 (v3.52)
        private void TryRollMoleSpriteEvent(CropTile tile, PlantInstance plant, PlantConfig cfg, int node)
        {
            if (plant.moleTheftEventConsumed)
                return;
            int bit = (node == 4) ? 4 : 8;
            if ((plant.moleSpriteRollMask & bit) != 0)
                return;
            plant.moleSpriteRollMask |= bit;

            if (UnityEngine.Random.value < cfg.moleSpriteProb)
            {
                plant.moleTheftEventConsumed = true;
                tile.moleTheft = MoleTheftFlag.AwaitingMoleTheft;
                SetPlantState(plant, PlantState.Paused);
                OnTileFlagsChanged?.Invoke(tile.tileId);
            }
        }

        // SPEC §9.12.4 (v3.52)：玩家在打地鼠界面点击「胜利」后调用。
        public bool CompleteMoleTheft(string tileId)
        {
            if (!tileById.TryGetValue(tileId, out var tile))
            {
                UnityEngine.Debug.LogWarning("CompleteMoleTheft: 未找到 tileId=" + tileId);
                return false;
            }
            if (tile.moleTheft != MoleTheftFlag.AwaitingMoleTheft)
            {
                UnityEngine.Debug.LogWarning("CompleteMoleTheft: tile.moleTheft 不是 AwaitingMoleTheft，tileId=" + tileId);
                return false;
            }
            tile.moleTheft = MoleTheftFlag.MoleTheftResolved;
            if (!string.IsNullOrEmpty(tile.plantInstanceId)
                && plantById.TryGetValue(tile.plantInstanceId, out var plant)
                && plant.state == PlantState.Paused
                && tile.water != WaterStage.Empty
                && tile.pest != PestFlag.AwaitingPestControl)
            {
                SetPlantState(plant, PlantState.Growing);
            }
            OnTileFlagsChanged?.Invoke(tile.tileId);
            return true;
        }

        // SPEC §9.12.4 (v3.82)：按 tileId 取田（附魔界面取激活植物精灵用）。
        public CropTile GetTileById(string tileId)
        {
            if (string.IsNullOrEmpty(tileId))
                return null;
            return tileById.TryGetValue(tileId, out var tile) ? tile : null;
        }

        // SPEC §9.12.4 (v3.82)：附魔玩法失败「放弃」→ 直接删除该田植物并复位田。
        public bool AbandonMoleTheftPlant(string tileId)
        {
            if (string.IsNullOrEmpty(tileId) || !tileById.TryGetValue(tileId, out var tile))
            {
                UnityEngine.Debug.LogWarning("AbandonMoleTheftPlant: 未找到 tileId=" + tileId);
                return false;
            }
            if (string.IsNullOrEmpty(tile.plantInstanceId))
            {
                UnityEngine.Debug.LogWarning("AbandonMoleTheftPlant: 无有效植物实例，tileId=" + tileId);
                return false;
            }

            string removedInstanceId = tile.plantInstanceId;
            if (plantById.TryGetValue(removedInstanceId, out var plant))
            {
                session.plants.Remove(plant);
                plantById.Remove(removedInstanceId);
            }

            tile.planting = PlantingFlag.AwaitingSeed;
            tile.fertilizer = FertilizerFlag.None;
            tile.water = WaterStage.Empty;
            tile.pest = PestFlag.None;
            tile.moleTheft = MoleTheftFlag.None;
            tile.harvest = HarvestFlag.None;
            tile.plantInstanceId = null;

            OnPlantStateChanged?.Invoke(removedInstanceId, PlantState.Wilted);
            OnTileFlagsChanged?.Invoke(tile.tileId);
            return true;
        }

        private void ApplyHarvest(CropTile tile)
        {
            if (string.IsNullOrEmpty(tile.plantInstanceId))
                return;
            if (!plantById.TryGetValue(tile.plantInstanceId, out var plant))
                return;
            if (!configById.TryGetValue(plant.plantConfigId, out var cfg))
                return;

            if (cfg.afterHarvest == AfterHarvest.Wilt)
            {
                session.plants.Remove(plant);
                plantById.Remove(plant.instanceId);

                tile.planting = PlantingFlag.AwaitingSeed;
                tile.fertilizer = FertilizerFlag.None;
                tile.water = WaterStage.Empty;
                tile.pest = PestFlag.None;
                tile.moleTheft = MoleTheftFlag.None;
                tile.harvest = HarvestFlag.None;
                tile.plantInstanceId = null;

                OnPlantStateChanged?.Invoke(plant.instanceId, PlantState.Wilted);
                OnTileFlagsChanged?.Invoke(tile.tileId);
                EmitHarvestFruitReward(tile.tileId, plant.plantConfigId, cfg);
            }
            else // Regrow
            {
                plant.waterConsumed = 0;
                plant.appearanceNode = 1;
                plant.currentStageRemainingSec = cfg.baseStageSeconds;
                // SPEC §9.7：Regrow 重置肥料维度时同步清空本周期施肥倍率。
                plant.appliedFertilizerSpeedMul = 0f;
                // SPEC §4.1.6 (v3.50)：Regrow 时重置虫灾抽取状态，允许新一轮生长重新触发。
                plant.pestEventConsumed = false;
                plant.pestSpriteRollMask = 0;
                plant.moleTheftEventConsumed = false;
                plant.moleSpriteRollMask = 0;
                SetPlantState(plant, PlantState.Growing);

                // 一致性修正（SPEC §11 v0.8）：SPEC §4.1.5 Regrow 分支原写 AwaitingHarvest，但与 §10.1
                // Rank 1 矛盾（重置后会立即再次被自动收获）；本实现把 harvest 维度回到 None，待新周期
                // waterConsumed==5 时再切到 AwaitingHarvest。
                tile.harvest = HarvestFlag.None;
                tile.fertilizer = FertilizerFlag.AwaitingFertilizer;
                tile.pest = PestFlag.PestControlled;
                tile.moleTheft = MoleTheftFlag.None;
                // tile.water 保留当前阶（SPEC §4.1.5 Regrow 分支）。

                OnTileFlagsChanged?.Invoke(tile.tileId);
                OnAppearanceNodeChanged?.Invoke(plant.instanceId, plant.appearanceNode);

                // Regrow 后若 water == Empty，按 §4.1.4 第 2 条切 Paused。
                if (tile.water == WaterStage.Empty)
                    SetPlantState(plant, PlantState.Paused);

                EmitHarvestFruitReward(tile.tileId, plant.plantConfigId, cfg);
            }
        }

        private void EmitHarvestFruitReward(string tileId, string plantConfigId, PlantConfig cfg)
        {
            if (cfg == null || string.IsNullOrEmpty(plantConfigId))
                return;
            int n = Mathf.Max(1, cfg.harvestFruitCount);
            AddOrStackFruit(plantConfigId, n);
            OnFruitBagChanged?.Invoke();
            OnHarvestFruitReady?.Invoke(tileId, plantConfigId, n);
        }

        private void AddOrStackFruit(string plantConfigId, int count)
        {
            if (string.IsNullOrEmpty(plantConfigId) || count <= 0)
                return;
            if (session.fruitBag == null)
                session.fruitBag = new PlayerFruitBag();
            var stacks = session.fruitBag.stacks;
            FruitStack stack = null;
            for (int i = 0; i < stacks.Count; i++)
            {
                if (stacks[i] != null && stacks[i].plantConfigId == plantConfigId)
                {
                    stack = stacks[i];
                    break;
                }
            }
            if (stack == null)
            {
                stack = new FruitStack { plantConfigId = plantConfigId, count = 0 };
                stacks.Add(stack);
            }
            stack.count += count;
        }

        // ============================================================
        // SPEC §4.1.10：农田变异机制（v3.18）
        // ============================================================

        /// <summary>
        /// SPEC §4.1.10.1：当某株植物推进到 appearanceNode==4 时，检测其所在行（连续 4 个 orderIndex）
        /// 是否满足变异触发条件；满足则调用 ApplyMutation 完成原子替换。
        /// </summary>
        private bool TryTriggerMutationForTile(CropTile triggerTile, PlantInstance triggerPlant)
        {
            if (triggerTile == null || triggerPlant == null)
                return false;
            int rowStartOrder = ((triggerTile.orderIndex - 1) / 4) * 4 + 1;
            if (rowStartOrder < 1 || rowStartOrder > 21)
                return false;

            var members = new List<CropTile>(4);
            string sharedConfigId = null;
            for (int k = 0; k < 4; k++)
            {
                var memberTile = GetTileByOrder(rowStartOrder + k);
                if (memberTile == null)
                    return false;
                if (memberTile.planting != PlantingFlag.Seeded)
                    return false;
                if (!string.IsNullOrEmpty(memberTile.lockedByMutationId))
                    return false;
                if (string.IsNullOrEmpty(memberTile.plantInstanceId))
                    return false;
                if (!plantById.TryGetValue(memberTile.plantInstanceId, out var memberPlant))
                    return false;
                if (memberPlant.appearanceNode != 4)
                    return false;
                if (sharedConfigId == null)
                    sharedConfigId = memberPlant.plantConfigId;
                else if (memberPlant.plantConfigId != sharedConfigId)
                    return false;
                members.Add(memberTile);
            }

            if (members.Count != 4)
                return false;

            ApplyMutation(members);
            return true;
        }

        /// <summary>
        /// SPEC §4.1.10.2：原子执行变异。删除 4 株 PlantInstance、锁定 4 田、创建 MutationPlant，
        /// 并按 orderIndex 升序触发 4 次 OnTileFlagsChanged，最后触发 1 次 OnMutationCreated。
        /// </summary>
        private void ApplyMutation(List<CropTile> members)
        {
            // 1. 品类抽签 + 内容抽签
            bool hasPet = session.petConfigs != null && session.petConfigs.Count > 0;
            bool hasSkill = session.skillConfigs != null && session.skillConfigs.Count > 0;
            if (!hasPet && !hasSkill)
            {
                UnityEngine.Debug.LogWarning("[PlantingService] ApplyMutation: petConfigs 与 skillConfigs 均为空，放弃本次变异。");
                return;
            }

            MutationKind kind;
            if (hasPet && hasSkill)
                kind = (UnityEngine.Random.value < 0.5f) ? MutationKind.Pet : MutationKind.Skill;
            else
                kind = hasPet ? MutationKind.Pet : MutationKind.Skill;

            string refId;
            if (kind == MutationKind.Pet)
            {
                int idx = UnityEngine.Random.Range(0, session.petConfigs.Count);
                refId = session.petConfigs[idx]?.id;
            }
            else
            {
                int idx = UnityEngine.Random.Range(0, session.skillConfigs.Count);
                refId = session.skillConfigs[idx]?.id;
            }
            if (string.IsNullOrEmpty(refId))
            {
                UnityEngine.Debug.LogWarning("[PlantingService] ApplyMutation: 抽签得到空 refId，放弃本次变异。");
                return;
            }

            // 2. 创建 MutationPlant
            mutationInstanceSeq += 1;
            string mutationId = "mut-" + mutationInstanceSeq.ToString("D4");
            var mutation = new MutationPlant
            {
                instanceId = mutationId,
                kind = kind,
                refId = refId,
                tileIds = new List<string>(4),
                state = PlantState.AwaitingHarvest,
            };

            // 3. 删除 4 株 PlantInstance + 写入 4 田锁定
            for (int i = 0; i < members.Count; i++)
            {
                var tile = members[i];
                if (!string.IsNullOrEmpty(tile.plantInstanceId)
                    && plantById.TryGetValue(tile.plantInstanceId, out var plant))
                {
                    session.plants.Remove(plant);
                    plantById.Remove(plant.instanceId);
                }
                tile.lockedByMutationId = mutationId;
                tile.planting = PlantingFlag.Seeded;
                tile.fertilizer = FertilizerFlag.None;
                tile.water = WaterStage.Empty;
                tile.pest = PestFlag.None;
                tile.moleTheft = MoleTheftFlag.None;
                tile.harvest = HarvestFlag.None;
                tile.plantInstanceId = null;
                mutation.tileIds.Add(tile.tileId);
            }

            session.mutations.Add(mutation);
            mutationById[mutationId] = mutation;

            // 4. 事件：先 4 次 OnTileFlagsChanged，再一次 OnMutationCreated
            for (int i = 0; i < members.Count; i++)
                OnTileFlagsChanged?.Invoke(members[i].tileId);
            OnMutationCreated?.Invoke(mutationId);
        }

        // SPEC §4.1.10 / §9.13（v3.59）：单格变异，抽签规则与 ApplyMutation 一致。
        public bool TriggerSingleTileMutation(string tileId)
        {
            if (string.IsNullOrEmpty(tileId) || !tileById.TryGetValue(tileId, out var tile))
            {
                UnityEngine.Debug.LogWarning("[PlantingService] TriggerSingleTileMutation: 未找到 tileId=" + tileId);
                return false;
            }
            if (!string.IsNullOrEmpty(tile.lockedByMutationId))
            {
                UnityEngine.Debug.LogWarning("[PlantingService] TriggerSingleTileMutation: 田已被变异锁定，tileId=" + tileId);
                return false;
            }
            if (string.IsNullOrEmpty(tile.plantInstanceId) || !plantById.ContainsKey(tile.plantInstanceId))
            {
                UnityEngine.Debug.LogWarning("[PlantingService] TriggerSingleTileMutation: 无有效植物实例，tileId=" + tileId);
                return false;
            }

            bool hasPet = session.petConfigs != null && session.petConfigs.Count > 0;
            bool hasSkill = session.skillConfigs != null && session.skillConfigs.Count > 0;
            if (!hasPet && !hasSkill)
            {
                UnityEngine.Debug.LogWarning("[PlantingService] TriggerSingleTileMutation: petConfigs 与 skillConfigs 均为空。");
                return false;
            }

            MutationKind kind;
            if (hasPet && hasSkill)
                kind = (UnityEngine.Random.value < 0.5f) ? MutationKind.Pet : MutationKind.Skill;
            else
                kind = hasPet ? MutationKind.Pet : MutationKind.Skill;

            string refId;
            if (kind == MutationKind.Pet)
            {
                int idx = UnityEngine.Random.Range(0, session.petConfigs.Count);
                refId = session.petConfigs[idx]?.id;
            }
            else
            {
                int idx = UnityEngine.Random.Range(0, session.skillConfigs.Count);
                refId = session.skillConfigs[idx]?.id;
            }
            if (string.IsNullOrEmpty(refId))
            {
                UnityEngine.Debug.LogWarning("[PlantingService] TriggerSingleTileMutation: 抽签得到空 refId，tileId=" + tileId);
                return false;
            }

            mutationInstanceSeq += 1;
            string mutationId = "mut-" + mutationInstanceSeq.ToString("D4");
            var mutation = new MutationPlant
            {
                instanceId = mutationId,
                kind = kind,
                refId = refId,
                tileIds = new List<string>(1),
                state = PlantState.AwaitingHarvest,
            };

            if (plantById.TryGetValue(tile.plantInstanceId, out var plantToRemove))
            {
                session.plants.Remove(plantToRemove);
                plantById.Remove(plantToRemove.instanceId);
            }

            tile.lockedByMutationId = mutationId;
            tile.planting = PlantingFlag.Seeded;
            tile.fertilizer = FertilizerFlag.None;
            tile.water = WaterStage.Empty;
            tile.pest = PestFlag.None;
            tile.moleTheft = MoleTheftFlag.None;
            tile.harvest = HarvestFlag.None;
            tile.plantInstanceId = null;
            mutation.tileIds.Add(tile.tileId);

            session.mutations.Add(mutation);
            mutationById[mutationId] = mutation;

            OnTileFlagsChanged?.Invoke(tile.tileId);
            OnMutationCreated?.Invoke(mutationId);
            return true;
        }

        public bool TryHarvestMutation(string mutationId)
        {
            if (string.IsNullOrEmpty(mutationId))
                return false;
            if (!mutationById.TryGetValue(mutationId, out var mutation))
                return false;
            if (mutation.state != PlantState.AwaitingHarvest)
                return false;

            mutation.state = PlantState.Harvested;
            mutationById.Remove(mutationId);
            session.mutations.Remove(mutation);

            // 被锁定田复位：与 §4.1.5 Wilt 路径一致（tileIds 含 1 或 4 块田，v3.59）。
            if (mutation.tileIds != null)
            {
                for (int i = 0; i < mutation.tileIds.Count; i++)
                {
                    if (!tileById.TryGetValue(mutation.tileIds[i], out var tile))
                        continue;
                    tile.lockedByMutationId = null;
                    tile.planting = PlantingFlag.AwaitingSeed;
                    tile.fertilizer = FertilizerFlag.None;
                    tile.water = WaterStage.Empty;
                    tile.pest = PestFlag.None;
                    tile.moleTheft = MoleTheftFlag.None;
                    tile.harvest = HarvestFlag.None;
                    tile.plantInstanceId = null;
                    OnTileFlagsChanged?.Invoke(tile.tileId);
                }
            }

            if (mutation.kind == MutationKind.Pet)
            {
                string petInstanceId = GrantPet(mutation.refId);
                if (!string.IsNullOrEmpty(petInstanceId))
                    TryAutoDeploy(petInstanceId);
            }

            OnMutationHarvested?.Invoke(mutationId, mutation.kind, mutation.refId);
            return true;
        }

        public PetDeployment GetPetDeployment() => session.petDeployment;

        public PetInstance GetPetInstance(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return null;
            petById.TryGetValue(instanceId, out var p);
            return p;
        }

        public string GetDeployedPetConfigId(PetFieldSlot slot)
        {
            var dep = session.petDeployment;
            if (dep == null)
                return null;

            string instanceId = slot == PetFieldSlot.LowerLeft
                ? dep.lowerLeftInstanceId
                : dep.upperLeftInstanceId;

            if (string.IsNullOrEmpty(instanceId))
                return null;

            var inst = GetPetInstance(instanceId);
            return inst?.petConfigId;
        }

        public IReadOnlyList<PetInstance> GetDeployedPetInstances()
        {
            var list = new List<PetInstance>(2);
            var dep = session.petDeployment;
            if (dep == null)
                return list;

            if (!string.IsNullOrEmpty(dep.lowerLeftInstanceId))
            {
                var lower = GetPetInstance(dep.lowerLeftInstanceId);
                if (lower != null)
                    list.Add(lower);
            }

            if (!string.IsNullOrEmpty(dep.upperLeftInstanceId))
            {
                var upper = GetPetInstance(dep.upperLeftInstanceId);
                if (upper != null)
                    list.Add(upper);
            }

            return list;
        }

        public int GetPetBagCount() =>
            session.petBag?.owned != null ? session.petBag.owned.Count : 0;

        public int GetFieldPetLimit() =>
            session.restrictionProfile != null
                ? Mathf.Max(0, session.restrictionProfile.fieldPetLimit)
                : PetDeploymentRules.DefaultFieldPetLimit;

        public RestrictionProfile GetRestrictionProfile() => session.restrictionProfile;

        public string GrantPet(string petConfigId)
        {
            if (string.IsNullOrEmpty(petConfigId) || !petConfigById.ContainsKey(petConfigId))
            {
                UnityEngine.Debug.LogWarning("[PlantingService] GrantPet: 未知 petConfigId=" + petConfigId);
                return null;
            }

            if (GetPetBagCount() >= PetDeploymentRules.MaxOwnedPets)
            {
                UnityEngine.Debug.LogWarning("[PlantingService] GrantPet: 精灵背包已满（上限 " +
                                 PetDeploymentRules.MaxOwnedPets + "）。");
                return null;
            }

            petInstanceSeq += 1;
            string instanceId = "pet-" + petInstanceSeq.ToString("D4");
            var inst = new PetInstance
            {
                instanceId = instanceId,
                petConfigId = petConfigId,
            };
            session.petBag.owned.Add(inst);
            petById[instanceId] = inst;
            OnPetBagChanged?.Invoke();
            return instanceId;
        }

        public bool TryAutoDeploy(string instanceId) => TryAssignToFirstEmptySlot(instanceId);

        public bool TryDeployPetToField(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId) || !petById.ContainsKey(instanceId))
                return false;

            if (IsPetDeployed(instanceId))
                return false;

            return TryAssignToFirstEmptySlot(instanceId);
        }

        public bool UndeployPet(PetFieldSlot slot)
        {
            var dep = session.petDeployment;
            if (dep == null)
                return false;

            bool changed = false;
            if (slot == PetFieldSlot.LowerLeft)
            {
                if (!string.IsNullOrEmpty(dep.lowerLeftInstanceId))
                {
                    dep.lowerLeftInstanceId = null;
                    changed = true;
                }
            }
            else if (slot == PetFieldSlot.UpperLeft)
            {
                if (!string.IsNullOrEmpty(dep.upperLeftInstanceId))
                {
                    dep.upperLeftInstanceId = null;
                    changed = true;
                }
            }

            if (changed)
                OnPetDeploymentChanged?.Invoke();
            return changed;
        }

        public IReadOnlyList<PetInstance> GetOwnedPets()
        {
            if (session.petBag?.owned == null)
                return Array.Empty<PetInstance>();
            return session.petBag.owned;
        }

        public bool IsPetDeployed(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return false;
            return TryGetDeploySlotOf(instanceId, out _);
        }

        public bool TryGetDeploySlotOf(string instanceId, out PetFieldSlot slot)
        {
            slot = default;
            var dep = session.petDeployment;
            if (dep == null || string.IsNullOrEmpty(instanceId))
                return false;

            if (string.Equals(dep.lowerLeftInstanceId, instanceId, StringComparison.Ordinal))
            {
                slot = PetFieldSlot.LowerLeft;
                return true;
            }

            if (string.Equals(dep.upperLeftInstanceId, instanceId, StringComparison.Ordinal))
            {
                slot = PetFieldSlot.UpperLeft;
                return true;
            }

            return false;
        }

        private bool TryAssignToFirstEmptySlot(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId) || !petById.ContainsKey(instanceId))
                return false;

            int limit = GetFieldPetLimit();
            var dep = session.petDeployment;
            if (dep == null)
                return false;

            int deployed = 0;
            if (!string.IsNullOrEmpty(dep.lowerLeftInstanceId)) deployed++;
            if (!string.IsNullOrEmpty(dep.upperLeftInstanceId)) deployed++;
            if (deployed >= limit)
                return false;

            if (string.IsNullOrEmpty(dep.lowerLeftInstanceId))
            {
                dep.lowerLeftInstanceId = instanceId;
                OnPetDeploymentChanged?.Invoke();
                return true;
            }

            if (string.IsNullOrEmpty(dep.upperLeftInstanceId))
            {
                dep.upperLeftInstanceId = instanceId;
                OnPetDeploymentChanged?.Invoke();
                return true;
            }

            return false;
        }

        public MutationPlant GetMutation(string mutationId)
        {
            if (string.IsNullOrEmpty(mutationId))
                return null;
            mutationById.TryGetValue(mutationId, out var m);
            return m;
        }

        public IReadOnlyList<MutationPlant> GetMutations() => session.mutations;

        public PetConfig GetPetConfig(string petConfigId)
        {
            if (string.IsNullOrEmpty(petConfigId))
                return null;
            petConfigById.TryGetValue(petConfigId, out var p);
            return p;
        }

        public SkillConfig GetSkillConfig(string skillConfigId)
        {
            if (string.IsNullOrEmpty(skillConfigId))
                return null;
            skillConfigById.TryGetValue(skillConfigId, out var s);
            return s;
        }

        // ============================================================
        // SPEC §B.8 开局引导性农田预置
        // 仅在构造期被调用一次；本路径不触发任何 §6 事件，UI 通过
        // FarmGridView.BuildInto 末尾的 RefreshAllSlots() 主动拉取初始快照。
        // ============================================================
        private void ApplyInitialGuidanceTiles()
        {
            if (kInitialGuidancePresets == null)
                return;
            for (int i = 0; i < kInitialGuidancePresets.Length; i++)
            {
                var preset = kInitialGuidancePresets[i];
                SetupAwaitingHarvestTile(preset.orderIndex, preset.plantConfigId);
            }
        }

        private void SetupAwaitingHarvestTile(int orderIndex, string plantConfigId)
        {
            if (orderIndex < 1 || orderIndex > session.farmTiles.Count)
            {
                UnityEngine.Debug.LogWarning($"[PlantingService] 引导预置 orderIndex={orderIndex} 越界，跳过。");
                return;
            }
            var tile = session.farmTiles[orderIndex - 1];
            if (tile == null || !tileById.ContainsKey(tile.tileId))
            {
                UnityEngine.Debug.LogWarning($"[PlantingService] 引导预置 orderIndex={orderIndex} 对应农田缺失，跳过。");
                return;
            }
            if (string.IsNullOrEmpty(plantConfigId) || !configById.ContainsKey(plantConfigId))
            {
                UnityEngine.Debug.LogWarning($"[PlantingService] 引导预置 plantConfigId='{plantConfigId}' 不存在，跳过。orderIndex={orderIndex}");
                return;
            }

            // 五维写入「待收获」终态：与 §4.1.4 第 5 步保持一致——
            // waterConsumed==5 进入 AwaitingHarvest，水自然耗尽到 Empty，
            // fertilizer 复位为 AwaitingFertilizer 以便下一周期再次施肥。
            tile.planting = PlantingFlag.Seeded;
            tile.fertilizer = FertilizerFlag.AwaitingFertilizer;
            tile.water = WaterStage.Empty;
            tile.pest = PestFlag.PestControlled;
            tile.moleTheft = MoleTheftFlag.None;
            tile.harvest = HarvestFlag.AwaitingHarvest;

            plantInstanceSeq += 1;
            var plant = new PlantInstance
            {
                instanceId = "plant-" + plantInstanceSeq.ToString("D4"),
                plantConfigId = plantConfigId,
                tileId = tile.tileId,
                state = PlantState.AwaitingHarvest,
                waterConsumed = 5,
                appearanceNode = 5,
                currentStageRemainingSec = 0f,
            };
            session.plants.Add(plant);
            plantById[plant.instanceId] = plant;
            tile.plantInstanceId = plant.instanceId;
        }

        // ============================================================
        // 内部辅助
        // ============================================================
        // 返回值：true 表示本调用触发了「农田变异」，调用方应跳出当前 Tick（plant 已被删除）。
        private bool AdvanceOneStage(CropTile tile, PlantInstance plant, PlantConfig cfg)
        {
            // tile.water 阶 -1
            switch (tile.water)
            {
                case WaterStage.W3: tile.water = WaterStage.W2; break;
                case WaterStage.W2: tile.water = WaterStage.W1; break;
                case WaterStage.W1: tile.water = WaterStage.Empty; break;
                case WaterStage.Empty: return false; // 异常防御：不该走到这里
            }

            plant.waterConsumed = Mathf.Min(5, plant.waterConsumed + 1);
            int newNode = Mathf.Min(5, plant.waterConsumed + 1);
            if (newNode != plant.appearanceNode)
            {
                plant.appearanceNode = newNode;
                OnAppearanceNodeChanged?.Invoke(plant.instanceId, plant.appearanceNode);

                // SPEC §4.1.6 (v3.50)：进入节点 2 或 3 时按概率抽取虫灾事件。
                if (newNode == 2 || newNode == 3)
                    TryRollPestSpriteEvent(tile, plant, cfg, newNode);

                // SPEC §4.1.6.1 (v3.52)：进入节点 4 或 5 时按概率抽取地鼠偷窃事件。
                if (newNode == 4 || newNode == 5)
                    TryRollMoleSpriteEvent(tile, plant, cfg, newNode);

                // SPEC §4.1.10.1：当 plant 推进到 appearanceNode==4（waterConsumed==3）时，
                // 若启用 `kRowMutationTriggerEnabled`，检测同行是否满足变异条件；满足则原子替换为 MutationPlant。
                // 注意：变异成功时本 plant 会被销毁，调用方应立即跳出 Tick 循环。
                if (kRowMutationTriggerEnabled
                    && plant.appearanceNode == 4
                    && TryTriggerMutationForTile(tile, plant))
                    return true;
            }

            if (plant.waterConsumed >= 5)
            {
                tile.harvest = HarvestFlag.AwaitingHarvest;
                tile.fertilizer = FertilizerFlag.AwaitingFertilizer;
                // SPEC §9.7：肥料维度复位时同步清空本周期施肥倍率，下一周期重新覆盖。
                plant.appliedFertilizerSpeedMul = 0f;
                plant.currentStageRemainingSec = 0f;
                SetPlantState(plant, PlantState.AwaitingHarvest);
                OnTileFlagsChanged?.Invoke(tile.tileId);
                return false;
            }

            plant.currentStageRemainingSec = cfg.baseStageSeconds;

            if (tile.water == WaterStage.Empty)
                SetPlantState(plant, PlantState.Paused);

            OnTileFlagsChanged?.Invoke(tile.tileId);
            return false;
        }

        private void SetPlantState(PlantInstance plant, PlantState newState)
        {
            if (plant.state == newState)
                return;
            plant.state = newState;
            OnPlantStateChanged?.Invoke(plant.instanceId, newState);
        }

        private void SetFocusTile(string tileId)
        {
            if (focusTileId == tileId)
                return;
            focusTileId = tileId;
            OnFocusChanged?.Invoke(tileId);
        }

        private SeedStack FindSeedStack(string plantConfigId)
        {
            for (int i = 0; i < session.seedBag.seeds.Count; i++)
                if (session.seedBag.seeds[i].plantConfigId == plantConfigId)
                    return session.seedBag.seeds[i];
            return null;
        }

        private SeedPackStack FindPackStack(SeedPackQuality q)
        {
            for (int i = 0; i < session.seedBag.seedPacks.Count; i++)
                if (session.seedBag.seedPacks[i].quality == q)
                    return session.seedBag.seedPacks[i];
            return null;
        }

        private FertilizerStack FindFertilizerStack(string fertilizerId)
        {
            if (string.IsNullOrEmpty(fertilizerId))
                return null;
            for (int i = 0; i < session.fertilizerBag.stacks.Count; i++)
                if (session.fertilizerBag.stacks[i].fertilizerId == fertilizerId)
                    return session.fertilizerBag.stacks[i];
            return null;
        }

        private string FallbackPlantConfigId()
        {
            foreach (var pair in configById)
                return pair.Key;
            return null;
        }

        // ============================================================
        // SPEC §6 / §9.8.12 (v3.40)：食物与体力
        // ============================================================
        public PlayerFoodBag GetFoodBag() => session.foodBag;

        public IReadOnlyList<FoodConfig> GetFoodConfigs() => session.foodConfigs;

        public FoodConfig GetFoodConfig(string foodId)
        {
            if (string.IsNullOrEmpty(foodId))
                return null;
            return foodConfigById != null && foodConfigById.TryGetValue(foodId, out var cfg) ? cfg : null;
        }

        public void SelectActiveFood(string foodId)
        {
            // 传空 / null = 清空选中。
            if (string.IsNullOrEmpty(foodId))
            {
                if (!string.IsNullOrEmpty(session.foodBag.activeId))
                {
                    session.foodBag.activeId = null;
                    OnFoodBagChanged?.Invoke();
                }
                return;
            }

            if (foodConfigById == null || !foodConfigById.ContainsKey(foodId))
            {
                UnityEngine.Debug.LogWarning("[PlantingService] SelectActiveFood: foodId 不存在于 foodConfigs，id=" + foodId);
                return;
            }
            var stack = FindFoodStack(foodId);
            if (stack == null || stack.count <= 0)
            {
                UnityEngine.Debug.LogWarning("[PlantingService] SelectActiveFood: foodId 库存为 0，id=" + foodId);
                return;
            }

            if (session.foodBag.activeId != foodId)
            {
                session.foodBag.activeId = foodId;
                OnFoodBagChanged?.Invoke();
            }
        }

        public string GetActiveFood() => session.foodBag != null ? session.foodBag.activeId : null;

        public bool EatOne(string foodId)
        {
            if (string.IsNullOrEmpty(foodId))
            {
                UnityEngine.Debug.LogWarning("[PlantingService] EatOne: foodId 为空");
                return false;
            }
            if (foodConfigById == null || !foodConfigById.TryGetValue(foodId, out var cfg))
            {
                UnityEngine.Debug.LogWarning("[PlantingService] EatOne: foodId 不存在 id=" + foodId);
                return false;
            }
            var stack = FindFoodStack(foodId);
            if (stack == null || stack.count <= 0)
            {
                UnityEngine.Debug.LogWarning("[PlantingService] EatOne: 库存不足 id=" + foodId);
                return false;
            }
            if (session.role == null)
                session.role = RoleStats.CreateDefault();
            if (session.role.stamina >= session.role.staminaMax)
            {
                UnityEngine.Debug.Log("[PlantingService] EatOne: 体力已满，无需食用");
                return false;
            }

            stack.count -= 1;
            if (stack.count <= 0)
            {
                session.foodBag.stacks.Remove(stack);
                if (session.foodBag.activeId == foodId)
                    session.foodBag.activeId = null;
            }

            int newStamina = session.role.stamina + cfg.staminaGain;
            if (newStamina < 0) newStamina = 0;
            if (newStamina > session.role.staminaMax) newStamina = session.role.staminaMax;
            session.role.stamina = newStamina;

            OnFoodBagChanged?.Invoke();
            OnStaminaChanged?.Invoke(session.role.stamina, session.role.staminaMax);
            return true;
        }

        public int EatToFull(string foodId)
        {
            if (string.IsNullOrEmpty(foodId))
            {
                UnityEngine.Debug.LogWarning("[PlantingService] EatToFull: foodId 为空");
                return 0;
            }
            if (foodConfigById == null || !foodConfigById.TryGetValue(foodId, out var cfg))
            {
                UnityEngine.Debug.LogWarning("[PlantingService] EatToFull: foodId 不存在 id=" + foodId);
                return 0;
            }
            if (session.role == null)
                session.role = RoleStats.CreateDefault();

            var stack = FindFoodStack(foodId);
            if (stack == null || stack.count <= 0)
            {
                UnityEngine.Debug.LogWarning("[PlantingService] EatToFull: 库存不足 id=" + foodId);
                return 0;
            }

            int consumed = 0;
            while (session.role.stamina < session.role.staminaMax && stack.count > 0)
            {
                stack.count -= 1;
                consumed += 1;

                int newStamina = session.role.stamina + cfg.staminaGain;
                if (newStamina < 0) newStamina = 0;
                if (newStamina > session.role.staminaMax) newStamina = session.role.staminaMax;
                session.role.stamina = newStamina;
            }

            if (stack.count <= 0)
            {
                session.foodBag.stacks.Remove(stack);
                if (session.foodBag.activeId == foodId)
                    session.foodBag.activeId = null;
            }

            if (consumed > 0)
            {
                OnFoodBagChanged?.Invoke();
                OnStaminaChanged?.Invoke(session.role.stamina, session.role.staminaMax);
            }
            return consumed;
        }

        public bool IsRoleFull()
        {
            if (session.role == null)
                return false;
            return session.role.stamina >= session.role.staminaMax;
        }

        private FoodStack FindFoodStack(string foodId)
        {
            if (string.IsNullOrEmpty(foodId) || session.foodBag == null || session.foodBag.stacks == null)
                return null;
            for (int i = 0; i < session.foodBag.stacks.Count; i++)
            {
                var s = session.foodBag.stacks[i];
                if (s != null && s.foodId == foodId)
                    return s;
            }
            return null;
        }

        // ============================================================
        // SPEC §9.8.13 (v3.41)：果实食用（吃果实换体力）
        // ============================================================
        public void SelectActiveFruit(string plantConfigId)
        {
            if (session.fruitBag == null)
                session.fruitBag = new PlayerFruitBag();

            if (string.IsNullOrEmpty(plantConfigId))
            {
                if (session.fruitBag.activeId != null)
                {
                    session.fruitBag.activeId = null;
                    OnFruitBagChanged?.Invoke();
                }
                return;
            }

            if (configById == null || !configById.ContainsKey(plantConfigId))
            {
                UnityEngine.Debug.LogWarning("[PlantingService] SelectActiveFruit: plantConfigId 不存在 id=" + plantConfigId);
                return;
            }
            var stack = FindFruitStack(plantConfigId);
            if (stack == null || stack.count <= 0)
            {
                UnityEngine.Debug.LogWarning("[PlantingService] SelectActiveFruit: 库存为 0 id=" + plantConfigId);
                return;
            }

            if (session.fruitBag.activeId != plantConfigId)
            {
                session.fruitBag.activeId = plantConfigId;
                OnFruitBagChanged?.Invoke();
            }
        }

        public string GetActiveFruit() => session.fruitBag != null ? session.fruitBag.activeId : null;

        public bool EatOneFruit(string plantConfigId)
        {
            if (string.IsNullOrEmpty(plantConfigId))
            {
                UnityEngine.Debug.LogWarning("[PlantingService] EatOneFruit: plantConfigId 为空");
                return false;
            }
            if (configById == null || !configById.TryGetValue(plantConfigId, out var cfg))
            {
                UnityEngine.Debug.LogWarning("[PlantingService] EatOneFruit: plantConfigId 不存在 id=" + plantConfigId);
                return false;
            }
            var stack = FindFruitStack(plantConfigId);
            if (stack == null || stack.count <= 0)
            {
                UnityEngine.Debug.LogWarning("[PlantingService] EatOneFruit: 库存不足 id=" + plantConfigId);
                return false;
            }
            if (session.role == null)
                session.role = RoleStats.CreateDefault();
            if (session.role.stamina >= session.role.staminaMax)
            {
                UnityEngine.Debug.Log("[PlantingService] EatOneFruit: 体力已满，无需食用");
                return false;
            }

            int gain = ResolveFruitStaminaGain(cfg);
            stack.count -= 1;
            if (stack.count <= 0)
            {
                session.fruitBag.stacks.Remove(stack);
                if (session.fruitBag.activeId == plantConfigId)
                    session.fruitBag.activeId = null;
            }

            int newStamina = session.role.stamina + gain;
            if (newStamina < 0) newStamina = 0;
            if (newStamina > session.role.staminaMax) newStamina = session.role.staminaMax;
            session.role.stamina = newStamina;

            OnFruitBagChanged?.Invoke();
            OnStaminaChanged?.Invoke(session.role.stamina, session.role.staminaMax);
            return true;
        }

        public int EatFruitToFull(string plantConfigId)
        {
            if (string.IsNullOrEmpty(plantConfigId))
            {
                UnityEngine.Debug.LogWarning("[PlantingService] EatFruitToFull: plantConfigId 为空");
                return 0;
            }
            if (configById == null || !configById.TryGetValue(plantConfigId, out var cfg))
            {
                UnityEngine.Debug.LogWarning("[PlantingService] EatFruitToFull: plantConfigId 不存在 id=" + plantConfigId);
                return 0;
            }
            if (session.role == null)
                session.role = RoleStats.CreateDefault();

            var stack = FindFruitStack(plantConfigId);
            if (stack == null || stack.count <= 0)
            {
                UnityEngine.Debug.LogWarning("[PlantingService] EatFruitToFull: 库存不足 id=" + plantConfigId);
                return 0;
            }

            int gain = ResolveFruitStaminaGain(cfg);
            int consumed = 0;
            while (session.role.stamina < session.role.staminaMax && stack.count > 0)
            {
                stack.count -= 1;
                consumed += 1;

                int newStamina = session.role.stamina + gain;
                if (newStamina < 0) newStamina = 0;
                if (newStamina > session.role.staminaMax) newStamina = session.role.staminaMax;
                session.role.stamina = newStamina;
            }

            if (stack.count <= 0)
            {
                session.fruitBag.stacks.Remove(stack);
                if (session.fruitBag.activeId == plantConfigId)
                    session.fruitBag.activeId = null;
            }

            if (consumed > 0)
            {
                OnFruitBagChanged?.Invoke();
                OnStaminaChanged?.Invoke(session.role.stamina, session.role.staminaMax);
            }
            return consumed;
        }

        // SPEC §6 / §12.9 (v3.43)：战斗开战扣体力。
        public bool TryConsumeStamina(int amount)
        {
            if (amount <= 0)
            {
                UnityEngine.Debug.LogWarning("[PlantingService] TryConsumeStamina: amount 非法 amount=" + amount);
                return false;
            }
            if (session.role == null)
                session.role = RoleStats.CreateDefault();
            if (session.role.stamina < amount)
                return false;

            int next = session.role.stamina - amount;
            if (next < 0) next = 0;
            if (next > session.role.staminaMax) next = session.role.staminaMax;
            session.role.stamina = next;
            OnStaminaChanged?.Invoke(session.role.stamina, session.role.staminaMax);
            return true;
        }

        // SPEC §9.8.13.6：果实→体力换算优先级
        //   fruitStaminaGain > 0 → 该值；否则常量 10。
        private static int ResolveFruitStaminaGain(PlantConfig cfg)
        {
            if (cfg == null)
                return 10;
            if (cfg.fruitStaminaGain > 0)
                return cfg.fruitStaminaGain;
            return 10;
        }

        private FruitStack FindFruitStack(string plantConfigId)
        {
            if (string.IsNullOrEmpty(plantConfigId) || session.fruitBag == null || session.fruitBag.stacks == null)
                return null;
            for (int i = 0; i < session.fruitBag.stacks.Count; i++)
            {
                var s = session.fruitBag.stacks[i];
                if (s != null && s.plantConfigId == plantConfigId)
                    return s;
            }
            return null;
        }
    }
}
