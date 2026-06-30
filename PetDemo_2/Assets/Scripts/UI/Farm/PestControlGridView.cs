// SPEC §9.11.3 / §9.11.9：灭虫小游戏 5×5 网格渲染与动画。
using System;
using System.Collections;
using System.Collections.Generic;
using PetDemo.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Farm
{
    public class PestControlGridView : MonoBehaviour
    {
        public const float SecondsPerCell = 0.2f;
        public const float PulseDuration = 0.15f;

        // SPEC §9.11.9（v3.79）：狼吃虫前置阶段参数。
        public const float EatEffectDuration = 0.5f;
        public const float EatShakeAmplitude = 18f;
        public const float EatApproachSeconds = 0.12f;
        public const float EatStepInSeconds = 0.12f;

        private const string ResBugSprite = "AirUI/Game_1_2";
        private const string ResWerewolfSprite = "AirUI/Game_1_3";
        private const string ResEatEffectSprite = "AirUI/Game_1_3_1";
        public const float CellSize = 150f;
        public const float CellGap = 8f;
        public static readonly Vector2 GridAnchoredPosition = new Vector2(0f, -75f);

        public static float GridTotalSize =>
            PestControlGameModel.GridSize * CellSize + (PestControlGameModel.GridSize - 1) * CellGap;

        private static readonly Color EmptyCellColor = new Color(0.18f, 0.18f, 0.18f, 0.95f);
        private static readonly Color BugIconFallback = new Color(0.55f, 0.75f, 0.20f, 1f);
        private static readonly Color WerewolfIconFallback = new Color(0.55f, 0.30f, 0.75f, 1f);

        private sealed class PieceVisual
        {
            public int tokenId;
            public PestControlEntityType entityType;
            public RectTransform root;
            public Image tileBg;
            public Image icon;
            public Text valueText;
        }

        private RectTransform gridRoot;
        private Image[,] slotBackgrounds;
        private readonly Dictionary<int, PieceVisual> piecesByToken = new Dictionary<int, PieceVisual>();
        private readonly List<GameObject> activeEatEffects = new List<GameObject>();
        private Sprite bugSprite;
        private Sprite werewolfSprite;
        private Sprite eatEffectSprite;
        private Font uiFont;
        private int nextTokenId = 1;
        private Coroutine animRoutine;

        public static PestControlGridView BuildInto(RectTransform parent)
        {
            var rootGo = new GameObject("PestControlGrid", typeof(RectTransform));
            var rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.SetParent(parent, false);
            rootRt.anchorMin = new Vector2(0.5f, 0.5f);
            rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);

            float total = GridTotalSize;
            rootRt.sizeDelta = new Vector2(total, total);
            rootRt.anchoredPosition = GridAnchoredPosition;

            var view = rootGo.AddComponent<PestControlGridView>();
            view.gridRoot = rootRt;
            view.uiFont = FarmGridView.LoadBuiltinFont();
            if (view.uiFont == null)
                view.uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            view.bugSprite = Resources.Load<Sprite>(ResBugSprite);
            view.werewolfSprite = Resources.Load<Sprite>(ResWerewolfSprite);
            view.eatEffectSprite = Resources.Load<Sprite>(ResEatEffectSprite);
            view.BuildSlotBackgrounds();
            return view;
        }

        public static Vector2 GetAnchoredPosition(int row, int col)
        {
            float x = col * (CellSize + CellGap);
            float y = -row * (CellSize + CellGap);
            return new Vector2(x, y);
        }

        private void BuildSlotBackgrounds()
        {
            int size = PestControlGameModel.GridSize;
            slotBackgrounds = new Image[size, size];
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    var slotGo = new GameObject($"Slot_{r}_{c}", typeof(RectTransform));
                    var slotRt = slotGo.GetComponent<RectTransform>();
                    slotRt.SetParent(gridRoot, false);
                    slotRt.anchorMin = new Vector2(0f, 1f);
                    slotRt.anchorMax = new Vector2(0f, 1f);
                    slotRt.pivot = new Vector2(0f, 1f);
                    slotRt.sizeDelta = new Vector2(CellSize, CellSize);
                    slotRt.anchoredPosition = GetAnchoredPosition(r, c);
                    var bg = slotGo.AddComponent<Image>();
                    bg.color = EmptyCellColor;
                    bg.raycastTarget = false;
                    slotBackgrounds[r, c] = bg;
                }
            }
        }

        public void SyncFromModel(PestControlGameModel model)
        {
            if (animRoutine != null)
            {
                StopCoroutine(animRoutine);
                animRoutine = null;
            }

            ClearEatEffects();
            ClearAllPieces();
            if (model == null)
                return;

            nextTokenId = 1;
            int size = PestControlGameModel.GridSize;
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    var cell = model.GetCell(r, c);
                    if (cell.isEmpty)
                        continue;
                    CreatePiece(nextTokenId++, r, c, cell);
                }
            }

            RefreshPieceDrawOrder();
        }

        public void PlaySwipePlan(PestControlSwipePlan plan, Action onComplete)
        {
            if (animRoutine != null)
                StopCoroutine(animRoutine);
            animRoutine = StartCoroutine(PlaySwipePlanRoutine(plan, onComplete));
        }

        private IEnumerator PlaySwipePlanRoutine(PestControlSwipePlan plan, Action onComplete)
        {
            if (plan == null || !plan.changed)
            {
                onComplete?.Invoke();
                yield break;
            }

            // SPEC §9.11.7（v3.79）：狼吃虫前置阶段（其他棋子保持原位）。
            HashSet<int> eatWolfTokens = null;
            if (plan.eatEvents != null && plan.eatEvents.Count > 0)
            {
                eatWolfTokens = new HashSet<int>();
                for (int i = 0; i < plan.eatEvents.Count; i++)
                    eatWolfTokens.Add(plan.eatEvents[i].wolfTokenId);

                yield return PlayWolfEatBugPhase(plan);
            }

            // 归位阶段：吃虫狼从虫子格滑到最终格，其余棋子从当前位置滑到最终格。
            var extraMoves = BuildEatWolfSettleMoves(plan);
            yield return PlayMoveGroups(plan, eatWolfTokens, extraMoves);
            RefreshPieceDrawOrder();

            yield return PlayPulsesOfKind(plan, PestControlCellPulseKind.Eat, eatWolfTokens);

            for (int i = 0; i < plan.eatConsumedTokenIds.Count; i++)
                DestroyPiece(plan.eatConsumedTokenIds[i]);

            yield return PlayPulsesOfKind(plan, PestControlCellPulseKind.Merge, null);

            for (int i = 0; i < plan.mergeConsumedTokenIds.Count; i++)
                DestroyPiece(plan.mergeConsumedTokenIds[i]);

            if (plan.gridAfterSwipe != null)
            {
                for (int i = 0; i < plan.moves.Count; i++)
                {
                    var move = plan.moves[i];
                    if (piecesByToken.TryGetValue(move.tokenId, out var visual))
                    {
                        visual.root.anchoredPosition = GetAnchoredPosition(move.toRow, move.toCol);
                        ApplyCellToPiece(visual, plan.gridAfterSwipe[move.toRow, move.toCol]);
                    }
                }

                for (int r = 0; r < PestControlGameModel.GridSize; r++)
                {
                    for (int c = 0; c < PestControlGameModel.GridSize; c++)
                    {
                        var cell = plan.gridAfterSwipe[r, c];
                        if (cell.isEmpty)
                            continue;
                        foreach (var kv in piecesByToken)
                        {
                            var pos = kv.Value.root.anchoredPosition;
                            if (Vector2.Distance(pos, GetAnchoredPosition(r, c)) < 2f)
                            {
                                ApplyCellToPiece(kv.Value, cell);
                                break;
                            }
                        }
                    }
                }
            }

            RefreshPieceDrawOrder();
            onComplete?.Invoke();
            animRoutine = null;
        }

        private IEnumerator PlayMoveGroups(PestControlSwipePlan plan, HashSet<int> skipTokens = null, List<PestControlPieceMove> extraMoves = null)
        {
            var working = new List<PestControlPieceMove>(plan != null ? plan.moves.Count + 4 : 4);
            if (plan != null)
            {
                for (int i = 0; i < plan.moves.Count; i++)
                {
                    var move = plan.moves[i];
                    if (skipTokens != null && skipTokens.Contains(move.tokenId))
                        continue;
                    working.Add(move);
                }
            }

            if (extraMoves != null)
                working.AddRange(extraMoves);

            if (working.Count == 0)
                yield break;

            working.Sort(CompareWorkingMoves);

            int index = 0;
            while (index < working.Count)
            {
                int order = working[index].actionOrder;
                float maxDuration = 0f;
                while (index < working.Count && working[index].actionOrder == order)
                {
                    var move = working[index];
                    index++;
                    if (!piecesByToken.TryGetValue(move.tokenId, out var visual))
                        continue;

                    // 归位阶段以棋子当前位置为起点：非吃虫棋子当前=起始，吃虫狼当前=虫子格。
                    Vector2 from = visual.root.anchoredPosition;
                    Vector2 to = GetAnchoredPosition(move.toRow, move.toCol);
                    float duration = SecondsPerCell * move.cellDistance;
                    if (duration > maxDuration)
                        maxDuration = duration;

                    StartCoroutine(AnimatePieceMove(visual.root, from, to, duration));
                }

                if (maxDuration > 0f)
                    yield return new WaitForSeconds(maxDuration);
            }
        }

        private static int CompareWorkingMoves(PestControlPieceMove a, PestControlPieceMove b)
        {
            int byOrder = a.actionOrder.CompareTo(b.actionOrder);
            if (byOrder != 0)
                return byOrder;
            return a.tokenId.CompareTo(b.tokenId);
        }

        private List<PestControlPieceMove> BuildEatWolfSettleMoves(PestControlSwipePlan plan)
        {
            if (plan == null || plan.eatEvents == null || plan.eatEvents.Count == 0)
                return null;

            var list = new List<PestControlPieceMove>(plan.eatEvents.Count);
            for (int i = 0; i < plan.eatEvents.Count; i++)
            {
                var ev = plan.eatEvents[i];
                int dist = Mathf.Abs(ev.finalRow - ev.bugFromRow) + Mathf.Abs(ev.finalCol - ev.bugFromCol);
                list.Add(new PestControlPieceMove
                {
                    tokenId = ev.wolfTokenId,
                    fromRow = ev.bugFromRow,
                    fromCol = ev.bugFromCol,
                    toRow = ev.finalRow,
                    toCol = ev.finalCol,
                    cellDistance = Mathf.Max(1, dist),
                    actionOrder = ev.actionOrder,
                });
            }

            return list;
        }

        private IEnumerator PlayWolfEatBugPhase(PestControlSwipePlan plan)
        {
            // 1) 接近：各吃虫狼并行滑到虫子相邻格（其余棋子静止）。
            float maxApproach = 0f;
            for (int i = 0; i < plan.eatEvents.Count; i++)
            {
                var ev = plan.eatEvents[i];
                if (!piecesByToken.TryGetValue(ev.wolfTokenId, out var wolf))
                    continue;

                Vector2 from = wolf.root.anchoredPosition;
                Vector2 to = ComputeApproachPosition(ev);
                float dist = Vector2.Distance(from, to) / (CellSize + CellGap);
                float duration = dist > 0.01f ? EatApproachSeconds * Mathf.Max(1f, dist) : 0f;
                if (duration > maxApproach)
                    maxApproach = duration;
                StartCoroutine(AnimatePieceMove(wolf.root, from, to, duration));
            }

            if (maxApproach > 0f)
                yield return new WaitForSeconds(maxApproach);

            // 2) 特效 + 剧烈震动 0.5s（仅特效图片自身抖动）。
            for (int i = 0; i < plan.eatEvents.Count; i++)
            {
                var ev = plan.eatEvents[i];
                Vector2 center = GetCellCenter(ev.bugFromRow, ev.bugFromCol);
                var effect = CreateEatEffect(center);
                activeEatEffects.Add(effect);
                StartCoroutine(ShakeEffect(effect.GetComponent<RectTransform>(), center));
            }

            yield return new WaitForSeconds(EatEffectDuration);
            ClearEatEffects();

            // 3) 进入：各吃虫狼步入虫子格，并销毁被吃虫子。
            for (int i = 0; i < plan.eatEvents.Count; i++)
            {
                var ev = plan.eatEvents[i];
                if (!piecesByToken.TryGetValue(ev.wolfTokenId, out var wolf))
                    continue;

                Vector2 from = wolf.root.anchoredPosition;
                Vector2 to = GetAnchoredPosition(ev.bugFromRow, ev.bugFromCol);
                StartCoroutine(AnimatePieceMove(wolf.root, from, to, EatStepInSeconds));
            }

            yield return new WaitForSeconds(EatStepInSeconds);

            for (int i = 0; i < plan.eatEvents.Count; i++)
                DestroyPiece(plan.eatEvents[i].bugTokenId);

            RefreshPieceDrawOrder();
        }

        private static Vector2 ComputeApproachPosition(PestControlEatEvent ev)
        {
            int dr = Mathf.Clamp(ev.wolfFromRow - ev.bugFromRow, -1, 1);
            int dc = Mathf.Clamp(ev.wolfFromCol - ev.bugFromCol, -1, 1);
            return GetAnchoredPosition(ev.bugFromRow + dr, ev.bugFromCol + dc);
        }

        private static Vector2 GetCellCenter(int row, int col)
        {
            return GetAnchoredPosition(row, col) + new Vector2(CellSize * 0.5f, -CellSize * 0.5f);
        }

        private GameObject CreateEatEffect(Vector2 center)
        {
            var go = new GameObject("EatEffect", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(gridRoot, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(CellSize * 1.4f, CellSize * 1.4f);
            rt.anchoredPosition = center;

            var img = go.AddComponent<Image>();
            img.sprite = eatEffectSprite;
            img.color = eatEffectSprite != null ? Color.white : new Color(1f, 0.85f, 0.2f, 0.9f);
            img.preserveAspect = true;
            img.raycastTarget = false;
            rt.SetAsLastSibling();
            return go;
        }

        private IEnumerator ShakeEffect(RectTransform rt, Vector2 center)
        {
            if (rt == null)
                yield break;

            float elapsed = 0f;
            while (elapsed < EatEffectDuration)
            {
                elapsed += Time.deltaTime;
                if (rt == null)
                    yield break;
                float ox = UnityEngine.Random.Range(-EatShakeAmplitude, EatShakeAmplitude);
                float oy = UnityEngine.Random.Range(-EatShakeAmplitude, EatShakeAmplitude);
                rt.anchoredPosition = center + new Vector2(ox, oy);
                yield return null;
            }

            if (rt != null)
                rt.anchoredPosition = center;
        }

        private void ClearEatEffects()
        {
            for (int i = 0; i < activeEatEffects.Count; i++)
            {
                if (activeEatEffects[i] != null)
                    Destroy(activeEatEffects[i]);
            }

            activeEatEffects.Clear();
        }

        private IEnumerator PlayPulsesOfKind(PestControlSwipePlan plan, PestControlCellPulseKind kind, HashSet<int> skipTokens)
        {
            if (plan?.gridAfterSwipe == null)
                yield break;

            int index = 0;
            while (index < plan.pulses.Count)
            {
                int order = plan.pulses[index].actionOrder;
                bool playedAny = false;
                while (index < plan.pulses.Count && plan.pulses[index].actionOrder == order)
                {
                    var pulse = plan.pulses[index];
                    index++;
                    if (pulse.kind != kind)
                        continue;
                    if (skipTokens != null && skipTokens.Contains(pulse.tokenId))
                        continue;

                    var cell = plan.gridAfterSwipe[pulse.row, pulse.col];
                    if (pulse.tokenId <= 0 || !piecesByToken.TryGetValue(pulse.tokenId, out var visual))
                        continue;

                    visual.root.anchoredPosition = GetAnchoredPosition(pulse.row, pulse.col);
                    ApplyCellToPiece(visual, cell);
                    StartCoroutine(PulseScale(visual.root));
                    playedAny = true;
                }

                if (playedAny)
                {
                    RefreshPieceDrawOrder();
                    yield return new WaitForSeconds(PulseDuration);
                }
            }
        }

        private static IEnumerator AnimatePieceMove(RectTransform rt, Vector2 from, Vector2 to, float duration)
        {
            if (rt == null)
                yield break;
            if (duration <= 0f)
            {
                rt.anchoredPosition = to;
                yield break;
            }

            float elapsed = 0f;
            rt.anchoredPosition = from;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rt.anchoredPosition = Vector2.Lerp(from, to, t);
                yield return null;
            }

            rt.anchoredPosition = to;
        }

        private static IEnumerator PulseScale(RectTransform rt)
        {
            if (rt == null)
                yield break;

            Vector3 baseScale = Vector3.one;
            float half = PulseDuration * 0.5f;
            float elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                rt.localScale = Vector3.Lerp(baseScale, baseScale * 1.15f, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                rt.localScale = Vector3.Lerp(baseScale * 1.15f, baseScale, t);
                yield return null;
            }

            rt.localScale = baseScale;
        }

        private void CreatePiece(int tokenId, int row, int col, PestControlCell cell)
        {
            var pieceGo = new GameObject($"Piece_{tokenId}", typeof(RectTransform));
            var pieceRt = pieceGo.GetComponent<RectTransform>();
            pieceRt.SetParent(gridRoot, false);
            pieceRt.anchorMin = new Vector2(0f, 1f);
            pieceRt.anchorMax = new Vector2(0f, 1f);
            pieceRt.pivot = new Vector2(0f, 1f);
            pieceRt.sizeDelta = new Vector2(CellSize, CellSize);
            pieceRt.anchoredPosition = GetAnchoredPosition(row, col);

            var tileBgGo = new GameObject("TileBg", typeof(RectTransform));
            var tileBgRt = tileBgGo.GetComponent<RectTransform>();
            tileBgRt.SetParent(pieceRt, false);
            tileBgRt.anchorMin = Vector2.zero;
            tileBgRt.anchorMax = Vector2.one;
            tileBgRt.offsetMin = Vector2.zero;
            tileBgRt.offsetMax = Vector2.zero;
            var tileBg = tileBgGo.AddComponent<Image>();
            tileBg.raycastTarget = false;

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.SetParent(pieceRt, false);
            iconRt.anchorMin = new Vector2(0.1f, 0.1f);
            iconRt.anchorMax = new Vector2(0.9f, 0.9f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            var icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var textGo = new GameObject("Value", typeof(RectTransform));
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.SetParent(pieceRt, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var valueText = textGo.AddComponent<Text>();
            valueText.alignment = TextAnchor.MiddleCenter;
            valueText.font = uiFont;
            valueText.fontSize = 45;
            valueText.fontStyle = FontStyle.Bold;
            valueText.color = Color.white;
            valueText.raycastTarget = false;

            var visual = new PieceVisual
            {
                tokenId = tokenId,
                root = pieceRt,
                tileBg = tileBg,
                icon = icon,
                valueText = valueText,
            };
            piecesByToken[tokenId] = visual;
            ApplyCellToPiece(visual, cell);
        }

        private void ApplyCellToPiece(PieceVisual visual, PestControlCell cell)
        {
            visual.entityType = cell.type;
            visual.valueText.text = cell.value.ToString();
            PestControlValueColorCatalog.GetColors(cell.value, out var wolfBg, out var bugBg);
            if (cell.type == PestControlEntityType.Bug)
            {
                visual.tileBg.color = bugBg;
                visual.icon.sprite = bugSprite;
                visual.icon.color = bugSprite != null ? Color.white : BugIconFallback;
            }
            else
            {
                visual.tileBg.color = wolfBg;
                visual.icon.sprite = werewolfSprite;
                visual.icon.color = werewolfSprite != null ? Color.white : WerewolfIconFallback;
            }
        }

        /// <summary>UGUI 同父节点下后绘制的 sibling 在上；虫子在下、狼人在上。</summary>
        private void RefreshPieceDrawOrder()
        {
            if (gridRoot == null)
                return;

            int slotCount = PestControlGameModel.GridSize * PestControlGameModel.GridSize;
            var bugs = new List<RectTransform>();
            var wolves = new List<RectTransform>();
            foreach (var kv in piecesByToken)
            {
                if (kv.Value.root == null)
                    continue;
                if (kv.Value.entityType == PestControlEntityType.Werewolf)
                    wolves.Add(kv.Value.root);
                else
                    bugs.Add(kv.Value.root);
            }

            int index = slotCount;
            for (int i = 0; i < bugs.Count; i++)
                bugs[i].SetSiblingIndex(index++);
            for (int i = 0; i < wolves.Count; i++)
                wolves[i].SetSiblingIndex(index++);
        }

        private void DestroyPiece(int tokenId)
        {
            if (!piecesByToken.TryGetValue(tokenId, out var visual))
                return;
            if (visual.root != null)
                Destroy(visual.root.gameObject);
            piecesByToken.Remove(tokenId);
        }

        private void ClearAllPieces()
        {
            foreach (var kv in piecesByToken)
            {
                if (kv.Value.root != null)
                    Destroy(kv.Value.root.gameObject);
            }

            piecesByToken.Clear();
        }

        public void Refresh(PestControlGameModel model)
        {
            SyncFromModel(model);
        }
    }
}
