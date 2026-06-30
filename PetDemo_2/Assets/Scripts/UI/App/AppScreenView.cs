// SPEC §9.15：APP 入口界面（预制体 AppScreenPanel）。
// 双页签：首页 App_1 / 消息 App_2；首页透明热区打开创角界面；消息行打开单人聊天面板。
using System;
using PetDemo.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class AppScreenView : MonoBehaviour
    {
        public const string ResPrefabPath = "Prefabs/Farm/AppScreenPanel";
        public const string PanelObjectName = "AppScreenPanel";

        private static AppScreenView instance;

        [SerializeField] private GameObject pageHome;
        [SerializeField] private GameObject pageMessages;
        [SerializeField] private Button tabHomeBtn;
        [SerializeField] private Button tabMessagesBtn;
        [SerializeField] private Button homeEnterHit;
        [SerializeField] private Button messageHit;

        private IPlantingService service;
        private RectTransform panelRt;
        private bool wired;
        private int activeTab;

        /// <summary>SPEC §9.15：首页透明热区点击，打开创角界面。</summary>
        public event Action OnOpenCharacterCreationRequested;

        /// <summary>SPEC §9.15：消息页点击消息行，打开单人聊天面板。</summary>
        public event Action OnOpenSingleChatRequested;

        public bool IsShown => gameObject != null && gameObject.activeSelf;

        public static AppScreenView BuildInto(RectTransform canvasRect, IPlantingService plantingService)
        {
            GetOrCreate(canvasRect);
            if (instance != null)
                instance.service = plantingService;
            return instance;
        }

        public static AppScreenView GetOrCreate(RectTransform canvasRect)
        {
            if (canvasRect == null)
            {
                UnityEngine.Debug.LogWarning("[AppScreenView] GetOrCreate: canvasRect 为空");
                return null;
            }

            if (instance != null && instance.panelRt != null)
                return instance;

            var existing = canvasRect.Find(PanelObjectName);
            if (existing != null)
            {
                var existView = existing.GetComponent<AppScreenView>();
                if (existView == null)
                    existView = existing.gameObject.AddComponent<AppScreenView>();
                existView.panelRt = existing as RectTransform;
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
                    "[AppScreenView] 缺少预制体 Resources/" + ResPrefabPath +
                    "，使用运行时回退 UI；请在编辑器执行 Tools/PetDemo/Generate App Screen Prefab。");
                go = AppScreenLayout.BuildRuntime(canvasRect);
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

            var view = go.GetComponent<AppScreenView>();
            if (view == null)
                view = go.AddComponent<AppScreenView>();
            view.panelRt = rt;
            instance = view;
            return view;
        }

        public void Show()
        {
            EnsureFieldsFromHierarchy();
            WireOnce();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            SelectTab(0);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void WireOnce()
        {
            if (wired)
                return;

            if (tabHomeBtn != null)
            {
                tabHomeBtn.onClick.RemoveAllListeners();
                tabHomeBtn.onClick.AddListener(() => SelectTab(0));
            }

            if (tabMessagesBtn != null)
            {
                tabMessagesBtn.onClick.RemoveAllListeners();
                tabMessagesBtn.onClick.AddListener(() => SelectTab(1));
            }

            if (homeEnterHit != null)
            {
                homeEnterHit.onClick.RemoveAllListeners();
                homeEnterHit.onClick.AddListener(OnHomeEnterHitClicked);
            }

            if (messageHit != null)
            {
                messageHit.onClick.RemoveAllListeners();
                messageHit.onClick.AddListener(OnMessageHitClicked);
            }

            wired = true;
        }

        private void SelectTab(int index)
        {
            activeTab = index;
            if (pageHome != null)
                pageHome.SetActive(index == 0);
            if (pageMessages != null)
                pageMessages.SetActive(index == 1);
        }

        private void OnHomeEnterHitClicked()
        {
            OnOpenCharacterCreationRequested?.Invoke();
        }

        private void OnMessageHitClicked()
        {
            OnOpenSingleChatRequested?.Invoke();
        }

        private void EnsureFieldsFromHierarchy()
        {
            if (panelRt == null)
                panelRt = transform as RectTransform;

            if (pageHome == null)
            {
                var t = transform.Find("PageHome");
                if (t != null)
                    pageHome = t.gameObject;
            }

            if (pageMessages == null)
            {
                var t = transform.Find("PageMessages");
                if (t != null)
                    pageMessages = t.gameObject;
            }

            if (tabHomeBtn == null)
            {
                var t = transform.Find("TabBar/TabHomeBtn");
                if (t != null)
                    tabHomeBtn = t.GetComponent<Button>();
            }

            if (tabMessagesBtn == null)
            {
                var t = transform.Find("TabBar/TabMessagesBtn");
                if (t != null)
                    tabMessagesBtn = t.GetComponent<Button>();
            }

            if (homeEnterHit == null)
            {
                var t = transform.Find("PageHome/HomeEnterHit");
                if (t != null)
                    homeEnterHit = t.GetComponent<Button>();
            }

            if (messageHit == null)
            {
                var t = transform.Find("PageMessages/MessageHit");
                if (t != null)
                    messageHit = t.GetComponent<Button>();
            }
        }
    }
}
