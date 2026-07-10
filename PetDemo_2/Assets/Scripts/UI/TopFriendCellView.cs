// SPEC §9.14.8 第 1 点（v3.158）：亲密度页签好友双列网格单元。
// 九宫格背景 friends_bg_1 + 头像/头像框/名字/性别图标/在线图标 + 左上 friends_bg_2 叠 IntimacyIcon/IntimacyText；
// 整体点击打开好友详情弹窗（去找Ta/去Ta家/发消息迁移至弹窗）。
using System;
using PetDemo.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class TopFriendCellView : MonoBehaviour
    {
        private const string ResXing2 = "AirUI/Xing_2";
        private const string ResXing2_1 = "AirUI/Xing_2_1";
        private const string ResIconMan = "AirUI/friends_icon_man";
        private const string ResIconWoman = "AirUI/friends_icon_woman";
        private const string ResOnline = "AirUI/friends_ing_1";
        private const string ResOffline = "AirUI/friends_ing_2";

        [SerializeField] private Button rootButton;
        [SerializeField] private Image background;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Image avatarFrameImage;
        [SerializeField] private Text nameText;
        [SerializeField] private Image genderIconImage;
        [SerializeField] private Image onlineIconImage;
        [SerializeField] private Text onlineText;
        [SerializeField] private Image intimacyBgImage;
        [SerializeField] private Image intimacyIconImage;
        [SerializeField] private Text intimacyText;

        private static readonly Color AvatarFallback = new Color(0.3f, 0.36f, 0.46f, 1f);
        private static readonly Color IconFallback = new Color(0.9f, 0.78f, 0.3f, 1f);

        private FriendProfile bound;
        private Action<FriendProfile> onClick;
        // SPEC §9.14.8：点击 IntimacyBg 区域，根据亲密度图标状态弹出对应弹窗。
        private Action<FriendProfile> onIntimacyBgClick;
        private bool wired;

        /// <summary>预制体模板仅含 UI 组件；运行时按子节点名自动绑定并挂载整体点击事件（仅一次）。</summary>
        public void AutoWire()
        {
            if (background == null)
                background = GetComponent<Image>();
            if (rootButton == null)
                rootButton = GetComponent<Button>();
            if (rootButton == null)
                rootButton = gameObject.AddComponent<Button>();
            if (avatarImage == null)
                avatarImage = FindImage("Avatar");
            if (avatarFrameImage == null)
                avatarFrameImage = FindImage("AvatarFrame");
            if (nameText == null)
                nameText = FindText("NameText");
            if (genderIconImage == null)
                genderIconImage = FindImage("GenderIcon");
            if (onlineIconImage == null)
                onlineIconImage = FindImage("OnlineIcon");
            if (onlineText == null)
                onlineText = FindText("OnlineText");
            if (intimacyBgImage == null)
                intimacyBgImage = FindImage("IntimacyBg");
            if (intimacyIconImage == null)
                intimacyIconImage = FindImage("IntimacyIcon");
            if (intimacyText == null)
                intimacyText = FindText("IntimacyText");

            if (wired)
                return;
            wired = true;
            if (rootButton != null)
            {
                rootButton.transition = Selectable.Transition.None;
                if (rootButton.targetGraphic == null)
                    rootButton.targetGraphic = background;
                rootButton.onClick.AddListener(HandleClick);
            }

            // SPEC §9.14.8：为 IntimacyBg 挂载独立 Button，拦截点击（不穿透至 rootButton）。
            if (intimacyBgImage != null)
            {
                intimacyBgImage.raycastTarget = true;
                var intimacyBgBtn = intimacyBgImage.GetComponent<Button>();
                if (intimacyBgBtn == null)
                    intimacyBgBtn = intimacyBgImage.gameObject.AddComponent<Button>();
                intimacyBgBtn.transition = Selectable.Transition.None;
                intimacyBgBtn.targetGraphic = intimacyBgImage;
                intimacyBgBtn.onClick.AddListener(HandleIntimacyBgClick);
            }
        }

        private void Awake()
        {
            AutoWire();
        }

        /// <param name="intimacyBgClickHandler">点击 IntimacyBg 区域时的回调（可为 null）。</param>
        /// <param name="intimacyDisplayOverride">SPEC §9.14.8（v3.203）：非 null 时覆盖 IntimacyText 展示值。</param>
        public void Bind(FriendProfile friend, Action<FriendProfile> clickHandler,
            Action<FriendProfile> intimacyBgClickHandler = null, int? intimacyDisplayOverride = null)
        {
            AutoWire();
            bound = friend;
            onClick = clickHandler;
            onIntimacyBgClick = intimacyBgClickHandler;

            if (nameText != null)
                nameText.text = friend != null ? friend.displayName : "";

            if (intimacyText != null)
            {
                if (friend == null)
                    intimacyText.text = "";
                else if (intimacyDisplayOverride.HasValue)
                    intimacyText.text = "亲密度 " + intimacyDisplayOverride.Value;
                else
                    intimacyText.text = "亲密度 " + friend.intimacy;
            }

            SetSpriteOrFallback(avatarImage, friend != null ? friend.avatarResource : null, AvatarFallback, true);

            ApplyAvatarFrame(friend);
            ApplyGenderIcon(friend);
            ApplyOnlineStatus(friend);
            ApplyIntimacyIcon(friend);
        }

        private void ApplyAvatarFrame(FriendProfile friend)
        {
            if (avatarFrameImage == null)
                return;
            string res = friend != null ? friend.avatarFrameResource : null;
            if (string.IsNullOrEmpty(res))
            {
                avatarFrameImage.gameObject.SetActive(false);
                return;
            }
            var sprite = Resources.Load<Sprite>(res);
            if (sprite != null)
            {
                avatarFrameImage.gameObject.SetActive(true);
                avatarFrameImage.sprite = sprite;
                avatarFrameImage.color = Color.white;
                avatarFrameImage.preserveAspect = true;
            }
            else
            {
                avatarFrameImage.gameObject.SetActive(false);
                UnityEngine.Debug.LogWarning("[TopFriendCellView] 缺少头像框 Resources/" + res + "。");
            }
        }

        private void ApplyGenderIcon(FriendProfile friend)
        {
            if (genderIconImage == null)
                return;
            string res = (friend != null && friend.isFemale) ? ResIconWoman : ResIconMan;
            SetSpriteOrFallback(genderIconImage, res, IconFallback, true);
        }

        private void ApplyOnlineStatus(FriendProfile friend)
        {
            bool online = friend != null && friend.online;
            if (onlineIconImage != null)
            {
                string res = online ? ResOnline : ResOffline;
                SetSpriteOrFallback(onlineIconImage, res, IconFallback, true);
            }
            if (onlineText != null)
            {
                onlineText.text = online ? "在线" : "离线";
                onlineText.color = Color.black;
            }
        }

        private void ApplyIntimacyIcon(FriendProfile friend)
        {
            if (intimacyIconImage == null)
                return;
            string res = (friend != null && friend.intimacyInterrupted) ? ResXing2_1 : ResXing2;
            SetSpriteOrFallback(intimacyIconImage, res, IconFallback, true);
            if (intimacyIconImage.sprite == null)
                UnityEngine.Debug.LogWarning("[TopFriendCellView] 缺少亲密度图标 Resources/" + res + "。");
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

        private void HandleClick()
        {
            if (bound != null)
                onClick?.Invoke(bound);
        }

        // SPEC §9.14.8：IntimacyBg 点击：将当前绑定好友传给上层回调，由上层决定弹出哪个弹窗。
        private void HandleIntimacyBgClick()
        {
            if (bound != null)
                onIntimacyBgClick?.Invoke(bound);
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
    }
}
