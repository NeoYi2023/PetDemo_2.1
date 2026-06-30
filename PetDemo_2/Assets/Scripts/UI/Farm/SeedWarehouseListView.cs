// 种子仓库面板内的双 Tab + 列表 UI（SPEC §9.4）。
// v3.95：TabBar/ListFrame 由 WarehouseBackground.prefab 承载；运行时 Initialize 绑定逻辑。
using System.Collections.Generic;
using PetDemo.Core;
using PetDemo.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Farm
{
    public class SeedWarehouseListView : MonoBehaviour
    {
        [SerializeField] private Button tabSeedButton;
        [SerializeField] private Button tabPackButton;
        [SerializeField] private Image tabSeedBg;
        [SerializeField] private Image tabPackBg;
        [SerializeField] private RectTransform listRoot;
        [SerializeField] private Text emptyHint;
        [SerializeField] private RectTransform packOptionTemplate;

        private IPlantingService service;
        private bool isSeedTab = true;
        private bool eventsSubscribed;
        private readonly List<EntryRow> rows = new List<EntryRow>();

        private static readonly Color TabActiveColor = new Color(1f, 1f, 1f, 1f);
        private static readonly Color TabInactiveColor = new Color(1f, 1f, 1f, 0.5f);
        private static readonly Color RowNormalColor = new Color(0f, 0f, 0f, 0.25f);
        private static readonly Color RowActiveColor = new Color(1f, 0.82f, 0.31f, 0.55f); // #FFD24F + alpha
        private static readonly Color OpaqueWhite = new Color(1f, 1f, 1f, 1f);
        private const string SeedPackCardSpriteResource = "item_1340000";
        private const string SeedPackCardSpriteResourceFallback = "AirUI/item_1340000";
        private const string SeedPackCardEditorAssetPath = "Assets/Scenes/Air/UI/item_1340000.png";

        public static SeedWarehouseListView BuildInto(RectTransform panelRect, IPlantingService svc)
        {
            if (panelRect == null || svc == null)
                return null;

            var view = panelRect.GetComponentInChildren<SeedWarehouseListView>(true);
            if (view != null)
            {
                view.BindPrefabTemplates(panelRect);
                view.Initialize(svc);
                return view;
            }

            var contentTr = panelRect.Find("SeedWarehouseContent");
            if (contentTr != null)
            {
                view = contentTr.GetComponent<SeedWarehouseListView>();
                if (view == null)
                    view = contentTr.gameObject.AddComponent<SeedWarehouseListView>();
                if (view.TryBindFromHierarchy(panelRect))
                {
                    view.Initialize(svc);
                    return view;
                }
            }

            UnityEngine.Debug.LogWarning(
                "SeedWarehouseListView: WarehouseBackground 预制体缺少 SeedWarehouseContent，回退为运行时代码布局。");
            return BuildFallback(panelRect, svc);
        }

        private static SeedWarehouseListView BuildFallback(RectTransform panelRect, IPlantingService svc)
        {
            var go = new GameObject("WarehouseListView");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(panelRect, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var view = go.AddComponent<SeedWarehouseListView>();
            view.BindPrefabTemplates(panelRect);
            view.BuildLayout(rt);
            view.Initialize(svc);
            return view;
        }

        public void Initialize(IPlantingService svc)
        {
            if (svc == null)
                return;

            UnsubscribeEvents();
            service = svc;
            WireTabButtons();
            BindPrefabTemplates(transform.parent as RectTransform);
            SubscribeEvents();
            SwitchTab(false);
        }

        public bool TryBindFromHierarchy(RectTransform panelRect)
        {
            var contentRt = transform as RectTransform;
            if (contentRt == null)
                return false;

            var tabBar = contentRt.Find("TabBar");
            if (tabBar != null)
            {
                var tabSeedTr = tabBar.Find("TabSeed");
                var tabPackTr = tabBar.Find("TabPack");
                if (tabSeedTr != null)
                {
                    tabSeedBg = tabSeedTr.GetComponent<Image>();
                    tabSeedButton = tabSeedTr.GetComponent<Button>();
                }
                if (tabPackTr != null)
                {
                    tabPackBg = tabPackTr.GetComponent<Image>();
                    tabPackButton = tabPackTr.GetComponent<Button>();
                }
            }

            var listFrameTr = contentRt.Find("ListFrame");
            if (listFrameTr != null)
            {
                listRoot = listFrameTr as RectTransform;
                var emptyTr = listFrameTr.Find("EmptyHint");
                if (emptyTr != null)
                    emptyHint = emptyTr.GetComponent<Text>();

                DisablePackCommonRowRootImage(listFrameTr.Find("EntryRow_Pack_Common"));
            }

            if (panelRect != null)
                BindPrefabTemplates(panelRect);

            return tabSeedButton != null && tabPackButton != null && listRoot != null && emptyHint != null;
        }

        private void BindPrefabTemplates(RectTransform panelRect)
        {
            if (packOptionTemplate != null)
            {
                packOptionTemplate.gameObject.SetActive(false);
                return;
            }

            if (panelRect == null)
                return;

            var templateTr = panelRect.Find("SeedPackOptionTemplate");
            if (templateTr != null)
            {
                packOptionTemplate = templateTr as RectTransform;
                packOptionTemplate.gameObject.SetActive(false);
            }
        }

        private void WireTabButtons()
        {
            if (tabSeedButton != null)
            {
                tabSeedButton.onClick.RemoveAllListeners();
                tabSeedButton.onClick.AddListener(() => SwitchTab(true));
            }
            if (tabPackButton != null)
            {
                tabPackButton.onClick.RemoveAllListeners();
                tabPackButton.onClick.AddListener(() => SwitchTab(false));
            }
        }

        private void BuildLayout(RectTransform root)
        {
            var tabBar = AddChildRect(root, "TabBar",
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                pivot: new Vector2(0.5f, 1f),
                anchoredPosition: new Vector2(0f, -24f),
                sizeDelta: new Vector2(1000f, 80f));

            tabSeedBg = AddChildImage(tabBar, "TabSeed",
                pos: new Vector2(-260f, 0f), size: new Vector2(480f, 80f));
            tabSeedBg.color = TabActiveColor;
            tabSeedBg.raycastTarget = true;
            tabSeedButton = tabSeedBg.gameObject.AddComponent<Button>();
            tabSeedButton.transition = Selectable.Transition.None;
            tabSeedButton.targetGraphic = tabSeedBg;

            var seedLabel = AddChildText(tabSeedBg.rectTransform, "Label",
                pos: Vector2.zero, size: new Vector2(480f, 80f),
                content: "种子", fontSize: 40, color: new Color(0.18f, 0.18f, 0.18f, 1f));
            seedLabel.alignment = TextAnchor.MiddleCenter;

            tabPackBg = AddChildImage(tabBar, "TabPack",
                pos: new Vector2(260f, 0f), size: new Vector2(480f, 80f));
            tabPackBg.color = TabInactiveColor;
            tabPackBg.raycastTarget = true;
            tabPackButton = tabPackBg.gameObject.AddComponent<Button>();
            tabPackButton.transition = Selectable.Transition.None;
            tabPackButton.targetGraphic = tabPackBg;

            var packLabel = AddChildText(tabPackBg.rectTransform, "Label",
                pos: Vector2.zero, size: new Vector2(480f, 80f),
                content: "种子包", fontSize: 40, color: new Color(0.18f, 0.18f, 0.18f, 1f));
            packLabel.alignment = TextAnchor.MiddleCenter;

            listRoot = AddChildRect(root, "ListFrame",
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                pivot: new Vector2(0.5f, 1f),
                anchoredPosition: new Vector2(0f, -128f),
                sizeDelta: new Vector2(1000f, 600f));

            emptyHint = AddChildText(listRoot, "EmptyHint",
                pos: Vector2.zero, size: new Vector2(900f, 80f),
                content: "暂无种子，去开包看看吧",
                fontSize: 32, color: new Color(0.92f, 0.92f, 0.92f, 0.85f));
            emptyHint.alignment = TextAnchor.MiddleCenter;
            emptyHint.gameObject.SetActive(false);
        }

        private void SubscribeEvents()
        {
            if (service == null || eventsSubscribed)
                return;
            service.OnSeedBagChanged += RebuildList;
            eventsSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (service == null || !eventsSubscribed)
                return;
            service.OnSeedBagChanged -= RebuildList;
            eventsSubscribed = false;
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void SwitchTab(bool seedTab)
        {
            isSeedTab = seedTab;
            if (tabSeedBg != null)
                tabSeedBg.color = isSeedTab ? TabActiveColor : TabInactiveColor;
            if (tabPackBg != null)
                tabPackBg.color = isSeedTab ? TabInactiveColor : TabActiveColor;
            RebuildList();
        }

        private void RebuildList()
        {
            if (service == null || listRoot == null || emptyHint == null)
                return;

            foreach (var r in rows)
            {
                if (r != null && r.rowGo != null)
                    Destroy(r.rowGo);
            }
            rows.Clear();

            var bag = service.GetSeedBag();
            int idx = 0;
            float rowHeight = 96f;
            float rowGap = 8f;

            if (isSeedTab)
            {
                if (bag != null && bag.seeds != null && bag.seeds.Count > 0)
                {
                    emptyHint.gameObject.SetActive(false);
                    for (int i = 0; i < bag.seeds.Count; i++)
                    {
                        var stack = bag.seeds[i];
                        var cfg = service.GetPlantConfig(stack.plantConfigId);
                        if (cfg == null)
                            continue;

                        var rowGo = BuildSeedRow(stack.plantConfigId, cfg.displayName, stack.count, idx, rowHeight, rowGap);
                        rows.Add(rowGo);
                        idx++;
                    }
                    if (idx == 0)
                        emptyHint.gameObject.SetActive(true);
                }
                else
                {
                    emptyHint.gameObject.SetActive(true);
                }
            }
            else
            {
                if (bag != null && bag.seedPacks != null && bag.seedPacks.Count > 0)
                {
                    emptyHint.gameObject.SetActive(false);
                    const float packRowHeight = 360f;
                    const float packRowGap = 20f;
                    for (int i = 0; i < bag.seedPacks.Count; i++)
                    {
                        var stack = bag.seedPacks[i];
                        var rowGo = BuildPackRow(stack.quality, GetQualityDisplayName(stack.quality), stack.count, idx, packRowHeight, packRowGap);
                        rows.Add(rowGo);
                        idx++;
                    }
                    if (idx == 0)
                        emptyHint.gameObject.SetActive(true);
                }
                else
                {
                    emptyHint.gameObject.SetActive(true);
                }
            }

            emptyHint.text = isSeedTab ? "暂无种子，去开包看看吧" : "暂无种子包";

            HighlightActiveRow();
        }

        private EntryRow BuildSeedRow(string plantConfigId, string displayName, int count, int slotIdx, float rowHeight, float rowGap)
        {
            float topY = -slotIdx * (rowHeight + rowGap);
            var rowGo = new GameObject("EntryRow_" + plantConfigId);
            var rowRt = rowGo.AddComponent<RectTransform>();
            rowRt.SetParent(listRoot, false);
            rowRt.anchorMin = new Vector2(0.5f, 1f);
            rowRt.anchorMax = new Vector2(0.5f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(-7f, 114f + topY);
            rowRt.sizeDelta = new Vector2(360f, rowHeight);

            var bg = rowGo.AddComponent<Image>();
            bg.color = RowNormalColor;
            bg.preserveAspect = true;
            bg.raycastTarget = true;

            var btn = rowGo.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = bg;

            var iconImg = AddChildImage(rowRt, "IconImage",
                pos: new Vector2(-380f, 0f), size: new Vector2(80f, 80f));
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            var cfg = service.GetPlantConfig(plantConfigId);
            if (cfg != null && cfg.appearanceSpriteIds != null && cfg.appearanceSpriteIds.Count > 0)
                iconImg.sprite = Resources.Load<Sprite>(cfg.appearanceSpriteIds[0]);

            var nameLabel = AddChildText(rowRt, "Name",
                pos: new Vector2(40f, 0f), size: new Vector2(560f, rowHeight),
                content: displayName, fontSize: 40,
                color: new Color(1f, 1f, 1f, 0.95f));
            nameLabel.alignment = TextAnchor.MiddleLeft;

            var countLabel = AddChildText(rowRt, "Count",
                pos: new Vector2(380f, 0f), size: new Vector2(160f, rowHeight),
                content: "× " + count, fontSize: 36,
                color: new Color(1f, 1f, 1f, 0.85f));
            countLabel.alignment = TextAnchor.MiddleRight;

            string capturedId = plantConfigId;
            btn.onClick.AddListener(() => service.SelectActive(ActiveKind.Seed, capturedId));

            return new EntryRow
            {
                rowGo = rowGo,
                bg = bg,
                activeKind = ActiveKind.Seed,
                activeId = plantConfigId,
            };
        }

        private EntryRow BuildPackRow(SeedPackQuality quality, string displayName, int count, int slotIdx, float rowHeight, float rowGap)
        {
            if (packOptionTemplate != null)
                return BuildPackRowFromTemplate(quality, count, slotIdx, rowHeight, rowGap);

            string qualityId = quality.ToString();
            float topY = -slotIdx * (rowHeight + rowGap);
            var rowGo = new GameObject("EntryRow_Pack_" + qualityId);
            var rowRt = rowGo.AddComponent<RectTransform>();
            rowRt.SetParent(listRoot, false);
            rowRt.anchorMin = new Vector2(0.5f, 1f);
            rowRt.anchorMax = new Vector2(0.5f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, topY);
            rowRt.sizeDelta = new Vector2(900f, rowHeight);

            var bg = rowGo.AddComponent<Image>();
            bg.color = RowNormalColor;
            bg.raycastTarget = true;

            var btn = rowGo.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = bg;

            var iconImg = AddChildImage(rowRt, "IconImage",
                pos: new Vector2(-417f, -395f), size: new Vector2(128f, 128f));
            iconImg.sprite = LoadSeedPackCardSprite();
            iconImg.color = OpaqueWhite;
            iconImg.raycastTarget = false;

            var nameLabel = AddChildText(rowRt, "Name",
                pos: new Vector2(-415f, -476f), size: new Vector2(96f, 96f),
                content: displayName, fontSize: 40,
                color: new Color(1f, 1f, 1f, 0.95f));
            nameLabel.alignment = TextAnchor.MiddleCenter;

            var countLabel = AddChildText(rowRt, "Count",
                pos: new Vector2(-422f, -515f), size: new Vector2(160f, 96f),
                content: "× " + count, fontSize: 36,
                color: new Color(1f, 1f, 1f, 0.85f));
            countLabel.alignment = TextAnchor.MiddleCenter;

            btn.onClick.AddListener(() => service.SelectActive(ActiveKind.Pack, qualityId));

            return new EntryRow
            {
                rowGo = rowGo,
                bg = bg,
                activeKind = ActiveKind.Pack,
                activeId = qualityId,
            };
        }

        private EntryRow BuildPackRowFromTemplate(SeedPackQuality quality, int count, int slotIdx, float rowHeight, float rowGap)
        {
            string qualityId = quality.ToString();
            float topY = -slotIdx * (rowHeight + rowGap);

            var rowRt = Instantiate(packOptionTemplate, listRoot, false);
            rowRt.gameObject.SetActive(true);
            rowRt.name = "EntryRow_Pack_" + qualityId;
            rowRt.anchoredPosition = new Vector2(0f, topY);
            rowRt.sizeDelta = new Vector2(360f, rowHeight);

            var bg = rowRt.GetComponent<Image>();
            if (bg == null)
                bg = rowRt.gameObject.AddComponent<Image>();
            bg.color = RowNormalColor;
            bg.raycastTarget = true;

            var button = rowRt.GetComponent<Button>();
            if (button == null)
                button = rowRt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = bg;

            var smallIconTr = rowRt.Find("SmallIcon");
            if (quality == SeedPackQuality.Common)
            {
                bg.enabled = false;
                bg.raycastTarget = false;
                var clickTarget = smallIconTr != null ? smallIconTr.GetComponent<Image>() : null;
                if (clickTarget != null)
                {
                    clickTarget.raycastTarget = true;
                    button.targetGraphic = clickTarget;
                }
            }

            var bigIconTr = rowRt.Find("BigIcon");
            if (bigIconTr != null)
            {
                var bigIcon = bigIconTr.GetComponent<Image>();
                if (bigIcon != null)
                {
                    bigIcon.sprite = LoadSeedPackCardSprite();
                    bigIcon.color = OpaqueWhite;
                }
            }

            if (smallIconTr != null)
            {
                var smallIcon = smallIconTr.GetComponent<Image>();
                if (smallIcon != null)
                {
                    smallIcon.sprite = LoadSeedPackCardSprite();
                    smallIcon.color = GetQualityColor(quality);
                    if (quality != SeedPackQuality.Common)
                        smallIcon.raycastTarget = false;
                }

                var countTr = smallIconTr.Find("Count");
                if (countTr != null)
                {
                    var countText = countTr.GetComponent<Text>();
                    if (countText != null)
                        countText.text = "x " + count;
                }
            }

            button.onClick.AddListener(() => service.SelectActive(ActiveKind.Pack, qualityId));

            return new EntryRow
            {
                rowGo = rowRt.gameObject,
                bg = bg,
                activeKind = ActiveKind.Pack,
                activeId = qualityId,
            };
        }

        private void HighlightActiveRow()
        {
            var bag = service.GetSeedBag();
            ActiveKind? activeKind = (bag != null && bag.active != null) ? bag.active.kind : (ActiveKind?)null;
            string activeId = (bag != null && bag.active != null) ? bag.active.id : null;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] == null || rows[i].bg == null || !rows[i].bg.enabled)
                    continue;
                bool isActive = activeKind.HasValue &&
                                rows[i].activeKind == activeKind.Value &&
                                rows[i].activeId == activeId;
                rows[i].bg.color = isActive ? RowActiveColor : RowNormalColor;
            }
        }

        private class EntryRow
        {
            public GameObject rowGo;
            public Image bg;
            public ActiveKind activeKind;
            public string activeId;
        }

        private static void DisablePackCommonRowRootImage(Transform rowTr)
        {
            if (rowTr == null)
                return;

            var bg = rowTr.GetComponent<Image>();
            if (bg != null)
            {
                bg.enabled = false;
                bg.raycastTarget = false;
            }
        }

        private static string GetQualityDisplayName(SeedPackQuality quality)
        {
            switch (quality)
            {
                case SeedPackQuality.Common: return "普通";
                case SeedPackQuality.Rare: return "稀有";
                case SeedPackQuality.Epic: return "史诗";
                case SeedPackQuality.Legendary: return "传说";
                default: return quality.ToString();
            }
        }

        private static Color GetQualityColor(SeedPackQuality quality)
        {
            switch (quality)
            {
                case SeedPackQuality.Common: return HexToColor("#F5F5F5");
                case SeedPackQuality.Rare: return HexToColor("#4FA3FF");
                case SeedPackQuality.Epic: return HexToColor("#A26CFF");
                case SeedPackQuality.Legendary: return HexToColor("#FF9F40");
                default: return Color.white;
            }
        }

        private static Sprite LoadSeedPackCardSprite()
        {
            var sprite = Resources.Load<Sprite>(SeedPackCardSpriteResource);
            if (sprite != null)
                return sprite;

            sprite = Resources.Load<Sprite>(SeedPackCardSpriteResourceFallback);
            if (sprite != null)
                return sprite;

#if UNITY_EDITOR
            sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(SeedPackCardEditorAssetPath);
            if (sprite != null)
                return sprite;
#endif

            UnityEngine.Debug.LogWarning(
                "SeedWarehouseListView: failed to load seed pack card sprite 'item_1340000'. " +
                "Expected Resources path: 'Assets/Resources/AirUI/item_1340000.png' (load key: 'AirUI/item_1340000').");
            return null;
        }

        private static Color HexToColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var color))
                return color;
            return Color.white;
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

        private static Image AddChildImage(RectTransform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return go.AddComponent<Image>();
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
