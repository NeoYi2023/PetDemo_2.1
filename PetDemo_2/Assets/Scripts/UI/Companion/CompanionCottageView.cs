// SPEC §9.8.18（v3.213；迁入公会 v3.236；已结伴进庄园 v3.238；传 partnerId v3.259）：伴侣小屋。
// 逻辑宿主挂在 GongHuiScreen 根下；入口为公会 Building_4「前往」与右上 CompanionCottageEntryButton。
// 未结伴 = 邀请弹窗（§9.8.18.3.1 预制体 InvitePartnerModal）；已结伴 = Show(partnerFriendId) 打开庄园。
// 邀请确定后延迟 2 秒在屏幕上方弹出「对方玩家接受了你的邀请」。
// 伴侣关系仅本次会话内存态，不写存档。
using System.Collections;
using PetDemo.Core;
using PetDemo.Farm;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Companion
{
    [DisallowMultipleComponent]
    public sealed class CompanionCottageView : MonoBehaviour
    {
        private const float AcceptDelaySeconds = 2f;

        // SPEC §9.8.18 / §9.8.9.14（v3.236）：公会右上图标入口。
        public const string ResHudEntryIcon = "AirUI/CompanionCabin_Icon";
        public const string EntryButtonName = "CompanionCottageEntryButton";

        private static readonly Color PopupColor = new Color(0.14f, 0.16f, 0.22f, 0.98f);
        private static readonly Color ConfirmColor = new Color(0.26f, 0.55f, 0.85f, 1f);
        private static readonly Color GiveUpColor = new Color(0.4f, 0.4f, 0.46f, 1f);
        private static readonly Color ToastColor = new Color(0f, 0f, 0f, 0.82f);
        private static readonly Color EntryFallbackColor = new Color(0.85f, 0.45f, 0.62f, 0.95f);

        private RectTransform hudRoot;
        private IPlantingService service;

        private Button entryButton;

        private InvitePartnerModalView inviteModal;
        private CompanionManorScreenView manorScreen;

        private RectTransform acceptPopupRt;
        private Text acceptPopupText;

        private RectTransform toastRt;
        private Text toastText;
        private Coroutine toastRoutine;
        private Coroutine acceptRoutine;

        // 会话内存态（无持久化）。
        private string partnerFriendId;
        private FriendProfile pendingFriend;

        public bool HasPartner => !string.IsNullOrEmpty(partnerFriendId);

        /// <summary>
        /// SPEC §9.8.18（v3.236）：在公会屏根下创建逻辑宿主，邀请弹窗挂到 HUD。
        /// </summary>
        public static CompanionCottageView BuildInto(
            RectTransform gongHuiRoot, RectTransform hudRoot, IPlantingService service)
        {
            if (gongHuiRoot == null || hudRoot == null)
            {
                UnityEngine.Debug.LogWarning("[CompanionCottageView] gongHuiRoot / hudRoot 为空，跳过伴侣小屋构建。");
                return null;
            }

            var existing = gongHuiRoot.Find("CompanionCottageHost");
            if (existing != null)
            {
                var existingView = existing.GetComponent<CompanionCottageView>();
                if (existingView != null)
                    return existingView;
            }

            var go = new GameObject("CompanionCottageHost", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(gongHuiRoot, false);
            StretchFull(rt);
            // 逻辑宿主不拦截射线。
            var canvasGroup = go.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            var view = go.AddComponent<CompanionCottageView>();
            view.hudRoot = hudRoot;
            view.service = service;
            rt.SetAsFirstSibling();

            if (service != null)
                view.inviteModal = InvitePartnerModalView.BuildInto(hudRoot, service, view.StartAcceptFlow);

            return view;
        }

        /// <summary>SPEC §9.8.19（v3.238）：注入伴侣庄园场景，供已结伴入口打开。</summary>
        public void BindManor(CompanionManorScreenView manor)
        {
            manorScreen = manor;
        }

        /// <summary>
        /// SPEC §9.8.18：伴侣小屋入口点击行为（Building_4「前往」与右上入口共用）。
        /// 未结伴→打开邀请弹窗；已结伴→打开 §9.8.19 庄园场景。
        /// </summary>
        public void TriggerEntry()
        {
            if (HasPartner)
            {
                if (manorScreen != null)
                {
                    UnityEngine.Debug.Log("[CompanionCottageView] 打开伴侣庄园。伴侣=" + partnerFriendId);
                    manorScreen.Show(partnerFriendId);
                }
                else
                {
                    UnityEngine.Debug.LogWarning(
                        "[CompanionCottageView] 未注入 CompanionManorScreenView，无法打开庄园。");
                    ShowToast("庄园场景未就绪");
                }
                return;
            }

            if (inviteModal != null)
                inviteModal.Show();
            else
                UnityEngine.Debug.LogWarning("[CompanionCottageView] 邀请弹窗未初始化（service 为空？）。");
        }

        /// <summary>
        /// SPEC §9.8.18 / §9.8.9.14（v3.236）：在 TopRightWorkflowActions 下挂 CompanionCottageEntryButton
        ///（WfZhuangYuan 正下方，i=4），点击复用 <see cref="TriggerEntry"/>。
        /// </summary>
        public void BuildHudEntry(RectTransform gongHuiRoot)
        {
            if (gongHuiRoot == null)
                return;

            var actionsRt = GongHuiScreenLayout.EnsureTopRightWorkflowActions(gongHuiRoot);
            if (actionsRt == null)
                return;

            var entryRt = actionsRt.Find(EntryButtonName) as RectTransform;
            if (entryRt == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[CompanionCottageView] 未找到 " + EntryButtonName + "，EnsureTopRightWorkflowActions 应已创建。");
                return;
            }

            var entryImage = entryRt.GetComponent<Image>();
            if (entryImage == null)
                entryImage = entryRt.gameObject.AddComponent<Image>();

            var entrySprite = Resources.Load<Sprite>(ResHudEntryIcon);
            if (entrySprite != null)
            {
                entryImage.sprite = entrySprite;
                entryImage.preserveAspect = true;
                entryImage.color = Color.white;
            }
            else if (entryImage.sprite == null)
            {
                entryImage.color = EntryFallbackColor;
                if (entryRt.Find("Label") == null)
                {
                    var labelRt = CreateChild(entryRt, "Label", Vector2.zero, Vector2.one,
                        new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                    StretchFull(labelRt);
                    var label = labelRt.gameObject.AddComponent<Text>();
                    label.text = "伴侣";
                    label.font = FarmGridView.LoadBuiltinFont();
                    label.fontSize = 36;
                    label.alignment = TextAnchor.MiddleCenter;
                    label.color = Color.white;
                    label.raycastTarget = false;
                }
                UnityEngine.Debug.LogWarning(
                    "[CompanionCottageView] 缺少图标 Resources/" + ResHudEntryIcon + "，使用文字按钮回退。");
            }
            entryImage.raycastTarget = true;

            entryButton = entryRt.GetComponent<Button>();
            if (entryButton == null)
            {
                entryButton = entryRt.gameObject.AddComponent<Button>();
                entryButton.transition = Selectable.Transition.None;
                entryButton.targetGraphic = entryImage;
            }

            entryButton.onClick.RemoveListener(TriggerEntry);
            entryButton.onClick.AddListener(TriggerEntry);
        }

        // ---- 2 秒模拟接受流程 ----

        private void StartAcceptFlow(FriendProfile friend)
        {
            if (friend == null)
                return;
            pendingFriend = friend;
            if (acceptRoutine != null)
                StopCoroutine(acceptRoutine);
            acceptRoutine = StartCoroutine(AcceptAfterDelay());
        }

        private IEnumerator AcceptAfterDelay()
        {
            yield return new WaitForSeconds(AcceptDelaySeconds);
            acceptRoutine = null;
            ShowAcceptPopup();
        }

        private void ShowAcceptPopup()
        {
            EnsureAcceptPopup();
            if (acceptPopupText != null && pendingFriend != null)
                acceptPopupText.text = pendingFriend.displayName + " 接受了你的邀请\n对方玩家接受了你的邀请";
            acceptPopupRt.SetAsLastSibling();
            acceptPopupRt.gameObject.SetActive(true);
        }

        private void EnsureAcceptPopup()
        {
            if (acceptPopupRt != null)
                return;

            var rootGo = new GameObject("CompanionAcceptPopup", typeof(RectTransform));
            var root = rootGo.GetComponent<RectTransform>();
            root.SetParent(hudRoot, false);
            StretchFull(root);
            MainHudLayerRoot.ApplySortTier(root, MainUiSortTier.HudPopup);

            var panel = CreateChild(root, "Panel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(880f, 380f));
            var panelImg = panel.gameObject.AddComponent<Image>();
            panelImg.color = PopupColor;
            panelImg.raycastTarget = true;

            acceptPopupText = CreateText(panel, "Message", "对方玩家接受了你的邀请",
                new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(800f, 180f), 40, TextAnchor.UpperCenter);

            CreateButton(panel, "ConfirmButton", "确定",
                new Vector2(0.5f, 0f), new Vector2(-180f, 60f), new Vector2(280f, 100f), ConfirmColor, 42, OnAcceptConfirm);
            CreateButton(panel, "GiveUpButton", "放弃",
                new Vector2(0.5f, 0f), new Vector2(180f, 60f), new Vector2(280f, 100f), GiveUpColor, 42, OnAcceptGiveUp);

            acceptPopupRt = root;
            root.gameObject.SetActive(false);
        }

        private void OnAcceptConfirm()
        {
            if (pendingFriend != null)
            {
                partnerFriendId = pendingFriend.id;
                UnityEngine.Debug.Log("[CompanionCottageView] 已与好友结为伴侣：" + pendingFriend.displayName);
            }
            pendingFriend = null;
            HideAcceptPopup();
        }

        private void OnAcceptGiveUp()
        {
            pendingFriend = null;
            HideAcceptPopup();
        }

        private void HideAcceptPopup()
        {
            if (acceptPopupRt != null)
                acceptPopupRt.gameObject.SetActive(false);
        }

        // ---- 顶部一次性提示 ----

        private void ShowToast(string message)
        {
            EnsureToast();
            if (toastText != null)
                toastText.text = message ?? string.Empty;
            toastRt.SetAsLastSibling();
            toastRt.gameObject.SetActive(true);
            if (toastRoutine != null)
                StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(HideToastAfter(1.5f));
        }

        private IEnumerator HideToastAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            toastRoutine = null;
            if (toastRt != null)
                toastRt.gameObject.SetActive(false);
        }

        private void EnsureToast()
        {
            if (toastRt != null)
                return;

            var rootGo = new GameObject("CompanionToast", typeof(RectTransform));
            var root = rootGo.GetComponent<RectTransform>();
            root.SetParent(hudRoot, false);
            StretchFull(root);
            MainHudLayerRoot.ApplySortTier(root, MainUiSortTier.HudPopup);

            var panel = CreateChild(root, "Panel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(640f, 110f));
            var panelImg = panel.gameObject.AddComponent<Image>();
            panelImg.color = ToastColor;
            panelImg.raycastTarget = false;

            toastText = CreateText(panel, "Text", "", Vector2.zero, Vector2.zero, Vector2.zero, 36, TextAnchor.MiddleCenter);
            var txtRt = toastText.rectTransform;
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;

            toastRt = root;
            root.gameObject.SetActive(false);
        }

        // 公会层隐藏时，收起弹窗/庄园并中止挂起申请，避免残留遮挡。
        private void OnDisable()
        {
            if (acceptRoutine != null)
            {
                StopCoroutine(acceptRoutine);
                acceptRoutine = null;
            }
            pendingFriend = null;
            if (inviteModal != null)
                inviteModal.Hide();
            HideAcceptPopup();
            if (toastRt != null)
                toastRt.gameObject.SetActive(false);
            if (manorScreen != null)
                manorScreen.Hide();
        }

        private void OnDestroy()
        {
            if (entryButton != null)
                entryButton.onClick.RemoveListener(TriggerEntry);
        }

        // ---- 构建工具 ----

        private static RectTransform CreateChild(
            RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return rt;
        }

        private static Button CreateButton(
            RectTransform parent, string name, string label,
            Vector2 anchorPivot, Vector2 anchoredPos, Vector2 size, Color color, int fontSize,
            UnityEngine.Events.UnityAction onClick)
        {
            var rt = CreateChild(parent, name, anchorPivot, anchorPivot, anchorPivot, anchoredPos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;
            if (onClick != null)
                btn.onClick.AddListener(onClick);

            var labelRt = CreateChild(rt, "Label", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            StretchFull(labelRt);
            var txt = labelRt.gameObject.AddComponent<Text>();
            txt.text = label;
            txt.font = FarmGridView.LoadBuiltinFont();
            txt.fontSize = fontSize;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.raycastTarget = false;
            return btn;
        }

        private static Text CreateText(
            RectTransform parent, string name, string content,
            Vector2 anchorPivot, Vector2 anchoredPos, Vector2 size, int fontSize, TextAnchor align)
        {
            var rt = CreateChild(parent, name, anchorPivot, anchorPivot, anchorPivot, anchoredPos, size);
            var txt = rt.gameObject.AddComponent<Text>();
            txt.text = content;
            txt.font = FarmGridView.LoadBuiltinFont();
            txt.fontSize = fontSize;
            txt.alignment = align;
            txt.color = Color.white;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
