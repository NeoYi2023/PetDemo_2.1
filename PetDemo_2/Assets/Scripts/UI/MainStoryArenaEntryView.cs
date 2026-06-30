// SPEC §9.8.16：主线层左下角「竞技场」入口（JingJi-1），升级窗首次展示后解锁。
using System.Collections.Generic;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class MainStoryArenaEntryView : MonoBehaviour
    {
        public const string ResEntryIcon = "AirUI/JingJi-1";

        private static readonly Vector2 EntrySize = new Vector2(231f, 250f);
        private static readonly Vector2 EntryAnchoredPos = new Vector2(20f, 216f);
        private static readonly Vector2 LabelSize = new Vector2(105f, 158f);
        private static readonly Vector2 LabelAnchoredPos = new Vector2(0f, 33f);
        private const int LabelFontSize = 48;

        private static readonly List<MainStoryArenaEntryView> ActiveEntries = new List<MainStoryArenaEntryView>();

        private RectTransform mainStoryRootRt;
        private RectTransform entryRootRt;
        private RectTransform canvasRectCache;
        private bool subscribedUnlock;

        public static MainStoryArenaEntryView BuildInto(
            RectTransform mainStoryRoot,
            RectTransform canvasRect)
        {
            if (mainStoryRoot == null || canvasRect == null)
                return null;

            var existing = mainStoryRoot.Find("MainStoryArenaEntry");
            if (existing != null)
            {
                var existView = existing.GetComponent<MainStoryArenaEntryView>();
                if (existView == null)
                    existView = existing.gameObject.AddComponent<MainStoryArenaEntryView>();
                existView.mainStoryRootRt = mainStoryRoot;
                existView.entryRootRt = existing as RectTransform;
                existView.canvasRectCache = canvasRect;
                existView.EnsureUnlockSubscription();
                existView.ApplyEntryLayout();
                ProtagonistLevelUpDialogView.SyncArenaUnlockFromSave();
                existView.RefreshVisibility();
                existView.entryRootRt.SetAsLastSibling();
                return existView;
            }

            var entryRt = BottomNavAttachedScreenLayout.CreateChildRect(
                mainStoryRoot, "MainStoryArenaEntry",
                new Vector2(0f, 0f), new Vector2(0f, 0f),
                EntryAnchoredPos, EntrySize);
            entryRt.pivot = new Vector2(0f, 0f);

            var entryImg = entryRt.gameObject.AddComponent<Image>();
            var entrySprite = Resources.Load<Sprite>(ResEntryIcon);
            if (entrySprite != null)
            {
                entryImg.sprite = entrySprite;
                entryImg.preserveAspect = true;
                entryImg.color = Color.white;
            }
            else
            {
                entryImg.color = new Color(0.25f, 0.22f, 0.3f, 0.9f);
                UnityEngine.Debug.LogWarning(
                    "[MainStoryArenaEntryView] 缺少入口图 Resources/" + ResEntryIcon);
            }

            entryImg.raycastTarget = true;
            var entryBtn = entryRt.gameObject.AddComponent<Button>();
            entryBtn.transition = Selectable.Transition.None;
            entryBtn.targetGraphic = entryImg;

            var labelRt = BottomNavAttachedScreenLayout.CreateChildRect(
                entryRt, "Label",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                LabelAnchoredPos, LabelSize);
            labelRt.pivot = new Vector2(0.5f, 0f);
            var labelText = labelRt.gameObject.AddComponent<Text>();
            labelText.text = "竞技场";
            labelText.font = FarmGridView.LoadBuiltinFont();
            labelText.fontSize = LabelFontSize;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.raycastTarget = false;

            var view = entryRt.gameObject.AddComponent<MainStoryArenaEntryView>();
            view.mainStoryRootRt = mainStoryRoot;
            view.entryRootRt = entryRt;
            view.canvasRectCache = canvasRect;
            entryBtn.onClick.AddListener(view.OnEntryClicked);

            view.EnsureUnlockSubscription();
            entryRt.gameObject.SetActive(false);
            view.ApplyEntryLayout();
            ProtagonistLevelUpDialogView.SyncArenaUnlockFromSave();
            view.RefreshVisibility();
            entryRt.SetAsLastSibling();
            return view;
        }

        /// <summary>升级窗关闭或解锁后，刷新所有已注册入口（避免 Show 时主线层未激活导致一直隐藏）。</summary>
        public static void RefreshAllEntries()
        {
            for (int i = ActiveEntries.Count - 1; i >= 0; i--)
            {
                if (ActiveEntries[i] == null)
                    ActiveEntries.RemoveAt(i);
                else
                    ActiveEntries[i].RefreshVisibility();
            }
        }

        private void EnsureUnlockSubscription()
        {
            if (subscribedUnlock)
                return;
            ProtagonistLevelUpDialogView.OnArenaEntryUnlocked += RefreshVisibility;
            subscribedUnlock = true;
        }

        private void ApplyEntryLayout()
        {
            if (entryRootRt == null)
                return;
            entryRootRt.anchorMin = new Vector2(0f, 0f);
            entryRootRt.anchorMax = new Vector2(0f, 0f);
            entryRootRt.pivot = new Vector2(0f, 0f);
            entryRootRt.anchoredPosition = EntryAnchoredPos;
            entryRootRt.sizeDelta = EntrySize;
            ApplyLabelLayout();
        }

        private void ApplyLabelLayout()
        {
            if (entryRootRt == null)
                return;
            var labelRt = entryRootRt.Find("Label") as RectTransform;
            if (labelRt == null)
                return;
            labelRt.anchorMin = new Vector2(0.5f, 0f);
            labelRt.anchorMax = new Vector2(0.5f, 0f);
            labelRt.pivot = new Vector2(0.5f, 0f);
            labelRt.anchoredPosition = LabelAnchoredPos;
            labelRt.sizeDelta = LabelSize;

            var labelText = labelRt.GetComponent<Text>();
            if (labelText != null)
                labelText.fontSize = LabelFontSize;
        }

        private void OnEntryClicked()
        {
            if (canvasRectCache == null)
                return;
            var panel = ArenaScreenPanelView.GetOrCreate(canvasRectCache);
            panel?.Show();
        }

        public void NotifyMainStoryVisibilityChanged() => RefreshVisibility();

        private void RefreshVisibility()
        {
            if (entryRootRt == null)
                return;
            bool show = ProtagonistLevelUpDialogView.IsArenaEntryUnlocked() &&
                        mainStoryRootRt != null &&
                        mainStoryRootRt.gameObject.activeInHierarchy;
            entryRootRt.gameObject.SetActive(show);
        }

        private void OnEnable()
        {
            if (!ActiveEntries.Contains(this))
                ActiveEntries.Add(this);
            RefreshVisibility();
        }

        private void OnDestroy()
        {
            ActiveEntries.Remove(this);
            if (subscribedUnlock)
                ProtagonistLevelUpDialogView.OnArenaEntryUnlocked -= RefreshVisibility;
        }
    }
}
