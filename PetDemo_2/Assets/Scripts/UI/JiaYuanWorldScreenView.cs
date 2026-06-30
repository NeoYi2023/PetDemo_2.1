// SPEC §9.8.14：家园世界视口 — JianYuan_2 大图；MainBackground(UI0) 全局隐藏。
using System;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class JiaYuanWorldScreenView : MonoBehaviour
    {
        public const string ResJianYuanBackground = "AirUI/JianYuan_2";

        [SerializeField] private bool worldScreenEnabled = true;

        private RectTransform rootRt;
        private RectTransform viewportRt;
        private RectTransform worldContentRt;
        private RectTransform mainBackgroundRt;
        private BottomNavBarView bottomNav;
        private JiaYuanWindEffectController windEffectController;

        public RectTransform WorldContent => worldContentRt;
        public RectTransform Viewport => viewportRt;

        /// <summary>
        /// 构建家园世界层（可在 BottomNavBar 创建前调用）；导航绑定见 <see cref="BindBottomNavBar"/>。
        /// </summary>
        public static JiaYuanWorldScreenView BuildWorldInto(
            RectTransform canvasRect,
            RectTransform mainBackground)
        {
            if (canvasRect == null)
                return null;

            var root = BottomNavAttachedScreenLayout.CreateChildRect(
                canvasRect, "JiaYuanWorldScreen",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(root);

            var viewport = BottomNavAttachedScreenLayout.CreateChildRect(
                root, "JiaYuanViewport",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            var worldContent = BottomNavAttachedScreenLayout.CreateChildRect(
                viewport, "JiaYuanWorldContent",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

            var bgRt = BottomNavAttachedScreenLayout.CreateChildRect(
                worldContent, "Background",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            var bgImage = bgRt.gameObject.AddComponent<Image>();
            var bgSprite = Resources.Load<Sprite>(ResJianYuanBackground);
            if (bgSprite != null)
            {
                var size = bgSprite.rect.size;
                worldContent.sizeDelta = size;
                bgRt.sizeDelta = size;
                bgImage.sprite = bgSprite;
                bgImage.preserveAspect = false;
                bgImage.color = Color.white;
            }
            else
            {
                worldContent.sizeDelta = new Vector2(1080f, 1920f);
                bgRt.sizeDelta = worldContent.sizeDelta;
                bgImage.color = new Color(0.10f, 0.12f, 0.18f, 1f);
                UnityEngine.Debug.LogWarning(
                    "[JiaYuanWorldScreenView] 缺少背景 Resources/" + ResJianYuanBackground + "，已使用纯色回退。");
            }

            bgImage.raycastTarget = false;

            root.gameObject.SetActive(false);

            var view = root.gameObject.AddComponent<JiaYuanWorldScreenView>();
            view.rootRt = root;
            view.viewportRt = viewport;
            view.worldContentRt = worldContent;
            view.mainBackgroundRt = mainBackground;

            var follow = viewport.gameObject.AddComponent<JiaYuanViewportFollowController>();
            follow.Initialize(viewport, worldContent);

            var windCtrl = root.gameObject.AddComponent<JiaYuanWindEffectController>();
            windCtrl.Initialize(root);
            view.windEffectController = windCtrl;

            if (mainBackground != null)
                mainBackground.gameObject.SetActive(false);

            return view;
        }

        /// <summary>
        /// 将家园世界层置于 <see cref="MainBackground"/> 之上、Canvas 其余 HUD（按钮、底栏、弹窗等）之下。
        /// 须在主界面全部 UI 节点创建完成后调用。
        /// </summary>
        public void ApplySortBelowAllHud()
        {
            if (rootRt == null)
                return;

            int targetIndex = 0;
            if (mainBackgroundRt != null)
                targetIndex = mainBackgroundRt.GetSiblingIndex() + 1;

            rootRt.SetSiblingIndex(targetIndex);
        }

        public void BindBottomNavBar(BottomNavBarView barView)
        {
            if (bottomNav != null)
                bottomNav.OnOpenChanged -= OnBottomNavOpenChanged;

            bottomNav = barView;
            if (bottomNav == null)
                return;

            bottomNav.OnOpenChanged += OnBottomNavOpenChanged;
            RefreshVisibility(bottomNav.OpenKey);
        }

        public void SetWorldScreenEnabled(bool enabled)
        {
            worldScreenEnabled = enabled;
            if (bottomNav != null)
                RefreshVisibility(bottomNav.OpenKey);
            else if (rootRt != null)
                rootRt.gameObject.SetActive(false);
        }

        public void BindFollowTarget(RectTransform villagerRoleRt)
        {
            if (viewportRt == null)
                return;
            var follow = viewportRt.GetComponent<JiaYuanViewportFollowController>();
            if (follow != null)
                follow.SetFollowTarget(villagerRoleRt);
        }

        /// <summary>SPEC §9.8.14 (v3.80.2)：主角一次性动画期间暂停视口跟随。</summary>
        public void SetFollowFrozen(bool frozen)
        {
            if (viewportRt == null)
                return;
            var follow = viewportRt.GetComponent<JiaYuanViewportFollowController>();
            if (follow != null)
                follow.SetFollowFrozen(frozen);
        }

        private void OnBottomNavOpenChanged(int index, string key)
        {
            RefreshVisibility(key);
        }

        private void RefreshVisibility(string openKey)
        {
            bool jiaYuan = !string.IsNullOrEmpty(openKey) &&
                           string.Equals(openKey, JiaYuanHomeFeatureEntriesView.JiaYuanNavKey, StringComparison.Ordinal);
            bool showWorld = jiaYuan && worldScreenEnabled;

            if (rootRt != null)
                rootRt.gameObject.SetActive(showWorld);

            if (mainBackgroundRt != null)
                mainBackgroundRt.gameObject.SetActive(false);

            if (viewportRt != null)
            {
                var follow = viewportRt.GetComponent<JiaYuanViewportFollowController>();
                if (follow != null)
                    follow.SetFollowEnabled(showWorld);
            }

            if (windEffectController != null)
                windEffectController.SetJiaYuanTabActive(showWorld);
        }

        private void OnDestroy()
        {
            if (bottomNav != null)
                bottomNav.OnOpenChanged -= OnBottomNavOpenChanged;
        }
    }
}
