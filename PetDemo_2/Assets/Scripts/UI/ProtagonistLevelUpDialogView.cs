// SPEC §12.10：战斗结算手动关闭后，于主线 Canvas 弹出「主角升级」演示窗。
// SPEC §9.8.16：首次 Show 解锁主线竞技场入口。
using System;
using PetDemo.Farm;
using PetDemo.Save;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class ProtagonistLevelUpDialogView : MonoBehaviour
    {
        public const string ResHeroImage = "AirUI/ShengJi_1";
        private const float PanelWidth = 1080f;
        private const float PanelHeight = 1920f;
        private const float ButtonRowHeight = 100f;
        private const float ButtonRowPosY = 478f;
        private const float LaterButtonPosX = -126f;
        private const float LaterButtonPosY = -117f;
        private const float GoButtonPosX = 149f;
        private const float GoButtonPosY = -120f;
        private const float ActionButtonWidth = 220f;
        private const float ActionButtonHeight = 80f;

        public static ProtagonistLevelUpDialogView Instance { get; private set; }

        /// <summary>SPEC §9.8.16：是否已解锁竞技场入口（运行时缓存，与存档 <see cref="UiProgress.mainStoryArenaEntryUnlocked"/> 同步）。</summary>
        public static bool ArenaEntryUnlocked { get; private set; }

        /// <summary>是否已解锁（优先读 <see cref="PlantingService"/> 存档字段）。</summary>
        public static bool IsArenaEntryUnlocked()
        {
            var svc = PlantingService.Instance;
            if (svc != null)
                return svc.IsMainStoryArenaEntryUnlocked();
            return ArenaEntryUnlocked;
        }

        /// <summary>读档后由 UI 构建调用，恢复解锁态并刷新入口。</summary>
        public static void SyncArenaUnlockFromSave()
        {
            var svc = PlantingService.Instance;
            if (svc != null && svc.IsMainStoryArenaEntryUnlocked())
                ArenaEntryUnlocked = true;
        }

        /// <summary>SPEC §9.8.16：首次解锁时派发，供 <see cref="MainStoryArenaEntryView"/> 刷新显隐。</summary>
        public static event Action OnArenaEntryUnlocked;

        private RectTransform modalRt;
        private RoleGrowthScreenView roleGrowthScreen;
        private BottomNavBarView bottomNav;

        public static ProtagonistLevelUpDialogView BuildInto(
            RectTransform canvasRect,
            RoleGrowthScreenView roleGrowth,
            BottomNavBarView barView)
        {
            if (canvasRect == null)
                return null;

            if (Instance != null)
            {
                if (Instance.modalRt == null)
                    Instance = null;
                else
                {
                    Instance.roleGrowthScreen = roleGrowth;
                    Instance.bottomNav = barView;
                    Instance.RebuildPanel();
                    return Instance;
                }
            }

            var staleModal = canvasRect.Find("ProtagonistLevelUpModal");
            if (staleModal != null)
                Destroy(staleModal.gameObject);

            var modalRt = BottomNavAttachedScreenLayout.CreateChildRect(
                canvasRect, "ProtagonistLevelUpModal",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(modalRt);
            modalRt.gameObject.SetActive(false);

            var dimImg = modalRt.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.55f);
            dimImg.raycastTarget = true;

            var view = modalRt.gameObject.AddComponent<ProtagonistLevelUpDialogView>();
            view.modalRt = modalRt;
            view.roleGrowthScreen = roleGrowth;
            view.bottomNav = barView;
            view.BuildPanel(modalRt);
            Instance = view;
            return view;
        }

        public static void RequestShowAfterBattleClose()
        {
            if (Instance == null || Instance.modalRt == null)
                return;
            Instance.Show();
        }

        private void RebuildPanel()
        {
            if (modalRt == null)
                return;

            var panel = modalRt.Find("Panel");
            if (panel != null)
                Destroy(panel.gameObject);

            BuildPanel(modalRt);
        }

        private void BuildPanel(RectTransform parent)
        {
            var panelRt = BottomNavAttachedScreenLayout.CreateChildRect(
                parent, "Panel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(PanelWidth, PanelHeight));

            var heroRt = BottomNavAttachedScreenLayout.CreateChildRect(
                panelRt, "HeroImage",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(PanelWidth, PanelHeight));
            var heroImg = heroRt.gameObject.AddComponent<Image>();
            var heroSprite = Resources.Load<Sprite>(ResHeroImage);
            if (heroSprite != null)
            {
                heroImg.sprite = heroSprite;
                heroImg.preserveAspect = true;
                heroImg.color = Color.white;
            }
            else
            {
                heroImg.color = new Color(0.2f, 0.16f, 0.28f, 1f);
                UnityEngine.Debug.LogWarning("[ProtagonistLevelUpDialogView] 缺少资源 " + ResHeroImage);
            }
            heroImg.raycastTarget = false;

            var buttonRowRt = BottomNavAttachedScreenLayout.CreateChildRect(
                panelRt, "ButtonRow",
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                Vector2.zero, Vector2.zero);
            buttonRowRt.offsetMin = new Vector2(24f, ButtonRowPosY);
            buttonRowRt.offsetMax = new Vector2(-24f, ButtonRowPosY + ButtonRowHeight);

            BuildActionButton(buttonRowRt, "LaterButton", "后续再说",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(LaterButtonPosX, LaterButtonPosY),
                Hide, hideImage: true, hideLabel: true);
            BuildActionButton(buttonRowRt, "GoButton", "前往",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(GoButtonPosX, GoButtonPosY),
                OnGoClicked, hideImage: true, hideLabel: true);
        }

        private void BuildActionButton(
            RectTransform parent,
            string name,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            UnityEngine.Events.UnityAction onClick,
            bool hideImage = false,
            bool hideLabel = false)
        {
            var btnRt = BottomNavAttachedScreenLayout.CreateChildRect(
                parent, name,
                anchorMin, anchorMax,
                anchoredPosition, new Vector2(ActionButtonWidth, ActionButtonHeight));

            Graphic targetGraphic = null;
            if (!hideLabel)
            {
                var labelRt = BottomNavAttachedScreenLayout.CreateChildRect(
                    btnRt, "Label",
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                BottomNavAttachedScreenLayout.StretchFull(labelRt);
                var labelText = labelRt.gameObject.AddComponent<Text>();
                labelText.text = label;
                labelText.font = FarmGridView.LoadBuiltinFont();
                labelText.fontSize = 36;
                labelText.alignment = TextAnchor.MiddleCenter;
                labelText.color = Color.white;
                labelText.raycastTarget = hideImage;
                targetGraphic = labelText;
            }

            if (!hideImage)
            {
                var btnImg = btnRt.gameObject.AddComponent<Image>();
                btnImg.color = new Color(0.31f, 0.64f, 1f, 1f);
                targetGraphic = btnImg;
            }
            else if (hideLabel)
            {
                var hitImg = btnRt.gameObject.AddComponent<Image>();
                hitImg.color = Color.clear;
                hitImg.raycastTarget = true;
                targetGraphic = hitImg;
            }

            var btn = btnRt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = targetGraphic;
            btn.onClick.AddListener(onClick);
        }

        private void OnGoClicked()
        {
            Hide();
            if (roleGrowthScreen != null)
                roleGrowthScreen.NavigateToTianFuPage(bottomNav);
        }

        public void Show()
        {
            if (modalRt == null)
                return;
            modalRt.SetAsLastSibling();
            modalRt.gameObject.SetActive(true);

            UnlockArenaEntryPersistent();
            MainStoryArenaEntryView.RefreshAllEntries();
        }

        private static void UnlockArenaEntryPersistent()
        {
            if (IsArenaEntryUnlocked())
            {
                ArenaEntryUnlocked = true;
                return;
            }

            ArenaEntryUnlocked = true;
            var svc = PlantingService.Instance;
            if (svc != null)
                svc.SetMainStoryArenaEntryUnlocked(true);
            GameSaveCoordinator.TrySaveActiveSlot();
            OnArenaEntryUnlocked?.Invoke();
        }

        public void Hide()
        {
            if (modalRt != null)
                modalRt.gameObject.SetActive(false);
            // 关闭升级窗后刷新竞技场入口（Show 时主线层可能尚未激活）。
            MainStoryArenaEntryView.RefreshAllEntries();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
