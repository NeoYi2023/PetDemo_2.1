// 种植系统对外接口；签名严格对齐 SPEC §6 概念层接口。
// 注意：种植 ActionType 与 SPEC §5 战斗 BattleAction.ActionType 同名，分命名空间避免后续冲突。
using System;
using System.Collections.Generic;
using PetDemo.Core;

namespace PetDemo.Farm
{
    public enum ActionType
    {
        Seed,
        Water,
        Fertilize,
        PestControl,
        Harvest,
    }

    public struct PreviewActionResult
    {
        public string tileId;
        public ActionType action;
    }

    public interface IPlantingService
    {
        // ---- SPEC §6 ----
        void SelectActive(ActiveKind kind, string id);
        ActiveSelection GetActive();
        string RollSeedPack(SeedPackQuality quality);
        bool ExecuteUnifiedAction();
        // 自 v2.9 起新增：直接以 tileId 触发播种（由 §9.4.6 仓库内按钮 + 手势驱动）。
        // 失败原因：tileId 不存在 / tile.planting != AwaitingSeed / active==null /
        // countOf(active)==0 / 对应 plantConfigId 不在 plantConfigs 中。
        bool TrySeedTile(string tileId);
        bool TryHarvestTile(string tileId);
        // SPEC §9.5.2 (v3.62)：精灵协助浇水；不触发 OnUnifiedActionExecuted。
        bool TryWaterTile(string tileId);
        // SPEC §9.5 (v3.87)：统一按钮浇水数据提交（work_1 开始后 0.5s）。
        bool CommitWaterTile(string tileId);
        // SPEC §9.1 (v3.71)：已受理未提交的待浇水次数（0..3；对应 JiaoShi_Dai_1/2/3）。
        int GetPendingWaterDisplayTier(string tileId);
        // SPEC §9.5 (v3.87)：施肥数据提交（work_1 开始后 0.5s）；不触发 OnFertilizeApplied。
        bool CommitFertilizeToTile(string tileId, string fertilizerId);
        void ApplyHarvestRoleReward(RoleStatType statType, int amount);
        PlayerFruitBag GetFruitBag();
        // SPEC §9.7.1 (v3.90)：可收获植物总数（田格可收获 + 待收获变异各计 1）。
        int GetHarvestablePlantCount();
        RoleStats GetRoleStats();

        // ---- SPEC §9.14：创角界面好友与创角状态 ----
        // GetFriends：返回当前会话好友列表（只读视图）。
        // GetCharacterCreation：返回创角状态（created / partnerFriendId）。
        // AddFriendFavor：将指定好友亲密度 +amount（封顶 100）；非法 id / amount<=0 / created 时返回 false。
        // CreateCharacterWith：好友亲密度 >= FriendCatalog.IntimacyThreshold 才成功，写 created=true 与 partnerFriendId。
        IReadOnlyList<FriendProfile> GetFriends();
        CharacterCreationState GetCharacterCreation();
        bool AddFriendFavor(string friendId, int amount = 10);
        bool CreateCharacterWith(string friendId);
        // CreateCharacterDirect（v3.117 / §9.14.1）：无伙伴直接创角，写 created=true 与 partnerFriendId=""，恒返回 true。
        bool CreateCharacterDirect();

        // ---- 自 v2.10 起新增：施肥三段式（SPEC §9.7） ----
        // SelectActiveFertilizer：写入 PlayerFertilizerBag.activeId；fertilizerId 必须存在于
        //   GameSession.fertilizerTypes，否则记录 Warning 并保持原值。成功后触发 OnFertilizerBagChanged。
        // GetActiveFertilizer：读取当前活跃肥料 id（未选时返回空字符串或 null）。
        // GetFertilizerBag：返回 GameSession.fertilizerBag 引用（UI 刷新用）。
        // ApplyFertilizerToTile（v3.87）：成功表示已受理并入队（OnFertilizeApplied 驱动动画）；
        //   数据在 CommitFertilizeToTile 时写入。失败原因不变。
        void SelectActiveFertilizer(string fertilizerId);
        string GetActiveFertilizer();
        PlayerFertilizerBag GetFertilizerBag();
        bool ApplyFertilizerToTile(string tileId);

        // ---- 自 v3.22 起新增：一键批量施肥（SPEC §9.7「全部施肥」按钮） ----
        // ApplyFertilizerToAllAwaitingTiles：按 orderIndex 1..20 升序遍历 20 块田，对每块满足
        //   tile.planting == Seeded && tile.fertilizer == AwaitingFertilizer && lockedByMutationId == null
        //   的农田调用一次内部施肥路径（与 ApplyFertilizerToTile 完全一致，逐田触发三连事件）。
        //   当 activeId 为空 / 不在 fertilizerTypes / 库存归零时立即停止。返回成功施肥的田数。
        int ApplyFertilizerToAllAwaitingTiles();

        // ---- 自 v3.4 起新增：战斗胜利掉落入包（SPEC §12.8 / §B.10） ----
        // GrantSeed：向 seedBag.seeds 增加指定 plantConfigId 的 count；不存在则新建堆叠。
        //   非法入参（空 id / count<=0 / id 不在 plantConfigs）仅 Warning 并返回 false。
        //   成功后触发 OnSeedBagChanged。
        // GrantFertilizer：向 fertilizerBag.stacks 增加指定 fertilizerId 的 count；不存在则新建堆叠。
        //   非法入参（空 id / count<=0 / id 不在 fertilizerTypes）仅 Warning 并返回 false。
        //   成功后触发 OnFertilizerBagChanged。
        // GrantSeedPack：向 seedBag.seedPacks 增加指定 quality 的 count；不存在则新建堆叠。
        //   非法入参（quality 非法 / count<=0）仅 Warning 并返回 false。
        //   成功后触发 OnSeedBagChanged。
        bool GrantSeed(string plantConfigId, int count);
        bool GrantFertilizer(string fertilizerId, int count);
        bool GrantSeedPack(SeedPackQuality quality, int count);

        // ---- 自 v3.18 起新增（SPEC §4.1.10 / §6）：农田变异机制 ----
        // TryHarvestMutation：玩家点击变异果实图标后调用；失败原因（返回 false）：mutationId 不存在 /
        //   mutation.state != AwaitingHarvest。成功后将 tileIds 中每田 lockedByMutationId 清空，按 Wilt 路径
        //   全维度复位，并依次触发 N 次 OnTileFlagsChanged + 1 次 OnMutationHarvested（N=1 或 4，v3.59）。
        // GetMutation / GetMutations：UI 拉取或枚举当前在场的 MutationPlant。
        // GetPetConfig / GetSkillConfig：根据 MutationPlant.refId 取静态配置（弹窗渲染用）。
        // TriggerSingleTileMutation（v3.59）：单格变异，由 §9.13 转盘「确定」调用；失败：tileId 不存在 /
        //   plantInstanceId 空 / lockedByMutationId 非空 / petConfigs 与 skillConfigs 皆空。
        bool TryHarvestMutation(string mutationId);
        bool TriggerSingleTileMutation(string tileId);
        MutationPlant GetMutation(string mutationId);
        IReadOnlyList<MutationPlant> GetMutations();
        PetConfig GetPetConfig(string petConfigId);
        SkillConfig GetSkillConfig(string skillConfigId);

        CropTile GetTile(int orderIndex);
        // v3.82（§9.12）：按 tileId 取田；附魔界面取激活植物精灵用。未找到返回 null。
        CropTile GetTileById(string tileId);
        // v3.82（§9.12）：附魔玩法失败「放弃」→ 删除该田植物并复位田。
        bool AbandonMoleTheftPlant(string tileId);
        ActionType? GetActionableActionOf(string tileId);
        string GetCurrentFocusTileId();
        void TickGrowth(float deltaSeconds);

        // ---- v0.8 P0 额外暴露：UI 预览（不真正执行） ----
        PreviewActionResult? PreviewNextAction();

        // ---- 数据访问（UI 渲染需要） ----
        int FarmTileCount { get; }
        CropTile GetTileByOrder(int orderIndex);
        PlantInstance GetPlant(string instanceId);
        PlantConfig GetPlantConfig(string plantConfigId);
        PlayerSeedBag GetSeedBag();
        FertilizerType GetFertilizerType(string fertilizerId);

        // ---- 事件总线 ----
        // SPEC §9.1 (v3.71)：统一按钮待浇水次数增减（刷新 PendingWaterIcon，不表示 tile.water 已提交）。
        event Action<string> OnWaterPendingChanged;
        event Action<string> OnTileFlagsChanged;
        event Action<string, PlantState> OnPlantStateChanged;
        event Action<string, int> OnAppearanceNodeChanged;
        // SPEC §9.1 (v3.94)：浇水/施肥/精灵协助浇水成功时派发；不含收获。
        event Action<string> OnPlantTileInteracted;
        event Action<string> OnFocusChanged;
        event Action<string, ActionType> OnUnifiedActionExecuted;
        event Action OnSeedBagChanged;
        event Action<string, SeedPackQuality, string> OnSeedRolledFromPack;
        event Action<string> OnPestEventTriggered;

        // SPEC §9.11.4 (v3.50)：消除虫灾。失败原因：tileId 不存在 / tile.pest != AwaitingPestControl。
        // 成功：tile.pest = PestControlled；若 tile.water != Empty 且 plant.state == Paused → Growing；触发 OnTileFlagsChanged。
        bool CompletePestControl(string tileId);

        // SPEC §9.11.4 (v3.80)：一次胜利清除农田内所有 AwaitingPestControl 田格。
        // 对每格套用与 CompletePestControl 相同的恢复逻辑（PestControlled + 视情况恢复 Growing），
        // 逐格触发 OnTileFlagsChanged(tileId)；返回被清除的田格数。
        int CompleteAllPestControl();

        // SPEC §9.11.4 (v3.81)：统计 pest == AwaitingPestControl 的田格数（与 PestEventIcon 显示条件一致）。
        int CountAwaitingPestControlTiles();

        // SPEC §9.12.4 (v3.52)：消除地鼠偷窃。失败原因：tileId 不存在 / tile.moleTheft != AwaitingMoleTheft。
        // 成功：tile.moleTheft = MoleTheftResolved；若 tile.water != Empty 且 plant.state == Paused → Growing；触发 OnTileFlagsChanged。
        bool CompleteMoleTheft(string tileId);

        event Action OnRoleStatsChanged;
        // SPEC §4.1.11：果实背包变化；普通收获写入后触发。
        event Action OnFruitBagChanged;
        // SPEC §4.1.11：收获入包后的可选表现事件（tileId, plantConfigId, fruitCount）。
        event Action<string, string, int> OnHarvestFruitReady;

        // 自 v2.10 起新增（SPEC §6 / §9.7）：肥料背包变化与一次施肥执行完成。
        event Action OnFertilizerBagChanged;
        event Action<string, string> OnFertilizeApplied;

        // 自 v3.18 起新增（SPEC §4.1.10）：变异植物创建 / 收获事件。
        // OnMutationCreated(mutationId)：4 格植物刚被创建（state==AwaitingHarvest）。
        // OnMutationHarvested(mutationId, kind, refId)：玩家点击图标完成收获后发出，UI 据此弹窗。
        event Action<string> OnMutationCreated;
        event Action<string, MutationKind, string> OnMutationHarvested;

        // SPEC §4.1.12 (v3.70)：精灵背包 / 上场变更。
        event Action OnPetBagChanged;
        event Action OnPetDeploymentChanged;

        string GrantPet(string petConfigId);
        bool TryAutoDeploy(string instanceId);
        bool TryDeployPetToField(string instanceId);
        bool UndeployPet(PetFieldSlot slot);
        PetDeployment GetPetDeployment();
        PetInstance GetPetInstance(string instanceId);
        string GetDeployedPetConfigId(PetFieldSlot slot);
        IReadOnlyList<PetInstance> GetOwnedPets();
        IReadOnlyList<PetInstance> GetDeployedPetInstances();
        bool IsPetDeployed(string instanceId);
        bool TryGetDeploySlotOf(string instanceId, out PetFieldSlot slot);
        int GetPetBagCount();
        int GetFieldPetLimit();
        RestrictionProfile GetRestrictionProfile();

        // ---- 自 v3.40 起新增（SPEC §6 / §9.8.12）：食物与体力 ----
        // GetFoodBag：返回 GameSession.foodBag 引用（UI 刷新用）。
        // GetFoodConfigs：返回 GameSession.foodConfigs 引用（UI 列表渲染、查名/查图标用）。
        // GetFoodConfig(id)：按 id 查找单条 FoodConfig，未找到返回 null。
        // SelectActiveFood(foodId)：写入 PlayerFoodBag.activeId；id 必须存在于 foodConfigs 且 count>0，
        //   传空字符串 / null 表示清空选中；非法时仅 Warning 并保持原值。成功后触发 OnFoodBagChanged。
        // GetActiveFood：读取当前活跃食物 id（未选时返回空字符串或 null）。
        // EatOne(foodId)：消耗 1 个指定食物，stamina += FoodConfig.staminaGain 并 clamp(0, staminaMax)。
        //   失败原因（返回 false）：foodId 不存在 / 库存 <= 0 / 已满（stamina == staminaMax）。
        //   成功后依次触发：OnFoodBagChanged、OnStaminaChanged(stamina, staminaMax)。
        // EatToFull(foodId)：在不超过上限前提下循环消耗指定 foodId 直至体力满或库存耗尽；
        //   每次 += staminaGain 且 clamp(0, staminaMax)。返回实际消耗份数。仅触发 1 次 OnFoodBagChanged
        //   与 1 次 OnStaminaChanged（合批），减少事件抖动。
        // IsRoleFull：等价于 stamina == staminaMax。
        PlayerFoodBag GetFoodBag();
        IReadOnlyList<FoodConfig> GetFoodConfigs();
        FoodConfig GetFoodConfig(string foodId);
        void SelectActiveFood(string foodId);
        string GetActiveFood();
        bool EatOne(string foodId);
        int EatToFull(string foodId);
        bool IsRoleFull();

        // SPEC §6 / §9.8.12 (v3.40)：食物背包变化与体力变化事件。
        event Action OnFoodBagChanged;
        event Action<int, int> OnStaminaChanged;

        // ---- 自 v3.41 起新增（SPEC §6 / §9.8.13）：果实食用 ----
        // SelectActiveFruit(plantConfigId)：写入 PlayerFruitBag.activeId；id 必须存在于 plantConfigs 且果实
        //   库存 count > 0，传空字符串 / null 表示清空选中；非法时仅 Warning 并保持原值；成功后触发 OnFruitBagChanged。
        // GetActiveFruit：读取当前活跃果实 plantConfigId（未选时返回空字符串或 null）。
        // EatOneFruit(plantConfigId)：消耗 1 个指定果实，stamina += FruitStaminaGain(plantConfigId) 并 clamp(0, staminaMax)。
        //   FruitStaminaGain 优先取 PlantConfig.fruitStaminaGain（>0），否则常量 10。失败原因（返回 false）：
        //   plantConfigId 不存在 / 果实库存 <= 0 / 已满。
        //   成功后依次触发：OnFruitBagChanged、OnStaminaChanged(stamina, staminaMax)。
        // EatFruitToFull(plantConfigId)：循环 EatOneFruit 直至体力满或库存耗尽；返回实际消耗的果实份数。
        //   仅触发 1 次 OnFruitBagChanged 与 1 次 OnStaminaChanged（合批）。
        void SelectActiveFruit(string plantConfigId);
        string GetActiveFruit();
        bool EatOneFruit(string plantConfigId);
        int EatFruitToFull(string plantConfigId);

        // SPEC §6 / §12.9 (v3.43)：战斗开战体力扣除。
        // TryConsumeStamina(amount)：若 amount<=0 或当前 stamina<amount 则返回 false 且不修改；否则 stamina-=amount 并 clamp(0,staminaMax)，触发 OnStaminaChanged。
        bool TryConsumeStamina(int amount);
    }
}
