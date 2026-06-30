// SPEC §9.8.9.10：公会社区 App_4 全屏弹层，任意位置点击关闭。
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class GongHuiCommunityOverlayView : MonoBehaviour
    {
        public const string ResCommunitySprite = "AirUI/App_4";
        public const string OverlayObjectName = "GongHuiCommunityOverlay";

        private static GongHuiCommunityOverlayView instance;

        private RectTransform overlayRt;
        private RectTransform canvasRectCache;

        public static GongHuiCommunityOverlayView GetOrCreate(RectTransform canvasRect)
        {
            if (canvasRect == null)
                return null;

            if (instance != null && instance.overlayRt != null)
            {
                instance.canvasRectCache = canvasRect;
                return instance;
            }

            var existing = canvasRect.Find(OverlayObjectName);
            if (existing != null)
            {
                var existView = existing.GetComponent<GongHuiCommunityOverlayView>();
                if (existView == null)
                    existView = existing.gameObject.AddComponent<GongHuiCommunityOverlayView>();
                existView.overlayRt = existing as RectTransform;
                existView.canvasRectCache = canvasRect;
                instance = existView;
                return existView;
            }

            var go = new GameObject(OverlayObjectName, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(canvasRect, false);
            BottomNavAttachedScreenLayout.StretchFull(rt);

            var bgImg = go.AddComponent<Image>();
            var sprite = Resources.Load<Sprite>(ResCommunitySprite);
            if (sprite != null)
            {
                bgImg.sprite = sprite;
                bgImg.preserveAspect = false;
                bgImg.color = Color.white;
            }
            else
            {
                bgImg.color = new Color(0.1f, 0.08f, 0.14f, 1f);
                UnityEngine.Debug.LogWarning(
                    "[GongHuiCommunityOverlayView] 缺少资源 Resources/" + ResCommunitySprite);
            }

            bgImg.raycastTarget = true;

            var tapBtn = go.AddComponent<Button>();
            tapBtn.transition = Selectable.Transition.None;
            tapBtn.targetGraphic = bgImg;

            var view = go.AddComponent<GongHuiCommunityOverlayView>();
            view.overlayRt = rt;
            view.canvasRectCache = canvasRect;
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

        public void Show()
        {
            if (overlayRt == null)
                return;
            overlayRt.gameObject.SetActive(true);
            MainHudLayerRoot.ApplySortTier(overlayRt, MainUiSortTier.HudPopup);
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
