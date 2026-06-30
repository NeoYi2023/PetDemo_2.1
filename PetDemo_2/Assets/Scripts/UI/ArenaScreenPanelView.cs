// SPEC §9.8.16：主线竞技场全屏界面（预制体 JingJi-2 + 挑战/返回）。
using System;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class ArenaScreenPanelView : MonoBehaviour
    {
        public const string ResPrefabPath = "Prefabs/Farm/ArenaScreenPanel";
        public const string PanelObjectName = "ArenaScreenPanel";

        private static ArenaScreenPanelView instance;

        [SerializeField] private Button challengeButton;
        [SerializeField] private Button backButton;

        private RectTransform panelRt;
        private RectTransform canvasRectCache;
        private BottomNavBarView bottomNav;
        private bool wired;
        private bool navSubscribed;

        public bool IsShown => gameObject != null && gameObject.activeSelf;

        public static ArenaScreenPanelView BuildInto(RectTransform canvasRect, BottomNavBarView barView)
        {
            GetOrCreate(canvasRect);
            if (instance != null)
            {
                instance.bottomNav = barView;
                instance.EnsureNavSubscription();
            }
            return instance;
        }

        public static ArenaScreenPanelView GetOrCreate(RectTransform canvasRect)
        {
            if (canvasRect == null)
            {
                UnityEngine.Debug.LogWarning("[ArenaScreenPanelView] GetOrCreate: canvasRect 为空");
                return null;
            }

            if (instance != null && instance.panelRt != null)
            {
                instance.canvasRectCache = canvasRect;
                return instance;
            }

            var existing = canvasRect.Find(PanelObjectName);
            if (existing != null)
            {
                var existView = existing.GetComponent<ArenaScreenPanelView>();
                if (existView == null)
                    existView = existing.gameObject.AddComponent<ArenaScreenPanelView>();
                existView.panelRt = existing as RectTransform;
                existView.canvasRectCache = canvasRect;
                instance = existView;
                return existView;
            }

            var prefab = Resources.Load<GameObject>(ResPrefabPath);
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, canvasRect, false);
            }
            else
            {
                UnityEngine.Debug.LogWarning(
                    "[ArenaScreenPanelView] 缺少预制体 Resources/" + ResPrefabPath +
                    "，使用运行时回退 UI；请在编辑器执行 Tools/PetDemo/Generate Arena Screen Panel Prefab。");
                go = BuildRuntimeFallback(canvasRect);
                if (go == null)
                    return null;
            }

            go.name = PanelObjectName;
            go.SetActive(false);
            var rt = go.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            var view = go.GetComponent<ArenaScreenPanelView>();
            if (view == null)
                view = go.AddComponent<ArenaScreenPanelView>();
            view.panelRt = rt;
            view.canvasRectCache = canvasRect;
            instance = view;
            return view;
        }

        private void EnsureNavSubscription()
        {
            if (navSubscribed || bottomNav == null)
                return;
            bottomNav.OnOpenChanged += OnBottomNavOpenChanged;
            navSubscribed = true;
        }

        private void OnBottomNavOpenChanged(int index, string key)
        {
            bool isMainStory = !string.IsNullOrEmpty(key) &&
                               string.Equals(key, MainStoryLineScreenView.ZhuXianNavKey, StringComparison.Ordinal);
            if (!isMainStory && IsShown)
                Hide();
        }

        public void Show()
        {
            EnsureFieldsFromHierarchy();
            WireButtonsOnce();

            gameObject.SetActive(true);
            PlaceAboveBottomNav();
        }

        public void Hide()
        {
            ArenaChallengeOverlayView.HideIfAny();
            gameObject.SetActive(false);
        }

        /// <summary>叠在 <see cref="BottomNavBar"/> 之上（SPEC §9.8.16）。</summary>
        private void PlaceAboveBottomNav()
        {
            if (bottomNav == null)
            {
                transform.SetAsLastSibling();
                return;
            }

            var barRt = bottomNav.transform as RectTransform;
            if (barRt != null && barRt.parent == transform.parent)
                transform.SetSiblingIndex(barRt.GetSiblingIndex() + 1);
            else
                transform.SetAsLastSibling();
        }

        private void WireButtonsOnce()
        {
            if (wired)
                return;

            if (challengeButton != null)
            {
                challengeButton.onClick.RemoveAllListeners();
                challengeButton.onClick.AddListener(OnChallengeClicked);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(Hide);
            }

            wired = true;
        }

        private void OnChallengeClicked()
        {
            if (canvasRectCache == null)
                canvasRectCache = transform.parent as RectTransform;
            var overlay = ArenaChallengeOverlayView.GetOrCreate(canvasRectCache);
            overlay?.Show();
        }

        private void EnsureFieldsFromHierarchy()
        {
            if (challengeButton == null)
                challengeButton = FindDescendantButton("ChallengeButton");
            if (backButton == null)
                backButton = FindDescendantButton("BackButton");
        }

        private Button FindDescendantButton(string nodeName)
        {
            var t = transform.Find(nodeName);
            if (t == null)
                t = FindDescendantByName(transform, nodeName);
            return t != null ? t.GetComponent<Button>() : null;
        }

        private static Transform FindDescendantByName(Transform root, string name)
        {
            if (root == null)
                return null;
            if (string.Equals(root.name, name, StringComparison.Ordinal))
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDescendantByName(root.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static GameObject BuildRuntimeFallback(RectTransform canvasRect)
        {
            const string ResBackground = "AirUI/JingJi-2";

            var rootGo = new GameObject(PanelObjectName, typeof(RectTransform));
            var rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.SetParent(canvasRect, false);
            BottomNavAttachedScreenLayout.StretchFull(rootRt);

            var view = rootGo.AddComponent<ArenaScreenPanelView>();
            BottomNavAttachedScreenLayout.AddStretchedResourcesBackground(
                rootRt, ResBackground, nameof(ArenaScreenPanelView));

            view.challengeButton = CreateRuntimeButton(rootRt, "ChallengeButton", "挑战",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 110f),
                new Color(0.85f, 0.35f, 0.2f, 1f));
            view.backButton = CreateRuntimeButton(rootRt, "BackButton", "返回",
                new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(120f, 80f),
                new Color(0.35f, 0.35f, 0.4f, 0.95f));

            return rootGo;
        }

        private static Button CreateRuntimeButton(
            RectTransform parent,
            string name,
            string label,
            Vector2 anchorPivot,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color)
        {
            var btnRt = BottomNavAttachedScreenLayout.CreateChildRect(
                parent, name, anchorPivot, anchorPivot, anchoredPosition, size);
            btnRt.pivot = anchorPivot;

            var img = btnRt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            var btn = btnRt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;

            var labelRt = BottomNavAttachedScreenLayout.CreateChildRect(
                btnRt, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BottomNavAttachedScreenLayout.StretchFull(labelRt);
            var txt = labelRt.gameObject.AddComponent<Text>();
            txt.text = label;
            txt.font = FarmGridView.LoadBuiltinFont();
            txt.fontSize = 36;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.raycastTarget = false;

            return btn;
        }

        private void OnDestroy()
        {
            if (bottomNav != null && navSubscribed)
                bottomNav.OnOpenChanged -= OnBottomNavOpenChanged;
            if (instance == this)
                instance = null;
        }
    }
}
