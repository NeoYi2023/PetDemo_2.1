// SPEC §9.8.8.7：选择关卡信息假图弹层。
using PetDemo.Core;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class LevelSelectLevelInfoOverlayView : MonoBehaviour
    {
        public const string ResFallbackInfoSprite = "AirUI/GuanQiaMiaoShu_1";
        public const string OverlayObjectName = "LevelSelectLevelInfoOverlay";

        private static LevelSelectLevelInfoOverlayView instance;

        private RectTransform overlayRt;
        private Image infoImage;
        private Text titleText;

        public static LevelSelectLevelInfoOverlayView GetOrCreate(RectTransform canvasRect)
        {
            if (canvasRect == null)
                return null;

            if (instance != null && instance.overlayRt != null)
                return instance;

            var existing = canvasRect.Find(OverlayObjectName);
            if (existing != null)
            {
                var existView = existing.GetComponent<LevelSelectLevelInfoOverlayView>();
                if (existView == null)
                    existView = existing.gameObject.AddComponent<LevelSelectLevelInfoOverlayView>();
                existView.overlayRt = existing as RectTransform;
                instance = existView;
                return existView;
            }

            var go = new GameObject(OverlayObjectName, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvasRect, false);
            BottomNavAttachedScreenLayout.StretchFull(rt);

            var dimImg = go.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.55f);
            dimImg.raycastTarget = true;

            var tapBtn = go.AddComponent<Button>();
            tapBtn.transition = Selectable.Transition.None;
            tapBtn.targetGraphic = dimImg;

            var panelRt = BottomNavAttachedScreenLayout.CreateChildRect(
                rt, "InfoPanel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(880f, 520f));
            var panelImg = panelRt.gameObject.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.10f, 0.16f, 0.96f);
            panelImg.raycastTarget = true;

            var titleRt = BottomNavAttachedScreenLayout.CreateChildRect(
                panelRt, "Title",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), new Vector2(760f, 56f));
            titleRt.pivot = new Vector2(0.5f, 1f);
            var title = titleRt.gameObject.AddComponent<Text>();
            title.font = FarmGridView.LoadBuiltinFont();
            title.fontSize = 40;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = Color.white;
            title.raycastTarget = false;

            var infoRt = BottomNavAttachedScreenLayout.CreateChildRect(
                panelRt, "InfoImage",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -20f), new Vector2(760f, 380f));
            var infoImg = infoRt.gameObject.AddComponent<Image>();
            infoImg.preserveAspect = true;
            infoImg.raycastTarget = false;

            var view = go.AddComponent<LevelSelectLevelInfoOverlayView>();
            view.overlayRt = rt;
            view.infoImage = infoImg;
            view.titleText = title;
            tapBtn.onClick.AddListener(view.Hide);
            go.SetActive(false);
            instance = view;
            return view;
        }

        public static void HideIfAny()
        {
            if (instance != null)
                instance.Hide();
        }

        public void Show(MainStoryLevelConfig levelConfig)
        {
            if (overlayRt == null)
                return;

            string title = levelConfig != null && !string.IsNullOrEmpty(levelConfig.displayName)
                ? levelConfig.displayName
                : "关卡信息";
            titleText.text = title;

            Sprite sprite = null;
            if (levelConfig != null && !string.IsNullOrEmpty(levelConfig.infoSpritePath))
                sprite = Resources.Load<Sprite>(levelConfig.infoSpritePath);
            if (sprite == null)
                sprite = Resources.Load<Sprite>(ResFallbackInfoSprite);

            if (sprite != null)
            {
                infoImage.sprite = sprite;
                infoImage.color = Color.white;
                infoImage.enabled = true;
            }
            else
            {
                infoImage.enabled = false;
                UnityEngine.Debug.LogWarning(
                    "[LevelSelectLevelInfoOverlayView] 缺少占位图 Resources/" + ResFallbackInfoSprite);
            }

            overlayRt.gameObject.SetActive(true);
            overlayRt.SetAsLastSibling();
        }

        public void Hide()
        {
            if (overlayRt != null)
                overlayRt.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
