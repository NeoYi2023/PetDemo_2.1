// 加好感任务列表「领取奖励」飞行特效：从指定屏幕坐标飞向任意屏幕坐标终点后自毁。
// 范式同 §13.6 StarFlyFx，但终点由参数指定（本需求终点为屏幕坐标 (377,895)）。
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    public static class RewardFlyFx
    {
        private const float IconSize = 96f;
        private const float DurationSec = 0.6f;

        /// <summary>
        /// 在 <paramref name="canvasRect"/> 顶层生成临时奖励图标，从 <paramref name="fromScreenPos"/>
        /// 飞向 <paramref name="toScreenPos"/>（均为屏幕像素坐标）后自毁。
        /// 缺图 / 入参非法时跳过并 Warning。
        /// </summary>
        public static void Play(RectTransform canvasRect, Vector2 fromScreenPos, Vector2 toScreenPos, string iconResource)
        {
            if (canvasRect == null)
            {
                UnityEngine.Debug.LogWarning("[RewardFlyFx] canvasRect 为空，跳过飞行特效。");
                return;
            }

            var sprite = !string.IsNullOrEmpty(iconResource) ? Resources.Load<Sprite>(iconResource) : null;
            if (sprite == null)
            {
                UnityEngine.Debug.LogWarning("[RewardFlyFx] 缺少图标 Resources/" + iconResource + "，跳过飞行特效。");
                return;
            }

            var cam = ResolveCanvasCamera(canvasRect);

            var fxGo = new GameObject("RewardFlyFx", typeof(RectTransform));
            var fxRt = fxGo.GetComponent<RectTransform>();
            fxRt.SetParent(canvasRect, false);
            fxRt.anchorMin = fxRt.anchorMax = new Vector2(0.5f, 0.5f);
            fxRt.pivot = new Vector2(0.5f, 0.5f);
            fxRt.sizeDelta = new Vector2(IconSize, IconSize);

            var fxImage = fxGo.AddComponent<Image>();
            fxImage.sprite = sprite;
            fxImage.preserveAspect = true;
            fxImage.raycastTarget = false;

            // 置顶：确保覆盖所有 HUD 层。
            fxRt.SetAsLastSibling();

            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, fromScreenPos, cam, out var fromLocal);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, toScreenPos, cam, out var toLocal);

            fxRt.anchoredPosition = fromLocal;

            var runner = fxGo.AddComponent<RewardFlyFxRunner>();
            runner.StartCoroutine(runner.FlyRoutine(fxRt, fromLocal, toLocal, DurationSec));
        }

        private static Camera ResolveCanvasCamera(RectTransform canvasRect)
        {
            var canvas = canvasRect != null ? canvasRect.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
                return null;
            return canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        }

        private sealed class RewardFlyFxRunner : MonoBehaviour
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
