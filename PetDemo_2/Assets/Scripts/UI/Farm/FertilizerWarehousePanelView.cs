// SPEC §9.7 / §3.12：肥料仓库预制体 UI — 槽位列表、详情区、双按钮关闭弹窗。
using System.Collections.Generic;
using PetDemo.Core;
using PetDemo.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Farm
{
    public class FertilizerWarehousePanelView : MonoBehaviour
    {
        private static readonly Color SlotNormalTint = Color.white;
        private static readonly Color SlotSelectedTint = new Color(1f, 0.82f, 0.31f, 1f);

        [SerializeField] private Image detailBigIcon;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Button btnApplyAll;
        [SerializeField] private Button btnApplyOne;
        [SerializeField] private Text emptyHint;
        [SerializeField] private RectTransform slotStrip;
        [SerializeField] private GameObject slotTemplate;

        private IPlantingService service;
        private RectTransform modalRoot;
        private readonly List<GameObject> slotPool = new List<GameObject>();
        private bool inRebuild;

        public void Initialize(IPlantingService plantingService, RectTransform fertilizeModalRoot)
        {
            service = plantingService;
            modalRoot = fertilizeModalRoot;

            if (btnApplyAll != null)
                btnApplyAll.onClick.AddListener(OnApplyAllClicked);
            if (btnApplyOne != null)
                btnApplyOne.onClick.AddListener(CloseModal);

            if (service != null)
                service.OnFertilizerBagChanged += OnBagChanged;
            Rebuild();
        }

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnDestroy()
        {
            if (service != null)
                service.OnFertilizerBagChanged -= OnBagChanged;
            if (btnApplyAll != null)
                btnApplyAll.onClick.RemoveListener(OnApplyAllClicked);
            if (btnApplyOne != null)
                btnApplyOne.onClick.RemoveListener(CloseModal);
        }

        private void OnBagChanged()
        {
            Rebuild();
        }

        private void CloseModal()
        {
            if (modalRoot != null)
                modalRoot.gameObject.SetActive(false);
        }

        // SPEC §9.7（v3.22）：「全部施肥」点击行为——先关闭弹窗回到主场景，
        // 再调用 IPlantingService.ApplyFertilizerToAllAwaitingTiles() 批量施肥；
        // 关窗顺序在前，使逐田触发的 OnFertilizeApplied 直接驱动主界面 §9.5 主角动画。
        private void OnApplyAllClicked()
        {
            CloseModal();
            if (service != null)
                service.ApplyFertilizerToAllAwaitingTiles();
        }

        private void Rebuild()
        {
            if (service == null)
                return;
            if (inRebuild)
                return;
            inRebuild = true;
            try
            {

            if (descriptionText != null)
                descriptionText.font = FarmGridView.LoadBuiltinFont();
            if (emptyHint != null)
                emptyHint.font = FarmGridView.LoadBuiltinFont();

            ClearSlots();

            var bag = service.GetFertilizerBag();
            var validIds = new List<string>();
            if (bag != null && bag.stacks != null)
            {
                for (int i = 0; i < bag.stacks.Count; i++)
                {
                    var s = bag.stacks[i];
                    if (s != null && !string.IsNullOrEmpty(s.fertilizerId) && s.count > 0)
                        validIds.Add(s.fertilizerId);
                }
            }
            bool isEmpty = validIds.Count == 0;
            if (emptyHint != null)
                emptyHint.gameObject.SetActive(isEmpty);
            if (detailBigIcon != null)
                detailBigIcon.gameObject.SetActive(!isEmpty);
            if (descriptionText != null)
                descriptionText.gameObject.SetActive(!isEmpty);
            if (btnApplyAll != null)
                btnApplyAll.gameObject.SetActive(!isEmpty);
            if (btnApplyOne != null)
                btnApplyOne.gameObject.SetActive(!isEmpty);

            if (isEmpty)
            {
                if (detailBigIcon != null)
                {
                    detailBigIcon.sprite = null;
                    detailBigIcon.enabled = false;
                }
                if (descriptionText != null)
                    descriptionText.text = string.Empty;
                return;
            }

            EnsureDefaultSelection(validIds);

            for (int i = 0; i < validIds.Count; i++)
            {
                string fid = validIds[i];
                var stack = FindStack(bag, fid);
                if (stack == null)
                    continue;
                var slotGo = CreateSlot(fid, stack.count);
                if (slotGo != null)
                    slotPool.Add(slotGo);
            }
            RefreshDetail();
            }
            finally
            {
                inRebuild = false;
            }
        }

        private void EnsureDefaultSelection(List<string> validIds)
        {
            if (validIds == null || validIds.Count == 0)
                return;
            string active = service.GetActiveFertilizer();
            if (string.IsNullOrEmpty(active))
            {
                service.SelectActiveFertilizer(validIds[0]);
                return;
            }
            bool ok = false;
            var bag = service.GetFertilizerBag();
            if (bag != null && bag.stacks != null)
            {
                for (int i = 0; i < bag.stacks.Count; i++)
                {
                    var s = bag.stacks[i];
                    if (s != null && s.fertilizerId == active && s.count > 0)
                    {
                        ok = true;
                        break;
                    }
                }
            }
            if (!ok)
                service.SelectActiveFertilizer(validIds[0]);
        }

        private static FertilizerStack FindStack(PlayerFertilizerBag bag, string fertilizerId)
        {
            if (bag?.stacks == null)
                return null;
            for (int i = 0; i < bag.stacks.Count; i++)
            {
                if (bag.stacks[i] != null && bag.stacks[i].fertilizerId == fertilizerId)
                    return bag.stacks[i];
            }
            return null;
        }

        private GameObject CreateSlot(string fertilizerId, int count)
        {
            if (slotTemplate == null || slotStrip == null)
                return null;

            var go = Instantiate(slotTemplate, slotStrip, false);
            go.name = "Slot_" + fertilizerId;
            go.SetActive(true);

            var btn = go.GetComponent<Button>();
            if (btn != null)
            {
                string cap = fertilizerId;
                btn.onClick.AddListener(() =>
                {
                    service.SelectActiveFertilizer(cap);
                    RefreshDetail();
                    HighlightSlots();
                });
            }

            var iconTr = go.transform.Find("Icon");
            if (iconTr != null)
            {
                var iconRt = iconTr as RectTransform;
                if (iconRt != null)
                {
                    if (iconRt.sizeDelta.x <= 0.01f || iconRt.sizeDelta.y <= 0.01f)
                    {
                        iconRt.sizeDelta = new Vector2(120f, 120f);
                    }
                }
                var iconImg = iconTr.GetComponent<Image>();
                if (iconImg != null)
                {
                    var ft = service.GetFertilizerType(fertilizerId);
                    string res = ft != null ? ft.iconResource : null;
                    if (!string.IsNullOrEmpty(res))
                    {
                        iconImg.sprite = Resources.Load<Sprite>(res);
                        iconImg.enabled = iconImg.sprite != null;
                    }
                    else
                    {
                        iconImg.sprite = null;
                        iconImg.enabled = false;
                    }
                }
            }

            var countTr = go.transform.Find("Count");
            if (countTr != null)
            {
                var t = countTr.GetComponent<Text>();
                if (t != null)
                {
                    t.font = FarmGridView.LoadBuiltinFont();
                    t.text = "× " + count;
                }
            }

            return go;
        }

        private void HighlightSlots()
        {
            string active = service != null ? service.GetActiveFertilizer() : null;
            for (int i = 0; i < slotPool.Count; i++)
            {
                var go = slotPool[i];
                if (go == null)
                    continue;
                string id = go.name.StartsWith("Slot_") ? go.name.Substring("Slot_".Length) : null;
                var img = go.GetComponent<Image>();
                if (img == null)
                    continue;
                bool sel = !string.IsNullOrEmpty(active) && id == active;
                img.color = sel ? SlotSelectedTint : SlotNormalTint;
            }
        }

        private void RefreshDetail()
        {
            string active = service != null ? service.GetActiveFertilizer() : null;
            var ft = !string.IsNullOrEmpty(active) ? service.GetFertilizerType(active) : null;

            if (detailBigIcon != null)
            {
                if (ft != null && !string.IsNullOrEmpty(ft.iconResource))
                {
                    detailBigIcon.sprite = Resources.Load<Sprite>(ft.iconResource);
                    detailBigIcon.enabled = detailBigIcon.sprite != null;
                }
                else
                {
                    detailBigIcon.sprite = null;
                    detailBigIcon.enabled = false;
                }
            }

            if (descriptionText != null)
            {
                if (ft != null)
                {
                    string desc = !string.IsNullOrEmpty(ft.description) ? ft.description : ft.displayName;
                    descriptionText.text = desc ?? string.Empty;
                }
                else
                {
                    descriptionText.text = string.Empty;
                }
            }

            HighlightSlots();
        }

        private void ClearSlots()
        {
            for (int i = 0; i < slotPool.Count; i++)
            {
                if (slotPool[i] != null)
                    Destroy(slotPool[i]);
            }
            slotPool.Clear();
        }

    }
}
