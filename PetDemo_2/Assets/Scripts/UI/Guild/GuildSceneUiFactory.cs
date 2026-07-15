// SPEC §9.8.9：公会场景名牌（建筑/NPC）共用 UGUI 构建工具。
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    public static class GuildSceneUiFactory
    {
        public const string NpcInteractButtonLabel = "拉手";
        /// <summary>SPEC §9.8.9.13 (v3.235)：建筑名牌 ActionButton/Label 统一文案。</summary>
        public const string BuildingActionButtonLabel = "前往";
        private const float NpcPlateWidth = 360f;
        private const float NpcPlateHeight = 180f;
        private const float ResponseAreaPlateHeight = 140f;
        private const float NpcAvatarSize = 84f;
        public static RectTransform CreateChildRect(
            RectTransform parent, string name,
            Vector2 anchor, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            return rt;
        }

        public static RectTransform CreatePlateRoot(
            RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size)
        {
            var rt = CreateChildRect(parent, name,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), anchoredPosition, size);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.08f, 0.14f, 0.78f);
            bg.raycastTarget = false;
            return rt;
        }

        public static Text AddText(
            RectTransform parent, string name, string content, int fontSize,
            Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var rt = CreateChildRect(parent, name, anchor, pivot, anchoredPosition, size);
            var text = rt.gameObject.AddComponent<Text>();
            text.text = content;
            text.font = LoadFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static void ApplyAvatarImage(Image img, Sprite avatarSprite, Color fallbackColor)
        {
            img.preserveAspect = true;
            if (avatarSprite != null)
            {
                img.sprite = avatarSprite;
                img.color = Color.white;
            }
            else
            {
                img.sprite = null;
                img.color = fallbackColor;
            }
        }

        /// <summary>SPEC §9.8.9.9：NPC 名牌（头像 + 名字 + InteractButton/Label）。</summary>
        public static RectTransform BuildNpcNamePlate(
            RectTransform parent,
            string displayName,
            Sprite avatarSprite,
            Color avatarFallbackColor,
            float plateOffsetY)
        {
            var rt = CreatePlateRoot(
                parent, "NamePlate", new Vector2(0f, plateOffsetY),
                new Vector2(NpcPlateWidth, NpcPlateHeight));

            var avatarRt = CreateChildRect(rt, "Avatar",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -16f), new Vector2(NpcAvatarSize, NpcAvatarSize));
            var avatarImg = avatarRt.gameObject.AddComponent<Image>();
            avatarImg.raycastTarget = false;
            ApplyAvatarImage(avatarImg, avatarSprite, avatarFallbackColor);

            AddText(rt, "NameText", displayName, 32,
                new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(112f, -58f), new Vector2(230f, 48f),
                TextAnchor.MiddleLeft);

            AddButton(rt, "InteractButton", NpcInteractButtonLabel,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 12f), new Vector2(140f, 56f));
            return rt;
        }

        /// <summary>SPEC §9.8.9.11：响应区域名牌（图标 + 名称；无按钮，auto-enter）。</summary>
        public static RectTransform BuildResponseAreaNamePlate(
            RectTransform parent,
            string displayName,
            Sprite iconSprite,
            Color iconFallbackColor,
            float plateOffsetY)
        {
            var rt = CreatePlateRoot(
                parent, "NamePlate", new Vector2(0f, plateOffsetY),
                new Vector2(NpcPlateWidth, ResponseAreaPlateHeight));

            var iconRt = CreateChildRect(rt, "Icon",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(16f, 0f), new Vector2(NpcAvatarSize, NpcAvatarSize));
            var iconImg = iconRt.gameObject.AddComponent<Image>();
            iconImg.raycastTarget = false;
            ApplyAvatarImage(iconImg, iconSprite, iconFallbackColor);

            AddText(rt, "NameText", displayName, 32,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(112f, 0f), new Vector2(230f, 48f),
                TextAnchor.MiddleLeft);
            return rt;
        }

        public static void SetNpcInteractButtonLabel(
            RectTransform plateRt, string label = NpcInteractButtonLabel)
        {
            if (plateRt == null)
                return;
            var labelTr = plateRt.Find("InteractButton/Label");
            if (labelTr == null)
                return;
            var text = labelTr.GetComponent<Text>();
            if (text != null)
                text.text = label;
        }

        /// <summary>SPEC §9.8.9.13 (v3.235)：建筑名牌 ActionButton/Label 强制同步为「前往」。</summary>
        public static void SetBuildingActionButtonLabel(
            RectTransform plateRt, string label = BuildingActionButtonLabel)
        {
            if (plateRt == null)
                return;
            var labelTr = plateRt.Find("ActionButton/Label");
            if (labelTr == null)
                return;
            var text = labelTr.GetComponent<Text>();
            if (text != null)
                text.text = label;
        }

        public static Button AddButton(
            RectTransform parent, string name, string label,
            Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            var rt = CreateChildRect(parent, name, anchor, pivot, anchoredPosition, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.96f, 0.62f, 0.18f, 0.95f);
            img.raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;

            AddText(rt, "Label", label, 28,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return btn;
        }

        public static Font LoadFont()
        {
            var font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }
}
