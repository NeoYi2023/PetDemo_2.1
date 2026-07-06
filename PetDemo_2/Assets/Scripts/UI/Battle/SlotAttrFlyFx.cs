// SPEC §12.12.6：老虎机属性获得飞入特效 — 关界面瞬间复制各 Reel 图标，延迟后飞向 DetailAttrButton。
using System.Collections;
using System.Collections.Generic;
using PetDemo.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Battle
{
    public static class SlotAttrFlyFx
    {
        public const float DelayBeforeFlySec = 0.3f;
        public const float FlyDurationSec = 0.5f;
        public const float EndScale = 0.25f;

        /// <summary>
        /// 按源 Reel 在 canvas 上复制飞行用图标，返回临时 RectTransform 列表（sprite 为 null 的轴跳过）。
        /// </summary>
        public static List<RectTransform> CreateIconsAtReels(RectTransform canvasRect, Image[] reelIcons)
        {
            var result = new List<RectTransform>();
            if (canvasRect == null || reelIcons == null)
                return result;

            var cam = StarFlyFx.ResolveCanvasCamera(canvasRect);

            for (int i = 0; i < reelIcons.Length; i++)
            {
                var srcImg = reelIcons[i];
                if (srcImg == null || srcImg.sprite == null)
                    continue;

                var srcRt = srcImg.rectTransform;
                var screenPos = RectTransformUtility.WorldToScreenPoint(cam, srcRt.position);

                var fxGo = new GameObject("SlotAttrFlyIcon_" + i, typeof(RectTransform));
                var fxRt = fxGo.GetComponent<RectTransform>();
                fxRt.SetParent(canvasRect, false);
                fxRt.anchorMin = fxRt.anchorMax = new Vector2(0.5f, 0.5f);
                fxRt.pivot = new Vector2(0.5f, 0.5f);
                fxRt.sizeDelta = srcRt.rect.size;
                fxRt.localScale = Vector3.one;

                var fxImage = fxGo.AddComponent<Image>();
                fxImage.sprite = srcImg.sprite;
                fxImage.preserveAspect = srcImg.preserveAspect;
                fxImage.color = srcImg.color;
                fxImage.raycastTarget = false;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screenPos, cam, out var localPos);
                fxRt.anchoredPosition = localPos;
                fxRt.SetAsLastSibling();

                result.Add(fxRt);
            }

            return result;
        }

        /// <summary>
        /// 延迟后飞行并销毁（icons 须已挂在 canvas 上）。
        /// </summary>
        public static void Launch(RectTransform canvasRect, IList<RectTransform> flyIcons, RectTransform targetRect)
        {
            if (canvasRect == null)
            {
                UnityEngine.Debug.LogWarning("[SlotAttrFlyFx] canvasRect 为空，跳过飞行特效。");
                DestroyIcons(flyIcons);
                return;
            }

            if (targetRect == null)
            {
                UnityEngine.Debug.LogWarning("[SlotAttrFlyFx] targetRect 为空，跳过飞行特效。");
                DestroyIcons(flyIcons);
                return;
            }

            if (flyIcons == null || flyIcons.Count == 0)
                return;

            var cam = StarFlyFx.ResolveCanvasCamera(canvasRect);
            var targetScreen = RectTransformUtility.WorldToScreenPoint(cam, targetRect.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, targetScreen, cam, out var targetLocal);

            var starts = new Vector2[flyIcons.Count];
            for (int i = 0; i < flyIcons.Count; i++)
            {
                var rt = flyIcons[i];
                starts[i] = rt != null ? rt.anchoredPosition : Vector2.zero;
            }

            var runnerGo = new GameObject("SlotAttrFlyFxRunner", typeof(RectTransform));
            var runnerRt = runnerGo.GetComponent<RectTransform>();
            runnerRt.SetParent(canvasRect, false);
            runnerRt.SetAsLastSibling();

            var runner = runnerGo.AddComponent<SlotAttrFlyFxRunner>();
            runner.StartCoroutine(runner.FlyRoutine(
                flyIcons, starts, targetLocal, DelayBeforeFlySec, FlyDurationSec, EndScale));
        }

        private static void DestroyIcons(IList<RectTransform> flyIcons)
        {
            if (flyIcons == null)
                return;
            for (int i = 0; i < flyIcons.Count; i++)
            {
                if (flyIcons[i] != null)
                    Object.Destroy(flyIcons[i].gameObject);
            }
        }

        private sealed class SlotAttrFlyFxRunner : MonoBehaviour
        {
            public IEnumerator FlyRoutine(
                IList<RectTransform> flyIcons,
                Vector2[] starts,
                Vector2 targetLocal,
                float delay,
                float duration,
                float endScale)
            {
                if (delay > 0f)
                    yield return new WaitForSeconds(delay);

                float t = 0f;
                var endScaleVec = Vector3.one * endScale;

                while (t < duration)
                {
                    t += Time.deltaTime;
                    float p = Mathf.Clamp01(t / duration);
                    float eased = 1f - Mathf.Pow(1f - p, 3f);

                    for (int i = 0; i < flyIcons.Count; i++)
                    {
                        var rt = flyIcons[i];
                        if (rt == null)
                            continue;
                        rt.anchoredPosition = Vector2.LerpUnclamped(starts[i], targetLocal, eased);
                        rt.localScale = Vector3.LerpUnclamped(Vector3.one, endScaleVec, eased);
                    }

                    yield return null;
                }

                for (int i = 0; i < flyIcons.Count; i++)
                {
                    if (flyIcons[i] != null)
                        Destroy(flyIcons[i].gameObject);
                }

                Destroy(gameObject);
            }
        }
    }
}
