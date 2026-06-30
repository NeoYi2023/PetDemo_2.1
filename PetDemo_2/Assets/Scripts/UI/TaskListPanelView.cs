// 加好感页签（ZhuanQianPopup）任务列表面板。
// 构建垂直 ScrollView，从 TaskListConfigCatalog 加载配置，实例化 TaskListRowView 行（Resources.Load + 运行时回退）。
// 状态机：GoTo →[点击前往：切 Claimable + onNavigate 跳转]→ Claimable →[点击领取：RewardFlyFx 飞行 + 切 Completed]→ Completed。
// 状态在内存中持久，跨 Show/Hide 保持。范式同 §13.2 FriendListPanelView 的 ScrollView 构建。
using System;
using System.Collections.Generic;
using PetDemo.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    public sealed class TaskListPanelView : MonoBehaviour
    {
        // 奖励飞行终点（屏幕像素坐标）。
        private static readonly Vector2 RewardFlyTargetScreenPos = new Vector2(377f, 895f);

        private RectTransform rootRt;
        private RectTransform contentRt;
        private RectTransform canvasRect;
        private Action<string> onNavigate;

        private readonly List<TaskConfig> tasks = new List<TaskConfig>();
        private readonly List<TaskButtonState> states = new List<TaskButtonState>();
        private readonly List<TaskListRowView> rows = new List<TaskListRowView>();
        private readonly List<GameObject> rowObjects = new List<GameObject>();
        private bool built;

        public bool IsShown => rootRt != null && rootRt.gameObject.activeSelf;

        /// <summary>在指定父节点下构建任务列表面板（默认隐藏）。</summary>
        /// <param name="parent">容器（通常为 ZhuanQianPopup 内的 TaskListContainer）。</param>
        /// <param name="canvas">根画布 RectTransform，用于飞行特效坐标转换。</param>
        /// <param name="navigateHandler">「前往」点击回调，参数为 navKey。</param>
        public static TaskListPanelView BuildInto(RectTransform parent, RectTransform canvas, Action<string> navigateHandler)
        {
            if (parent == null)
                return null;

            var rootGo = new GameObject("TaskListPanel", typeof(RectTransform));
            var root = rootGo.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            StretchFull(root);

            var view = rootGo.AddComponent<TaskListPanelView>();
            view.rootRt = root;
            view.canvasRect = canvas;
            view.onNavigate = navigateHandler;
            view.BuildHierarchy(root);
            view.LoadRows();

            rootGo.SetActive(false);
            return view;
        }

        public void Show()
        {
            if (rootRt == null)
                return;
            rootRt.gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            if (rootRt != null)
                rootRt.gameObject.SetActive(false);
        }

        private void BuildHierarchy(RectTransform root)
        {
            // ScrollView（占满面板）。
            var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.SetParent(root, false);
            StretchFull(scrollRt);
            scrollGo.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.4f);
            scrollGo.GetComponent<Image>().raycastTarget = true;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.SetParent(scrollRt, false);
            StretchFull(viewportRt);
            viewportGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.SetParent(viewportRt, false);
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;

            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 12f;
            vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.childAlignment = TextAnchor.UpperCenter;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewportRt;
            scroll.content = contentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
        }

        private void LoadRows()
        {
            if (built)
                return;
            built = true;

            tasks.Clear();
            states.Clear();
            ClearRows();

            var configs = TaskListConfigCatalog.LoadTaskConfigs();
            var template = LoadRowTemplate();

            for (int i = 0; i < configs.Count; i++)
            {
                tasks.Add(configs[i]);
                // 所有任务初始统一为「前往」。
                states.Add(TaskButtonState.GoTo);

                var go = Instantiate(template, contentRt, false);
                go.name = "TaskRow_" + configs[i].id;
                go.SetActive(true);
                var row = go.GetComponent<TaskListRowView>();
                if (row == null)
                    row = go.AddComponent<TaskListRowView>();

                int capturedIndex = i;
                row.Bind(configs[i], TaskButtonState.GoTo,
                    r => OnGoToClicked(capturedIndex),
                    r => OnClaimClicked(capturedIndex));

                rows.Add(row);
                rowObjects.Add(go);
            }
        }

        private static GameObject LoadRowTemplate()
        {
            var prefab = Resources.Load<GameObject>(TaskListRowView.ResPrefabPath);
            if (prefab != null)
                return prefab;

            // 兼容尚未运行预制体生成器：用运行时构建模板。
            var runtimeTemplate = TaskListRowView.BuildRuntimeTemplate();
            runtimeTemplate.name = "TaskListRowRuntimeTemplate";
            return runtimeTemplate;
        }

        private void OnGoToClicked(int index)
        {
            if (index < 0 || index >= rows.Count)
                return;

            // 需求 4：点击「前往」进行跳转后，强制将按钮状态变为「领取奖励」。
            states[index] = TaskButtonState.Claimable;
            rows[index].SetState(TaskButtonState.Claimable);

            // 发起跳转（复用 OnNavigateToBottomNav 体系）。
            var navKey = tasks[index].navKey;
            if (!string.IsNullOrEmpty(navKey))
                onNavigate?.Invoke(navKey);

            // 状态变更后按类型重排（Claimable 上浮）。
            SortRows();
        }

        private void OnClaimClicked(int index)
        {
            if (index < 0 || index >= rows.Count)
                return;
            if (states[index] != TaskButtonState.Claimable)
                return;

            // 需求 6：奖励图标从展示奖励的位置飞向屏幕坐标 (377,895)，飞到后消失。
            var fromScreenPos = rows[index].GetRewardIconScreenPos();
            var iconResource = tasks[index].rewardIconResource;
            if (canvasRect != null)
                RewardFlyFx.Play(canvasRect, fromScreenPos, RewardFlyTargetScreenPos, iconResource);

            // 飞行结束后切为「已完成」灰态（这里立即切态，飞行并行播放）。
            states[index] = TaskButtonState.Completed;
            rows[index].SetState(TaskButtonState.Completed);

            // 状态变更后按类型重排（Completed 下沉）。
            SortRows();
        }

        /// <summary>刷新所有行显示（恢复状态）并按按钮类型重排顺序。</summary>
        public void Refresh()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null)
                    rows[i].SetState(states[i]);
            }
            SortRows();
        }

        /// <summary>按按钮状态对行重排：Claimable > GoTo > Completed；同态保持原相对顺序（稳定）。</summary>
        /// <remarks>仅调整 rowObjects 的 sibling index，四条并行数组下标保持不变，故行回调的 capturedIndex 仍正确命中。</remarks>
        private void SortRows()
        {
            if (rowObjects.Count == 0)
                return;

            var order = new List<int>(rowObjects.Count);
            for (int i = 0; i < rowObjects.Count; i++)
                order.Add(i);

            order.Sort((a, b) =>
            {
                int cmp = StateSortKey(states[a]).CompareTo(StateSortKey(states[b]));
                return cmp != 0 ? cmp : a.CompareTo(b);
            });

            for (int i = 0; i < order.Count; i++)
            {
                if (rowObjects[order[i]] != null)
                    rowObjects[order[i]].transform.SetSiblingIndex(i);
            }
        }

        private static int StateSortKey(TaskButtonState state)
        {
            switch (state)
            {
                case TaskButtonState.Claimable: return 0;
                case TaskButtonState.GoTo: return 1;
                case TaskButtonState.Completed: return 2;
                default: return 3;
            }
        }

        private void ClearRows()
        {
            for (int i = 0; i < rowObjects.Count; i++)
            {
                if (rowObjects[i] != null)
                    Destroy(rowObjects[i]);
            }
            rowObjects.Clear();
            rows.Clear();
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
