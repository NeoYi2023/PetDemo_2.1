// SPEC §9.8.18（v3.213；预制体优先 v3.242；性别图标 v3.255）：伴侣小屋「邀请伴侣」弹窗 + 内嵌好友选择列表。
// 优先 Resources/Prefabs/Farm/InvitePartnerModal；列表行克隆隐藏的 FriendRowTemplate。
// 编辑器：Tools/PetDemo/Generate Invite Partner Modal Prefab。
// 标题 / 介绍 / 「添加好友」(+) / 已选展示 / 确定 / 取消；
// (+) 展开好友列表：按好感度降序，仅无伴侣可选；性别用 friends_icon_man/woman。
using System;
using System.Collections.Generic;
using PetDemo.Core;
using PetDemo.Farm;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Companion
{
    [DisallowMultipleComponent]
    public sealed class InvitePartnerModalView : MonoBehaviour
    {
        public const string PrefabResourcePath = "Prefabs/Farm/InvitePartnerModal";
        public const string HostRootName = "InvitePartnerModal";
        public const string FriendRowTemplateName = "FriendRowTemplate";

        private const float RowHeight = 150f;
        private const string ResIconMan = "AirUI/friends_icon_man";
        private const string ResIconWoman = "AirUI/friends_icon_woman";

        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color PanelColor = new Color(0.13f, 0.15f, 0.21f, 0.98f);
        private static readonly Color RowColor = new Color(0.16f, 0.18f, 0.24f, 0.95f);
        private static readonly Color RowSelectedColor = new Color(0.26f, 0.45f, 0.30f, 1f);
        private static readonly Color RowDisabledColor = new Color(0.12f, 0.12f, 0.14f, 0.9f);
        private static readonly Color AvatarFallbackColor = new Color(0.3f, 0.36f, 0.46f, 1f);
        private static readonly Color GenderIconFallbackColor = new Color(0.9f, 0.78f, 0.3f, 1f);
        private static readonly Color OnlineColor = new Color(0.35f, 0.82f, 0.4f, 1f);
        private static readonly Color OfflineColor = new Color(0.6f, 0.6f, 0.65f, 1f);
        private static readonly Color ConfirmColor = new Color(0.26f, 0.55f, 0.85f, 1f);
        private static readonly Color ConfirmDisabledColor = new Color(0.3f, 0.32f, 0.36f, 1f);
        private static readonly Color CancelColor = new Color(0.4f, 0.4f, 0.46f, 1f);
        private static readonly Color AddButtonColor = new Color(0.85f, 0.45f, 0.22f, 1f);

        [SerializeField] private RectTransform rootRt;
        [SerializeField] private Image dimImage;
        [SerializeField] private Button dimButton;
        [SerializeField] private RectTransform listPanelRt;
        [SerializeField] private RectTransform contentRt;
        [SerializeField] private GameObject friendRowTemplate;
        [SerializeField] private Text selectedInfoText;
        [SerializeField] private Button addButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Image confirmButtonImage;
        [SerializeField] private Button cancelButton;

        private IPlantingService service;
        private Action<FriendProfile> onConfirm;
        private bool wired;

        private FriendProfile selectedFriend;
        private readonly List<GameObject> rowObjects = new List<GameObject>();
        private readonly Dictionary<string, Image> rowBgById = new Dictionary<string, Image>();

        public bool IsShown => rootRt != null && rootRt.gameObject.activeSelf;

        /// <summary>
        /// SPEC §9.8.18.3.1（v3.242）：预制体优先装配到 HUD；缺资源时回退骨架。
        /// </summary>
        public static InvitePartnerModalView BuildInto(
            RectTransform hudRoot, IPlantingService service, Action<FriendProfile> onConfirm)
        {
            if (hudRoot == null || service == null)
                return null;

            var existing = hudRoot.Find(HostRootName);
            if (existing != null)
            {
                var existingView = existing.GetComponent<InvitePartnerModalView>();
                if (existingView != null)
                {
                    existingView.Bind(service, onConfirm);
                    existingView.EnsureWired();
                    return existingView;
                }
            }

            InvitePartnerModalView view = null;
            var prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab != null)
            {
                var go = Instantiate(prefab, hudRoot);
                go.name = HostRootName;
                view = go.GetComponent<InvitePartnerModalView>();
                if (view == null)
                {
                    UnityEngine.Debug.LogWarning(
                        "[InvitePartnerModalView] 预制体缺少 InvitePartnerModalView，改用运行时回退。");
                    Destroy(go);
                }
            }

            if (view == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[InvitePartnerModalView] 缺少预制体 Resources/" + PrefabResourcePath +
                    "，使用运行时回退 UI；请在编辑器执行 Tools/PetDemo/Generate Invite Partner Modal Prefab。");
                view = BuildRuntimeFallback(hudRoot);
            }

            var rt = (RectTransform)view.transform;
            StretchFull(rt);
            MainHudLayerRoot.ApplySortTier(rt, MainUiSortTier.HudModal);
            view.rootRt = rt;
            view.Bind(service, onConfirm);
            view.EnsureWired();
            view.gameObject.SetActive(false);
            return view;
        }

        public void Bind(IPlantingService svc, Action<FriendProfile> confirmHandler)
        {
            service = svc;
            onConfirm = confirmHandler;
        }

        public void SetModalRefs(
            Image dimImg, Button dimBtn, RectTransform listPanel, RectTransform content,
            GameObject rowTemplate, Text selectedInfo, Button add, Button confirm, Image confirmImg,
            Button cancel)
        {
            dimImage = dimImg;
            dimButton = dimBtn;
            listPanelRt = listPanel;
            contentRt = content;
            friendRowTemplate = rowTemplate;
            selectedInfoText = selectedInfo;
            addButton = add;
            confirmButton = confirm;
            confirmButtonImage = confirmImg;
            cancelButton = cancel;
        }

        public void Show()
        {
            if (rootRt == null)
                rootRt = (RectTransform)transform;
            EnsureWired();
            ClearSelection();
            if (listPanelRt != null)
                listPanelRt.gameObject.SetActive(false);
            rootRt.SetAsLastSibling();
            rootRt.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (rootRt != null)
                rootRt.gameObject.SetActive(false);
            else
                gameObject.SetActive(false);
        }

        private void EnsureWired()
        {
            if (wired)
                return;
            ResolveRefsFromHierarchy();

            if (dimButton != null)
            {
                dimButton.onClick.RemoveListener(Hide);
                dimButton.onClick.AddListener(Hide);
            }

            if (addButton != null)
            {
                addButton.onClick.RemoveListener(ToggleList);
                addButton.onClick.AddListener(ToggleList);
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(OnConfirmClick);
                confirmButton.onClick.AddListener(OnConfirmClick);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(Hide);
                cancelButton.onClick.AddListener(Hide);
            }

            if (friendRowTemplate != null)
                friendRowTemplate.SetActive(false);

            wired = true;
        }

        private void ResolveRefsFromHierarchy()
        {
            if (rootRt == null)
                rootRt = (RectTransform)transform;

            if (dimImage == null)
            {
                var dim = transform.Find("Dim");
                if (dim != null)
                    dimImage = dim.GetComponent<Image>();
            }

            if (dimButton == null && dimImage != null)
            {
                dimButton = dimImage.GetComponent<Button>();
                if (dimButton == null)
                {
                    dimButton = dimImage.gameObject.AddComponent<Button>();
                    dimButton.transition = Selectable.Transition.None;
                    dimButton.targetGraphic = dimImage;
                }
            }

            var panel = transform.Find("Panel");
            if (listPanelRt == null && panel != null)
                listPanelRt = panel.Find("FriendListPanel") as RectTransform;

            if (contentRt == null && listPanelRt != null)
            {
                var content = listPanelRt.Find("ScrollView/Viewport/Content");
                if (content != null)
                    contentRt = content as RectTransform;
            }

            if (friendRowTemplate == null && contentRt != null)
            {
                var t = contentRt.Find(FriendRowTemplateName);
                if (t != null)
                    friendRowTemplate = t.gameObject;
            }

            if (selectedInfoText == null && panel != null)
            {
                var info = panel.Find("SelectedInfo");
                if (info != null)
                    selectedInfoText = info.GetComponent<Text>();
            }

            if (addButton == null && panel != null)
            {
                var add = panel.Find("AddButton");
                if (add != null)
                    addButton = add.GetComponent<Button>();
            }

            if (confirmButton == null && panel != null)
            {
                var confirm = panel.Find("ConfirmButton");
                if (confirm != null)
                {
                    confirmButton = confirm.GetComponent<Button>();
                    confirmButtonImage = confirm.GetComponent<Image>();
                }
            }

            if (cancelButton == null && panel != null)
            {
                var cancel = panel.Find("CancelButton");
                if (cancel != null)
                    cancelButton = cancel.GetComponent<Button>();
            }
        }

        private void ToggleList()
        {
            if (listPanelRt == null)
                return;
            bool willShow = !listPanelRt.gameObject.activeSelf;
            listPanelRt.gameObject.SetActive(willShow);
            if (willShow)
                RefreshList();
        }

        private void RefreshList()
        {
            if (contentRt == null || service == null)
                return;

            for (int i = 0; i < rowObjects.Count; i++)
            {
                if (rowObjects[i] != null)
                    Destroy(rowObjects[i]);
            }
            rowObjects.Clear();
            rowBgById.Clear();

            var sorted = SortByIntimacyDesc(service.GetFriends());
            for (int i = 0; i < sorted.Count; i++)
                BuildFriendRow(sorted[i]);
        }

        private static List<FriendProfile> SortByIntimacyDesc(IEnumerable<FriendProfile> friends)
        {
            var list = new List<FriendProfile>();
            if (friends != null)
            {
                foreach (var f in friends)
                {
                    if (f != null && !string.IsNullOrEmpty(f.id))
                        list.Add(f);
                }
            }
            list.Sort((a, b) =>
            {
                if (a.intimacy != b.intimacy)
                    return b.intimacy.CompareTo(a.intimacy);
                return string.CompareOrdinal(a.id, b.id);
            });
            return list;
        }

        private void BuildFriendRow(FriendProfile friend)
        {
            if (friend == null || contentRt == null)
                return;

            bool selectable = !friend.hasPartner;
            RectTransform row;
            Image rowImg;

            if (friendRowTemplate != null)
            {
                var go = Instantiate(friendRowTemplate, contentRt);
                go.name = "FriendRow_" + friend.id;
                go.SetActive(true);
                row = go.GetComponent<RectTransform>();
                rowImg = go.GetComponent<Image>();
                if (rowImg == null)
                    rowImg = go.AddComponent<Image>();
                ApplyFriendRowData(go.transform, friend, selectable);
            }
            else
            {
                row = BuildFriendRowFallback(contentRt, friend, selectable, out rowImg);
            }

            rowImg.color = selectable ? RowColor : RowDisabledColor;
            rowBgById[friend.id] = rowImg;

            var rowBtn = row.GetComponent<Button>();
            if (rowBtn == null)
                rowBtn = row.gameObject.AddComponent<Button>();
            rowBtn.transition = Selectable.Transition.None;
            rowBtn.targetGraphic = rowImg;
            rowBtn.interactable = selectable;
            rowBtn.onClick.RemoveAllListeners();
            if (selectable)
            {
                var captured = friend;
                rowBtn.onClick.AddListener(() => SelectFriend(captured));
            }

            rowObjects.Add(row.gameObject);
        }

        private static void ApplyFriendRowData(Transform row, FriendProfile friend, bool selectable)
        {
            var avatarImg = row.Find("Avatar")?.GetComponent<Image>();
            if (avatarImg != null)
            {
                avatarImg.raycastTarget = false;
                avatarImg.preserveAspect = true;
                var avatarSprite = !string.IsNullOrEmpty(friend.avatarResource)
                    ? Resources.Load<Sprite>(friend.avatarResource) : null;
                if (avatarSprite != null)
                {
                    avatarImg.sprite = avatarSprite;
                    avatarImg.color = selectable ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f);
                }
                else
                {
                    avatarImg.sprite = null;
                    avatarImg.color = AvatarFallbackColor;
                }
            }

            SetChildText(row, "NameText", friend.displayName);
            ApplyGenderIcon(row, friend);

            SetChildText(row, "IntimacyText", "好感度 " + friend.intimacy);
            var partnerText = SetChildText(row, "PartnerText", friend.hasPartner ? "已有伴侣" : "单身");
            if (partnerText != null)
            {
                partnerText.color = friend.hasPartner
                    ? new Color(0.9f, 0.4f, 0.4f, 1f)
                    : new Color(0.5f, 0.85f, 0.55f, 1f);
            }

            var onlineText = SetChildText(row, "OnlineText", friend.online ? "在线" : "离线");
            if (onlineText != null)
                onlineText.color = friend.online ? OnlineColor : OfflineColor;
        }

        private static void ApplyGenderIcon(Transform row, FriendProfile friend)
        {
            var genderTf = row.Find("GenderIcon") ?? row.Find("GenderText");
            if (genderTf == null)
                return;

            // Graphic 互斥：旧 GenderText 的 Text 须先卸再挂 Image。
            var legacyText = genderTf.GetComponent<Text>();
            if (legacyText != null)
                UnityEngine.Object.DestroyImmediate(legacyText);

            if (genderTf.name == "GenderText")
                genderTf.name = "GenderIcon";

            var genderImg = genderTf.GetComponent<Image>();
            if (genderImg == null)
                genderImg = genderTf.gameObject.AddComponent<Image>();

            genderImg.raycastTarget = false;
            genderImg.preserveAspect = true;
            string res = friend != null && friend.isFemale ? ResIconWoman : ResIconMan;
            var sprite = Resources.Load<Sprite>(res);
            if (sprite != null)
            {
                genderImg.sprite = sprite;
                genderImg.color = Color.white;
            }
            else
            {
                genderImg.sprite = null;
                genderImg.color = GenderIconFallbackColor;
            }
        }

        private static Text SetChildText(Transform parent, string childName, string content)
        {
            var t = parent.Find(childName)?.GetComponent<Text>();
            if (t != null)
                t.text = content ?? string.Empty;
            return t;
        }

        private void SelectFriend(FriendProfile friend)
        {
            if (friend == null || friend.hasPartner)
                return;

            if (selectedFriend != null && rowBgById.TryGetValue(selectedFriend.id, out var prevBg) && prevBg != null)
                prevBg.color = RowColor;

            selectedFriend = friend;
            if (rowBgById.TryGetValue(friend.id, out var bg) && bg != null)
                bg.color = RowSelectedColor;

            if (selectedInfoText != null)
                selectedInfoText.text = "已选：" + friend.displayName;

            if (confirmButton != null)
                confirmButton.interactable = true;
            if (confirmButtonImage != null)
                confirmButtonImage.color = ConfirmColor;
        }

        private void ClearSelection()
        {
            selectedFriend = null;
            if (selectedInfoText != null)
                selectedInfoText.text = "未选择好友";
            if (confirmButton != null)
                confirmButton.interactable = false;
            if (confirmButtonImage != null)
                confirmButtonImage.color = ConfirmDisabledColor;
        }

        private void OnConfirmClick()
        {
            if (selectedFriend == null)
                return;
            var friend = selectedFriend;
            Hide();
            onConfirm?.Invoke(friend);
        }

        private static InvitePartnerModalView BuildRuntimeFallback(RectTransform parent)
        {
            var rootGo = new GameObject(HostRootName, typeof(RectTransform));
            var root = rootGo.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            StretchFull(root);

            var view = rootGo.AddComponent<InvitePartnerModalView>();
            view.rootRt = root;
            BuildModalSkeleton(root, view);
            return view;
        }

        /// <summary>
        /// 搭建弹窗骨架；预制体生成器与运行时回退共用（SPEC §9.8.18.3.1）。
        /// </summary>
        public static void BuildModalSkeleton(RectTransform root, InvitePartnerModalView view)
        {
            if (root == null || view == null)
                return;
            view.rootRt = root;

            var dim = CreateChild(root, "Dim", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            StretchFull(dim);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = DimColor;
            dimImg.raycastTarget = true;
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.targetGraphic = dimImg;

            var panel = CreateChild(root, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 1320f));
            var panelImg = panel.gameObject.AddComponent<Image>();
            panelImg.color = PanelColor;
            panelImg.raycastTarget = true;

            CreateText(panel, "Title", "邀请伴侣", new Vector2(0.5f, 1f), new Vector2(0f, -60f),
                new Vector2(760f, 80f), 52, TextAnchor.MiddleCenter);

            CreateText(panel, "Intro",
                "在这里可以邀请一位好友成为你的伴侣。\n点击下方「+」从好友中挑选，向对方发起邀请。",
                new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(760f, 140f), 32, TextAnchor.UpperCenter);

            CreateText(panel, "AddLabel", "添加好友", new Vector2(0f, 1f), new Vector2(70f, -320f),
                new Vector2(300f, 60f), 36, TextAnchor.MiddleLeft);
            var addBtn = CreateButton(panel, "AddButton", "+",
                new Vector2(0f, 1f), new Vector2(360f, -350f), new Vector2(90f, 90f), AddButtonColor, 56, null);

            var selectedInfo = CreateText(panel, "SelectedInfo", "未选择好友",
                new Vector2(1f, 1f), new Vector2(-70f, -320f), new Vector2(420f, 60f), 34, TextAnchor.MiddleRight);

            var listPanelRt = CreateChild(panel, "FriendListPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(800f, 620f));
            var listBg = listPanelRt.gameObject.AddComponent<Image>();
            listBg.color = new Color(0.08f, 0.1f, 0.14f, 1f);
            listBg.raycastTarget = true;
            var contentRt = BuildScrollView(listPanelRt);
            var rowTemplate = BuildFriendRowTemplate(contentRt);
            listPanelRt.gameObject.SetActive(false);

            var confirmBtn = CreateButton(panel, "ConfirmButton", "确定",
                new Vector2(0.5f, 0f), new Vector2(-190f, 70f), new Vector2(300f, 100f), ConfirmDisabledColor, 44, null);
            confirmBtn.interactable = false;
            var cancelBtn = CreateButton(panel, "CancelButton", "取消",
                new Vector2(0.5f, 0f), new Vector2(190f, 70f), new Vector2(300f, 100f), CancelColor, 44, null);

            view.SetModalRefs(
                dimImg, dimBtn, listPanelRt, contentRt, rowTemplate,
                selectedInfo, addBtn, confirmBtn, confirmBtn.GetComponent<Image>(), cancelBtn);
        }

        private static RectTransform BuildScrollView(RectTransform parent)
        {
            var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.SetParent(parent, false);
            StretchFull(scrollRt);
            scrollRt.offsetMin = new Vector2(12f, 12f);
            scrollRt.offsetMax = new Vector2(-12f, -12f);
            scrollGo.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.1f, 0.6f);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.SetParent(scrollRt, false);
            StretchFull(viewportRt);
            viewportGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.SetParent(viewportRt, false);
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;

            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 10f;
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.childAlignment = TextAnchor.UpperCenter;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewportRt;
            scroll.content = contentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return contentRt;
        }

        private static GameObject BuildFriendRowTemplate(RectTransform contentRt)
        {
            var row = CreateChild(contentRt, FriendRowTemplateName, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, RowHeight));
            var rowImg = row.gameObject.AddComponent<Image>();
            rowImg.color = RowColor;
            var rowLe = row.gameObject.AddComponent<LayoutElement>();
            rowLe.preferredHeight = RowHeight;
            rowLe.minHeight = RowHeight;

            var avatar = CreateChild(row, "Avatar", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(85f, 0f), new Vector2(110f, 110f));
            var avatarImg = avatar.gameObject.AddComponent<Image>();
            avatarImg.raycastTarget = false;
            avatarImg.preserveAspect = true;
            avatarImg.color = AvatarFallbackColor;

            CreateText(row, "NameText", "昵称", new Vector2(0f, 1f), new Vector2(160f, -16f),
                new Vector2(300f, 56f), 38, TextAnchor.UpperLeft);
            CreateGenderIcon(row, new Vector2(0f, 1f), new Vector2(470f, -20f), new Vector2(48f, 48f));
            CreateText(row, "IntimacyText", "好感度 0", new Vector2(0f, 0f), new Vector2(160f, 16f),
                new Vector2(360f, 48f), 30, TextAnchor.LowerLeft);
            CreateText(row, "PartnerText", "单身", new Vector2(1f, 0f), new Vector2(-150f, 16f),
                new Vector2(180f, 48f), 30, TextAnchor.LowerRight);
            CreateText(row, "OnlineText", "在线", new Vector2(1f, 1f), new Vector2(-70f, -16f),
                new Vector2(120f, 56f), 32, TextAnchor.UpperRight);

            var rowBtn = row.gameObject.AddComponent<Button>();
            rowBtn.transition = Selectable.Transition.None;
            rowBtn.targetGraphic = rowImg;

            row.gameObject.SetActive(false);
            return row.gameObject;
        }

        private static RectTransform BuildFriendRowFallback(
            RectTransform contentRt, FriendProfile friend, bool selectable, out Image rowImg)
        {
            var row = CreateChild(contentRt, "FriendRow_" + friend.id, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, RowHeight));
            rowImg = row.gameObject.AddComponent<Image>();
            var rowLe = row.gameObject.AddComponent<LayoutElement>();
            rowLe.preferredHeight = RowHeight;
            rowLe.minHeight = RowHeight;

            var avatar = CreateChild(row, "Avatar", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(85f, 0f), new Vector2(110f, 110f));
            avatar.gameObject.AddComponent<Image>();
            CreateText(row, "NameText", "", new Vector2(0f, 1f), new Vector2(160f, -16f),
                new Vector2(300f, 56f), 38, TextAnchor.UpperLeft);
            CreateGenderIcon(row, new Vector2(0f, 1f), new Vector2(470f, -20f), new Vector2(48f, 48f));
            CreateText(row, "IntimacyText", "", new Vector2(0f, 0f), new Vector2(160f, 16f),
                new Vector2(360f, 48f), 30, TextAnchor.LowerLeft);
            CreateText(row, "PartnerText", "", new Vector2(1f, 0f), new Vector2(-150f, 16f),
                new Vector2(180f, 48f), 30, TextAnchor.LowerRight);
            CreateText(row, "OnlineText", "", new Vector2(1f, 1f), new Vector2(-70f, -16f),
                new Vector2(120f, 56f), 32, TextAnchor.UpperRight);
            ApplyFriendRowData(row, friend, selectable);
            return row;
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

        private static Image CreateGenderIcon(
            RectTransform parent, Vector2 anchorPivot, Vector2 anchoredPos, Vector2 size)
        {
            var rt = CreateChild(parent, "GenderIcon", anchorPivot, anchorPivot, new Vector2(0f, 1f),
                anchoredPos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.color = GenderIconFallbackColor;
            return img;
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
