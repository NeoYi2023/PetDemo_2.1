// SPEC §13.1：家园「好友」入口按钮 — 位于 §9.7.1 收获视角入口正上方 (450, 608)；
// 仅 JiaYuan Tab 激活；点击打开 §13.2 好友列表弹窗。
using System;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Friend
{
    [DisallowMultipleComponent]
    public sealed class FriendEntryView : MonoBehaviour
    {
        public const string ResEntryIcon = "AirUI/HaoYouList_0";
        public const string JiaYuanNavKey = "JiaYuan";

        private const float EntrySize = 150f;
        private const float EntryPosX = 450f;
        private const float EntryPosY = 608f; // 收获入口 (450, 438) + 150 + 间距 20（SPEC §13.1）

        private BottomNavBarView bottomNav;
        private Action onClicked;
        private RectTransform rootRt;

        public static FriendEntryView BuildInto(
            RectTransform hudRoot,
            BottomNavBarView barView,
            Action onClicked)
        {
            if (hudRoot == null)
                return null;

            var rootGo = new GameObject("FriendEntryLayer", typeof(RectTransform));
            var root = rootGo.GetComponent<RectTransform>();
            root.SetParent(hudRoot, false);
            StretchFull(root);

            var entryRt = CreateChildRect(root, "FriendEntryButton",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(EntryPosX, EntryPosY), new Vector2(EntrySize, EntrySize));
            var entryImage = entryRt.gameObject.AddComponent<Image>();
            var entrySprite = Resources.Load<Sprite>(ResEntryIcon);
            if (entrySprite != null)
            {
                entryImage.sprite = entrySprite;
                entryImage.preserveAspect = true;
                entryImage.color = Color.white;
            }
            else
            {
                // 缺图回退：纯色块 + 文字「好友」（SPEC §13.1）。
                entryImage.color = new Color(0.26f, 0.55f, 0.85f, 0.95f);
                var labelRt = CreateChildRect(entryRt, "Label",
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(EntrySize, 60f));
                var label = labelRt.gameObject.AddComponent<Text>();
                label.text = "好友";
                label.font = FarmGridView.LoadBuiltinFont();
                label.fontSize = 40;
                label.alignment = TextAnchor.MiddleCenter;
                label.color = Color.white;
                label.raycastTarget = false;
                UnityEngine.Debug.LogWarning(
                    "[FriendEntryView] 缺少图标 Resources/" + ResEntryIcon + "，使用文字按钮回退。");
            }
            entryImage.raycastTarget = true;

            var entryBtn = entryRt.gameObject.AddComponent<Button>();
            entryBtn.transition = Selectable.Transition.None;
            entryBtn.targetGraphic = entryImage;

            var view = rootGo.AddComponent<FriendEntryView>();
            view.rootRt = root;
            view.bottomNav = barView;
            view.onClicked = onClicked;
            entryBtn.onClick.AddListener(view.OnEntryClicked);

            if (barView != null)
            {
                barView.OnOpenChanged += view.OnBottomNavOpenChanged;
                view.OnBottomNavOpenChanged(barView.OpenIndex, barView.OpenKey);
            }

            return view;
        }

        private void OnEntryClicked()
        {
            onClicked?.Invoke();
        }

        private void OnBottomNavOpenChanged(int index, string key)
        {
            bool jiaYuan = !string.IsNullOrEmpty(key) &&
                           string.Equals(key, JiaYuanNavKey, StringComparison.Ordinal);
            if (rootRt != null)
                rootRt.gameObject.SetActive(jiaYuan);
        }

        private void OnDestroy()
        {
            if (bottomNav != null)
                bottomNav.OnOpenChanged -= OnBottomNavOpenChanged;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static RectTransform CreateChildRect(
            RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }
    }
}
