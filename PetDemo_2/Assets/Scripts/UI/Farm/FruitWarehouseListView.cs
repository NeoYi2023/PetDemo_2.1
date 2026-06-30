// 果实背包弹窗内只读列表（SPEC §4.1.11 / §9.9）。
using System.Collections.Generic;
using PetDemo.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Farm
{
    public class FruitWarehouseListView : MonoBehaviour
    {
        private IPlantingService service;
        private Text emptyHint;
        private RectTransform listRoot;
        private readonly List<GameObject> rowObjects = new List<GameObject>();

        public static FruitWarehouseListView BuildInto(RectTransform panelRect, IPlantingService svc)
        {
            var go = new GameObject("FruitWarehouseListView");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(panelRect, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var view = go.AddComponent<FruitWarehouseListView>();
            view.service = svc;
            view.BuildLayout(rt);
            view.SubscribeEvents();
            view.RebuildList();
            return view;
        }

        private void BuildLayout(RectTransform root)
        {
            AddChildText(root, "Title",
                new Vector2(0f, -48f), new Vector2(900f, 64f),
                "果实背包", 44, new Color(0.15f, 0.15f, 0.15f, 1f)).alignment = TextAnchor.MiddleCenter;

            listRoot = AddChildRect(root, "ListFrame",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -140f), new Vector2(1000f, 620f));

            emptyHint = AddChildText(listRoot, "EmptyHint",
                Vector2.zero, new Vector2(900f, 80f),
                "暂无果实，收获作物后会出现在这里",
                32, new Color(0.35f, 0.35f, 0.35f, 0.9f));
            emptyHint.alignment = TextAnchor.MiddleCenter;
            emptyHint.gameObject.SetActive(false);
        }

        private void SubscribeEvents()
        {
            if (service != null)
                service.OnFruitBagChanged += RebuildList;
        }

        private void OnDestroy()
        {
            if (service != null)
                service.OnFruitBagChanged -= RebuildList;
        }

        private void RebuildList()
        {
            foreach (var go in rowObjects)
            {
                if (go != null)
                    Destroy(go);
            }
            rowObjects.Clear();

            if (service == null)
                return;

            var bag = service.GetFruitBag();
            var stacks = bag != null ? bag.stacks : null;
            int shown = 0;
            float rowHeight = 72f;
            float rowGap = 10f;
            float y = -rowHeight * 0.5f - 8f;

            if (stacks != null)
            {
                for (int i = 0; i < stacks.Count; i++)
                {
                    var s = stacks[i];
                    if (s == null || string.IsNullOrEmpty(s.plantConfigId) || s.count <= 0)
                        continue;
                    var cfg = service.GetPlantConfig(s.plantConfigId);
                    string name = cfg != null && !string.IsNullOrEmpty(cfg.displayName)
                        ? cfg.displayName
                        : s.plantConfigId;
                    var rowRt = AddChildRect(listRoot, "FruitRow_" + s.plantConfigId,
                        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, y), new Vector2(960f, rowHeight));
                    var bg = rowRt.gameObject.AddComponent<Image>();
                    bg.color = new Color(0f, 0f, 0f, 0.12f);
                    bg.raycastTarget = false;

                    var label = AddChildText(rowRt, "Label",
                        new Vector2(-120f, 0f), new Vector2(560f, rowHeight),
                        name + "  ×" + s.count.ToString(),
                        34, new Color(0.12f, 0.12f, 0.12f, 1f));
                    label.alignment = TextAnchor.MiddleLeft;

                    Sprite iconSprite = null;
                    if (cfg != null)
                    {
                        var path = cfg.ResolveFruitIconResourcePath();
                        if (!string.IsNullOrEmpty(path))
                            iconSprite = Resources.Load<Sprite>(path);
                    }
                    if (iconSprite != null)
                    {
                        var iconRt = AddChildRect(rowRt, "Icon",
                            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(-420f, 0f), new Vector2(56f, 56f));
                        var iconImg = iconRt.gameObject.AddComponent<Image>();
                        iconImg.sprite = iconSprite;
                        iconImg.preserveAspect = true;
                        iconImg.raycastTarget = false;
                    }

                    rowObjects.Add(rowRt.gameObject);
                    shown++;
                    y -= rowHeight + rowGap;
                }
            }

            emptyHint.gameObject.SetActive(shown == 0);
        }

        private static RectTransform AddChildRect(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            return rt;
        }

        private static Text AddChildText(RectTransform parent, string name, Vector2 pos, Vector2 size,
            string content, int fontSize, Color color)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var txt = go.AddComponent<Text>();
            txt.text = content;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.font = FarmGridView.LoadBuiltinFont();
            txt.raycastTarget = false;
            return txt;
        }
    }
}
