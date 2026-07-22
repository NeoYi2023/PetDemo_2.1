// 加好感页签（ZhuanQianPopup）任务列表单行视图。
// 结构：行背景 + 任务图标(TaskIcon) + 描述(Description) + 奖励图标(RewardIcon)+数量(RewardCount)
//       + 三态互斥按钮(GoToButton/ClaimButton/CompletedButton，同位置)。
// v3.266：RewardIcon 固定 AirUI/ExpIcon_1，RewardCount 显示 expReward（暂不绑定旧道具奖励列）。
// 范式同 §9.14.8 TopFriendCellView：运行时 AutoWire 按子节点名绑定 + 静态 BuildRuntimeTemplate 构建层级。
using System;
using PetDemo.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    /// <summary>任务行按钮三态：前往 → 领取奖励 → 已完成（灰态、不可点击、不跳转）。</summary>
    public enum TaskButtonState
    {
        GoTo,
        Claimable,
        Completed,
    }

    [DisallowMultipleComponent]
    public sealed class TaskListRowView : MonoBehaviour
    {
        public const string ResPrefabPath = "Prefabs/Farm/TaskListRow";

        private const float RowHeight = 140f;

        private static readonly Color RowFallbackColor = new Color(0.16f, 0.18f, 0.24f, 0.95f);
        private static readonly Color IconFallbackColor = new Color(0.3f, 0.36f, 0.46f, 1f);
        private static readonly Color GoToButtonColor = new Color(0.26f, 0.55f, 0.85f, 1f);
        private static readonly Color ClaimButtonColor = new Color(0.35f, 0.78f, 0.42f, 1f);
        private static readonly Color CompletedButtonColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        [SerializeField] private Image background;
        [SerializeField] private Image taskIcon;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private Text rewardCountText;
        [SerializeField] private Button goToButton;
        [SerializeField] private Button claimButton;
        [SerializeField] private Button completedButton;

        private TaskConfig boundConfig;
        private TaskButtonState boundState;
        private Action<TaskListRowView> onGoTo;
        private Action<TaskListRowView> onClaim;
        private bool wired;

        /// <summary>预制体模板仅含 UI 组件；运行时按子节点名自动绑定并挂载按钮事件（仅一次）。</summary>
        public void AutoWire()
        {
            if (background == null)
                background = GetComponent<Image>();
            if (taskIcon == null)
                taskIcon = FindImage("TaskIcon");
            if (descriptionText == null)
                descriptionText = FindText("Description");
            if (rewardIcon == null)
                rewardIcon = FindImage("RewardIcon");
            if (rewardCountText == null)
                rewardCountText = FindText("RewardCount");
            if (goToButton == null)
                goToButton = FindButton("GoToButton");
            if (claimButton == null)
                claimButton = FindButton("ClaimButton");
            if (completedButton == null)
                completedButton = FindButton("CompletedButton");

            if (wired)
                return;
            wired = true;
            if (goToButton != null)
            {
                goToButton.transition = Selectable.Transition.ColorTint;
                if (goToButton.targetGraphic == null && goToButton.image != null)
                    goToButton.targetGraphic = goToButton.image;
                goToButton.onClick.AddListener(HandleGoTo);
            }
            if (claimButton != null)
            {
                claimButton.transition = Selectable.Transition.ColorTint;
                if (claimButton.targetGraphic == null && claimButton.image != null)
                    claimButton.targetGraphic = claimButton.image;
                claimButton.onClick.AddListener(HandleClaim);
            }
            if (completedButton != null)
            {
                completedButton.transition = Selectable.Transition.None;
                completedButton.interactable = false;
            }
        }

        private void Awake()
        {
            AutoWire();
        }

        /// <summary>绑定任务数据与回调，并应用初始状态。</summary>
        public void Bind(TaskConfig config, TaskButtonState state, Action<TaskListRowView> goToHandler, Action<TaskListRowView> claimHandler)
        {
            AutoWire();
            boundConfig = config;
            boundState = state;
            onGoTo = goToHandler;
            onClaim = claimHandler;

            if (descriptionText != null)
                descriptionText.text = config.description;

            // v3.266：暂时删除原 RewardIcon/RewardCount 道具信息，改为经验产出展示。
            if (rewardCountText != null)
                rewardCountText.text = "x" + config.expReward;

            SetSpriteOrFallback(taskIcon, config.iconResource, IconFallbackColor, true);
            SetSpriteOrFallback(rewardIcon, TaskListConfigCatalog.ExpRewardIconResource, IconFallbackColor, true);

            SetState(state);
        }

        /// <summary>三态互斥切换：仅对应按钮可见，Completed 强制灰化不可交互。</summary>
        public void SetState(TaskButtonState state)
        {
            boundState = state;
            if (goToButton != null)
                goToButton.gameObject.SetActive(state == TaskButtonState.GoTo);
            if (claimButton != null)
                claimButton.gameObject.SetActive(state == TaskButtonState.Claimable);
            if (completedButton != null)
            {
                completedButton.gameObject.SetActive(state == TaskButtonState.Completed);
                completedButton.interactable = false;
                var img = completedButton.targetGraphic as Image;
                if (img != null)
                    img.color = CompletedButtonColor;
            }
        }

        public TaskButtonState CurrentState => boundState;
        public TaskConfig Config => boundConfig;

        /// <summary>返回行内奖励图标的屏幕像素坐标（飞行起点）。</summary>
        public Vector2 GetRewardIconScreenPos()
        {
            if (rewardIcon == null)
                return Vector2.zero;
            var cam = ResolveCanvasCamera();
            return RectTransformUtility.WorldToScreenPoint(cam, rewardIcon.rectTransform.position);
        }

        private void HandleGoTo()
        {
            if (boundState != TaskButtonState.GoTo)
                return;
            onGoTo?.Invoke(this);
        }

        private void HandleClaim()
        {
            if (boundState != TaskButtonState.Claimable)
                return;
            onClaim?.Invoke(this);
        }

        private Camera ResolveCanvasCamera()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                return null;
            return canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        }

        private static void SetSpriteOrFallback(Image img, string resource, Color fallback, bool preserveAspect)
        {
            if (img == null)
                return;
            Sprite sprite = !string.IsNullOrEmpty(resource) ? Resources.Load<Sprite>(resource) : null;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = preserveAspect;
            }
            else
            {
                img.sprite = null;
                img.color = fallback;
            }
        }

        private Image FindImage(string nodeName)
        {
            var t = FindDescendant(transform, nodeName);
            return t != null ? t.GetComponent<Image>() : null;
        }

        private Text FindText(string nodeName)
        {
            var t = FindDescendant(transform, nodeName);
            return t != null ? t.GetComponent<Text>() : null;
        }

        private Button FindButton(string nodeName)
        {
            var t = FindDescendant(transform, nodeName);
            return t != null ? t.GetComponent<Button>() : null;
        }

        private static Transform FindDescendant(Transform root, string nodeName)
        {
            if (root == null)
                return null;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == nodeName)
                    return child;
                var found = FindDescendant(child, nodeName);
                if (found != null)
                    return found;
            }
            return null;
        }

        // ============================================================
        // 运行时模板构建（Resources.Load 失败回退用，结构与编辑器烘焙的 .prefab 一致）
        // ============================================================

        /// <summary>构建行层级 GameObject（隐藏返回），供运行时回退与编辑器生成器共用。</summary>
        public static GameObject BuildRuntimeTemplate()
        {
            var go = new GameObject("TaskListRow", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.pivot = new Vector2(0.5f, 0.5f);

            var bg = go.AddComponent<Image>();
            bg.color = RowFallbackColor;
            bg.raycastTarget = true;

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = RowHeight;
            le.minHeight = RowHeight;

            // 任务图标（左）。
            var iconRt = CreateChild(rt, "TaskIcon",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(80f, 0f), new Vector2(96f, 96f));
            var iconImg = iconRt.gameObject.AddComponent<Image>();
            iconImg.raycastTarget = false;
            iconImg.preserveAspect = true;

            // 描述（中）。
            var descRt = CreateChild(rt, "Description",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(0f, 0.5f), new Vector2(160f, 0f), new Vector2(-360f, 0f));
            var descTxt = descRt.gameObject.AddComponent<Text>();
            descTxt.text = "任务描述";
            descTxt.font = Farm.FarmGridView.LoadBuiltinFont();
            descTxt.fontSize = 32;
            descTxt.alignment = TextAnchor.MiddleLeft;
            descTxt.color = Color.white;
            descTxt.raycastTarget = false;
            descTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            descTxt.verticalOverflow = VerticalWrapMode.Overflow;

            // 奖励图标（右中）。
            var rewardIconRt = CreateChild(rt, "RewardIcon",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(-360f, 0f), new Vector2(80f, 80f));
            var rewardIconImg = rewardIconRt.gameObject.AddComponent<Image>();
            rewardIconImg.raycastTarget = false;
            rewardIconImg.preserveAspect = true;

            // 奖励数量（右中，紧贴奖励图标右侧）。
            var rewardCountRt = CreateChild(rt, "RewardCount",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(-270f, 0f), new Vector2(120f, 50f));
            var rewardCountTxt = rewardCountRt.gameObject.AddComponent<Text>();
            rewardCountTxt.text = "x0";
            rewardCountTxt.font = Farm.FarmGridView.LoadBuiltinFont();
            rewardCountTxt.fontSize = 30;
            rewardCountTxt.alignment = TextAnchor.MiddleCenter;
            rewardCountTxt.color = Color.white;
            rewardCountTxt.raycastTarget = false;

            // 三态按钮（同一位置，右端）。
            var btnSize = new Vector2(180f, 80f);
            var btnPos = new Vector2(-100f, 0f);
            BuildStateButton(rt, "GoToButton", "前往", GoToButtonColor, btnSize, btnPos);
            BuildStateButton(rt, "ClaimButton", "领取奖励", ClaimButtonColor, btnSize, btnPos);
            BuildStateButton(rt, "CompletedButton", "已完成", CompletedButtonColor, btnSize, btnPos);

            go.SetActive(false);
            return go;
        }

        private static void BuildStateButton(RectTransform parent, string name, string label, Color color, Vector2 size, Vector2 pos)
        {
            var btnRt = CreateChild(parent, name,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0.5f, 0.5f), pos, size);
            var img = btnRt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            var btn = btnRt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;

            var labelRt = CreateChild(btnRt, "Label", Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var txt = labelRt.gameObject.AddComponent<Text>();
            txt.text = label;
            txt.font = Farm.FarmGridView.LoadBuiltinFont();
            txt.fontSize = 30;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.raycastTarget = false;
        }

        private static RectTransform CreateChild(
            RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return rt;
        }
    }
}
