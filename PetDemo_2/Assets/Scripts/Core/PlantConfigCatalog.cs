// 默认植物 / 种子包 / 初始仓库配置目录，对应 SPEC §B.2 / §B.4 / §B.5。
// 自 v1.3 起：运行时优先从 Resources/Configs/Farm/ 下的 CSV 装载（plants.csv、
// seed_pack_contents.csv、initial_inventory.csv），缺表或解析失败时回退到下方
// BuildDefault* 内置默认值，确保 P0 闭环不被破坏。plants.csv 自 v3.43 起支持可选列
// fruitIcon（果实专属图标，见 PlantConfig.fruitIconResource）。
//
// 注意：P0 验收期 baseStageSeconds 临时取 3.0f 以便在 Editor PlayMode 中快速跑通闭环；
// 正式美术调参时改回附录 B.2 默认值（30/45/30/25/50/35）。SPEC §11 v0.8 已注明。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PetDemo.Core
{
    public static class PlantConfigCatalog
    {
        // P0 验收期统一缩短至 3 秒/阶；正式版本改回附录 B.2 默认值。
        public const float TestStageSeconds = 3.0f;

        // CSV 装载路径（Resources.Load 不带扩展名）。
        public const string PlantsCsvResourcePath = "Configs/Farm/plants";
        public const string PackContentsCsvResourcePath = "Configs/Farm/seed_pack_contents";
        public const string InitialInventoryCsvResourcePath = "Configs/Farm/initial_inventory";
        public const string FertilizersCsvResourcePath = "Configs/Farm/fertilizers";
        // SPEC §B.11 / §B.12 (v3.18)
        public const string PetsCsvResourcePath = "Configs/Mutation/pets";
        public const string SkillsCsvResourcePath = "Configs/Mutation/skills";

        // ============================================================
        // CSV 装载入口（带 fallback）
        // ============================================================
        public static List<PlantConfig> LoadPlantConfigsFromCsv()
        {
            var ta = Resources.Load<TextAsset>(PlantsCsvResourcePath);
            if (ta == null || string.IsNullOrEmpty(ta.text))
            {
                UnityEngine.Debug.LogWarning("[PlantConfigCatalog] plants.csv 未找到，回退到 BuildDefaultPlantConfigs。");
                return BuildDefaultPlantConfigs();
            }

            var table = CsvTable.Parse(ta.text);
            int idxId = table.IndexOfHeader("id");
            int idxName = table.IndexOfHeader("displayName");
            int idxS1 = table.IndexOfHeader("sprite1");
            int idxS2 = table.IndexOfHeader("sprite2");
            int idxS3 = table.IndexOfHeader("sprite3");
            int idxS4 = table.IndexOfHeader("sprite4");
            int idxS5 = table.IndexOfHeader("sprite5");
            int idxSp1 = table.IndexOfHeader("spine1");
            int idxSp2 = table.IndexOfHeader("spine2");
            int idxSp3 = table.IndexOfHeader("spine3");
            int idxSp4 = table.IndexOfHeader("spine4");
            int idxSp5 = table.IndexOfHeader("spine5");
            int idxFruitIcon = table.IndexOfHeader("fruitIcon");
            int idxHarvestFruit = table.IndexOfHeader("harvestFruitCount");
            int idxStage = table.IndexOfHeader("baseStageSeconds");
            int idxFert = table.IndexOfHeader("fertilizerSpeedMul");
            int idxAfter = table.IndexOfHeader("afterHarvest");
            int idxPestInt = table.IndexOfHeader("pestEventIntervalSec");
            int idxPestProb = table.IndexOfHeader("pestEventProb");
            int idxPestSpriteProb = table.IndexOfHeader("pestSpriteProb");
            int idxMoleSpriteProb = table.IndexOfHeader("moleSpriteProb");
            int idxReward = table.IndexOfHeader("harvestRoleReward");
            int idxEatBuff = table.IndexOfHeader("eatBuffIcon");
            if (idxId < 0 || idxName < 0 || idxS1 < 0 || idxS2 < 0 || idxS3 < 0 || idxS4 < 0 || idxS5 < 0
                || idxHarvestFruit < 0
                || idxStage < 0 || idxFert < 0 || idxAfter < 0 || idxPestInt < 0 || idxPestProb < 0
                || idxPestSpriteProb < 0 || idxMoleSpriteProb < 0 || idxReward < 0)
            {
                UnityEngine.Debug.LogWarning("[PlantConfigCatalog] plants.csv 缺少必需列，回退到 BuildDefaultPlantConfigs。");
                return BuildDefaultPlantConfigs();
            }

            var list = new List<PlantConfig>(table.rows.Count);
            for (int i = 0; i < table.rows.Count; i++)
            {
                var row = table.rows[i];
                string id = row.Get(idxId);
                string name = row.Get(idxName);
                string s1 = row.Get(idxS1);
                string s2 = row.Get(idxS2);
                string s3 = row.Get(idxS3);
                string s4 = row.Get(idxS4);
                string s5 = row.Get(idxS5);
                string afterRaw = row.Get(idxAfter);
                string rewardRaw = row.Get(idxReward);

                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name)
                    || string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)
                    || string.IsNullOrEmpty(s3) || string.IsNullOrEmpty(s4)
                    || string.IsNullOrEmpty(s5) || string.IsNullOrEmpty(afterRaw) || string.IsNullOrEmpty(rewardRaw))
                {
                    UnityEngine.Debug.LogWarning($"[PlantConfigCatalog] plants.csv 第 {row.lineNumber} 行字段缺失，跳过。");
                    continue;
                }

                if (!TryParseFloat(row.Get(idxStage), out float baseStageSec)
                    || !TryParseFloat(row.Get(idxFert), out float fertMul)
                    || !TryParseFloat(row.Get(idxPestInt), out float pestInt)
                    || !TryParseFloat(row.Get(idxPestProb), out float pestProb)
                    || !TryParseFloat(row.Get(idxPestSpriteProb), out float pestSpriteProb)
                    || !TryParseFloat(row.Get(idxMoleSpriteProb), out float moleSpriteProb))
                {
                    UnityEngine.Debug.LogWarning($"[PlantConfigCatalog] plants.csv 第 {row.lineNumber} 行数值列解析失败，跳过。");
                    continue;
                }

                if (!Enum.TryParse<AfterHarvest>(afterRaw, false, out var afterHarvest))
                {
                    UnityEngine.Debug.LogWarning($"[PlantConfigCatalog] plants.csv 第 {row.lineNumber} 行 afterHarvest='{afterRaw}' 非法，跳过。");
                    continue;
                }
                if (!TryParseHarvestRoleStat(rewardRaw, out var rewardStat))
                {
                    UnityEngine.Debug.LogWarning($"[PlantConfigCatalog] plants.csv 第 {row.lineNumber} 行 harvestRoleReward='{rewardRaw}' 非法，跳过。");
                    continue;
                }

                if (!int.TryParse(row.Get(idxHarvestFruit).Trim(), System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out int harvestFruit) || harvestFruit <= 0)
                {
                    UnityEngine.Debug.LogWarning($"[PlantConfigCatalog] plants.csv 第 {row.lineNumber} 行 harvestFruitCount 非法，跳过。");
                    continue;
                }

                string fruitIconRes = null;
                if (idxFruitIcon >= 0)
                {
                    var fi = row.Get(idxFruitIcon).Trim();
                    if (!string.IsNullOrEmpty(fi))
                        fruitIconRes = fi;
                }

                string eatBuffRes = null;
                if (idxEatBuff >= 0)
                {
                    var eb = row.Get(idxEatBuff).Trim();
                    if (!string.IsNullOrEmpty(eb))
                        eatBuffRes = eb;
                }

                var spineIds = ReadOptionalSpineColumns(row, idxSp1, idxSp2, idxSp3, idxSp4, idxSp5);

                list.Add(new PlantConfig
                {
                    id = id,
                    displayName = name,
                    appearanceSpriteIds = new List<string> { s1, s2, s3, s4, s5 },
                    appearanceSpineIds = spineIds,
                    fruitIconResource = fruitIconRes,
                    harvestFruitCount = harvestFruit,
                    eatBuffIconResource = eatBuffRes,
                    baseStageSeconds = baseStageSec,
                    fertilizerSpeedMul = fertMul,
                    afterHarvest = afterHarvest,
                    pestEventIntervalSec = pestInt,
                    pestEventProb = pestProb,
                    pestSpriteProb = Mathf.Clamp01(pestSpriteProb),
                    moleSpriteProb = Mathf.Clamp01(moleSpriteProb),
                    harvestRewardStat = rewardStat,
                });
            }

            if (list.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[PlantConfigCatalog] plants.csv 全部行非法，回退到 BuildDefaultPlantConfigs。");
                return BuildDefaultPlantConfigs();
            }
            return list;
        }

        public static List<SeedPackContents> LoadPackContentsFromCsv()
        {
            var ta = Resources.Load<TextAsset>(PackContentsCsvResourcePath);
            if (ta == null || string.IsNullOrEmpty(ta.text))
            {
                UnityEngine.Debug.LogWarning("[PlantConfigCatalog] seed_pack_contents.csv 未找到，回退到 BuildDefaultPackContents。");
                return BuildDefaultPackContents();
            }

            var table = CsvTable.Parse(ta.text);
            int idxQuality = table.IndexOfHeader("quality");
            int idxPlantId = table.IndexOfHeader("plantConfigId");
            int idxWeight = table.IndexOfHeader("weight");
            if (idxQuality < 0 || idxPlantId < 0 || idxWeight < 0)
            {
                UnityEngine.Debug.LogWarning("[PlantConfigCatalog] seed_pack_contents.csv 缺少必需列，回退到 BuildDefaultPackContents。");
                return BuildDefaultPackContents();
            }

            var byQuality = new Dictionary<SeedPackQuality, SeedPackContents>();
            for (int i = 0; i < table.rows.Count; i++)
            {
                var row = table.rows[i];
                string qStr = row.Get(idxQuality);
                string plantId = row.Get(idxPlantId);
                if (string.IsNullOrEmpty(qStr) || string.IsNullOrEmpty(plantId))
                {
                    UnityEngine.Debug.LogWarning($"[PlantConfigCatalog] seed_pack_contents.csv 第 {row.lineNumber} 行字段缺失，跳过。");
                    continue;
                }
                if (!Enum.TryParse<SeedPackQuality>(qStr, false, out var quality))
                {
                    UnityEngine.Debug.LogWarning($"[PlantConfigCatalog] seed_pack_contents.csv 第 {row.lineNumber} 行 quality='{qStr}' 非法，跳过。");
                    continue;
                }
                if (!TryParseFloat(row.Get(idxWeight), out float weight) || weight < 0f)
                {
                    UnityEngine.Debug.LogWarning($"[PlantConfigCatalog] seed_pack_contents.csv 第 {row.lineNumber} 行 weight 非法，跳过。");
                    continue;
                }

                if (!byQuality.TryGetValue(quality, out var contents))
                {
                    contents = new SeedPackContents { quality = quality, entries = new List<SeedPackContentsEntry>() };
                    byQuality[quality] = contents;
                }
                contents.entries.Add(new SeedPackContentsEntry { plantConfigId = plantId, weight = weight });
            }

            if (byQuality.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[PlantConfigCatalog] seed_pack_contents.csv 全部行非法，回退到 BuildDefaultPackContents。");
                return BuildDefaultPackContents();
            }

            var list = new List<SeedPackContents>(byQuality.Count);
            foreach (var kv in byQuality)
                list.Add(kv.Value);
            return list;
        }

        public static InitialInventory LoadInitialInventoryFromCsv()
        {
            var ta = Resources.Load<TextAsset>(InitialInventoryCsvResourcePath);
            if (ta == null || string.IsNullOrEmpty(ta.text))
            {
                UnityEngine.Debug.LogWarning("[PlantConfigCatalog] initial_inventory.csv 未找到，回退到 BuildDefaultInitialInventory。");
                return BuildDefaultInitialInventory();
            }

            var table = CsvTable.Parse(ta.text);
            int idxKind = table.IndexOfHeader("kind");
            int idxId = table.IndexOfHeader("id");
            int idxCount = table.IndexOfHeader("count");
            if (idxKind < 0 || idxId < 0 || idxCount < 0)
            {
                UnityEngine.Debug.LogWarning("[PlantConfigCatalog] initial_inventory.csv 缺少必需列，回退到 BuildDefaultInitialInventory。");
                return BuildDefaultInitialInventory();
            }

            var inv = new InitialInventory();
            for (int i = 0; i < table.rows.Count; i++)
            {
                var row = table.rows[i];
                string kind = row.Get(idxKind);
                string id = row.Get(idxId);
                if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(id))
                {
                    UnityEngine.Debug.LogWarning($"[PlantConfigCatalog] initial_inventory.csv 第 {row.lineNumber} 行字段缺失，跳过。");
                    continue;
                }
                if (!int.TryParse(row.Get(idxCount), out int count) || count < 1)
                {
                    UnityEngine.Debug.LogWarning($"[PlantConfigCatalog] initial_inventory.csv 第 {row.lineNumber} 行 count 非法（须 >= 1），跳过。");
                    continue;
                }

                if (string.Equals(kind, "Seed", StringComparison.OrdinalIgnoreCase))
                {
                    inv.seeds.Add(new SeedStack { plantConfigId = id, count = count });
                }
                else if (string.Equals(kind, "Pack", StringComparison.OrdinalIgnoreCase))
                {
                    if (!Enum.TryParse<SeedPackQuality>(id, false, out var quality))
                    {
                        UnityEngine.Debug.LogWarning($"[PlantConfigCatalog] initial_inventory.csv 第 {row.lineNumber} 行 Pack id='{id}' 非法品质，跳过。");
                        continue;
                    }
                    inv.packs.Add(new SeedPackStack { quality = quality, count = count });
                }
                else if (string.Equals(kind, "Fertilizer", StringComparison.OrdinalIgnoreCase))
                {
                    // SPEC §B.5 / §B.7：肥料行同样按 (id, count) 装载，id 须存在于 §B.7 配置表
                    // （此处不强校验 id 存在性，留给 PlantingService 在选中时再做防御性判断）。
                    inv.fertilizers.Add(new FertilizerStack { fertilizerId = id, count = count });
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[PlantConfigCatalog] initial_inventory.csv 第 {row.lineNumber} 行 kind='{kind}' 非法（仅支持 Seed/Pack/Fertilizer），跳过。");
                }
            }

            if (inv.seeds.Count == 0 && inv.packs.Count == 0 && inv.fertilizers.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[PlantConfigCatalog] initial_inventory.csv 全部行非法，回退到 BuildDefaultInitialInventory。");
                return BuildDefaultInitialInventory();
            }
            return inv;
        }

        // ============================================================
        // SPEC §B.7：肥料类型表 (fertilizers.csv) 装载入口
        // ============================================================
        public static List<FertilizerType> LoadFertilizerTypesFromCsv()
        {
            var ta = Resources.Load<TextAsset>(FertilizersCsvResourcePath);
            if (ta == null || string.IsNullOrEmpty(ta.text))
            {
                UnityEngine.Debug.LogWarning("[PlantConfigCatalog] fertilizers.csv 未找到，回退到 BuildDefaultFertilizerTypes。");
                return BuildDefaultFertilizerTypes();
            }

            var table = CsvTable.Parse(ta.text);
            int idxId = table.IndexOfHeader("id");
            int idxName = table.IndexOfHeader("displayName");
            int idxSpeed = table.IndexOfHeader("speedMul");
            int idxDesc = table.IndexOfHeader("description");
            int idxIcon = table.IndexOfHeader("iconResource");
            if (idxId < 0 || idxName < 0 || idxSpeed < 0)
            {
                UnityEngine.Debug.LogWarning("[PlantConfigCatalog] fertilizers.csv 缺少必需列，回退到 BuildDefaultFertilizerTypes。");
                return BuildDefaultFertilizerTypes();
            }

            var list = new List<FertilizerType>(table.rows.Count);
            for (int i = 0; i < table.rows.Count; i++)
            {
                var row = table.rows[i];
                string id = row.Get(idxId);
                string name = row.Get(idxName);
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
                {
                    UnityEngine.Debug.LogWarning($"[PlantConfigCatalog] fertilizers.csv 第 {row.lineNumber} 行字段缺失，跳过。");
                    continue;
                }
                if (!TryParseFloat(row.Get(idxSpeed), out float speedMul) || speedMul <= 0f)
                {
                    UnityEngine.Debug.LogWarning($"[PlantConfigCatalog] fertilizers.csv 第 {row.lineNumber} 行 speedMul 非法（须 > 0），跳过。");
                    continue;
                }

                string desc = idxDesc >= 0 ? row.Get(idxDesc)?.Trim() : null;
                string iconRes = idxIcon >= 0 ? row.Get(idxIcon)?.Trim() : null;

                list.Add(new FertilizerType
                {
                    id = id,
                    displayName = name,
                    speedMul = speedMul,
                    description = desc,
                    iconResource = iconRes,
                });
            }

            if (list.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[PlantConfigCatalog] fertilizers.csv 全部行非法，回退到 BuildDefaultFertilizerTypes。");
                return BuildDefaultFertilizerTypes();
            }
            return list;
        }

        // ============================================================
        // 内置默认值（fallback）
        // ============================================================
        public static List<PlantConfig> BuildDefaultPlantConfigs()
        {
            return new List<PlantConfig>
            {
                MakePlantConfig(
                    id: "fanqie",
                    displayName: "番茄",
                    spriteBase: "NongZuoWu/FanQie",
                    baseStageSeconds: TestStageSeconds,
                    afterHarvest: AfterHarvest.Regrow,
                    pestEventIntervalSec: 60f,
                    pestEventProb: 0.20f,
                    rewardStat: RoleStatType.Atk,
                    harvestFruitCount: 2),
                MakePlantConfig(
                    id: "xigua",
                    displayName: "西瓜",
                    spriteBase: "NongZuoWu/XiGua",
                    baseStageSeconds: TestStageSeconds,
                    afterHarvest: AfterHarvest.Wilt,
                    pestEventIntervalSec: 90f,
                    pestEventProb: 0.15f,
                    rewardStat: RoleStatType.MaxHp,
                    harvestFruitCount: 20),
                MakePlantConfig(
                    id: "lajiao",
                    displayName: "辣椒",
                    spriteBase: "NongZuoWu/LaJiao",
                    baseStageSeconds: TestStageSeconds,
                    afterHarvest: AfterHarvest.Regrow,
                    pestEventIntervalSec: 60f,
                    pestEventProb: 0.25f,
                    rewardStat: RoleStatType.Atk,
                    harvestFruitCount: 1),
                MakePlantConfig(
                    id: "chaomei",
                    displayName: "草莓",
                    spriteBase: "NongZuoWu/ChaoMei",
                    baseStageSeconds: TestStageSeconds,
                    afterHarvest: AfterHarvest.Regrow,
                    pestEventIntervalSec: 50f,
                    pestEventProb: 0.20f,
                    rewardStat: RoleStatType.Agility,
                    harvestFruitCount: 1),
                MakePlantConfig(
                    id: "nangua",
                    displayName: "南瓜",
                    spriteBase: "NongZuoWu/NanGua",
                    baseStageSeconds: TestStageSeconds,
                    afterHarvest: AfterHarvest.Wilt,
                    pestEventIntervalSec: 90f,
                    pestEventProb: 0.15f,
                    rewardStat: RoleStatType.Def,
                    harvestFruitCount: 2),
                MakePlantConfig(
                    id: "huasheng",
                    displayName: "花生",
                    spriteBase: "NongZuoWu/HuaSheng",
                    baseStageSeconds: TestStageSeconds,
                    afterHarvest: AfterHarvest.Wilt,
                    pestEventIntervalSec: 75f,
                    pestEventProb: 0.18f,
                    rewardStat: RoleStatType.Def,
                    harvestFruitCount: 1),
            };
        }

        // 与 plants.csv 对齐：节点 1/2 为通用幼苗 MoRen，节点 3/4/5 为作物 -1/-2/-3（待收获为 -3）。
        private static PlantConfig MakePlantConfig(
            string id,
            string displayName,
            string spriteBase,
            float baseStageSeconds,
            AfterHarvest afterHarvest,
            float pestEventIntervalSec,
            float pestEventProb,
            RoleStatType rewardStat,
            int harvestFruitCount,
            float pestSpriteProb = 0.25f,
            float moleSpriteProb = 0.22f,
            string eatBuffIconResource = null)
        {
            return new PlantConfig
            {
                id = id,
                displayName = displayName,
                appearanceSpriteIds = new List<string>
                {
                    "NongZuoWu/MoRen_1",
                    "NongZuoWu/MoRen_2",
                    spriteBase + "-1",
                    spriteBase + "-2",
                    spriteBase + "-3",
                },
                appearanceSpineIds = new List<string> { "", "", "", "", "" },
                baseStageSeconds = baseStageSeconds,
                fertilizerSpeedMul = 1.5f,
                afterHarvest = afterHarvest,
                pestEventIntervalSec = pestEventIntervalSec,
                pestEventProb = pestEventProb,
                pestSpriteProb = Mathf.Clamp01(pestSpriteProb),
                moleSpriteProb = Mathf.Clamp01(moleSpriteProb),
                harvestRewardStat = rewardStat,
                harvestFruitCount = harvestFruitCount,
                eatBuffIconResource = eatBuffIconResource,
            };
        }

        public static List<SeedPackContents> BuildDefaultPackContents()
        {
            return new List<SeedPackContents>
            {
                new SeedPackContents
                {
                    quality = SeedPackQuality.Common,
                    entries = new List<SeedPackContentsEntry>
                    {
                        new SeedPackContentsEntry { plantConfigId = "fanqie", weight = 60f },
                        new SeedPackContentsEntry { plantConfigId = "lajiao", weight = 30f },
                        new SeedPackContentsEntry { plantConfigId = "chaomei", weight = 10f },
                    },
                },
                new SeedPackContents
                {
                    quality = SeedPackQuality.Rare,
                    entries = new List<SeedPackContentsEntry>
                    {
                        new SeedPackContentsEntry { plantConfigId = "fanqie", weight = 30f },
                        new SeedPackContentsEntry { plantConfigId = "xigua", weight = 10f },
                        new SeedPackContentsEntry { plantConfigId = "lajiao", weight = 30f },
                        new SeedPackContentsEntry { plantConfigId = "chaomei", weight = 25f },
                        new SeedPackContentsEntry { plantConfigId = "huasheng", weight = 5f },
                    },
                },
                new SeedPackContents
                {
                    quality = SeedPackQuality.Epic,
                    entries = new List<SeedPackContentsEntry>
                    {
                        new SeedPackContentsEntry { plantConfigId = "xigua", weight = 30f },
                        new SeedPackContentsEntry { plantConfigId = "chaomei", weight = 35f },
                        new SeedPackContentsEntry { plantConfigId = "nangua", weight = 25f },
                        new SeedPackContentsEntry { plantConfigId = "huasheng", weight = 10f },
                    },
                },
                new SeedPackContents
                {
                    quality = SeedPackQuality.Legendary,
                    entries = new List<SeedPackContentsEntry>
                    {
                        new SeedPackContentsEntry { plantConfigId = "fanqie", weight = 8f },
                        new SeedPackContentsEntry { plantConfigId = "xigua", weight = 25f },
                        new SeedPackContentsEntry { plantConfigId = "lajiao", weight = 8f },
                        new SeedPackContentsEntry { plantConfigId = "chaomei", weight = 9f },
                        new SeedPackContentsEntry { plantConfigId = "nangua", weight = 25f },
                        new SeedPackContentsEntry { plantConfigId = "huasheng", weight = 25f },
                    },
                },
            };
        }

        // SPEC §B.5.2 Demo 默认值（与 initial_inventory.csv 一致）。
        // 默认附带 1 个 demo 肥料，便于肥料仓库 UI 开局可见验收。
        public static InitialInventory BuildDefaultInitialInventory()
        {
            var inv = new InitialInventory();
            inv.seeds.Add(new SeedStack { plantConfigId = "fanqie", count = 10 });
            inv.seeds.Add(new SeedStack { plantConfigId = "chaomei", count = 5 });
            inv.packs.Add(new SeedPackStack { quality = SeedPackQuality.Common, count = 3 });
            inv.fertilizers.Add(new FertilizerStack { fertilizerId = "demo", count = 1 });
            // SPEC §9.8.12 (v3.40)：Demo 默认食物（用于「饿肚子提示框 → 食物仓库」流程）。
            inv.foods.Add(new FoodStack { foodId = "rougan", count = 3 });
            inv.foods.Add(new FoodStack { foodId = "mantou", count = 2 });
            return inv;
        }

        // SPEC §9.8.12 (v3.40)：Demo 默认食物静态表。
        // P1 可改为读取 Resources/Configs/Farm/foods.csv；本期不引入 CSV。
        public static List<FoodConfig> BuildDefaultFoodConfigs()
        {
            return new List<FoodConfig>
            {
                new FoodConfig
                {
                    id = "rougan",
                    displayName = "肉干",
                    staminaGain = 20,
                    iconResourcePath = "AirUI/CangKu",
                    description = "Demo 占位食物，+20 体力。",
                },
                new FoodConfig
                {
                    id = "mantou",
                    displayName = "馒头",
                    staminaGain = 20,
                    iconResourcePath = "AirUI/CangKu",
                    description = "Demo 占位食物，+20 体力。",
                },
            };
        }

        // SPEC §9.8.12 (v3.40)：将 InitialInventory.foods 转为 PlayerFoodBag。
        public static PlayerFoodBag BuildDefaultFoodBag(InitialInventory inv)
        {
            var bag = new PlayerFoodBag();
            if (inv != null && inv.foods != null)
            {
                for (int i = 0; i < inv.foods.Count; i++)
                {
                    var f = inv.foods[i];
                    if (f == null || string.IsNullOrEmpty(f.foodId) || f.count <= 0)
                        continue;
                    bag.stacks.Add(new FoodStack { foodId = f.foodId, count = f.count });
                }
            }
            return bag;
        }

        // SPEC §B.7.2 Demo 默认值。
        public static List<FertilizerType> BuildDefaultFertilizerTypes()
        {
            return new List<FertilizerType>
            {
                new FertilizerType
                {
                    id = "demo",
                    displayName = "占位肥料",
                    speedMul = 1.5f,
                    description = "Demo 占位描述。",
                    iconResource = "AirUI/ShiFei-1",
                },
            };
        }

        private static List<string> ReadOptionalSpineColumns(
            CsvRow row, int idxSp1, int idxSp2, int idxSp3, int idxSp4, int idxSp5)
        {
            if (idxSp1 < 0 || idxSp2 < 0 || idxSp3 < 0 || idxSp4 < 0 || idxSp5 < 0)
                return new List<string> { "", "", "", "", "" };

            string ReadCell(int idx)
            {
                var v = row.Get(idx);
                return string.IsNullOrWhiteSpace(v) ? "" : v.Trim();
            }

            return new List<string>
            {
                ReadCell(idxSp1),
                ReadCell(idxSp2),
                ReadCell(idxSp3),
                ReadCell(idxSp4),
                ReadCell(idxSp5),
            };
        }

        private static bool TryParseFloat(string s, out float v)
        {
            return float.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out v);
        }

        private static bool TryParseHarvestRoleStat(string raw, out RoleStatType stat)
        {
            stat = RoleStatType.Atk;
            if (string.IsNullOrEmpty(raw))
                return false;

            raw = raw.Trim();
            var key = raw;
            int colon = raw.IndexOf(':');
            if (colon >= 0)
                key = raw.Substring(0, colon).Trim();

            switch (key)
            {
                case "atk":
                    stat = RoleStatType.Atk;
                    return true;
                case "def":
                    stat = RoleStatType.Def;
                    return true;
                case "maxHp":
                    stat = RoleStatType.MaxHp;
                    return true;
                case "agility":
                    stat = RoleStatType.Agility;
                    return true;
                default:
                    return false;
            }
        }

        // ============================================================
        // SPEC §B.11 / §B.12（v3.18）：精灵 / 技能配置表装载
        // ============================================================
        public static List<PetConfig> LoadPetConfigsFromCsv()
        {
            var ta = Resources.Load<TextAsset>(PetsCsvResourcePath);
            if (ta == null || string.IsNullOrEmpty(ta.text))
            {
                UnityEngine.Debug.LogWarning("[PlantConfigCatalog] pets.csv 未找到，回退到 BuildDefaultPetConfigs。");
                return BuildDefaultPetConfigs();
            }

            var table = CsvTable.Parse(ta.text);
            int idxId = table.IndexOfHeader("id");
            int idxName = table.IndexOfHeader("displayName");
            int idxTrait = table.IndexOfHeader("traitDescription");
            int idxPrefab = table.IndexOfHeader("prefabResource");
            int idxAnims = table.IndexOfHeader("animations");
            if (idxId < 0 || idxName < 0 || idxTrait < 0 || idxPrefab < 0 || idxAnims < 0)
            {
                UnityEngine.Debug.LogWarning("[PlantConfigCatalog] pets.csv 缺少必需列，回退到 BuildDefaultPetConfigs。");
                return BuildDefaultPetConfigs();
            }

            var list = new List<PetConfig>(table.rows.Count);
            for (int i = 0; i < table.rows.Count; i++)
            {
                var row = table.rows[i];
                string id = row.Get(idxId);
                string name = row.Get(idxName);
                string trait = row.Get(idxTrait);
                string prefab = row.Get(idxPrefab);
                string animsRaw = row.Get(idxAnims);

                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name)
                    || string.IsNullOrEmpty(prefab))
                {
                    UnityEngine.Debug.LogWarning($"[PlantConfigCatalog] pets.csv 第 {row.lineNumber} 行字段缺失，跳过。");
                    continue;
                }

                var anims = new List<string>();
                if (!string.IsNullOrEmpty(animsRaw))
                {
                    var parts = animsRaw.Split(';');
                    for (int k = 0; k < parts.Length; k++)
                    {
                        var p = parts[k] != null ? parts[k].Trim() : null;
                        if (!string.IsNullOrEmpty(p))
                            anims.Add(p);
                    }
                }

                list.Add(new PetConfig
                {
                    id = id,
                    displayName = name,
                    traitDescription = trait ?? string.Empty,
                    prefabResource = prefab,
                    randomAnimations = anims,
                });
            }

            if (list.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[PlantConfigCatalog] pets.csv 全部行非法，回退到 BuildDefaultPetConfigs。");
                return BuildDefaultPetConfigs();
            }
            return list;
        }

        public static List<SkillConfig> LoadSkillConfigsFromCsv()
        {
            var ta = Resources.Load<TextAsset>(SkillsCsvResourcePath);
            if (ta == null || string.IsNullOrEmpty(ta.text))
            {
                UnityEngine.Debug.LogWarning("[PlantConfigCatalog] skills.csv 未找到，回退到 BuildDefaultSkillConfigs。");
                return BuildDefaultSkillConfigs();
            }

            var table = CsvTable.Parse(ta.text);
            int idxId = table.IndexOfHeader("id");
            int idxName = table.IndexOfHeader("displayName");
            int idxDesc = table.IndexOfHeader("description");
            int idxIcon = table.IndexOfHeader("iconResource");
            if (idxId < 0 || idxName < 0 || idxDesc < 0 || idxIcon < 0)
            {
                UnityEngine.Debug.LogWarning("[PlantConfigCatalog] skills.csv 缺少必需列，回退到 BuildDefaultSkillConfigs。");
                return BuildDefaultSkillConfigs();
            }

            var list = new List<SkillConfig>(table.rows.Count);
            for (int i = 0; i < table.rows.Count; i++)
            {
                var row = table.rows[i];
                string id = row.Get(idxId);
                string name = row.Get(idxName);
                string desc = row.Get(idxDesc);
                string icon = row.Get(idxIcon);

                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name)
                    || string.IsNullOrEmpty(icon))
                {
                    UnityEngine.Debug.LogWarning($"[PlantConfigCatalog] skills.csv 第 {row.lineNumber} 行字段缺失，跳过。");
                    continue;
                }

                list.Add(new SkillConfig
                {
                    id = id,
                    displayName = name,
                    description = desc ?? string.Empty,
                    iconResource = icon,
                });
            }

            if (list.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[PlantConfigCatalog] skills.csv 全部行非法，回退到 BuildDefaultSkillConfigs。");
                return BuildDefaultSkillConfigs();
            }
            return list;
        }

        // SPEC §B.11.2 Demo 默认值。
        public static List<PetConfig> BuildDefaultPetConfigs()
        {
            return new List<PetConfig>
            {
                new PetConfig
                {
                    id = "pet_slime",
                    displayName = "纯净史莱姆",
                    traitDescription = "跳跃中蓄积水分，浇水时回血明显加成。",
                    prefabResource = "Pets/Monster_11_Pure Slime",
                    randomAnimations = new List<string> { "idle", "jump" },
                },
                new PetConfig
                {
                    id = "pet_hamy_q",
                    displayName = "白晶仓鼠",
                    traitDescription = "每次收获额外掉落 1 颗低品质种子。",
                    prefabResource = "Pets/Monster_100_Hamy Alquartz",
                    randomAnimations = new List<string> { "idle", "happy" },
                },
                new PetConfig
                {
                    id = "pet_book",
                    displayName = "普通魔典",
                    traitDescription = "防御回合获得 +20 临时格挡率。",
                    prefabResource = "Pets/Monster_70_Normal Book",
                    randomAnimations = new List<string> { "idle", "attack" },
                },
                new PetConfig
                {
                    id = "pet_mushroom",
                    displayName = "普通蘑菇",
                    traitDescription = "每 30 秒概率喷洒孢子，使邻格 +1 水。",
                    prefabResource = "Pets/Monster_64_Mushroom",
                    randomAnimations = new List<string> { "idle" },
                },
            };
        }

        // SPEC §B.12.2 Demo 默认值。
        public static List<SkillConfig> BuildDefaultSkillConfigs()
        {
            return new List<SkillConfig>
            {
                new SkillConfig
                {
                    id = "skill_1001",
                    displayName = "烈焰冲击",
                    description = "对目标造成 150% 攻击力的火焰伤害。",
                    iconResource = "SkilIcon/Skill1001",
                },
                new SkillConfig
                {
                    id = "skill_1002",
                    displayName = "寒霜护甲",
                    description = "下一回合获得 30% 减伤。",
                    iconResource = "SkilIcon/Skill1002",
                },
            };
        }
    }
}
