// SPEC §9.5.3 (v3.89)：精灵互动头顶 HUD（喂食/抚摸按钮、好感图标）。
using System;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    public sealed class PetInteractionHudView
    {
        private const string FeedIconPath = "AirUI/Pet_HuDong_1";
        private const string PetIconPath = "AirUI/Pet_HuDong_2";
        private const string FavorIconPath = "AirUI/HaoGan_1";
        private const float ChoiceButtonsPosY = 500f;
        private const float FeedButtonPosX = -116f;
        private const float PetButtonPosX = 145f;
        private const float ChoiceButtonWidth = 193f;
        private const float ChoiceButtonHeight = 199f;
        private const float FavorIconPosY = 500f;
        private const float FavorIconWidth = 193f;
        private const float FavorIconHeight = 199f;

        private static Sprite sFeedSprite;
        private static Sprite sPetSprite;
        private static Sprite sFavorSprite;
        private static bool sSpritesLoaded;

        private RectTransform _hudRoot;
        private RectTransform _choiceRoot;
        private RectTransform _favorRoot;

        public void ShowChoiceButtons(
            RectTransform petRt,
            float headHalfHeight,
            Action onFeed,
            Action onPet)
        {
            if (petRt == null)
                return;

            EnsureSpritesLoaded();
            HideAll();

            _hudRoot = CreateHudChild(petRt, "PetInteractionHud");
            _choiceRoot = CreateHudChild(_hudRoot, "ChoiceButtons");
            _choiceRoot.anchoredPosition = new Vector2(0f, ChoiceButtonsPosY);

            bool hasFeed = sFeedSprite != null;
            bool hasPet = sPetSprite != null;
            if (!hasFeed && !hasPet)
            {
                UnityEngine.Debug.LogWarning("[PetInteractionHudView] 喂食/抚摸图标均缺失。");
                return;
            }

            if (hasFeed)
            {
                CreateChoiceButton(
                    _choiceRoot, "FeedButton", sFeedSprite,
                    new Vector2(FeedButtonPosX, 0f), onFeed);
            }

            if (hasPet)
            {
                CreateChoiceButton(
                    _choiceRoot, "PetButton", sPetSprite,
                    new Vector2(PetButtonPosX, 0f), onPet);
            }
        }

        public void ShowFavorIcon(RectTransform petRt, float headHalfHeight)
        {
            if (petRt == null)
                return;

            EnsureSpritesLoaded();
            if (_choiceRoot != null)
            {
                UnityEngine.Object.Destroy(_choiceRoot.gameObject);
                _choiceRoot = null;
            }

            if (sFavorSprite == null)
            {
                UnityEngine.Debug.LogWarning("[PetInteractionHudView] 好感图标缺失：" + FavorIconPath);
                return;
            }

            if (_hudRoot == null)
                _hudRoot = CreateHudChild(petRt, "PetInteractionHud");

            if (_favorRoot != null)
                UnityEngine.Object.Destroy(_favorRoot.gameObject);

            _favorRoot = CreateHudChild(_hudRoot, "FavorIcon");
            _favorRoot.anchoredPosition = new Vector2(0f, FavorIconPosY);
            _favorRoot.sizeDelta = new Vector2(FavorIconWidth, FavorIconHeight);

            var img = _favorRoot.gameObject.AddComponent<Image>();
            img.sprite = sFavorSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
        }

        public void HideAll()
        {
            if (_hudRoot != null)
            {
                UnityEngine.Object.Destroy(_hudRoot.gameObject);
                _hudRoot = null;
            }

            _choiceRoot = null;
            _favorRoot = null;
        }

        private static void EnsureSpritesLoaded()
        {
            if (sSpritesLoaded)
                return;
            sSpritesLoaded = true;
            sFeedSprite = Resources.Load<Sprite>(FeedIconPath);
            sPetSprite = Resources.Load<Sprite>(PetIconPath);
            sFavorSprite = Resources.Load<Sprite>(FavorIconPath);
            if (sFeedSprite == null)
                UnityEngine.Debug.LogWarning("[PetInteractionHudView] 缺失：" + FeedIconPath);
            if (sPetSprite == null)
                UnityEngine.Debug.LogWarning("[PetInteractionHudView] 缺失：" + PetIconPath);
            if (sFavorSprite == null)
                UnityEngine.Debug.LogWarning("[PetInteractionHudView] 缺失：" + FavorIconPath);
        }

        private static RectTransform CreateHudChild(RectTransform parent, string name)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
            return rt;
        }

        private static void CreateChoiceButton(
            RectTransform parent,
            string name,
            Sprite sprite,
            Vector2 anchoredPosition,
            Action onClick)
        {
            var rt = CreateHudChild(parent, name);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = new Vector2(ChoiceButtonWidth, ChoiceButtonHeight);

            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = true;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            if (onClick != null)
                btn.onClick.AddListener(() => onClick());
        }
    }
}
