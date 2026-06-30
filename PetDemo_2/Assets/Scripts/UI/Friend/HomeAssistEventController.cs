// SPEC §13.5：自家家园好友协助事件 — 每次进入家园（底栏切到 JiaYuan）随机 1 个有植物的田，
// 在其上方显示 HYXieZhu_1 图标按钮；点击后图标消失，并在主角村民屏幕位置生成 Xing_2 飞向左上角（§13.6）。
using System;
using System.Collections.Generic;
using PetDemo.Core;
using PetDemo.Farm;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Friend
{
    [DisallowMultipleComponent]
    public sealed class HomeAssistEventController : MonoBehaviour
    {
        public const string ResAssistIcon = "AirUI/HYXieZhu_1";
        public const string JiaYuanNavKey = "JiaYuan";

        private const float IconSize = 150f;
        private const float IconOffsetY = 120f;

        private IPlantingService service;
        private RectTransform canvasRect;
        private MainRoleCunminPresenter rolePresenter;
        private BottomNavBarView bottomNav;

        private RectTransform iconRt;
        private RectTransform iconSlotRt;
        private bool depthSortApplied;

        public static HomeAssistEventController Attach(
            GameObject host,
            IPlantingService plantingService,
            RectTransform canvas,
            MainRoleCunminPresenter presenter,
            BottomNavBarView barView)
        {
            if (host == null || plantingService == null)
                return null;

            var controller = host.GetComponent<HomeAssistEventController>();
            if (controller == null)
                controller = host.AddComponent<HomeAssistEventController>();
            controller.service = plantingService;
            controller.canvasRect = canvas;
            controller.rolePresenter = presenter;

            if (controller.bottomNav != null)
                controller.bottomNav.OnOpenChanged -= controller.OnBottomNavOpenChanged;
            controller.bottomNav = barView;
            if (barView != null)
            {
                barView.OnOpenChanged += controller.OnBottomNavOpenChanged;
                // 装配时若已停留在家园 Tab，立即生成一次（含创角期间世界隐藏的情况，显示后自然可见）。
                if (string.Equals(barView.OpenKey, JiaYuanNavKey, StringComparison.Ordinal))
                    controller.RespawnIcon();
            }

            return controller;
        }

        private void OnDestroy()
        {
            if (bottomNav != null)
                bottomNav.OnOpenChanged -= OnBottomNavOpenChanged;
            RemoveIcon();
        }

        private void OnBottomNavOpenChanged(int index, string key)
        {
            if (string.Equals(key, JiaYuanNavKey, StringComparison.Ordinal))
                RespawnIcon();
        }

        private void Update()
        {
            // §9.1.4 排序器会剔除「不活跃层级」的条目（如创角期间世界隐藏时注册的图标），
            // 世界重新可见后在此自愈式补注册，保证图标盖在植物之上。
            if (iconRt == null || iconSlotRt == null)
                return;
            if (!iconRt.gameObject.activeInHierarchy)
            {
                depthSortApplied = false;
                return;
            }
            if (!depthSortApplied)
                RegisterDepthSort(iconSlotRt);
        }

        /// <summary>SPEC §13.5：每次进入家园重新随机；无植物田时本次不出现。</summary>
        private void RespawnIcon()
        {
            RemoveIcon();

            var grid = FarmGridView.Instance;
            if (service == null || grid == null)
                return;

            var plantedTileIds = CollectPlantedTileIds();
            if (plantedTileIds.Count == 0)
                return;

            string tileId = plantedTileIds[UnityEngine.Random.Range(0, plantedTileIds.Count)];
            var slotRt = grid.GetTileSlotRect(tileId);
            if (slotRt == null)
                return;
            iconSlotRt = slotRt;

            var gridRoot = grid.transform as RectTransform;
            if (gridRoot == null)
                return;

            var go = new GameObject("HomeAssistIcon", typeof(RectTransform));
            iconRt = go.GetComponent<RectTransform>();
            iconRt.SetParent(gridRoot, false);
            iconRt.SetAsLastSibling();
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(IconSize, IconSize);
            var local = (Vector2)gridRoot.InverseTransformPoint(slotRt.position);
            iconRt.anchoredPosition = local + new Vector2(0f, IconOffsetY);

            var img = go.AddComponent<Image>();
            var sprite = Resources.Load<Sprite>(ResAssistIcon);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.preserveAspect = true;
                img.color = Color.white;
            }
            else
            {
                img.color = new Color(1f, 0.85f, 0.35f, 0.85f);
                UnityEngine.Debug.LogWarning(
                    "[HomeAssistEventController] 缺少图标 Resources/" + ResAssistIcon + "，使用纯色占位。");
            }
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;
            btn.onClick.AddListener(OnIconClicked);

            // SPEC §9.1.4：参与家园世界 Y 轴深度排序（同 §4.1.10 变异图标 LayerOffset）。
            RegisterDepthSort(slotRt);
        }

        private List<string> CollectPlantedTileIds()
        {
            var result = new List<string>();
            for (int i = 1; i <= service.FarmTileCount; i++)
            {
                var tile = service.GetTileByOrder(i);
                if (tile == null)
                    continue;
                if (string.IsNullOrEmpty(tile.plantInstanceId))
                    continue;
                // 被变异锁定的田仅显示泥土（§4.1.10.3），跳过。
                if (!string.IsNullOrEmpty(tile.lockedByMutationId))
                    continue;
                result.Add(tile.tileId);
            }
            return result;
        }

        private void RegisterDepthSort(RectTransform slotRt)
        {
            var sorter = JiaYuanWorldDepthSorter.Instance;
            if (sorter == null || iconRt == null || slotRt == null)
                return;

            float sortY = sorter.ResolveSortYFromWorldPoint(slotRt.position);
            sorter.RegisterOrUpdate(new DepthSortEntry
            {
                Visual = iconRt,
                SortY = sortY,
                LayerOffset = JiaYuanWorldDepthLayer.MutationIcon,
                Active = true,
                UseVisualCenterY = false,
            });
            sorter.MarkDirty();
            depthSortApplied = true;
        }

        private void OnIconClicked()
        {
            RemoveIcon();
            PlayStarFlyFromVillager();
        }

        private void RemoveIcon()
        {
            iconSlotRt = null;
            depthSortApplied = false;
            if (iconRt == null)
                return;
            JiaYuanWorldDepthSorter.Instance?.Unregister(iconRt);
            Destroy(iconRt.gameObject);
            iconRt = null;
        }

        private void PlayStarFlyFromVillager()
        {
            if (canvasRect == null)
                return;

            Vector2 fromScreen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var villagerRt = rolePresenter != null ? rolePresenter.VillagerRoleRectTransform : null;
            if (villagerRt != null)
            {
                var cam = StarFlyFx.ResolveCanvasCamera(canvasRect);
                fromScreen = RectTransformUtility.WorldToScreenPoint(cam, villagerRt.position);
            }

            StarFlyFx.Play(canvasRect, fromScreen);
        }
    }
}
