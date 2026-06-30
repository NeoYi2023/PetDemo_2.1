// SPEC §13：Play 启动时的三存档槽选择界面。
using System;
using PetDemo.Save;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class SaveSlotPickerView : MonoBehaviour
    {
        private const float EnterButtonWidth = 560f;
        private const float DeleteButtonWidth = 160f;
        private const float RowHeight = 88f;
        private const float RowSpacingY = 120f;

        private RectTransform rootRt;
        private RectTransform rowsRoot;
        private RectTransform confirmModal;
        private Action<int> onSlotChosen;
        private int pendingDeleteSlot = -1;

        public static SaveSlotPickerView Show(Transform host, Action<int> onChosen)
        {
            var go = new GameObject("SaveSlotPicker", typeof(RectTransform));
            go.transform.SetParent(host, false);
            var view = go.AddComponent<SaveSlotPickerView>();
            view.onSlotChosen = onChosen;
            view.Build();
            return view;
        }

        public void DestroyPicker()
        {
            if (gameObject != null)
                Destroy(gameObject);
        }

        private void Build()
        {
            var canvasGo = new GameObject("SaveSlotCanvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = MainUiSortTier.HudPopup;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            rootRt = canvasGo.GetComponent<RectTransform>();
            StretchFull(rootRt);

            var dim = CreateChildImage(rootRt, "Dim", new Color(0.05f, 0.08f, 0.14f, 0.92f));
            StretchFull(dim.rectTransform);

            var panel = CreateChildRect(rootRt, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(900f, 720f));

            CreateTitle(panel, "选择存档");

            rowsRoot = CreateChildRect(panel, "Rows", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(860f, 480f));

            BuildConfirmModal(rootRt);
            RefreshRows();
        }

        private void RefreshRows()
        {
            if (rowsRoot == null)
                return;

            for (int i = rowsRoot.childCount - 1; i >= 0; i--)
                Destroy(rowsRoot.GetChild(i).gameObject);

            float startY = RowSpacingY;
            for (int slot = 0; slot < GameBootContext.SaveSlotCount; slot++)
            {
                float y = startY - slot * RowSpacingY;
                BuildRow(slot, y);
            }
        }

        private void BuildRow(int slotIndex, float posY)
        {
            var info = GameSaveRepository.GetSlotDisplayInfo(slotIndex);
            var rowRt = CreateChildRect(rowsRoot, "Row_" + slotIndex,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, posY), new Vector2(860f, RowHeight));

            float halfGap = 12f;
            float enterX = -(DeleteButtonWidth + halfGap) * 0.5f;
            float deleteX = (EnterButtonWidth + halfGap) * 0.5f;

            var enterBtn = CreateRowButton(rowRt, "Enter", info.enterLabel,
                new Vector2(enterX, 0f), new Vector2(EnterButtonWidth, RowHeight),
                new Color(0.22f, 0.52f, 0.88f, 1f), () => onSlotChosen?.Invoke(slotIndex));

            var deleteBtn = CreateRowButton(rowRt, "Delete", "删除",
                new Vector2(deleteX, 0f), new Vector2(DeleteButtonWidth, RowHeight),
                new Color(0.78f, 0.28f, 0.28f, 1f), () => ShowDeleteConfirm(slotIndex));

            if (!info.hasSave)
            {
                // 空槽仍可进入（新游戏）；删除对空槽无操作但仍可点（会弹确认后无文件）
            }
        }

        private void ShowDeleteConfirm(int slotIndex)
        {
            pendingDeleteSlot = slotIndex;
            if (confirmModal != null)
                confirmModal.gameObject.SetActive(true);
        }

        private void HideDeleteConfirm()
        {
            pendingDeleteSlot = -1;
            if (confirmModal != null)
                confirmModal.gameObject.SetActive(false);
        }

        private void ConfirmDelete()
        {
            if (pendingDeleteSlot >= 0)
                GameSaveRepository.Delete(pendingDeleteSlot);
            HideDeleteConfirm();
            RefreshRows();
        }

        private void BuildConfirmModal(RectTransform parent)
        {
            confirmModal = CreateChildRect(parent, "DeleteConfirmModal",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            confirmModal.gameObject.SetActive(false);

            var dim = CreateChildImage(confirmModal, "Dim", new Color(0f, 0f, 0f, 0.65f));
            StretchFull(dim.rectTransform);

            var box = CreateChildRect(confirmModal, "Box",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(640f, 280f));
            var boxImg = box.gameObject.AddComponent<Image>();
            boxImg.color = new Color(0.14f, 0.18f, 0.26f, 0.98f);

            var msgRt = CreateChildRect(box, "Message",
                new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f),
                Vector2.zero, new Vector2(580f, 100f));
            var msgText = msgRt.gameObject.AddComponent<Text>();
            msgText.font = FarmGridView.LoadBuiltinFont();
            msgText.fontSize = 34;
            msgText.alignment = TextAnchor.MiddleCenter;
            msgText.color = Color.white;
            msgText.text = "确定删除存档？";
            msgText.raycastTarget = false;

            CreateRowButton(box, "Cancel", "取消",
                new Vector2(-140f, -60f), new Vector2(200f, 72f),
                new Color(0.35f, 0.38f, 0.45f, 1f), HideDeleteConfirm);

            CreateRowButton(box, "Ok", "确定",
                new Vector2(140f, -60f), new Vector2(200f, 72f),
                new Color(0.78f, 0.28f, 0.28f, 1f), ConfirmDelete);

            // 动态更新确认文案
            confirmModal.gameObject.AddComponent<SaveSlotDeleteConfirmBinder>()
                .Init(msgText, () => pendingDeleteSlot);
        }

        private static void CreateTitle(RectTransform parent, string title)
        {
            var titleRt = CreateChildRect(parent, "Title",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -48f), new Vector2(800f, 72f));
            var text = titleRt.gameObject.AddComponent<Text>();
            text.font = FarmGridView.LoadBuiltinFont();
            text.fontSize = 48;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = title;
            text.raycastTarget = false;
        }

        private static Button CreateRowButton(
            RectTransform parent,
            string name,
            string label,
            Vector2 pos,
            Vector2 size,
            Color bgColor,
            Action onClick)
        {
            var btnRt = CreateChildRect(parent, name,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            var img = btnRt.gameObject.AddComponent<Image>();
            img.color = bgColor;

            var labelRt = CreateChildRect(btnRt, "Label",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var labelText = labelRt.gameObject.AddComponent<Text>();
            labelText.font = FarmGridView.LoadBuiltinFont();
            labelText.fontSize = 32;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.text = label;
            labelText.raycastTarget = false;

            var btn = btnRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());
            return btn;
        }

        private static Image CreateChildImage(RectTransform parent, string name, Color color)
        {
            var rt = CreateChildRect(parent, name,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static RectTransform CreateChildRect(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            return rt;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>更新删除确认框文案。</summary>
        private sealed class SaveSlotDeleteConfirmBinder : MonoBehaviour
        {
            private Text messageText;
            private Func<int> getPendingSlot;

            public void Init(Text text, Func<int> getSlot)
            {
                messageText = text;
                getPendingSlot = getSlot;
            }

            private void OnEnable()
            {
                if (messageText == null || getPendingSlot == null)
                    return;
                int slot = getPendingSlot();
                int n = slot >= 0 ? slot + 1 : 0;
                messageText.text = n > 0
                    ? "确定删除存档 " + n + "？"
                    : "确定删除存档？";
            }
        }
    }
}
