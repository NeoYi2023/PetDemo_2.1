// SPEC §9.14.13 (v3.208)：主角升级全屏展示面板。
using System;
using System.Collections.Generic;
using PetDemo.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class RoleLevelUpPanelView : MonoBehaviour
    {
        public const string ResPrefabPath = "Prefabs/Farm/RoleLevelUpPanel";
        public const string PanelObjectName = "RoleLevelUpPanel";

        private static RoleLevelUpPanelView instance;

        [SerializeField] private Text levelUpTitle;
        [SerializeField] private Text levelNumber;
        [SerializeField] private Text levelText;
        [SerializeField] private RectTransform expFillRect;
        [SerializeField] private Image expFillImage;
        [SerializeField] private Text expText;
        [SerializeField] private RectTransform unlockContent;
        [SerializeField] private RectTransform unlockRowTemplate;
        [SerializeField] private Button okButton;

        private RectTransform panelRt;
        private float expTrackWidth;
        private readonly Queue<int> pendingLevels = new Queue<int>();
        private RoleStats displayRole;
        private readonly List<GameObject> spawnedRows = new List<GameObject>();

        public event Action OnClosed;

        public bool IsShown => gameObject != null && gameObject.activeSelf;

        public static RoleLevelUpPanelView GetOrCreate(RectTransform parentRect)
        {
            if (parentRect == null)
            {
                Debug.LogWarning("[RoleLevelUpPanelView] GetOrCreate: parentRect 为空");
                return null;
            }

            if (instance != null && instance.panelRt != null)
            {
                if (instance.panelRt.parent != parentRect)
                    instance.panelRt.SetParent(parentRect, false);
                return instance;
            }

            var existing = parentRect.Find(PanelObjectName);
            if (existing != null)
            {
                var existView = existing.GetComponent<RoleLevelUpPanelView>();
                if (existView == null)
                    existView = existing.gameObject.AddComponent<RoleLevelUpPanelView>();
                existView.panelRt = existing as RectTransform;
                existView.EnsureFieldsFromHierarchy();
                instance = existView;
                return existView;
            }

            var prefab = Resources.Load<GameObject>(ResPrefabPath);
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, parentRect, false);
                go.name = PanelObjectName;
            }
            else
            {
                go = RoleLevelUpPanelLayout.BuildRuntime(parentRect);
            }

            var view = go.GetComponent<RoleLevelUpPanelView>();
            if (view == null)
                view = go.AddComponent<RoleLevelUpPanelView>();
            view.panelRt = go.GetComponent<RectTransform>();
            view.EnsureFieldsFromHierarchy();
            instance = view;
            return view;
        }

        /// <summary>将升到的等级按升序入队并展示第一级；若已在展示则追加队列。</summary>
        public void EnqueueLevels(RoleStats role, IList<int> leveledToLevels)
        {
            if (role == null || leveledToLevels == null || leveledToLevels.Count == 0)
                return;

            displayRole = role;
            for (int i = 0; i < leveledToLevels.Count; i++)
            {
                int lv = leveledToLevels[i];
                if (lv >= 1)
                    pendingLevels.Enqueue(lv);
            }

            if (!IsShown)
                ShowNextOrHide();
        }

        public void Hide()
        {
            ClearSpawnedRows();
            pendingLevels.Clear();
            gameObject.SetActive(false);
            OnClosed?.Invoke();
        }

        private void OnDestroy()
        {
            if (okButton != null)
                okButton.onClick.RemoveListener(OnOkClicked);
            if (instance == this)
                instance = null;
        }

        private void WireOkButton()
        {
            EnsureFieldsFromHierarchy();
            if (okButton == null)
            {
                Debug.LogWarning("[RoleLevelUpPanelView] OkButton 未找到，无法绑定关闭。");
                return;
            }

            // SPEC §9.14.13 v3.210：强制不透明命中区，避免透明 Sprite 穿透导致 onClick 不触发。
            var okRt = okButton.transform as RectTransform;
            var hit = RoleLevelUpPanelLayout.EnsureOpaqueHitArea(okRt);
            if (hit != null)
                okButton.targetGraphic = hit;

            okButton.onClick.RemoveListener(OnOkClicked);
            okButton.onClick.AddListener(OnOkClicked);
            okButton.interactable = true;
            if (okButton.targetGraphic != null)
                okButton.targetGraphic.raycastTarget = true;
        }

        private void OnOkClicked()
        {
            if (pendingLevels.Count == 0)
                Hide();
            else
                ShowNextOrHide();
        }

        private void ShowNextOrHide()
        {
            EnsureFieldsFromHierarchy();
            RoleLevelUpPanelLayout.EnsureClickableLayout(panelRt);
            WireOkButton();

            if (pendingLevels.Count == 0)
            {
                Hide();
                return;
            }

            int level = pendingLevels.Dequeue();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (okButton != null)
                okButton.transform.SetAsLastSibling();

            if (levelUpTitle != null)
                levelUpTitle.text = "Level UP!";
            if (levelNumber != null)
                levelNumber.text = level.ToString();

            RefreshLevelExp(displayRole, displayLevel: level);
            RebuildUnlockRows(level);
        }

        private void RefreshLevelExp(RoleStats role, int displayLevel = -1)
        {
            int level = displayLevel > 0
                ? displayLevel
                : (role != null ? Mathf.Max(1, role.level) : 1);
            int currentExp = role != null ? Mathf.Max(0, role.currentExp) : 0;
            int expToNext = role != null && role.expToNextLevel > 0 ? role.expToNextLevel : 100;

            if (levelText != null)
                levelText.text = level.ToString();
            if (expText != null)
                expText.text = $"{currentExp}/{expToNext}";

            EnsureExpTrackWidth();
            if (expFillRect != null)
            {
                float ratio = expToNext <= 0 ? 0f : Mathf.Clamp01((float)currentExp / expToNext);
                float width = expTrackWidth * ratio;
                var sd = expFillRect.sizeDelta;
                sd.x = width;
                expFillRect.sizeDelta = sd;
            }
            if (expFillImage != null)
                expFillImage.gameObject.SetActive(currentExp > 0);
        }

        private void EnsureExpTrackWidth()
        {
            if (expFillRect == null)
                return;
            var parent = expFillRect.parent as RectTransform;
            if (parent != null && parent.rect.width > 0f)
                expTrackWidth = parent.rect.width;
            else if (expTrackWidth <= 0f)
                expTrackWidth = 800f;
        }

        private void RebuildUnlockRows(int level)
        {
            ClearSpawnedRows();
            EnsureUnlockRefs();
            if (unlockContent == null || unlockRowTemplate == null)
            {
                Debug.LogWarning("[RoleLevelUpPanelView] UnlockContent/Template 缺失，无法展示解锁项。");
                return;
            }

            unlockRowTemplate.gameObject.SetActive(false);

            var unlocks = RoleLevelUnlockCatalog.GetUnlocksForLevel(level);
            for (int i = 0; i < unlocks.Count; i++)
            {
                var cfg = unlocks[i];
                var rowGo = Instantiate(unlockRowTemplate.gameObject, unlockContent, false);
                rowGo.name = "UnlockRow_" + cfg.unlockId;
                rowGo.SetActive(true);

                var icon = rowGo.transform.Find("Icon")?.GetComponent<Image>();
                if (icon != null && !string.IsNullOrEmpty(cfg.iconPath))
                {
                    var spr = Resources.Load<Sprite>(cfg.iconPath);
                    if (spr != null)
                    {
                        icon.sprite = spr;
                        icon.color = Color.white;
                        icon.preserveAspect = true;
                    }
                }

                var title = FindText(rowGo.transform, "Title");
                if (title != null)
                {
                    title.text = cfg.title ?? string.Empty;
                    title.color = Color.white;
                }

                var desc = FindText(rowGo.transform, "Description");
                if (desc != null)
                {
                    desc.text = cfg.description ?? string.Empty;
                    desc.color = new Color(0.9f, 0.92f, 0.95f, 1f);
                }

                spawnedRows.Add(rowGo);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(unlockContent);
        }

        private static Text FindText(Transform root, string name)
        {
            if (root == null)
                return null;
            var direct = root.Find(name)?.GetComponent<Text>();
            if (direct != null)
                return direct;
            var nested = root.Find("Texts/" + name)?.GetComponent<Text>();
            if (nested != null)
                return nested;
            var deep = FindDeepChild(root, name);
            return deep != null ? deep.GetComponent<Text>() : null;
        }

        private void ClearSpawnedRows()
        {
            for (int i = 0; i < spawnedRows.Count; i++)
            {
                if (spawnedRows[i] != null)
                    Destroy(spawnedRows[i]);
            }
            spawnedRows.Clear();
        }

        private void EnsureFieldsFromHierarchy()
        {
            if (panelRt == null)
                panelRt = transform as RectTransform;

            RoleLevelUpPanelLayout.EnsureClickableLayout(panelRt);

            if (levelUpTitle == null)
            {
                var t = transform.Find("Content/UpperSection/LevelUpTitle");
                if (t != null)
                    levelUpTitle = t.GetComponent<Text>();
            }
            if (levelNumber == null)
            {
                var t = transform.Find("Content/UpperSection/LevelNumber");
                if (t != null)
                    levelNumber = t.GetComponent<Text>();
            }
            if (levelText == null)
            {
                var t = transform.Find("Content/UpperSection/LevelExpRow/LevelBadge/LevelText");
                if (t != null)
                    levelText = t.GetComponent<Text>();
            }
            if (expFillRect == null)
            {
                var t = transform.Find("Content/UpperSection/LevelExpRow/ExpBarRoot/ExpFill");
                if (t != null)
                {
                    expFillRect = t as RectTransform;
                    expFillImage = t.GetComponent<Image>();
                }
            }
            if (expText == null)
            {
                var t = transform.Find("Content/UpperSection/LevelExpRow/ExpBarRoot/ExpText");
                if (t != null)
                    expText = t.GetComponent<Text>();
            }

            EnsureUnlockRefs();

            if (okButton == null)
            {
                var t = transform.Find("OkButton");
                if (t == null)
                    t = transform.Find("Content/OkButton");
                if (t == null)
                    t = FindDeepChild(transform, "OkButton");
                if (t != null)
                    okButton = t.GetComponent<Button>();
            }

            var levelExp = transform.Find("Content/UpperSection/LevelExpRow") as RectTransform;
            if (levelExp != null)
                HomeTabPanelLayout.EnsureLevelExpRowContents(levelExp);
        }

        private void EnsureUnlockRefs()
        {
            if (unlockContent == null)
            {
                var t = transform.Find("Content/UnlockSection/UnlockContent");
                if (t == null)
                    t = transform.Find("Content/UnlockSection/UnlockScroll/Viewport/UnlockContent");
                if (t == null)
                    t = FindDeepChild(transform, "UnlockContent");
                if (t != null)
                    unlockContent = t as RectTransform;
            }

            if (unlockRowTemplate == null)
            {
                Transform t = null;
                if (unlockContent != null)
                    t = unlockContent.Find("UnlockRowTemplate");
                if (t == null)
                    t = FindDeepChild(transform, "UnlockRowTemplate");
                if (t != null)
                    unlockRowTemplate = t as RectTransform;
            }
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
                return null;
            if (root.name == name)
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeepChild(root.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
