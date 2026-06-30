// SPEC §9.8.14 (v3.94/v3.98/v3.99)：家园风效循环调度（停留 30s → 播放 10s → 重计）。
using System;
using System.Collections;
using UnityEngine;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class JiaYuanWindEffectController : MonoBehaviour
    {
        public const string DefaultWindPrefabResourcePath = "SpecialEffects/Wind";

        public static event Action<bool> OnWindStateChanged;

        [SerializeField] private float dwellSeconds = 30f;
        [SerializeField] private float windDurationSeconds = 10f;
        [SerializeField] private string windPrefabResourcePath = DefaultWindPrefabResourcePath;
        [SerializeField] private int particleSortingOrder = 5;

        private RectTransform screenRootParent;
        private RectTransform effectHolderRt;
        private bool jiaYuanTabActive;
        private float dwellTimer;
        private bool windPlaying;
        private GameObject windInstance;
        private Coroutine windRoutine;

        public bool IsWindActive => windPlaying;

        /// <summary>风效挂在 JiaYuanWorldScreen 根层最后子节点（不随 WorldContent 平移，且在视口内容之上）。</summary>
        public void Initialize(RectTransform screenRoot)
        {
            screenRootParent = screenRoot;
        }

        public void SetJiaYuanTabActive(bool active)
        {
            if (jiaYuanTabActive == active)
                return;

            jiaYuanTabActive = active;
            if (!jiaYuanTabActive)
            {
                dwellTimer = 0f;
                StopWindImmediate();
            }
        }

        private void Update()
        {
            if (!jiaYuanTabActive || windPlaying)
                return;

            dwellTimer += Time.deltaTime;
            if (dwellTimer < dwellSeconds)
                return;

            if (windRoutine != null)
                StopCoroutine(windRoutine);
            windRoutine = StartCoroutine(WindPlaybackRoutine());
        }

        private IEnumerator WindPlaybackRoutine()
        {
            windPlaying = true;
            dwellTimer = 0f;
            EnsureWindInstance();
            if (windInstance != null)
            {
                if (effectHolderRt != null)
                    effectHolderRt.SetAsLastSibling();
                windInstance.SetActive(true);
                RestartParticleSystems(windInstance);
            }

            OnWindStateChanged?.Invoke(true);

            yield return new WaitForSeconds(Mathf.Max(0.1f, windDurationSeconds));

            if (windInstance != null)
                windInstance.SetActive(false);

            windPlaying = false;
            OnWindStateChanged?.Invoke(false);
            dwellTimer = 0f;
            windRoutine = null;
        }

        private void StopWindImmediate()
        {
            if (windRoutine != null)
            {
                StopCoroutine(windRoutine);
                windRoutine = null;
            }

            if (!windPlaying)
                return;

            if (windInstance != null)
                windInstance.SetActive(false);

            windPlaying = false;
            OnWindStateChanged?.Invoke(false);
        }

        private void EnsureWindInstance()
        {
            if (windInstance != null || screenRootParent == null)
                return;

            var path = string.IsNullOrWhiteSpace(windPrefabResourcePath)
                ? DefaultWindPrefabResourcePath
                : windPrefabResourcePath.Trim();

            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[JiaYuanWindEffectController] 风效预制体未找到：Resources/" + path
                    + " — 请确认 Assets/Resources/SpecialEffects/Wind.prefab 已存在（可由 wind.unitypackage 解压导入）。");
                return;
            }

            effectHolderRt = CreateEffectHolder(screenRootParent);
            effectHolderRt.SetAsLastSibling();
            windInstance = Instantiate(prefab, effectHolderRt, false);
            windInstance.name = "WindEffect";
            windInstance.transform.localPosition = Vector3.zero;
            windInstance.transform.localRotation = Quaternion.identity;
            windInstance.transform.localScale = Vector3.one;
            ConfigureParticleSystemsForUiCamera(windInstance);
            windInstance.SetActive(false);
            DisableRaycasts(windInstance);
        }

        private RectTransform CreateEffectHolder(RectTransform parent)
        {
            var holderGo = new GameObject("WindEffectHolder", typeof(RectTransform));
            var holderRt = holderGo.GetComponent<RectTransform>();
            holderRt.SetParent(parent, false);
            StretchFullScreen(holderRt);
            return holderRt;
        }

        private void ConfigureParticleSystemsForUiCamera(GameObject root)
        {
            if (root == null)
                return;

            var systems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (ps == null)
                    continue;

                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;

                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer == null)
                    continue;
                renderer.sortingLayerName = "Default";
                renderer.sortingOrder = particleSortingOrder + i;
                renderer.alignment = ParticleSystemRenderSpace.View;
            }
        }

        private static void RestartParticleSystems(GameObject root)
        {
            if (root == null)
                return;

            var systems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                if (ps == null)
                    continue;
                ps.Clear(true);
                ps.Play(true);
            }
        }

        private static void StretchFullScreen(RectTransform rt)
        {
            if (rt == null)
                return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        private static void DisableRaycasts(GameObject root)
        {
            if (root == null)
                return;
            var graphics = root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
                graphics[i].raycastTarget = false;
        }

        private void OnDestroy()
        {
            if (windRoutine != null)
                StopCoroutine(windRoutine);
            if (windPlaying)
                OnWindStateChanged?.Invoke(false);
            if (windInstance != null)
                Destroy(windInstance);
            if (effectHolderRt != null)
                Destroy(effectHolderRt.gameObject);
        }
    }
}
