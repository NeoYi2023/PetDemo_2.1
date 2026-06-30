// FarmBootstrap：MonoBehaviour 入口，负责创建 PlantingService 单例并按帧推进 TickGrowth。
// 由 AirMainMenuRuntimeBuilder 在 UI 构建前自挂载，确保 UI 拿到 PlantingService.Instance。
//
// 自 v1.3 起：植物配置、种子包权重、初始仓库三张表均从 Resources/Configs/Farm/*.csv
// 优先装载（SPEC §B.2.1 / §B.4 / §B.5）；任意一张缺失或非法时由 PlantConfigCatalog
// 内部回退到 BuildDefault*，整体行为与改造前完全一致。
// 自 v2.10 起新增：肥料类型表 fertilizers.csv（SPEC §B.7）同样在此装载并注入服务。
// 自 v3.74 起：读 GameBootContext，支持从本地存档槽恢复快照（SPEC §13）。
using PetDemo.Core;
using PetDemo.Save;
using UnityEngine;

namespace PetDemo.Farm
{
    [DisallowMultipleComponent]
    public class FarmBootstrap : MonoBehaviour
    {
        public PlantingService Service { get; private set; }

        private void Awake()
        {
            if (PlantingService.Instance != null)
            {
                Service = PlantingService.Instance;
                return;
            }

            var configs = PlantConfigCatalog.LoadPlantConfigsFromCsv();
            var packs = PlantConfigCatalog.LoadPackContentsFromCsv();
            var inventory = PlantConfigCatalog.LoadInitialInventoryFromCsv();
            var fertilizerTypes = PlantConfigCatalog.LoadFertilizerTypesFromCsv();
            var petConfigs = PlantConfigCatalog.LoadPetConfigsFromCsv();
            var skillConfigs = PlantConfigCatalog.LoadSkillConfigsFromCsv();

            GameSaveSnapshot snapshot = null;
            if (GameBootContext.HasEnteredGame && !GameBootContext.IsNewGame)
                snapshot = GameBootContext.PendingSnapshot;

            Service = new PlantingService(
                configs,
                packs,
                inventory,
                fertilizerTypes,
                petConfigs,
                skillConfigs,
                foodConfigs: null,
                loadSnapshot: snapshot);
        }

        private void Update()
        {
            Service?.TickGrowth(Time.deltaTime);
        }
    }
}
