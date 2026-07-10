// SPEC §13：GameSession 运行时快照（JsonUtility DTO）。
using System;
using System.Collections.Generic;
using PetDemo.Core;
using PetDemo.Farm;

namespace PetDemo.Save
{
    [Serializable]
    public class GameSaveSnapshot
    {
        public int formatVersion = GameSaveRepository.FormatVersion;
        public long savedAtUtcTicks;
        public int plantInstanceSeq;
        public int mutationInstanceSeq;
        public int petInstanceSeq;
        public string focusTileId;

        public RoleStatsSave role;
        public CropTileSave[] farmTiles;
        public PlantInstanceSave[] plants;
        public MutationPlantSave[] mutations;
        public PlayerSeedBagSave seedBag;
        public PlayerFertilizerBagSave fertilizerBag;
        public PlayerFruitBagSave fruitBag;
        public PlayerFoodBagSave foodBag;
        public PlayerPetBagSave petBag;
        public PetDeploymentSave petDeployment;
        public RestrictionProfileSave restrictionProfile;
        public UiProgressSave uiProgress;
        // SPEC §9.14.4：创角界面好友列表与创角状态。
        public FriendProfileSave[] friends;
        public CharacterCreationSave characterCreation;
        // SPEC §9.14.12 (v3.194)：训练会话。
        public TrainingSessionSave trainingSession;

        public static GameSaveSnapshot FromPlantingService(PlantingService service)
        {
            if (service == null)
                return null;

            var session = service.GetSessionForSave();
            if (session == null)
                return null;

            return new GameSaveSnapshot
            {
                formatVersion = GameSaveRepository.FormatVersion,
                savedAtUtcTicks = DateTime.UtcNow.Ticks,
                plantInstanceSeq = service.GetPlantInstanceSeqForSave(),
                mutationInstanceSeq = service.GetMutationInstanceSeqForSave(),
                petInstanceSeq = service.GetPetInstanceSeqForSave(),
                focusTileId = service.GetFocusTileIdForSave(),
                role = RoleStatsSave.From(session.role),
                farmTiles = CropTileSave.FromList(session.farmTiles),
                plants = PlantInstanceSave.FromList(session.plants),
                mutations = MutationPlantSave.FromList(session.mutations),
                seedBag = PlayerSeedBagSave.From(session.seedBag),
                fertilizerBag = PlayerFertilizerBagSave.From(session.fertilizerBag),
                fruitBag = PlayerFruitBagSave.From(session.fruitBag),
                foodBag = PlayerFoodBagSave.From(session.foodBag),
                petBag = PlayerPetBagSave.From(session.petBag),
                petDeployment = PetDeploymentSave.From(session.petDeployment),
                restrictionProfile = RestrictionProfileSave.From(session.restrictionProfile),
                uiProgress = UiProgressSave.From(session.uiProgress),
                friends = FriendProfileSave.FromList(session.friends),
                characterCreation = CharacterCreationSave.From(session.characterCreation),
                trainingSession = TrainingSessionSave.From(session.trainingSession),
            };
        }

        public static void ApplyToSession(GameSaveSnapshot snapshot, GameSession session)
        {
            if (snapshot == null || session == null)
                return;

            session.role = snapshot.role != null ? snapshot.role.ToModel() : RoleStats.CreateDefault();
            session.farmTiles = CropTileSave.ToList(snapshot.farmTiles);
            session.plants = PlantInstanceSave.ToList(snapshot.plants);
            session.mutations = MutationPlantSave.ToList(snapshot.mutations);
            session.seedBag = snapshot.seedBag != null ? snapshot.seedBag.ToModel() : new PlayerSeedBag();
            session.fertilizerBag = snapshot.fertilizerBag != null
                ? snapshot.fertilizerBag.ToModel()
                : new PlayerFertilizerBag();
            session.fruitBag = snapshot.fruitBag != null ? snapshot.fruitBag.ToModel() : new PlayerFruitBag();
            session.foodBag = snapshot.foodBag != null ? snapshot.foodBag.ToModel() : new PlayerFoodBag();
            session.petBag = snapshot.petBag != null ? snapshot.petBag.ToModel() : new PlayerPetBag();
            session.petDeployment = snapshot.petDeployment != null
                ? snapshot.petDeployment.ToModel()
                : new PetDeployment();
            session.restrictionProfile = snapshot.restrictionProfile != null
                ? snapshot.restrictionProfile.ToModel()
                : new RestrictionProfile();
            session.uiProgress = snapshot.uiProgress != null
                ? snapshot.uiProgress.ToModel()
                : new UiProgress();

            // SPEC §9.14.4：好友列表为空（旧档/缺字段）时回退默认目录。
            var friends = FriendProfileSave.ToList(snapshot.friends);
            session.friends = (friends != null && friends.Count > 0)
                ? friends
                : FriendCatalog.BuildDefault();
            session.characterCreation = snapshot.characterCreation != null
                ? snapshot.characterCreation.ToModel()
                : new CharacterCreationState();
            session.trainingSession = snapshot.trainingSession != null
                ? snapshot.trainingSession.ToModel()
                : new TrainingSession();
        }

        public static int ResolveSeqFromIds(IEnumerable<string> ids, string prefix)
        {
            int max = 0;
            if (ids == null)
                return 0;

            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id) || !id.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                if (int.TryParse(id.Substring(prefix.Length), out int n) && n > max)
                    max = n;
            }

            return max;
        }
    }

    [Serializable]
    public class RoleStatsSave
    {
        public string displayName;
        public int atk;
        public int def;
        public int maxHp;
        public int currentHp;
        public int agility;
        public int stamina;
        public int staminaMax;
        public float critRate;
        public float comboRate;
        public float counterRate;
        public float blockRate;
        public float critResist;
        public float comboResist;
        public float counterResist;
        public float blockResist;
        public int criticalHit;
        public int combo;
        public int counterattack;
        public int stun;
        public int evasion;
        public int lifeSteal;
        public int intelligence;
        public int memory;
        public int imagination;
        public int physique;
        public int charm;
        public int emotionalIntelligence;
        public int level;
        public int currentExp;
        public int expToNextLevel;

        public static RoleStatsSave From(RoleStats r)
        {
            if (r == null)
                return null;
            return new RoleStatsSave
            {
                displayName = r.displayName,
                atk = r.atk,
                def = r.def,
                maxHp = r.maxHp,
                currentHp = r.currentHp,
                agility = r.agility,
                stamina = r.stamina,
                staminaMax = r.staminaMax,
                critRate = r.critRate,
                comboRate = r.comboRate,
                counterRate = r.counterRate,
                blockRate = r.blockRate,
                critResist = r.critResist,
                comboResist = r.comboResist,
                counterResist = r.counterResist,
                blockResist = r.blockResist,
                criticalHit = r.criticalHit,
                combo = r.combo,
                counterattack = r.counterattack,
                stun = r.stun,
                evasion = r.evasion,
                lifeSteal = r.lifeSteal,
                intelligence = r.intelligence,
                memory = r.memory,
                imagination = r.imagination,
                physique = r.physique,
                charm = r.charm,
                emotionalIntelligence = r.emotionalIntelligence,
                level = r.level,
                currentExp = r.currentExp,
                expToNextLevel = r.expToNextLevel,
            };
        }

        public RoleStats ToModel()
        {
            var role = new RoleStats
            {
                displayName = displayName,
                atk = atk,
                def = def,
                maxHp = maxHp,
                currentHp = currentHp,
                agility = agility,
                stamina = stamina,
                staminaMax = staminaMax,
                critRate = critRate,
                comboRate = comboRate,
                counterRate = counterRate,
                blockRate = blockRate,
                critResist = critResist,
                comboResist = comboResist,
                counterResist = counterResist,
                blockResist = blockResist,
                criticalHit = criticalHit,
                combo = combo,
                counterattack = counterattack,
                stun = stun,
                evasion = evasion,
                lifeSteal = lifeSteal,
                intelligence = intelligence,
                memory = memory,
                imagination = imagination,
                physique = physique,
                charm = charm,
                emotionalIntelligence = emotionalIntelligence,
                level = level,
                currentExp = currentExp,
                expToNextLevel = expToNextLevel,
            };
            ApplyHexDefaultsIfLegacyUnset(role);
            ApplyLevelExpDefaultsIfLegacyUnset(role);
            ApplyGrowthAttrsDefaultsIfLegacyUnset(role);
            return role;
        }

        /// <summary>旧存档无等级经验字段时回填 §9.14.11 默认值。</summary>
        private static void ApplyLevelExpDefaultsIfLegacyUnset(RoleStats role)
        {
            if (role == null)
                return;
            if (role.level <= 0)
                role.level = 1;
            if (role.currentExp < 0)
                role.currentExp = 0;
            if (role.expToNextLevel <= 0)
                role.expToNextLevel = 100;
        }

        /// <summary>旧存档无六宫字段时（全 0）回填 §5 默认值。</summary>
        private static void ApplyHexDefaultsIfLegacyUnset(RoleStats role)
        {
            if (role == null)
                return;
            if (role.criticalHit != 0 || role.combo != 0 || role.counterattack != 0
                || role.stun != 0 || role.evasion != 0 || role.lifeSteal != 0)
                return;
            role.criticalHit = 3;
            role.combo = 6;
            role.counterattack = 12;
            role.stun = 2;
            role.evasion = 4;
            role.lifeSteal = 8;
        }

        /// <summary>旧存档无家园成长六属性时（全 0）按当前等级从表回填（§B.21）。</summary>
        private static void ApplyGrowthAttrsDefaultsIfLegacyUnset(RoleStats role)
        {
            if (role == null)
                return;
            if (role.intelligence != 0 || role.memory != 0 || role.imagination != 0
                || role.physique != 0 || role.charm != 0 || role.emotionalIntelligence != 0)
                return;
            RoleLevelConfigCatalog.ApplyToRole(role);
        }
    }

    // SPEC §9.14.4：创角好友档案存档 DTO。
    [Serializable]
    public class FriendProfileSave
    {
        public string id;
        public string displayName;
        public string avatarResource;
        public bool online;
        public int intimacy;

        public static FriendProfileSave[] FromList(List<FriendProfile> list)
        {
            if (list == null || list.Count == 0)
                return Array.Empty<FriendProfileSave>();
            var arr = new FriendProfileSave[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var f = list[i];
                arr[i] = f == null ? null : new FriendProfileSave
                {
                    id = f.id,
                    displayName = f.displayName,
                    avatarResource = f.avatarResource,
                    online = f.online,
                    intimacy = f.intimacy,
                };
            }
            return arr;
        }

        public static List<FriendProfile> ToList(FriendProfileSave[] arr)
        {
            var list = new List<FriendProfile>();
            if (arr == null)
                return list;
            for (int i = 0; i < arr.Length; i++)
            {
                var s = arr[i];
                if (s == null || string.IsNullOrEmpty(s.id))
                    continue;
                list.Add(new FriendProfile
                {
                    id = s.id,
                    displayName = s.displayName,
                    avatarResource = s.avatarResource,
                    online = s.online,
                    intimacy = s.intimacy,
                });
            }
            return list;
        }
    }

    // SPEC §9.14.4：创角状态存档 DTO。
    [Serializable]
    public class CharacterCreationSave
    {
        public bool created;
        public string partnerFriendId;
        // SPEC §9.14.4（v3.203）：亲密度页签展示模式；旧档缺字段视为 Normal。
        public int friendListMode;
        // SPEC §9.14.4（v3.206）：开局营救待完成；旧档缺字段视为 false。
        public bool openingRescuePending;

        public static CharacterCreationSave From(CharacterCreationState state)
        {
            if (state == null)
                return null;
            return new CharacterCreationSave
            {
                created = state.created,
                partnerFriendId = state.partnerFriendId,
                friendListMode = (int)state.friendListMode,
                openingRescuePending = state.openingRescuePending,
            };
        }

        public CharacterCreationState ToModel()
        {
            return new CharacterCreationState
            {
                created = created,
                partnerFriendId = partnerFriendId,
                friendListMode = (FriendListMode)friendListMode,
                openingRescuePending = openingRescuePending,
            };
        }
    }

    /// <summary>SPEC §9.14.12 (v3.194)：训练会话存档。</summary>
    [Serializable]
    public class TrainingSessionSave
    {
        public string courseId;
        public long endUnixMs;
        public int activeFilterMask;

        public static TrainingSessionSave From(TrainingSession state)
        {
            if (state == null)
                return null;
            return new TrainingSessionSave
            {
                courseId = state.courseId ?? string.Empty,
                endUnixMs = state.endUnixMs,
                activeFilterMask = state.activeFilterMask,
            };
        }

        public TrainingSession ToModel()
        {
            return new TrainingSession
            {
                courseId = courseId ?? string.Empty,
                endUnixMs = endUnixMs,
                activeFilterMask = activeFilterMask & 0x3F,
            };
        }
    }

    [Serializable]
    public class CropTileSave
    {
        public string tileId;
        public int orderIndex;
        public int planting;
        public int fertilizer;
        public int water;
        public int pest;
        public int moleTheft;
        public int harvest;
        public string plantInstanceId;
        public string lockedByMutationId;

        public static CropTileSave[] FromList(List<CropTile> list)
        {
            if (list == null || list.Count == 0)
                return Array.Empty<CropTileSave>();
            var arr = new CropTileSave[list.Count];
            for (int i = 0; i < list.Count; i++)
                arr[i] = From(list[i]);
            return arr;
        }

        public static List<CropTile> ToList(CropTileSave[] arr)
        {
            var list = new List<CropTile>();
            if (arr == null)
                return list;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] != null)
                    list.Add(arr[i].ToModel());
            }
            return list;
        }

        private static CropTileSave From(CropTile t)
        {
            return new CropTileSave
            {
                tileId = t.tileId,
                orderIndex = t.orderIndex,
                planting = (int)t.planting,
                fertilizer = (int)t.fertilizer,
                water = (int)t.water,
                pest = (int)t.pest,
                moleTheft = (int)t.moleTheft,
                harvest = (int)t.harvest,
                plantInstanceId = t.plantInstanceId,
                lockedByMutationId = t.lockedByMutationId,
            };
        }

        public CropTile ToModel()
        {
            return new CropTile
            {
                tileId = tileId,
                orderIndex = orderIndex,
                planting = (PlantingFlag)planting,
                fertilizer = (FertilizerFlag)fertilizer,
                water = (WaterStage)water,
                pest = (PestFlag)pest,
                moleTheft = (MoleTheftFlag)moleTheft,
                harvest = (HarvestFlag)harvest,
                plantInstanceId = plantInstanceId,
                lockedByMutationId = lockedByMutationId,
            };
        }
    }

    [Serializable]
    public class PlantInstanceSave
    {
        public string instanceId;
        public string plantConfigId;
        public string tileId;
        public int state;
        public int waterConsumed;
        public int appearanceNode;
        public float currentStageRemainingSec;
        public float appliedFertilizerSpeedMul;
        public bool pestEventConsumed;
        public int pestSpriteRollMask;
        public bool moleTheftEventConsumed;
        public int moleSpriteRollMask;

        public static PlantInstanceSave[] FromList(List<PlantInstance> list)
        {
            if (list == null || list.Count == 0)
                return Array.Empty<PlantInstanceSave>();
            var arr = new PlantInstanceSave[list.Count];
            for (int i = 0; i < list.Count; i++)
                arr[i] = From(list[i]);
            return arr;
        }

        public static List<PlantInstance> ToList(PlantInstanceSave[] arr)
        {
            var list = new List<PlantInstance>();
            if (arr == null)
                return list;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] != null)
                    list.Add(arr[i].ToModel());
            }
            return list;
        }

        private static PlantInstanceSave From(PlantInstance p)
        {
            return new PlantInstanceSave
            {
                instanceId = p.instanceId,
                plantConfigId = p.plantConfigId,
                tileId = p.tileId,
                state = (int)p.state,
                waterConsumed = p.waterConsumed,
                appearanceNode = p.appearanceNode,
                currentStageRemainingSec = p.currentStageRemainingSec,
                appliedFertilizerSpeedMul = p.appliedFertilizerSpeedMul,
                pestEventConsumed = p.pestEventConsumed,
                pestSpriteRollMask = p.pestSpriteRollMask,
                moleTheftEventConsumed = p.moleTheftEventConsumed,
                moleSpriteRollMask = p.moleSpriteRollMask,
            };
        }

        public PlantInstance ToModel()
        {
            return new PlantInstance
            {
                instanceId = instanceId,
                plantConfigId = plantConfigId,
                tileId = tileId,
                state = (PlantState)state,
                waterConsumed = waterConsumed,
                appearanceNode = appearanceNode,
                currentStageRemainingSec = currentStageRemainingSec,
                appliedFertilizerSpeedMul = appliedFertilizerSpeedMul,
                pestEventConsumed = pestEventConsumed,
                pestSpriteRollMask = pestSpriteRollMask,
                moleTheftEventConsumed = moleTheftEventConsumed,
                moleSpriteRollMask = moleSpriteRollMask,
            };
        }
    }

    [Serializable]
    public class MutationPlantSave
    {
        public string instanceId;
        public int kind;
        public string refId;
        public string[] tileIds;
        public int state;

        public static MutationPlantSave[] FromList(List<MutationPlant> list)
        {
            if (list == null || list.Count == 0)
                return Array.Empty<MutationPlantSave>();
            var arr = new MutationPlantSave[list.Count];
            for (int i = 0; i < list.Count; i++)
                arr[i] = From(list[i]);
            return arr;
        }

        public static List<MutationPlant> ToList(MutationPlantSave[] arr)
        {
            var list = new List<MutationPlant>();
            if (arr == null)
                return list;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] != null)
                    list.Add(arr[i].ToModel());
            }
            return list;
        }

        private static MutationPlantSave From(MutationPlant m)
        {
            return new MutationPlantSave
            {
                instanceId = m.instanceId,
                kind = (int)m.kind,
                refId = m.refId,
                tileIds = m.tileIds != null ? m.tileIds.ToArray() : Array.Empty<string>(),
                state = (int)m.state,
            };
        }

        public MutationPlant ToModel()
        {
            return new MutationPlant
            {
                instanceId = instanceId,
                kind = (MutationKind)kind,
                refId = refId,
                tileIds = tileIds != null ? new List<string>(tileIds) : new List<string>(),
                state = (PlantState)state,
            };
        }
    }

    [Serializable]
    public class SeedStackSave
    {
        public string plantConfigId;
        public int count;
    }

    [Serializable]
    public class SeedPackStackSave
    {
        public int quality;
        public int count;
    }

    [Serializable]
    public class ActiveSelectionSave
    {
        public int kind;
        public string id;
    }

    [Serializable]
    public class PlayerSeedBagSave
    {
        public SeedStackSave[] seeds;
        public SeedPackStackSave[] seedPacks;
        public ActiveSelectionSave active;

        public static PlayerSeedBagSave From(PlayerSeedBag bag)
        {
            if (bag == null)
                return new PlayerSeedBagSave();
            return new PlayerSeedBagSave
            {
                seeds = CopySeeds(bag.seeds),
                seedPacks = CopyPacks(bag.seedPacks),
                active = bag.active != null
                    ? new ActiveSelectionSave { kind = (int)bag.active.kind, id = bag.active.id }
                    : null,
            };
        }

        public PlayerSeedBag ToModel()
        {
            var bag = new PlayerSeedBag();
            if (seeds != null)
            {
                for (int i = 0; i < seeds.Length; i++)
                {
                    if (seeds[i] == null)
                        continue;
                    bag.seeds.Add(new SeedStack { plantConfigId = seeds[i].plantConfigId, count = seeds[i].count });
                }
            }
            if (seedPacks != null)
            {
                for (int i = 0; i < seedPacks.Length; i++)
                {
                    if (seedPacks[i] == null)
                        continue;
                    bag.seedPacks.Add(new SeedPackStack
                    {
                        quality = (SeedPackQuality)seedPacks[i].quality,
                        count = seedPacks[i].count,
                    });
                }
            }
            if (active != null)
                bag.active = new ActiveSelection { kind = (ActiveKind)active.kind, id = active.id };
            return bag;
        }

        private static SeedStackSave[] CopySeeds(List<SeedStack> list)
        {
            if (list == null || list.Count == 0)
                return Array.Empty<SeedStackSave>();
            var arr = new SeedStackSave[list.Count];
            for (int i = 0; i < list.Count; i++)
                arr[i] = new SeedStackSave { plantConfigId = list[i].plantConfigId, count = list[i].count };
            return arr;
        }

        private static SeedPackStackSave[] CopyPacks(List<SeedPackStack> list)
        {
            if (list == null || list.Count == 0)
                return Array.Empty<SeedPackStackSave>();
            var arr = new SeedPackStackSave[list.Count];
            for (int i = 0; i < list.Count; i++)
                arr[i] = new SeedPackStackSave { quality = (int)list[i].quality, count = list[i].count };
            return arr;
        }
    }

    [Serializable]
    public class FertilizerStackSave
    {
        public string fertilizerId;
        public int count;
    }

    [Serializable]
    public class PlayerFertilizerBagSave
    {
        public FertilizerStackSave[] stacks;
        public string activeId;

        public static PlayerFertilizerBagSave From(PlayerFertilizerBag bag)
        {
            if (bag == null)
                return new PlayerFertilizerBagSave();
            FertilizerStackSave[] stacksArr = Array.Empty<FertilizerStackSave>();
            if (bag.stacks != null && bag.stacks.Count > 0)
            {
                stacksArr = new FertilizerStackSave[bag.stacks.Count];
                for (int i = 0; i < bag.stacks.Count; i++)
                    stacksArr[i] = new FertilizerStackSave
                    {
                        fertilizerId = bag.stacks[i].fertilizerId,
                        count = bag.stacks[i].count,
                    };
            }
            return new PlayerFertilizerBagSave { stacks = stacksArr, activeId = bag.activeId };
        }

        public PlayerFertilizerBag ToModel()
        {
            var bag = new PlayerFertilizerBag { activeId = activeId };
            if (stacks != null)
            {
                for (int i = 0; i < stacks.Length; i++)
                {
                    if (stacks[i] == null)
                        continue;
                    bag.stacks.Add(new FertilizerStack
                    {
                        fertilizerId = stacks[i].fertilizerId,
                        count = stacks[i].count,
                    });
                }
            }
            return bag;
        }
    }

    [Serializable]
    public class FruitStackSave
    {
        public string plantConfigId;
        public int count;
    }

    [Serializable]
    public class PlayerFruitBagSave
    {
        public FruitStackSave[] stacks;
        public string activeId;

        public static PlayerFruitBagSave From(PlayerFruitBag bag)
        {
            if (bag == null)
                return new PlayerFruitBagSave();
            FruitStackSave[] stacksArr = Array.Empty<FruitStackSave>();
            if (bag.stacks != null && bag.stacks.Count > 0)
            {
                stacksArr = new FruitStackSave[bag.stacks.Count];
                for (int i = 0; i < bag.stacks.Count; i++)
                    stacksArr[i] = new FruitStackSave
                    {
                        plantConfigId = bag.stacks[i].plantConfigId,
                        count = bag.stacks[i].count,
                    };
            }
            return new PlayerFruitBagSave { stacks = stacksArr, activeId = bag.activeId };
        }

        public PlayerFruitBag ToModel()
        {
            var bag = new PlayerFruitBag { activeId = activeId };
            if (stacks != null)
            {
                for (int i = 0; i < stacks.Length; i++)
                {
                    if (stacks[i] == null)
                        continue;
                    bag.stacks.Add(new FruitStack
                    {
                        plantConfigId = stacks[i].plantConfigId,
                        count = stacks[i].count,
                    });
                }
            }
            return bag;
        }
    }

    [Serializable]
    public class FoodStackSave
    {
        public string foodId;
        public int count;
    }

    [Serializable]
    public class PlayerFoodBagSave
    {
        public FoodStackSave[] stacks;
        public string activeId;

        public static PlayerFoodBagSave From(PlayerFoodBag bag)
        {
            if (bag == null)
                return new PlayerFoodBagSave();
            FoodStackSave[] stacksArr = Array.Empty<FoodStackSave>();
            if (bag.stacks != null && bag.stacks.Count > 0)
            {
                stacksArr = new FoodStackSave[bag.stacks.Count];
                for (int i = 0; i < bag.stacks.Count; i++)
                    stacksArr[i] = new FoodStackSave { foodId = bag.stacks[i].foodId, count = bag.stacks[i].count };
            }
            return new PlayerFoodBagSave { stacks = stacksArr, activeId = bag.activeId };
        }

        public PlayerFoodBag ToModel()
        {
            var bag = new PlayerFoodBag { activeId = activeId };
            if (stacks != null)
            {
                for (int i = 0; i < stacks.Length; i++)
                {
                    if (stacks[i] == null)
                        continue;
                    bag.stacks.Add(new FoodStack { foodId = stacks[i].foodId, count = stacks[i].count });
                }
            }
            return bag;
        }
    }

    [Serializable]
    public class PetInstanceSave
    {
        public string instanceId;
        public string petConfigId;
    }

    [Serializable]
    public class PlayerPetBagSave
    {
        public PetInstanceSave[] owned;

        public static PlayerPetBagSave From(PlayerPetBag bag)
        {
            if (bag?.owned == null || bag.owned.Count == 0)
                return new PlayerPetBagSave { owned = Array.Empty<PetInstanceSave>() };
            var arr = new PetInstanceSave[bag.owned.Count];
            for (int i = 0; i < bag.owned.Count; i++)
                arr[i] = new PetInstanceSave
                {
                    instanceId = bag.owned[i].instanceId,
                    petConfigId = bag.owned[i].petConfigId,
                };
            return new PlayerPetBagSave { owned = arr };
        }

        public PlayerPetBag ToModel()
        {
            var bag = new PlayerPetBag();
            if (owned != null)
            {
                for (int i = 0; i < owned.Length; i++)
                {
                    if (owned[i] == null)
                        continue;
                    bag.owned.Add(new PetInstance
                    {
                        instanceId = owned[i].instanceId,
                        petConfigId = owned[i].petConfigId,
                    });
                }
            }
            return bag;
        }
    }

    [Serializable]
    public class PetDeploymentSave
    {
        public string lowerLeftInstanceId;
        public string upperLeftInstanceId;

        public static PetDeploymentSave From(PetDeployment d)
        {
            if (d == null)
                return new PetDeploymentSave();
            return new PetDeploymentSave
            {
                lowerLeftInstanceId = d.lowerLeftInstanceId,
                upperLeftInstanceId = d.upperLeftInstanceId,
            };
        }

        public PetDeployment ToModel()
        {
            return new PetDeployment
            {
                lowerLeftInstanceId = lowerLeftInstanceId,
                upperLeftInstanceId = upperLeftInstanceId,
            };
        }
    }

    [Serializable]
    public class UiProgressSave
    {
        public bool mainStoryArenaEntryUnlocked;
        public int mainStoryHighestClearedLevel;

        public static UiProgressSave From(UiProgress progress)
        {
            if (progress == null)
                return new UiProgressSave();
            return new UiProgressSave
            {
                mainStoryArenaEntryUnlocked = progress.mainStoryArenaEntryUnlocked,
                mainStoryHighestClearedLevel = progress.mainStoryHighestClearedLevel,
            };
        }

        public UiProgress ToModel()
        {
            return new UiProgress
            {
                mainStoryArenaEntryUnlocked = mainStoryArenaEntryUnlocked,
                mainStoryHighestClearedLevel = mainStoryHighestClearedLevel,
            };
        }
    }

    [Serializable]
    public class RestrictionProfileSave
    {
        public int fieldPetLimit;
        public bool accepted;

        public static RestrictionProfileSave From(RestrictionProfile r)
        {
            if (r == null)
                return new RestrictionProfileSave();
            return new RestrictionProfileSave { fieldPetLimit = r.fieldPetLimit, accepted = r.accepted };
        }

        public RestrictionProfile ToModel()
        {
            return new RestrictionProfile { fieldPetLimit = fieldPetLimit, accepted = accepted };
        }
    }
}
