// SPEC §9.8.9.12：公会全景模式 — 拉远镜头至世界全览、禁用摇杆、强制显示全部 NamePlate 并保持字号。
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class GuildPanoramaController : MonoBehaviour
    {
        private const float FitMargin = 0.95f;
        // SPEC §9.8.9.12：全景态建筑/响应区名牌固定放大补偿（ScaleX/Y = 2.5）与 NameText 字号（42）。
        private const float PanoramaPlateScale = 2.5f;
        private const int PanoramaPlateFontSize = 42;

        private RectTransform viewportRt;
        private RectTransform worldContentRt;
        private JiaYuanViewportFollowController followController;
        private VirtualJoystickView joystick;
        private GuildProximityController proximityController;
        private GuildPlayerController playerController;

        private Vector3 savedWorldScale;
        private Vector2 savedContentPos;
        private bool isPanoramaActive;
        private bool initialized;
        private RectTransform blackBackdropRt;

        public bool IsPanoramaActive => isPanoramaActive;

        public void Initialize(
            RectTransform viewport,
            RectTransform worldContent,
            JiaYuanViewportFollowController follow,
            VirtualJoystickView joystickView,
            GuildProximityController proximity,
            GuildPlayerController player)
        {
            viewportRt = viewport;
            worldContentRt = worldContent;
            followController = follow;
            joystick = joystickView;
            proximityController = proximity;
            playerController = player;
            initialized = viewportRt != null && worldContentRt != null;
        }

        public void TogglePanorama()
        {
            if (isPanoramaActive)
                ExitPanorama();
            else
                EnterPanorama();
        }

        public void ExitPanoramaIfActive()
        {
            if (isPanoramaActive)
                ExitPanorama();
        }

        public void EnterPanorama()
        {
            if (!initialized || isPanoramaActive || worldContentRt == null)
                return;

            savedWorldScale = worldContentRt.localScale;
            savedContentPos = worldContentRt.anchoredPosition;

            float fitScale = ComputeFitScale();

            SetBackdropVisible(true);

            worldContentRt.localScale = new Vector3(fitScale, fitScale, 1f);
            worldContentRt.anchoredPosition = Vector2.zero;

            if (followController != null)
                followController.SetFollowFrozen(true);

            if (joystick != null)
                joystick.SetInputEnabled(false);
            if (playerController != null)
                playerController.SetMovementEnabled(false);

            if (proximityController != null)
                proximityController.SetPanoramaMode(true, PanoramaPlateScale, PanoramaPlateFontSize);

            isPanoramaActive = true;
        }

        public void ExitPanorama()
        {
            if (!isPanoramaActive || worldContentRt == null)
                return;

            if (proximityController != null)
                proximityController.SetPanoramaMode(false);

            worldContentRt.localScale = savedWorldScale;
            worldContentRt.anchoredPosition = savedContentPos;

            if (followController != null)
            {
                followController.SetFollowFrozen(false);
                followController.SnapOnce();
            }

            if (joystick != null)
                joystick.SetInputEnabled(true);
            if (playerController != null)
                playerController.SetMovementEnabled(true);

            SetBackdropVisible(false);

            isPanoramaActive = false;
        }

        // SPEC §9.8.9.12：全景态在面板最底层铺满纯黑背景，退出即隐藏。
        private void SetBackdropVisible(bool show)
        {
            if (show)
            {
                if (blackBackdropRt == null)
                    blackBackdropRt = BuildBackdrop();
                if (blackBackdropRt != null)
                {
                    blackBackdropRt.SetAsFirstSibling();
                    blackBackdropRt.gameObject.SetActive(true);
                }
            }
            else if (blackBackdropRt != null)
            {
                blackBackdropRt.gameObject.SetActive(false);
            }
        }

        private RectTransform BuildBackdrop()
        {
            var go = new GameObject("PanoramaBlackBackdrop", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            var img = go.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;
            return rt;
        }

        private float ComputeFitScale()
        {
            if (viewportRt == null || worldContentRt == null)
                return GongHuiScreenView.WorldContentLocalScale.x;

            var viewSize = viewportRt.rect.size;
            var contentSize = worldContentRt.rect.size;
            if (contentSize.x <= 0f || contentSize.y <= 0f)
                return GongHuiScreenView.WorldContentLocalScale.x;

            float fit = Mathf.Min(viewSize.x / contentSize.x, viewSize.y / contentSize.y) * FitMargin;
            return Mathf.Max(0.01f, fit);
        }

        private void OnDisable()
        {
            if (isPanoramaActive)
                ExitPanorama();
        }
    }
}
