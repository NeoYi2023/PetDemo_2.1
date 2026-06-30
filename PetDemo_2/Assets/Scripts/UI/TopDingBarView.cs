// SPEC §9.8.15：底部导航「家园 / 角色 / 公会 / 商店」Tab 顶部 DingUI 装饰条。
// SPEC §9.8.15.1 (v3.159)：公会 Tab 拉手后左下角登记 NPC 跟随头像列。
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class TopDingBarView : MonoBehaviour
    {
        public const string ResDingSprite = "AirUI/DingUI";

        private const float FollowerAvatarSize = 84f;
        private const float FollowerAvatarSpacing = 8f;
        private const float FollowerAvatarInsetX = 16f;
        private const float FollowerAvatarInsetY = 16f;

        private static readonly string[] VisibleNavKeys =
        {
            JiaYuanHomeFeatureEntriesView.JiaYuanNavKey,
            RoleGrowthScreenView.JueSeNavKey,
            BottomNavSimpleBackgroundScreenView.GongHuiNavKey,
            BottomNavSimpleBackgroundScreenView.ShangDianNavKey,
        };

        /// <summary>SPEC §9.8.15（v3.122 JiaYuan / v3.158 GongHui）：点击 DingUI 可跳转创角界面的底栏 Tab。</summary>
        private static readonly string[] CharacterCreationNavKeys =
        {
            JiaYuanHomeFeatureEntriesView.JiaYuanNavKey,
            GongHuiScreenView.GongHuiNavKey,
        };

        private RectTransform rootRt;
        private RectTransform followerAvatarStackRt;
        private BottomNavBarView bottomNav;
        private GuildNpcFollowController guildFollowController;
        private Button clickButton;
        private Action onNavigateToCharacterCreation;
        private string currentNavKey;
        private readonly List<string> followerNpcIds = new List<string>();

        /// <summary>
        /// 构建主 Canvas 最顶层 DingUI；须在全部其它 UI 构建完成后调用以保证 <c>SetAsLastSibling</c> 置顶。
        /// </summary>
        public static TopDingBarView BuildInto(RectTransform canvasRect, BottomNavBarView barView)
        {
            if (canvasRect == null || barView == null)
                return null;

            var rootGo = new GameObject("TopDingBar", typeof(RectTransform));
            var root = rootGo.GetComponent<RectTransform>();
            root.SetParent(canvasRect, false);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = Vector2.zero;

            var sprite = Resources.Load<Sprite>(ResDingSprite);
            var image = rootGo.AddComponent<Image>();
            image.raycastTarget = true;
            image.preserveAspect = true;

            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
                image.SetNativeSize();
            }
            else
            {
                image.color = new Color(0f, 0f, 0f, 0f);
                root.sizeDelta = Vector2.zero;
                UnityEngine.Debug.LogWarning(
                    "[" + nameof(TopDingBarView) + "] 缺少顶部装饰图 Resources/" + ResDingSprite +
                    "，TopDingBar 将保持隐藏。");
            }

            root.SetAsLastSibling();
            rootGo.SetActive(false);

            var clickButton = rootGo.AddComponent<Button>();
            clickButton.transition = Selectable.Transition.None;

            var view = rootGo.AddComponent<TopDingBarView>();
            view.rootRt = root;
            view.bottomNav = barView;
            view.clickButton = clickButton;
            view.followerAvatarStackRt = BuildFollowerAvatarStack(root);
            view.clickButton.onClick.AddListener(view.OnClick);
            barView.OnOpenChanged += view.OnBottomNavOpenChanged;
            view.OnBottomNavOpenChanged(barView.OpenIndex, barView.OpenKey);
            return view;
        }

        /// <summary>SPEC §9.8.15.1：订阅公会跟随控制器，拉手成功后追加左下角头像。</summary>
        public void BindGuildFollowController(GuildNpcFollowController controller)
        {
            if (guildFollowController == controller)
                return;

            if (guildFollowController != null)
                guildFollowController.FollowerAdded -= OnFollowerAdded;

            guildFollowController = controller;
            if (guildFollowController != null)
                guildFollowController.FollowerAdded += OnFollowerAdded;
        }

        /// <summary>SPEC §9.8.15（v3.122/v3.158）：家园 / 公会 Tab 点击 DingUI 时打开创角界面。</summary>
        public void BindNavigateToCharacterCreation(Action navigate)
        {
            onNavigateToCharacterCreation = navigate;
            RefreshClickInteractable();
        }

        private static RectTransform BuildFollowerAvatarStack(RectTransform parent)
        {
            var stackGo = new GameObject("FollowerAvatarStack", typeof(RectTransform));
            var stackRt = stackGo.GetComponent<RectTransform>();
            stackRt.SetParent(parent, false);
            stackRt.anchorMin = new Vector2(0f, 0f);
            stackRt.anchorMax = new Vector2(0f, 0f);
            stackRt.pivot = new Vector2(0f, 1f);
            stackRt.anchoredPosition = new Vector2(FollowerAvatarInsetX, -FollowerAvatarInsetY);
            stackRt.sizeDelta = Vector2.zero;
            stackGo.SetActive(false);
            return stackRt;
        }

        private void OnFollowerAdded(GuildNpcMarker marker)
        {
            if (!IsGongHuiNavKey(currentNavKey))
                return;
            AddFollowerAvatar(marker);
        }

        private void AddFollowerAvatar(GuildNpcMarker marker)
        {
            if (marker == null || followerAvatarStackRt == null)
                return;

            string npcId = marker.NpcId;
            if (string.IsNullOrEmpty(npcId))
                return;

            for (int i = 0; i < followerNpcIds.Count; i++)
            {
                if (string.Equals(followerNpcIds[i], npcId, StringComparison.Ordinal))
                    return;
            }

            int index = followerNpcIds.Count;
            followerNpcIds.Add(npcId);

            var avatarRt = GuildSceneUiFactory.CreateChildRect(
                followerAvatarStackRt, "FollowerAvatar_" + npcId,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -(index * (FollowerAvatarSize + FollowerAvatarSpacing))),
                new Vector2(FollowerAvatarSize, FollowerAvatarSize));

            var avatarImg = avatarRt.gameObject.AddComponent<Image>();
            avatarImg.raycastTarget = false;
            marker.ApplyAvatarToImage(avatarImg);

            followerAvatarStackRt.gameObject.SetActive(true);
        }

        private void ClearFollowerAvatars()
        {
            followerNpcIds.Clear();
            if (followerAvatarStackRt == null)
                return;

            for (int i = followerAvatarStackRt.childCount - 1; i >= 0; i--)
                Destroy(followerAvatarStackRt.GetChild(i).gameObject);

            followerAvatarStackRt.gameObject.SetActive(false);
        }

        private void OnBottomNavOpenChanged(int index, string key)
        {
            if (!IsGongHuiNavKey(key))
                ClearFollowerAvatars();

            currentNavKey = key;
            bool show = ShouldShowForKey(key) && HasVisibleSprite();
            if (rootRt == null)
                return;

            rootRt.gameObject.SetActive(show);
            if (show)
                rootRt.SetAsLastSibling();
            RefreshClickInteractable();
        }

        private void RefreshClickInteractable()
        {
            if (clickButton == null)
                return;

            bool canNavigate = onNavigateToCharacterCreation != null
                && rootRt != null
                && rootRt.gameObject.activeSelf
                && CanNavigateToCharacterCreationForKey(currentNavKey);
            clickButton.interactable = canNavigate;
        }

        private void OnClick()
        {
            if (clickButton == null || !clickButton.interactable)
                return;
            onNavigateToCharacterCreation?.Invoke();
        }

        private bool HasVisibleSprite()
        {
            if (rootRt == null)
                return false;
            var image = rootRt.GetComponent<Image>();
            return image != null && image.sprite != null;
        }

        private static bool IsGongHuiNavKey(string key)
        {
            return string.Equals(key, GongHuiScreenView.GongHuiNavKey, StringComparison.Ordinal);
        }

        internal static bool ShouldShowForKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            for (int i = 0; i < VisibleNavKeys.Length; i++)
            {
                if (string.Equals(key, VisibleNavKeys[i], StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool CanNavigateToCharacterCreationForKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            for (int i = 0; i < CharacterCreationNavKeys.Length; i++)
            {
                if (string.Equals(key, CharacterCreationNavKeys[i], StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private void OnDestroy()
        {
            if (clickButton != null)
                clickButton.onClick.RemoveListener(OnClick);
            if (bottomNav != null)
                bottomNav.OnOpenChanged -= OnBottomNavOpenChanged;
            if (guildFollowController != null)
                guildFollowController.FollowerAdded -= OnFollowerAdded;
        }
    }
}
