// 纯 C# 数据结构，零 Unity 依赖；字段名严格对齐 SPEC §5。
// SPEC：SPEC_FarmBattleDemo.md §4.1 / §5 / §6
using System.Collections.Generic;

namespace PetDemo.Core
{
    public enum PlantingFlag
    {
        AwaitingSeed,
        Seeded,
    }

    public enum FertilizerFlag
    {
        None,
        AwaitingFertilizer,
        Fertilized,
    }

    public enum WaterStage
    {
        Empty,
        W1,
        W2,
        W3,
    }

    public enum PestFlag
    {
        None,
        AwaitingPestControl,
        PestControlled,
    }

    // SPEC §4.1.6.1 (v3.52)：地鼠偷窃事件维度。
    public enum MoleTheftFlag
    {
        None,
        AwaitingMoleTheft,
        MoleTheftResolved,
    }

    public enum HarvestFlag
    {
        None,
        AwaitingHarvest,
        Harvested,
    }

    public enum AfterHarvest
    {
        Wilt,
        Regrow,
    }

    public enum PlantState
    {
        Growing,
        Paused,
        AwaitingHarvest,
        Wilted,
        // SPEC §5 (v3.18)：MutationPlant 的终态；普通 PlantInstance 不会进入此状态。
        Harvested,
    }

    public enum SeedPackQuality
    {
        Common,
        Rare,
        Epic,
        Legendary,
    }

    public enum ActiveKind
    {
        Seed,
        Pack,
    }

    public class CropTile
    {
        public string tileId;
        public int orderIndex;
        public PlantingFlag planting;
        public FertilizerFlag fertilizer;
        public WaterStage water;
        public PestFlag pest;
        public MoleTheftFlag moleTheft;
        public HarvestFlag harvest;
        public string plantInstanceId;

        // SPEC §4.1.10 / §5 (v3.18)：当被「农田变异」锁定时写入 MutationPlant.instanceId；
        // 非空时所有 5 类操作（Seed/Water/Fertilize/PestControl/Harvest）短路返回 false。
        public string lockedByMutationId;
    }

    public class PlantInstance
    {
        public string instanceId;
        public string plantConfigId;
        public string tileId;
        public PlantState state;
        public int waterConsumed;
        public int appearanceNode;
        public float currentStageRemainingSec;

        // SPEC §9.7 / §B.7：本次施肥生效期间的速度倍率（覆盖 PlantConfig.fertilizerSpeedMul）。
        // 0 = 未应用过肥料或周期复位后；TickGrowth 在 tile.fertilizer == Fertilized 且本字段 > 0 时使用本值。
        public float appliedFertilizerSpeedMul;

        // SPEC §4.1.6 (v3.50)：虫灾抽取状态。
        // pestEventConsumed：本株是否已触发过虫灾（一生一次上限）。
        // pestSpriteRollMask：位标记，bit0=节点2已抽，bit1=节点3已抽（无论成败均标记）。
        public bool pestEventConsumed;
        public int pestSpriteRollMask;

        // SPEC §4.1.6.1 (v3.52)：地鼠偷窃抽取状态。
        public bool moleTheftEventConsumed;
        public int moleSpriteRollMask;
    }

    public enum PlantAppearanceKind
    {
        Sprite,
        Spine,
    }

    public struct PlantNodeAppearance
    {
        public PlantAppearanceKind kind;
        public string resourcePath;
    }

    public class PlantConfig
    {
        public string id;
        public string displayName;
        public List<string> appearanceSpriteIds;

        /// <summary>
        /// 可选；长度 0 或 5。非空项表示该生长节点农田主视觉优先 Spine，空串回退 <see cref="appearanceSpriteIds"/>。
        /// </summary>
        public List<string> appearanceSpineIds;
        public float baseStageSeconds;
        public float fertilizerSpeedMul;
        public AfterHarvest afterHarvest;
        public float pestEventIntervalSec;
        public float pestEventProb;
        // SPEC §4.1.6 (v3.50)：进入 appearanceNode 2/3 时各抽一次的虫灾概率（0..1）。
        public float pestSpriteProb;
        // SPEC §4.1.6.1 (v3.52)：进入 appearanceNode 4/5 时各抽一次的地鼠偷窃概率（0..1）。
        public float moleSpriteProb;
        public RoleStatType harvestRewardStat = RoleStatType.Atk;

        /// <summary>
        /// 每次收获写入果实背包的数量（下限 1 由服务层 clamp）。
        /// </summary>
        public int harvestFruitCount = 1;

        // SPEC §9.8.13.6 (v3.41)：果实→体力换算的优先字段；默认 10。
        // 回退顺序：fruitStaminaGain > 0 → 该值；否则常量 10。
        public int fruitStaminaGain = 10;

        /// <summary>
        /// 吃下果实后用于演示的 Buff 图标 Resources 路径（不含扩展名）；不写入战斗属性、不参与战斗结算。
        /// </summary>
        public string eatBuffIconResource;

        /// <summary>
        /// 果实背包 / 收获飞入动效专用精灵的 Resources 路径（不含扩展名）。空则回退到成熟节点
        /// <see cref="appearanceSpriteIds"/> 最后一项。
        /// </summary>
        public string fruitIconResource;

        /// <summary>
        /// 供果实 UI 使用的精灵路径：非空 <see cref="fruitIconResource"/> 优先，否则成熟节点外观路径。
        /// </summary>
        public string ResolveFruitIconResourcePath()
        {
            if (!string.IsNullOrWhiteSpace(fruitIconResource))
                return fruitIconResource.Trim();
            if (appearanceSpriteIds != null && appearanceSpriteIds.Count > 0)
            {
                var s = appearanceSpriteIds[appearanceSpriteIds.Count - 1];
                return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
            }
            return null;
        }

        /// <summary>
        /// 农田主视觉：节点 N 若配置了 spine 路径则优先 Spine，否则回退 sprite。
        /// </summary>
        public PlantNodeAppearance ResolveFarmAppearance(int appearanceNode)
        {
            int idx = appearanceNode - 1;
            if (appearanceSpineIds != null && idx >= 0 && idx < appearanceSpineIds.Count)
            {
                var spine = appearanceSpineIds[idx];
                if (!string.IsNullOrWhiteSpace(spine))
                {
                    return new PlantNodeAppearance
                    {
                        kind = PlantAppearanceKind.Spine,
                        resourcePath = spine.Trim(),
                    };
                }
            }

            string spritePath = null;
            if (appearanceSpriteIds != null && idx >= 0 && idx < appearanceSpriteIds.Count)
            {
                var s = appearanceSpriteIds[idx];
                if (!string.IsNullOrWhiteSpace(s))
                    spritePath = s.Trim();
            }

            return new PlantNodeAppearance
            {
                kind = PlantAppearanceKind.Sprite,
                resourcePath = spritePath,
            };
        }
    }

    public enum RoleStatType
    {
        Atk,
        Def,
        MaxHp,
        Agility,
    }

    public class SeedStack
    {
        public string plantConfigId;
        public int count;
    }

    public class SeedPackStack
    {
        public SeedPackQuality quality;
        public int count;
    }

    public class ActiveSelection
    {
        public ActiveKind kind;
        public string id;
    }

    public class PlayerSeedBag
    {
        public List<SeedStack> seeds = new List<SeedStack>();
        public List<SeedPackStack> seedPacks = new List<SeedPackStack>();
        public ActiveSelection active;
    }

    public class SeedPackContentsEntry
    {
        public string plantConfigId;
        public float weight;
    }

    public class SeedPackContents
    {
        public SeedPackQuality quality;
        public List<SeedPackContentsEntry> entries = new List<SeedPackContentsEntry>();
    }

    // SPEC §5 / §B.7：肥料类型静态配置（自 v2.10 起；§3.12 增 description/iconResource）。
    public class FertilizerType
    {
        public string id;
        public string displayName;
        public float speedMul = 1.5f;
        public string description;
        public string iconResource;
    }

    // SPEC §5：肥料背包内的堆叠条目（以 fertilizerId 为堆叠键）。
    public class FertilizerStack
    {
        public string fertilizerId;
        public int count;
    }

    // SPEC §5 / §9.7：玩家肥料仓库 = 背包；activeId 为空表示未选中任何肥料。
    public class PlayerFertilizerBag
    {
        public List<FertilizerStack> stacks = new List<FertilizerStack>();
        public string activeId;
    }

    /// <summary>
    /// SPEC §4.1.11：果实背包堆叠条目（按 plantConfigId）。
    /// </summary>
    public class FruitStack
    {
        public string plantConfigId;
        public int count;
    }

    /// <summary>
    /// SPEC §4.1.11 / §9.8.13 (v3.41)：玩家果实背包。
    /// 自 v3.41 起新增 activeId，与 §9.8.13 统一仓库 26 槽的单选互斥状态关联；
    /// 写入路径仅有 IPlantingService.SelectActiveFruit / EatOneFruit / EatFruitToFull。
    /// </summary>
    public class PlayerFruitBag
    {
        public List<FruitStack> stacks = new List<FruitStack>();
        public string activeId;
    }

    /// <summary>
    /// SPEC §5 / §9.8.12 (v3.40)：食物道具静态配置。
    /// Demo 阶段不引入 CSV，由 PlantConfigCatalog.BuildDefaultFoodConfigs 提供。
    /// </summary>
    public class FoodConfig
    {
        public string id;
        public string displayName;
        public int staminaGain = 20;
        public string iconResourcePath;
        public string description;
    }

    /// <summary>
    /// SPEC §5 / §9.8.12 (v3.40)：食物背包内的堆叠条目（以 foodId 为堆叠键）。
    /// </summary>
    public class FoodStack
    {
        public string foodId;
        public int count;
    }

    /// <summary>
    /// SPEC §5 / §9.8.12 (v3.40)：玩家食物仓库；activeId 为空表示未选中任何食物。
    /// </summary>
    public class PlayerFoodBag
    {
        public List<FoodStack> stacks = new List<FoodStack>();
        public string activeId;
    }

    /// <summary>
    /// 主角与战斗单位同形属性（SPEC §5）；农场收获等逻辑可写回 Tier-1。
    /// </summary>
    public class RoleStats
    {
        public string displayName = "Role";

        public int atk = 12;
        public int def = 5;
        public int maxHp = 25;
        public int currentHp = 25;
        public int agility = 2;

        // SPEC §5 / §9.8.12 (v3.40)：体力字段。默认 0，上限 100；
        // 写入入口仅有 IPlantingService.EatOne / EatToFull；
        // 「主角已吃饱」 ≡ stamina == staminaMax。
        public int stamina = 0;
        public int staminaMax = 100;

        public float critRate = 0.05f;
        public float comboRate = 0.05f;
        public float counterRate = 0.05f;
        public float blockRate = 0.05f;

        public float critResist;
        public float comboResist;
        public float counterResist;
        public float blockResist;

        // SPEC §5 / §12.13 (v3.180)：六宫属性局外初始值（详细属性弹窗；与 Tier-2 触发率独立）。
        public int criticalHit = 3;
        public int combo = 6;
        public int counterattack = 12;
        public int stun = 2;
        public int evasion = 4;
        public int lifeSteal = 8;

        // SPEC §5 / §9.14.11 / §B.21 (v3.188)：家园成长六属性（与战斗六宫独立）。
        public int intelligence;
        public int memory;
        public int imagination;
        public int physique;
        public int charm;
        public int emotionalIntelligence;

        // SPEC §9.14.11 / §B.23 (v3.186 / v3.208)：等级与经验；升级不扣减 currentExp。
        public int level = 1;
        public int currentExp = 0;
        public int expToNextLevel = 100;

        public static RoleStats CreateDefault()
        {
            var role = new RoleStats();
            RoleLevelConfigCatalog.ApplyToRole(role);
            return role;
        }
    }

    // SPEC §4.1.10 / §5 (v3.18)：农田变异机制相关数据结构。
    public enum MutationKind
    {
        Pet,
        Skill,
    }

    /// <summary>
    /// SPEC §4.1.10：一株「变异植物」的运行时实例（四格合并或 §9.13 单格转盘）。
    /// 由 PlantingService.ApplyMutation / TriggerSingleTileMutation 创建；状态仅取 AwaitingHarvest / Harvested。
    /// </summary>
    public class MutationPlant
    {
        public string instanceId;
        public MutationKind kind;
        public string refId;            // kind=Pet 时为 PetConfig.id；kind=Skill 时为 SkillConfig.id
        public List<string> tileIds = new List<string>(); // 长度 1（单格）或 4（四格合并），按 orderIndex 升序
        public PlantState state = PlantState.AwaitingHarvest;
    }

    /// <summary>
    /// SPEC §B.11：精灵静态配置。
    /// </summary>
    public class PetConfig
    {
        public string id;
        public string displayName;
        public string traitDescription;
        public string prefabResource;   // 例 "Pets/Monster_11_Pure Slime"
        public List<string> randomAnimations = new List<string>();
    }

    /// <summary>
    /// SPEC §B.12：技能静态配置。
    /// </summary>
    public class SkillConfig
    {
        public string id;
        public string displayName;
        public string description;
        public string iconResource;     // 例 "SkilIcon/Skill1001"
    }

    // SPEC §4.1.12 (v3.70)：默认上场数量上限（本期不可提升）。
    public static class PetDeploymentRules
    {
        public const int DefaultFieldPetLimit = 2;
        // SPEC §4.1.12 / §9.10.5 (v3.71)：精灵背包容量上限。
        public const int MaxOwnedPets = 200;
    }

    // SPEC §4.1.12 (v3.70)：精灵上场槽位。
    public enum PetFieldSlot
    {
        LowerLeft,
        UpperLeft,
    }

    public class PetInstance
    {
        public string instanceId;
        public string petConfigId;
    }

    public class PetDeployment
    {
        public string lowerLeftInstanceId;
        public string upperLeftInstanceId;
    }

    public class PlayerPetBag
    {
        public List<PetInstance> owned = new List<PetInstance>();
    }

    public class RestrictionProfile
    {
        public int fieldPetLimit = PetDeploymentRules.DefaultFieldPetLimit;
        public bool accepted = true;
    }

    /// <summary>SPEC §9.8.8.7：主线关卡按钮三态。</summary>
    public enum MainStoryLevelState
    {
        Locked,
        Available,
        Cleared,
    }

    /// <summary>SPEC §9.8.8.7：主线关卡槽位布局（来自 main_story_level_slots.csv）。</summary>
    public class MainStoryLevelSlotLayout
    {
        public int slotIndex;
        public float posX;
        public float posY;
        public float width = 120f;
        public float height = 120f;
    }

    /// <summary>SPEC §9.8.8.7 / §9.8.8.8：主线关卡静态配置（来自 main_story_levels.csv）。</summary>
    public class MainStoryLevelConfig
    {
        public int levelNumber;
        public bool isBoss;
        public string displayName;
        public string infoSpritePath;
        /// <summary>SPEC §9.8.8.8：胜利固定产出（固定产出模式，无随机）。</summary>
        public List<InvasionRewardConfig> victoryRewards = new List<InvasionRewardConfig>();
    }

    /// <summary>SPEC §9.8.16 / §13：跨会话保留的 UI 解锁与进度（写入存档快照）。</summary>
    public class UiProgress
    {
        public bool mainStoryArenaEntryUnlocked;
        /// <summary>SPEC §9.8.8.7：已挑战胜利的最高关卡编号（0 = 未通关任何关）。</summary>
        public int mainStoryHighestClearedLevel;
    }

    /// <summary>SPEC §9.14.2：创角界面好友档案（头像/名字/亲密度/在线）。</summary>
    public class FriendProfile
    {
        public string id;
        public string displayName;
        public string avatarResource;   // 头像 Resources 路径，如 "AirUI/WanJia_icon_1"
        public bool online;
        public int intimacy;            // 0..100

        // SPEC §9.14.8 第 1 点（自 v3.158）：TopFriends.csv 提供的扩展字段（仅 TopFriend 列表使用）。
        public bool isFemale;               // 性别：true=女(friends_icon_woman)，false=男(friends_icon_man)
        public bool intimacyInterrupted;    // 亲密度是否中断：true=Xing_2_1，false=Xing_2
        public string avatarFrameResource;  // 头像框 Resources 路径，留空=无头像框
        public string spinePrefabPath;      // 模型 Spine 预制体 Resources 路径
    }

    /// <summary>SPEC §9.14.2（v3.203）：亲密度页签好友列表展示模式。</summary>
    public enum FriendListMode
    {
        Normal = 0,
        RecommendPrompt = 1,
        WerewolfListZero = 2,
        TownSearch = 3,
    }

    /// <summary>SPEC §9.14.2：创角状态（主角是否已创建 + 所用好友）。</summary>
    public class CharacterCreationState
    {
        public bool created;
        public string partnerFriendId;
        /// <summary>SPEC §9.14.2（v3.203）：亲密度页签展示模式；旧档缺字段视为 Normal。</summary>
        public FriendListMode friendListMode;
        /// <summary>SPEC §9.14.11（v3.206）：开局营救待完成；新档 true，营救后 false；旧档缺字段视为 false。</summary>
        public bool openingRescuePending;
    }

    /// <summary>
    /// SPEC §9.14.12 (v3.194)：挂机训练会话。
    /// courseId 空表示无进行中训练；activeFilterMask 为 6bit 筛选状态。
    /// </summary>
    public class TrainingSession
    {
        public string courseId = string.Empty;
        public long endUnixMs;
        public int activeFilterMask;

        public bool HasActiveCourse => !string.IsNullOrEmpty(courseId);
    }

    public class GameSession
    {
        public RoleStats role = RoleStats.CreateDefault();
        public UiProgress uiProgress = new UiProgress();
        // SPEC §9.14：创角界面好友列表与创角状态（随存档持久化）。
        public List<FriendProfile> friends = new List<FriendProfile>();
        public CharacterCreationState characterCreation = new CharacterCreationState();
        // SPEC §9.14.12 (v3.194)：训练会话。
        public TrainingSession trainingSession = new TrainingSession();
        public List<CropTile> farmTiles = new List<CropTile>();
        public List<PlantInstance> plants = new List<PlantInstance>();
        public PlayerSeedBag seedBag = new PlayerSeedBag();
        public PlayerFertilizerBag fertilizerBag = new PlayerFertilizerBag();
        public PlayerFruitBag fruitBag = new PlayerFruitBag();
        // SPEC §5 / §9.8.12 (v3.40)：食物仓库 + 食物静态表。
        public PlayerFoodBag foodBag = new PlayerFoodBag();
        public List<FoodConfig> foodConfigs = new List<FoodConfig>();
        public List<PlantConfig> plantConfigs = new List<PlantConfig>();
        public List<SeedPackContents> packContents = new List<SeedPackContents>();
        public List<FertilizerType> fertilizerTypes = new List<FertilizerType>();

        // SPEC §4.1.10 / §B.11 / §B.12 (v3.18)：
        public List<MutationPlant> mutations = new List<MutationPlant>();
        public List<PetConfig> petConfigs = new List<PetConfig>();
        public List<SkillConfig> skillConfigs = new List<SkillConfig>();

        // SPEC §4.1.12 (v3.70)：精灵背包与上场部署。
        public PlayerPetBag petBag = new PlayerPetBag();
        public PetDeployment petDeployment = new PetDeployment();
        public RestrictionProfile restrictionProfile = new RestrictionProfile();
    }

    // 初始仓库装载结果（SPEC §B.5）：由 PlantConfigCatalog.LoadInitialInventoryFromCsv 产出，
    // 由 FarmBootstrap 注入到 PlantingService 构造函数。
    // 自 v2.10 起新增 fertilizers 字段，承载 §B.5 的 kind=Fertilizer 行。
    public class InitialInventory
    {
        public List<SeedStack> seeds = new List<SeedStack>();
        public List<SeedPackStack> packs = new List<SeedPackStack>();
        public List<FertilizerStack> fertilizers = new List<FertilizerStack>();
        // SPEC §9.8.12 (v3.40)：Demo 默认食物（由 PlantConfigCatalog.BuildDefaultFoodBag 注入）。
        public List<FoodStack> foods = new List<FoodStack>();
    }

    // SPEC §12.5 / §B.9：入侵战斗中单位的静态配置（来自 invasion_units.csv）。
    public class InvasionUnitConfig
    {
        public string unitId;
        public string displayName;
        public int attack;
        public int maxHp;
        // SPEC §B.9 (v3.172)：该敌方单位的骨骼预制体 Resources 路径（供 §12.11.10 嵌入战斗按单位切换敌人形象）；可空。
        public string skeletonPrefab;
    }

    // SPEC §12.5：入口图标 / 全局阶段。
    public enum InvasionPhase
    {
        Countdown,
        Invading,
        InBattle,
    }

    // SPEC §12.5：战斗内当前出手方。
    public enum BattleTurn
    {
        Player,
        Enemy,
        Result,
    }

    // SPEC §12.5：战斗会话运行时状态；由 InvasionService.OpenBattle() 创建，CloseBattle() 清空。
    public class BattleSession
    {
        public int playerHp;
        public int playerMaxHp;
        public int playerAttack;

        public int enemyHp;
        public int enemyMaxHp;
        public int enemyAttack;

        public BattleTurn turn = BattleTurn.Player;
        public bool playerWon;
    }

    // SPEC §12.8 / §B.10：怪物入侵战斗胜利掉落类型。
    public enum InvasionRewardKind
    {
        Seed,
        Fertilizer,
        SeedPack,
    }

    // SPEC §12.8 / §B.10：怪物入侵战斗胜利掉落条目（CSV: invasion_victory_rewards.csv）。
    public class InvasionRewardConfig
    {
        public InvasionRewardKind kind;
        public string id;
        public int count;
    }
}
