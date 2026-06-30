// SPEC §9.15.2：单人聊天面板（预制体 AppSingleChatPanel）。
// 背景 App_3；打开 1s 后显示狼宝 ZhuJue_Q，再 0.5s 显示 ZhuJue_Q_XI；点击狼宝跳转创角界面。
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class AppSingleChatPanelView : MonoBehaviour
    {
        public const string ResPrefabPath = "Prefabs/Farm/AppSingleChatPanel";
        public const string PanelObjectName = "AppSingleChatPanel";

        private const float WolfRevealDelaySeconds = 1f;
        private const float BadgeRevealDelaySeconds = 0.5f;

        private static AppSingleChatPanelView instance;

        [SerializeField] private GameObject wolfButton;
        [SerializeField] private GameObject wolfBadge;

        private RectTransform panelRt;
        private bool wired;
        private Coroutine revealRoutine;

        /// <summary>SPEC §9.15.2：点击狼宝，打开创角界面。</summary>
        public event Action OnWolfClicked;

        public bool IsShown => gameObject != null && gameObject.activeSelf;

        public static AppSingleChatPanelView GetOrCreate(RectTransform parentRect)
        {
            if (parentRect == null)
            {
                UnityEngine.Debug.LogWarning("[AppSingleChatPanelView] GetOrCreate: parentRect 为空");
                return null;
            }

            if (instance != null && instance.panelRt != null)
                return instance;

            var existing = parentRect.Find(PanelObjectName);
            if (existing != null)
            {
                var existView = existing.GetComponent<AppSingleChatPanelView>();
                if (existView == null)
                    existView = existing.gameObject.AddComponent<AppSingleChatPanelView>();
                existView.panelRt = existing as RectTransform;
                instance = existView;
                return existView;
            }

            var prefab = Resources.Load<GameObject>(ResPrefabPath);
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, parentRect, false);
            }
            else
            {
                UnityEngine.Debug.LogWarning(
                    "[AppSingleChatPanelView] 缺少预制体 Resources/" + ResPrefabPath +
                    "，使用运行时回退 UI；请在编辑器执行 Tools/PetDemo/Generate App Single Chat Panel Prefab。");
                go = AppSingleChatPanelLayout.BuildRuntime(parentRect);
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

            var view = go.GetComponent<AppSingleChatPanelView>();
            if (view == null)
                view = go.AddComponent<AppSingleChatPanelView>();
            view.panelRt = rt;
            instance = view;
            return view;
        }

        public void Show()
        {
            EnsureFieldsFromHierarchy();
            WireOnce();
            ResetWolfVisuals();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            StopRevealRoutine();
            revealRoutine = StartCoroutine(RevealWolfSequence());
        }

        public void Hide()
        {
            StopRevealRoutine();
            ResetWolfVisuals();
            gameObject.SetActive(false);
        }

        private void WireOnce()
        {
            if (wired)
                return;

            if (wolfButton != null)
            {
                var btn = wolfButton.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(OnWolfButtonClicked);
                }
            }

            wired = true;
        }

        private IEnumerator RevealWolfSequence()
        {
            yield return new WaitForSeconds(WolfRevealDelaySeconds);

            if (!IsShown)
                yield break;

            if (wolfButton != null)
                wolfButton.SetActive(true);

            yield return new WaitForSeconds(BadgeRevealDelaySeconds);

            if (!IsShown)
                yield break;

            if (wolfBadge != null)
                wolfBadge.SetActive(true);

            revealRoutine = null;
        }

        private void StopRevealRoutine()
        {
            if (revealRoutine != null)
            {
                StopCoroutine(revealRoutine);
                revealRoutine = null;
            }
        }

        private void ResetWolfVisuals()
        {
            if (wolfButton != null)
                wolfButton.SetActive(false);
            if (wolfBadge != null)
                wolfBadge.SetActive(false);
        }

        private void OnWolfButtonClicked()
        {
            OnWolfClicked?.Invoke();
        }

        private void EnsureFieldsFromHierarchy()
        {
            if (panelRt == null)
                panelRt = transform as RectTransform;

            if (wolfButton == null)
            {
                var t = transform.Find("WolfButton");
                if (t != null)
                    wolfButton = t.gameObject;
            }

            if (wolfBadge == null)
            {
                var t = transform.Find("WolfBadge");
                if (t != null)
                    wolfBadge = t.gameObject;
            }
        }
    }
}
