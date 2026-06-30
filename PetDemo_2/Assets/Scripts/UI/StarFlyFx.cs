// SPEC §13.6：Xing_2 飞行特效 — 从指定屏幕坐标飞向画布左上角（范式同 §9.6 PlayHarvestFruitFx）。
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    public static class StarFlyFx
    {
        public const string ResStarIcon = "AirUI/Xing_2";

        private const float IconSize = 96f;
        private const float DurationSec = 0.6f;
        // 终点 = 画布左上角内缩 (90, -90)。
        private static readonly Vector2 TopLeftInset = new Vector2(90f, -90f);

        /// <summary>
        /// 在 <paramref name="canvasRect"/> 顶层生成 Xing_2 临时图标，从 <paramref name="fromScreenPos"/>
        /// 飞向画布左上角后自毁。缺图 / 入参非法时跳过并 Warning。
        /// </summary>
        public static void Play(RectTransform canvasRect, Vector2 fromScreenPos)
        {
            if (canvasRect == null)
            {
                UnityEngine.Debug.LogWarning("[StarFlyFx] canvasRect 为空，跳过飞行特效。");
                return;
            }

            var sprite = Resources.Load<Sprite>(ResStarIcon);
            if (sprite == null)
            {
                UnityEngine.Debug.LogWarning("[StarFlyFx] 缺少图标 Resources/" + ResStarIcon + "，跳过飞行特效。");
                return;
            }

            var fxGo = new GameObject("StarFlyFx", typeof(RectTransform));
            var fxRt = fxGo.GetComponent<RectTransform>();
            fxRt.SetParent(canvasRect, false);
            fxRt.anchorMin = fxRt.anchorMax = new Vector2(0.5f, 0.5f);
            fxRt.pivot = new Vector2(0.5f, 0.5f);
            fxRt.sizeDelta = new Vector2(IconSize, IconSize);

            var fxImage = fxGo.AddComponent<Image>();
            fxImage.sprite = sprite;
            fxImage.preserveAspect = true;
            fxImage.raycastTarget = false;

            // 置顶：确保覆盖好友家园层（HudModal）与战斗层（HudOverlay）之上（SPEC §13.6）。
            MainHudLayerRoot.ApplySortTier(fxRt, MainUiSortTier.HudTop);

            var cam = ResolveCanvasCamera(canvasRect);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, fromScreenPos, cam, out var fromLocal);

            var rect = canvasRect.rect;
            var toLocal = new Vector2(rect.xMin + TopLeftInset.x, rect.yMax + TopLeftInset.y);

            fxRt.anchoredPosition = fromLocal;

            var runner = fxGo.AddComponent<StarFlyFxRunner>();
            runner.StartCoroutine(runner.FlyRoutine(fxRt, fromLocal, toLocal, DurationSec));
        }

        internal static Camera ResolveCanvasCamera(RectTransform canvasRect)
        {
            var canvas = canvasRect != null ? canvasRect.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
                return null;
            return canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        }

        private sealed class StarFlyFxRunner : MonoBehaviour
        {
            public IEnumerator FlyRoutine(RectTransform fxRt, Vector2 fromLocal, Vector2 toLocal, float duration)
            {
                float t = 0f;
                while (t < duration)
                {
                    t += Time.deltaTime;
                    float p = Mathf.Clamp01(t / duration);
                    float eased = 1f - Mathf.Pow(1f - p, 3f);
                    if (fxRt == null)
                        yield break;
                    fxRt.anchoredPosition = Vector2.LerpUnclamped(fromLocal, toLocal, eased);
                    yield return null;
                }
                Destroy(gameObject);
            }
        }
    }
}
