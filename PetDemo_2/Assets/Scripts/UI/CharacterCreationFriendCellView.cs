// SPEC §9.14.3：创角好友列表 — 单行好友单元（头像/名字/亲密度/在线）。
using System;
using PetDemo.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class CharacterCreationFriendCellView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Text nameText;
        [SerializeField] private Text intimacyText;
        [SerializeField] private Image onlineDot;
        [SerializeField] private Text onlineText;

        private static readonly Color RowColor = new Color(0.16f, 0.18f, 0.24f, 0.95f);
        private static readonly Color OnlineColor = new Color(0.35f, 0.82f, 0.4f, 1f);
        private static readonly Color OfflineColor = new Color(0.5f, 0.5f, 0.55f, 1f);
        private static readonly Color AvatarFallback = new Color(0.3f, 0.36f, 0.46f, 1f);

        private FriendProfile bound;
        private Action<FriendProfile> clickHandler;

        /// <summary>预制体模板仅含 UI 组件；运行时按子节点名自动绑定。</summary>
        public void AutoWire()
        {
            if (button == null)
                button = GetComponent<Button>();
            if (background == null)
                background = GetComponent<Image>();
            if (avatarImage == null)
                avatarImage = FindImage("Avatar");
            if (nameText == null)
                nameText = FindText("NameText");
            if (intimacyText == null)
                intimacyText = FindText("IntimacyText");
            if (onlineDot == null)
                onlineDot = FindImage("OnlineDot");
            if (onlineText == null)
                onlineText = FindText("OnlineText");
        }

        private void Awake()
        {
            AutoWire();
            if (button != null)
                button.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClick);
        }

        public void Bind(FriendProfile friend, Action<FriendProfile> onClick)
        {
            bound = friend;
            clickHandler = onClick;

            if (background != null)
                background.color = RowColor;

            if (nameText != null)
                nameText.text = friend != null ? friend.displayName : "";

            if (intimacyText != null)
                intimacyText.text = friend != null ? ("亲密度 " + friend.intimacy) : "";

            bool online = friend != null && friend.online;
            if (onlineDot != null)
                onlineDot.color = online ? OnlineColor : OfflineColor;
            if (onlineText != null)
            {
                onlineText.text = online ? "在线" : "离线";
                onlineText.color = Color.black;
            }

            if (avatarImage != null)
            {
                Sprite sprite = null;
                if (friend != null && !string.IsNullOrEmpty(friend.avatarResource))
                    sprite = Resources.Load<Sprite>(friend.avatarResource);
                if (sprite != null)
                {
                    avatarImage.sprite = sprite;
                    avatarImage.color = Color.white;
                    avatarImage.preserveAspect = true;
                }
                else
                {
                    avatarImage.sprite = null;
                    avatarImage.color = AvatarFallback;
                }
            }
        }

        private void HandleClick()
        {
            if (bound != null)
                clickHandler?.Invoke(bound);
        }

        private Image FindImage(string nodeName)
        {
            var t = transform.Find(nodeName);
            return t != null ? t.GetComponent<Image>() : null;
        }

        private Text FindText(string nodeName)
        {
            var t = transform.Find(nodeName);
            return t != null ? t.GetComponent<Text>() : null;
        }
    }
}
