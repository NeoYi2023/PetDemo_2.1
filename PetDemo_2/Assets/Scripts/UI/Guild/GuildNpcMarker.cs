// SPEC §9.8.9.3 / §9.8.9.4 / §9.8.9.9：公会场景 NPC 标记 — 人工摆放出生点；主角进入半径时头顶显示
// 「头像 + 名字 + 互动按钮」；头像/名字默认取 §9.14.2 FriendCatalog，可被 Inspector 覆盖。
// v3.124：互动按钮点击触发 OnInteract 回调（接 GuildNpcFollowController 跟随主角）。
// SPEC §9.8.9.8：互动进入跟随后 NamePlate 永久隐藏（同 Tab 会话内），改为 NPC 正上方常驻
// 独立 Avatar（84×84，资源与名牌 Avatar 一致）；离开公会 Tab 退出跟随 UI 模式，接近检测名牌恢复。
// v3.156：按 skeletonKind 区分 LangRen/LangMeiRen；Npc_1 名牌上方显示 HuDong_DongZuo_1 动作图标。
using System;
using System.Collections.Generic;
using PetDemo.Core;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class GuildNpcMarker : MonoBehaviour
    {
        private const float AvatarSize = 84f;
        private const float NamePlateHeight = 180f;
        private const float ActionIconSize = 80f;
        private static readonly Color AvatarFallbackColor = new Color(0.45f, 0.55f, 0.75f, 1f);
        private static readonly Color ActionIconFallbackColor = new Color(0.96f, 0.62f, 0.18f, 0.95f);

        [SerializeField] private string npcId = "friend-01";
        [SerializeField] private string displayNameOverride = "";
        [SerializeField] private string avatarResourceOverride = "";
        [SerializeField] private GuildNpcSkeletonKind skeletonKind = GuildNpcSkeletonKind.LangRen;
        [SerializeField] private bool showActionIcon;
        [SerializeField] private string actionIconResource = "AirUI/HuDong_DongZuo_1";
        [SerializeField] private float actionIconOffsetAbovePlate = 24f;
        [SerializeField] private float interactRadius = 220f;
        [SerializeField] private float plateOffsetY = 330f;

        private RectTransform plateRt;
        private RectTransform actionIconRt;
        private RectTransform overheadAvatarRt;
        private bool profileResolved;
        private bool isFollowing;
        private bool interactButtonWired;
        private string resolvedName;
        private string resolvedAvatarResource;

        private void Awake()
        {
            TryAcquirePlateFromHierarchy();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            TryAcquirePlateFromHierarchy();
            if (plateRt != null)
                GuildSceneUiFactory.SetNpcInteractButtonLabel(plateRt);
        }
#endif

        public RectTransform Rt => (RectTransform)transform;
        public float InteractRadius => interactRadius;
        public string NpcId => npcId;
        public bool IsFollowing => isFollowing;
        public GuildNpcSkeletonKind SkeletonKind => skeletonKind;
        public bool ShowActionIcon => showActionIcon;

        /// <summary>v3.124：互动按钮点击回调（由 GongHuiScreenView 订阅接跟随控制器）。</summary>
        public Action<GuildNpcMarker> OnInteract;

        /// <summary>v3.156：动作图标点击回调（由 GongHuiScreenView 订阅接 work_2 编排器）。</summary>
        public Action<GuildNpcMarker> OnActionIconClick;

        /// <summary>运行时生成的 NpcSpine 根节点（朝向翻转用）。</summary>
        public RectTransform SpineRt { get; private set; }

        /// <summary>运行时生成的 NpcSpine 骨骼（动画切换用；占位色块回退时为 null）。</summary>
        public SkeletonGraphic NpcSkeleton { get; private set; }

        public void AttachSpine(RectTransform spineRt, SkeletonGraphic skeleton)
        {
            SpineRt = spineRt;
            NpcSkeleton = skeleton;
        }

        public void SetNpcId(string value)
        {
            npcId = value;
            profileResolved = false;
        }

        public void SetSkeletonKind(GuildNpcSkeletonKind kind)
        {
            skeletonKind = kind;
        }

        public void SetShowActionIcon(bool show)
        {
            showActionIcon = show;
        }

        /// <summary>SPEC §9.8.9.8：切换跟随 UI 模式（隐藏名牌 / 显示头顶 Avatar）。</summary>
        public void SetFollowing(bool following)
        {
            if (isFollowing == following)
                return;
            isFollowing = following;

            if (following)
            {
                if (plateRt != null)
                    plateRt.gameObject.SetActive(false);
                if (actionIconRt != null)
                    actionIconRt.gameObject.SetActive(false);

                if (overheadAvatarRt == null)
                    overheadAvatarRt = BuildOverheadAvatar();
                if (overheadAvatarRt != null)
                    overheadAvatarRt.gameObject.SetActive(true);
            }
            else
            {
                if (overheadAvatarRt != null)
                    overheadAvatarRt.gameObject.SetActive(false);
            }
        }

        public void SetPlateVisible(bool visible)
        {
            if (isFollowing)
                return;
            if (visible && plateRt == null)
                TryAcquirePlateFromHierarchy();
            if (visible && plateRt == null)
                plateRt = BuildPlate();
            if (visible && plateRt != null)
            {
                GuildSceneUiFactory.SetNpcInteractButtonLabel(plateRt);
                WireInteractButton(plateRt.Find("InteractButton")?.GetComponent<Button>());
            }
            if (visible && showActionIcon && actionIconRt == null)
                actionIconRt = BuildActionIcon();
            if (plateRt != null && plateRt.gameObject.activeSelf != visible)
                plateRt.gameObject.SetActive(visible);
            if (actionIconRt != null && actionIconRt.gameObject.activeSelf != visible)
                actionIconRt.gameObject.SetActive(visible);
        }

        private void TryAcquirePlateFromHierarchy()
        {
            if (plateRt != null)
                return;
            var existing = Rt.Find("NamePlate") as RectTransform;
            if (existing == null)
                return;
            plateRt = existing;
            GuildSceneUiFactory.SetNpcInteractButtonLabel(plateRt);
        }

        private void WireInteractButton(Button btn)
        {
            if (interactButtonWired || btn == null)
                return;
            interactButtonWired = true;
            ResolveProfile();
            string capturedName = resolvedName;
            string capturedId = npcId;
            btn.onClick.AddListener(() =>
            {
                UnityEngine.Debug.Log("[GongHuiScreen] NPC 拉手按钮：" + capturedName + " (" + capturedId + ")");
                OnInteract?.Invoke(this);
            });
        }

        private void ResolveProfile()
        {
            if (profileResolved)
                return;
            profileResolved = true;

            resolvedName = displayNameOverride;
            resolvedAvatarResource = avatarResourceOverride;
            if (!string.IsNullOrEmpty(resolvedName) && !string.IsNullOrEmpty(resolvedAvatarResource))
                return;

            FriendProfile match = null;
            List<FriendProfile> catalog = FriendCatalog.BuildDefault();
            for (int i = 0; i < catalog.Count; i++)
            {
                if (catalog[i] != null && string.Equals(catalog[i].id, npcId))
                {
                    match = catalog[i];
                    break;
                }
            }

            if (string.IsNullOrEmpty(resolvedName))
                resolvedName = match != null ? match.displayName : (string.IsNullOrEmpty(npcId) ? "公会成员" : npcId);
            if (string.IsNullOrEmpty(resolvedAvatarResource) && match != null)
                resolvedAvatarResource = match.avatarResource;
        }

        /// <summary>SPEC §9.8.15.1：供 TopDingBar 等 HUD 复用与名牌一致的头像资源。</summary>
        public void ApplyAvatarToImage(Image img)
        {
            ResolveProfile();
            var avatarSprite = string.IsNullOrEmpty(resolvedAvatarResource)
                ? null
                : Resources.Load<Sprite>(resolvedAvatarResource);
            GuildSceneUiFactory.ApplyAvatarImage(img, avatarSprite, AvatarFallbackColor);
        }

        // SPEC §9.8.9.8：跟随态头顶 Avatar，水平居中悬于 NPC 正上方。
        private RectTransform BuildOverheadAvatar()
        {
            ResolveProfile();

            var avatarRt = GuildSceneUiFactory.CreateChildRect(Rt, "Avatar",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, plateOffsetY), new Vector2(AvatarSize, AvatarSize));
            var avatarImg = avatarRt.gameObject.AddComponent<Image>();
            avatarImg.raycastTarget = false;
            ApplyAvatarToImage(avatarImg);
            return avatarRt;
        }

        // SPEC §9.8.9.9：NamePlate 正上方的动作图标按钮。
        private RectTransform BuildActionIcon()
        {
            float iconY = plateOffsetY + NamePlateHeight + actionIconOffsetAbovePlate;
            var rt = GuildSceneUiFactory.CreateChildRect(Rt, "ActionIconButton",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f),
                new Vector2(0f, iconY), new Vector2(ActionIconSize, ActionIconSize));

            var img = rt.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = true;
            var sprite = string.IsNullOrEmpty(actionIconResource)
                ? null
                : Resources.Load<Sprite>(actionIconResource);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
            }
            else
            {
                img.sprite = null;
                img.color = ActionIconFallbackColor;
                UnityEngine.Debug.LogWarning(
                    "[GuildNpcMarker] 动作图标资源缺失：" + actionIconResource);
            }

            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;
            string capturedId = npcId;
            btn.onClick.AddListener(() =>
            {
                UnityEngine.Debug.Log("[GongHuiScreen] NPC 动作图标点击：" + capturedId);
                OnActionIconClick?.Invoke(this);
            });
            return rt;
        }

        // 名牌运行时懒创建（SPEC §9.8.9.6），底部枢轴悬在 NPC 头顶。
        private RectTransform BuildPlate()
        {
            ResolveProfile();
            var avatarSprite = string.IsNullOrEmpty(resolvedAvatarResource)
                ? null
                : Resources.Load<Sprite>(resolvedAvatarResource);
            var rt = GuildSceneUiFactory.BuildNpcNamePlate(
                Rt, resolvedName, avatarSprite, AvatarFallbackColor, plateOffsetY);
            WireInteractButton(rt.Find("InteractButton")?.GetComponent<Button>());
            return rt;
        }
    }
}
