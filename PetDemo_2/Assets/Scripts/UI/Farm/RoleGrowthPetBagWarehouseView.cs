// SPEC §9.10.5：精灵背包 — 可滚动仓库网格。
using System;
using System.Collections.Generic;
using PetDemo.Core;
using PetDemo.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Farm
{
    [DisallowMultipleComponent]
    public sealed class RoleGrowthPetBagWarehouseView : MonoBehaviour
    {
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private GameObject cellTemplate;

        private IPlantingService service;
        private readonly List<RoleGrowthPetBagCellView> cells = new List<RoleGrowthPetBagCellView>();

        public void Bind(IPlantingService plantingService)
        {
            service = plantingService;
            if (cellTemplate != null)
                cellTemplate.SetActive(false);
        }

        public void Refresh()
        {
            if (service == null || contentRoot == null || cellTemplate == null)
                return;

            var owned = service.GetOwnedPets();
            var sorted = new List<PetInstance>();
            if (owned != null)
            {
                for (int i = 0; i < owned.Count; i++)
                {
                    var p = owned[i];
                    if (p != null && !string.IsNullOrEmpty(p.instanceId))
                        sorted.Add(p);
                }
            }

            sorted.Sort((a, b) => string.Compare(a.instanceId, b.instanceId, StringComparison.Ordinal));

            int count = Mathf.Min(sorted.Count, PetDeploymentRules.MaxOwnedPets);
            EnsureCellCount(count);

            for (int i = 0; i < cells.Count; i++)
            {
                if (i < count)
                {
                    var inst = sorted[i];
                    var cfg = service.GetPetConfig(inst.petConfigId);
                    bool deployed = service.IsPetDeployed(inst.instanceId);
                    cells[i].gameObject.SetActive(true);
                    cells[i].Bind(inst, cfg, deployed, OnCellClicked);
                }
                else
                {
                    cells[i].gameObject.SetActive(false);
                }
            }
        }

        private void EnsureCellCount(int count)
        {
            while (cells.Count < count)
            {
                var cellGo = Instantiate(cellTemplate, contentRoot);
                cellGo.SetActive(true);
                var cell = cellGo.GetComponent<RoleGrowthPetBagCellView>();
                if (cell == null)
                    cell = cellGo.AddComponent<RoleGrowthPetBagCellView>();
                cell.AutoWire();
                cells.Add(cell);
            }
        }

        private void OnCellClicked(PetInstance inst)
        {
            if (service == null || inst == null || string.IsNullOrEmpty(inst.instanceId))
                return;

            if (service.IsPetDeployed(inst.instanceId))
                return;

            if (!service.TryDeployPetToField(inst.instanceId))
                UnityEngine.Debug.LogWarning("[RoleGrowthPetBagWarehouseView] 上阵失败（槽位已满或实例无效）：" + inst.instanceId);
        }
    }
}
