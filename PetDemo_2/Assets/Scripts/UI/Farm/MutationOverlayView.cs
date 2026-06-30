// MutationOverlayView：在 FarmGridRoot 之上叠加一层「四格植物」图标层（SPEC §4.1.10 / §5.2 v3.18）。
// 监听 IPlantingService.OnMutationCreated / OnMutationHarvested，
// 为每个变异植物在 4 田 anchoredPosition 平均值处放置一个可点击图标（占位 sprite）。
// 点击图标先播放 §9.1.2 摆动动画，再转发到 PlantingService.TryHarvestMutation。
using System.Collections;
using System.Collections.Generic;
using PetDemo.Core;
using PetDemo.Farm;
using PetDemo.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PetDemo.UI.Farm
{
    public class MutationOverlayView : MonoBehaviour
    {
        // SPEC §4.1.10.4（v3.59）：四格 / 单格变异果实入口图标按变异类型区分。
        // - kind=Pet   -> AirUI/ShiWu_2
        // - kind=Skill -> AirUI/DaShouHuo_2
        private const string PetIconResPath = "AirUI/ShiWu_2";
        private const string SkillIconResPath = "AirUI/DaShouHuo_2";
        private const float IconSize = 240f;

        public static MutationOverlayView Instance { get; private set; }

        private FarmGridView gridView;
        private IPlantingService service;
        private RectTransform overlayRoot;
        private readonly Dictionary<string, RectTransform> itemByMutationId = new Dictionary<string, RectTransform>();
        private static Sprite sPetIconSprite;
        private static Sprite sSkillIconSprite;

        public static MutationOverlayView Attach(FarmGridView grid, IPlantingService svc)
        {
            if (grid == null || svc == null)
                return null;
            var existing = grid.GetComponent<MutationOverlayView>();
            if (existing != null)
                return existing;

            var view = grid.gameObject.AddComponent<MutationOverlayView>();
            view.gridView = grid;
            view.service = svc;
            view.BuildOverlayRoot(grid.transform as RectTransform);
            view.SubscribeEvents();
            view.RebuildAll();
            Instance = view;
            return view;
        }

        private void BuildOverlayRoot(RectTransform parent)
        {
            if (parent == null)
                return;
            var go = new GameObject("MutationOverlay");
            overlayRoot = go.AddComponent<RectTransform>();
            overlayRoot.SetParent(parent, false);
            overlayRoot.anchorMin = new Vector2(0.5f, 0.5f);
            overlayRoot.anchorMax = new Vector2(0.5f, 0.5f);
            overlayRoot.pivot = new Vector2(0.5f, 0.5f);
            overlayRoot.anchoredPosition = Vector2.zero;
            overlayRoot.sizeDelta = parent.sizeDelta;
            // 确保图标层位于网格之上：放在父节点末尾。
            overlayRoot.SetAsLastSibling();
        }

        private void SubscribeEvents()
        {
            if (service == null)
                return;
            service.OnMutationCreated += HandleMutationCreated;
            service.OnMutationHarvested += HandleMutationHarvested;
        }

        /// <summary>§9.1.4：排序器就绪后补注册已有变异图标。</summary>
        public void RefreshDepthSortAll()
        {
            if (service == null)
                return;
            foreach (var kv in itemByMutationId)
            {
                if (kv.Value == null)
                    continue;
                var mutation = service.GetMutation(kv.Key);
                if (mutation?.tileIds == null)
                    continue;
                RegisterMutationDepthSort(kv.Value, mutation.tileIds);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            var sorter = JiaYuanWorldDepthSorter.Instance;
            if (sorter != null)
            {
                foreach (var kv in itemByMutationId)
                {
                    if (kv.Value != null)
                        sorter.Unregister(kv.Value);
                }
            }

            if (service == null)
                return;
            service.OnMutationCreated -= HandleMutationCreated;
            service.OnMutationHarvested -= HandleMutationHarvested;
        }

        private void HandleMutationCreated(string mutationId)
        {
            CreateOrUpdateItem(mutationId);
        }

        private void HandleMutationHarvested(string mutationId, MutationKind kind, string refId)
        {
            RemoveItem(mutationId);
        }

        private void RebuildAll()
        {
            if (service == null)
                return;
            var list = service.GetMutations();
            if (list == null)
                return;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (m == null) continue;
                CreateOrUpdateItem(m.instanceId);
            }
        }

        private void CreateOrUpdateItem(string mutationId)
        {
            if (string.IsNullOrEmpty(mutationId) || overlayRoot == null || gridView == null || service == null)
                return;
            var mutation = service.GetMutation(mutationId);
            if (mutation == null || mutation.tileIds == null || mutation.tileIds.Count == 0)
                return;

            if (itemByMutationId.TryGetValue(mutationId, out var existing) && existing != null)
            {
                existing.anchoredPosition = ComputeCenterAnchoredPosition(mutation.tileIds);
                RegisterMutationDepthSort(existing, mutation.tileIds);
                return;
            }

            var go = new GameObject("MutationItem_" + mutationId);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(overlayRoot, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(IconSize, IconSize);
            rt.anchoredPosition = ComputeCenterAnchoredPosition(mutation.tileIds);

            var image = go.AddComponent<Image>();
            image.sprite = LoadIconSprite(mutation.kind);
            image.preserveAspect = true;
            image.raycastTarget = true;
            // 若加载失败仍保留点击区域（半透明灰），确保玩家能完成收获验收。
            if (image.sprite == null)
                image.color = new Color(1f, 0.9f, 0.4f, 0.7f);

            var clickHandler = go.AddComponent<MutationItemClickHandler>();
            clickHandler.Bind(service, mutationId);

            itemByMutationId[mutationId] = rt;
            RegisterMutationDepthSort(rt, mutation.tileIds);
        }

        private void RemoveItem(string mutationId)
        {
            if (!itemByMutationId.TryGetValue(mutationId, out var rt))
                return;
            itemByMutationId.Remove(mutationId);
            JiaYuanWorldDepthSorter.Instance?.Unregister(rt);
            if (rt != null)
                Destroy(rt.gameObject);
        }

        private void RegisterMutationDepthSort(RectTransform itemRt, List<string> tileIds)
        {
            var sorter = JiaYuanWorldDepthSorter.Instance;
            if (sorter == null || itemRt == null || tileIds == null || tileIds.Count == 0)
                return;

            Vector3 worldSum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < tileIds.Count; i++)
            {
                var tileRect = gridView.GetTileSlotRect(tileIds[i]);
                if (tileRect == null)
                    continue;
                worldSum += tileRect.position;
                count++;
            }

            if (count == 0)
                return;

            float sortY = sorter.ResolveSortYFromWorldPoint(worldSum / count);
            sorter.RegisterOrUpdate(new DepthSortEntry
            {
                Visual = itemRt,
                SortY = sortY,
                LayerOffset = JiaYuanWorldDepthLayer.MutationIcon,
                Active = true,
                UseVisualCenterY = false,
            });
            sorter.MarkDirty();
        }

        private Vector2 ComputeCenterAnchoredPosition(List<string> tileIds)
        {
            // 使用世界坐标平均，再转换回 OverlayRoot 的本地坐标，规避 GridLayoutGroup
            // 与不同 anchor 设置带来的坐标系差异。
            if (overlayRoot == null) return Vector2.zero;
            Vector3 worldSum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < tileIds.Count; i++)
            {
                var tileRect = gridView.GetTileSlotRect(tileIds[i]);
                if (tileRect == null) continue;
                worldSum += tileRect.position;
                count++;
            }
            if (count == 0) return Vector2.zero;
            Vector3 worldCenter = worldSum / count;
            Vector3 localCenter = overlayRoot.InverseTransformPoint(worldCenter);
            return new Vector2(localCenter.x, localCenter.y);
        }

        private static Sprite LoadIconSprite(MutationKind kind)
        {
            if (kind == MutationKind.Pet)
            {
                if (sPetIconSprite == null)
                    sPetIconSprite = Resources.Load<Sprite>(PetIconResPath);
                return sPetIconSprite;
            }

            if (sSkillIconSprite == null)
                sSkillIconSprite = Resources.Load<Sprite>(SkillIconResPath);
            return sSkillIconSprite;
        }
    }

    /// <summary>
    /// 单个 MutationItem 的点击响应：§9.1.2 摆动后再 TryHarvestMutation。
    /// </summary>
    public class MutationItemClickHandler : MonoBehaviour, IPointerClickHandler
    {
        private IPlantingService service;
        private string mutationId;
        private Coroutine harvestSwingRoutine;

        public void Bind(IPlantingService svc, string id)
        {
            service = svc;
            mutationId = id;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (service == null || string.IsNullOrEmpty(mutationId))
                return;
            if (harvestSwingRoutine != null || PlantHarvestSwingAnimator.ActiveSwingCount > 0)
                return;

            var mutation = service.GetMutation(mutationId);
            if (mutation == null || mutation.state != PlantState.AwaitingHarvest)
                return;

            var rt = transform as RectTransform;
            if (rt == null)
                return;

            harvestSwingRoutine = StartCoroutine(HarvestSwingThenMutationRoutine(rt));
        }

        private IEnumerator HarvestSwingThenMutationRoutine(RectTransform iconRt)
        {
            PlantHarvestSwingAnimator.BeginSwing();
            var image = GetComponent<Image>();
            if (image != null)
                image.raycastTarget = false;

            try
            {
                yield return PlantHarvestSwingAnimator.RunSwingSequence(iconRt);

                if (service != null && !string.IsNullOrEmpty(mutationId))
                {
                    var mutation = service.GetMutation(mutationId);
                    if (mutation != null && mutation.state == PlantState.AwaitingHarvest)
                        service.TryHarvestMutation(mutationId);
                }
            }
            finally
            {
                PlantHarvestSwingAnimator.EndSwing();

                if (image != null)
                    image.raycastTarget = true;
                harvestSwingRoutine = null;
            }
        }

        private void OnDisable()
        {
            if (harvestSwingRoutine == null)
                return;

            StopCoroutine(harvestSwingRoutine);
            harvestSwingRoutine = null;

            var rt = transform as RectTransform;
            if (rt != null)
                rt.localEulerAngles = Vector3.zero;
        }
    }
}
