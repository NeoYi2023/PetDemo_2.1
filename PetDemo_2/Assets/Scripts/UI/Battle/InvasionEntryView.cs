// SPEC §12.2：怪物入侵顶部入口图标 + 倒计时文本。
// v3.33：kInvasionEntryRootEnabled=false 时不构建 InvasionEntryRoot / 闪屏层（SPEC §12.2）。
// - Countdown 阶段：图标 RuQin_0，按钮 interactable=false，下方显示倒计时文本（fontSize 32）。
// - Invading 阶段：图标 RuQin_1，按钮 interactable=true，倒计时文本隐藏；点击调用 OpenBattle。
// - InBattle 阶段：图标保持 RuQin_1（视觉无差），按钮临时禁用避免重入。
using System.Collections;
using PetDemo.Battle;
using PetDemo.Core;
using PetDemo.UI.Farm;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI.Battle
{
    [DisallowMultipleComponent]
    public class InvasionEntryView : MonoBehaviour
    {
        /// <summary>
        /// false：不构建顶部入口与切态闪屏（Demo 默认，SPEC §12.2 / v3.33）。
        /// </summary>
        public const bool kInvasionEntryRootEnabled = false;

        public const string ResIconCountdown = "AirUI/RuQin_0";
        public const string ResIconInvading = "AirUI/RuQin_1";

        public struct InvadingFxConfig
        {
            public float breathSpeed;
            public float breathAmplitude;
            public float blinkSpeed;
            public float minAlpha;

            public static InvadingFxConfig Default => new InvadingFxConfig
            {
                breathSpeed = 2.1f,
                breathAmplitude = 0.10f,
                blinkSpeed = 5.2f,
                minAlpha = 0.55f
            };
        }

        private InvasionService service;
        private Image iconImage;
        private Button button;
        private Text countdownText;

        private Sprite spriteCountdown;
        private Sprite spriteInvading;
        private Coroutine invadingFxCoroutine;
        private Coroutine phaseFlashCoroutine;
        private Image fullScreenFlashImage;
        private Vector3 baseIconScale = Vector3.one;
        private Color baseIconColor = Color.white;
        private bool wasInvadingVisual;
        private InvadingFxConfig fxConfig = InvadingFxConfig.Default;
        private const int PhaseSwitchFlashCount = 2;
        private const int PhaseSwitchFlashFrames = 20;

        public static InvasionEntryView BuildInto(
            RectTransform canvasRect,
            InvasionService svc,
            InvadingFxConfig? invadingFxConfig = null)
        {
            if (canvasRect == null || svc == null)
                return null;
            if (!kInvasionEntryRootEnabled)
                return null;

            var spriteCd = Resources.Load<Sprite>(ResIconCountdown);
            var spriteInv = Resources.Load<Sprite>(ResIconInvading);
            if (spriteCd == null || spriteInv == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[InvasionEntryView] 缺少图标资源 RuQin_0 / RuQin_1（Resources/AirUI/）。入口将不可见。");
            }

            var rootRt = CreateChildRect(
                canvasRect, "InvasionEntryRoot",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, Vector2.zero);

            var iconRt = CreateChildRect(
                rootRt, "InvasionEntryButton",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -150f), new Vector2(150f, 150f));
            var img = iconRt.gameObject.AddComponent<Image>();
            img.sprite = spriteCd != null ? spriteCd : spriteInv;
            img.preserveAspect = true;
            img.raycastTarget = true;
            var btn = iconRt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;

            var textRt = CreateChildRect(
                rootRt, "InvasionCountdownText",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -260f), new Vector2(260f, 60f));
            var text = textRt.gameObject.AddComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontSize = 32;
            text.font = FarmGridView.LoadBuiltinFont();
            text.raycastTarget = false;
            text.text = "";

            var flashRt = CreateChildRect(
                canvasRect, "InvasionPhaseFlashOverlay",
                Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            StretchFull(flashRt);
            flashRt.SetAsLastSibling();
            var flashImage = flashRt.gameObject.AddComponent<Image>();
            flashImage.color = new Color(1f, 0f, 0f, 0f);
            flashImage.raycastTarget = false;

            var view = rootRt.gameObject.AddComponent<InvasionEntryView>();
            view.service = svc;
            view.iconImage = img;
            view.button = btn;
            view.countdownText = text;
            view.spriteCountdown = spriteCd;
            view.spriteInvading = spriteInv;
            view.fxConfig = invadingFxConfig ?? InvadingFxConfig.Default;
            view.fullScreenFlashImage = flashImage;
            if (img != null)
            {
                view.baseIconScale = img.rectTransform.localScale;
                view.baseIconColor = img.color;
            }

            btn.onClick.AddListener(view.OnIconClicked);
            view.SubscribeEvents();
            view.ApplyPhase(svc.GetPhase());
            view.ApplyCountdown(svc.GetCountdownRemaining());
            return view;
        }

        private void SubscribeEvents()
        {
            if (service == null)
                return;
            service.OnPhaseChanged += ApplyPhase;
            service.OnCountdownTick += ApplyCountdown;
        }

        private void OnDestroy()
        {
            if (service != null)
            {
                service.OnPhaseChanged -= ApplyPhase;
                service.OnCountdownTick -= ApplyCountdown;
            }
            StopInvadingFx();
            StopPhaseSwitchFlash();
        }

        private void OnIconClicked()
        {
            if (service == null)
                return;
            if (service.GetPhase() != InvasionPhase.Invading)
                return;
            service.OpenBattle();
        }

        private void ApplyPhase(InvasionPhase next)
        {
            bool isInvadingVisual = next != InvasionPhase.Countdown;
            if (iconImage != null)
            {
                iconImage.sprite = isInvadingVisual
                    ? spriteInvading
                    : spriteCountdown;
            }

            if (isInvadingVisual && !wasInvadingVisual)
            {
                StartInvadingFx();
                StartPhaseSwitchFlash();
            }
            else if (!isInvadingVisual && wasInvadingVisual)
            {
                StopInvadingFx();
                StopPhaseSwitchFlash();
            }
            wasInvadingVisual = isInvadingVisual;

            if (button != null)
                button.interactable = next == InvasionPhase.Invading;

            if (countdownText != null)
                countdownText.gameObject.SetActive(next == InvasionPhase.Countdown);
        }

        private void StartInvadingFx()
        {
            if (iconImage == null)
                return;
            StopInvadingFx();
            invadingFxCoroutine = StartCoroutine(PlayInvadingFxLoop());
        }

        private void StopInvadingFx()
        {
            if (invadingFxCoroutine != null)
            {
                StopCoroutine(invadingFxCoroutine);
                invadingFxCoroutine = null;
            }

            if (iconImage == null)
                return;

            iconImage.rectTransform.localScale = baseIconScale;
            iconImage.color = baseIconColor;
        }

        private void StartPhaseSwitchFlash()
        {
            if (fullScreenFlashImage == null)
                return;
            StopPhaseSwitchFlash();
            phaseFlashCoroutine = StartCoroutine(PlayPhaseSwitchFlash());
        }

        private void StopPhaseSwitchFlash()
        {
            if (phaseFlashCoroutine != null)
            {
                StopCoroutine(phaseFlashCoroutine);
                phaseFlashCoroutine = null;
            }

            if (fullScreenFlashImage == null)
                return;
            var color = fullScreenFlashImage.color;
            color.a = 0f;
            fullScreenFlashImage.color = color;
        }

        private IEnumerator PlayPhaseSwitchFlash()
        {
            if (fullScreenFlashImage == null)
                yield break;

            for (int flashIndex = 0; flashIndex < PhaseSwitchFlashCount; flashIndex++)
            {
                for (int frame = 0; frame < PhaseSwitchFlashFrames; frame++)
                {
                    float t = (frame + 1f) / PhaseSwitchFlashFrames;
                    float peak = 1f - Mathf.Abs(t * 2f - 1f);
                    var color = fullScreenFlashImage.color;
                    color.a = Mathf.Lerp(0f, 0.5f, peak);
                    fullScreenFlashImage.color = color;
                    yield return null;
                }

                var clear = fullScreenFlashImage.color;
                clear.a = 0f;
                fullScreenFlashImage.color = clear;
                yield return null;
            }

            phaseFlashCoroutine = null;
        }

        private IEnumerator PlayInvadingFxLoop()
        {
            if (iconImage == null)
                yield break;

            float breathSpeed = Mathf.Max(0.01f, fxConfig.breathSpeed);
            float breathAmp = Mathf.Max(0f, fxConfig.breathAmplitude);
            float blinkSpeed = Mathf.Max(0.01f, fxConfig.blinkSpeed);
            float minAlpha = Mathf.Clamp01(fxConfig.minAlpha);
            float maxAlpha = Mathf.Clamp01(baseIconColor.a);
            if (minAlpha > maxAlpha)
                minAlpha = maxAlpha;

            while (true)
            {
                float t = Time.unscaledTime;
                float breathWave = (Mathf.Sin(t * breathSpeed) + 1f) * 0.5f;
                float blinkWave = (Mathf.Sin(t * blinkSpeed) + 1f) * 0.5f;

                float scaleMul = 1f + (breathWave * 2f - 1f) * breathAmp;
                iconImage.rectTransform.localScale = baseIconScale * scaleMul;

                Color color = baseIconColor;
                color.a = Mathf.Lerp(minAlpha, maxAlpha, blinkWave);
                iconImage.color = color;

                yield return null;
            }
        }

        private void ApplyCountdown(float remainingSeconds)
        {
            if (countdownText == null)
                return;
            if (service != null && service.GetPhase() != InvasionPhase.Countdown)
            {
                countdownText.text = "";
                return;
            }

            int totalSec = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
            if (totalSec >= 60)
            {
                int mm = totalSec / 60;
                int ss = totalSec % 60;
                countdownText.text = string.Format("{0:00}:{1:00}", mm, ss);
            }
            else
            {
                countdownText.text = totalSec.ToString();
            }
        }

        private static RectTransform CreateChildRect(
            RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
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
