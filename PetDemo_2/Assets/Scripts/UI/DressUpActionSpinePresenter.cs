// SPEC §9.14.9（v3.153）：装扮 Tab2 动作页 Spine 预览（LangRen / LangMeiRen）。
using System;
using System.Collections;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.UI
{
    /// <summary>
    /// Tab2 点击道具后在 PlayerRole / FriendRole 挂点构建 Spine UI，并驱动待机 / 动作预览。
    /// </summary>
    public sealed class DressUpActionSpinePresenter
    {
        public const string ActionSpineChildName = "ActionSpine";
        private const string SkeletonGraphicShaderName = "Spine/SkeletonGraphic";
        private const string StandbyClip = "standby_1";
        private const string Work2Clip = "work_2";
        private const float FriendWork2DelaySec = 0.5f;
        private const float ActionAnimTimeoutSec = 12f;

        private const string PlayerSkeletonEditorPath =
            "Assets/Scenes/Air/LangRen/Role_cslangren/Role_cslangren_SkeletonData.asset";
        private const string FriendSkeletonEditorPath =
            "Assets/Scenes/Air/LangMeiRen/Role_langmeiren/Role_langmeiren_SkeletonData.asset";
        private const string PlayerPrefabResourcesPath = "Prefabs/Air/Hero_Role_cunmin";

        private static readonly string[] StandbyFallbacks = { "animation", "idle", "exclusive_2" };
        private static readonly Vector2 GraphicSize = GuildSpineCharacterBuilder.DefaultGraphicSize;
        private static readonly Vector2 RoleAnchoredPosition = new Vector2(0f, -218f);
        private static readonly Vector3 RoleLocalScale = new Vector3(0.7f, 0.7f, 1f);

        private static readonly Dictionary<string, Action<DressUpActionSpinePresenter, MonoBehaviour>> ActionHandlers =
            new Dictionary<string, Action<DressUpActionSpinePresenter, MonoBehaviour>>(StringComparer.Ordinal)
            {
                { "dz_001", (presenter, host) => presenter.PlayWork2Sequence(host) },
            };

        private readonly RectTransform playerMount;
        private readonly RectTransform friendMount;
        private readonly Image playerImage;
        private readonly Image friendImage;
        private readonly SkeletonDataAsset playerSkeletonOverride;
        private readonly SkeletonDataAsset friendSkeletonOverride;

        private SkeletonGraphic playerSpine;
        private SkeletonGraphic friendSpine;
        private Coroutine actionRoutine;
        private MonoBehaviour coroutineHost;
        private bool isActive;
        private Vector3 playerMountBaseScale = Vector3.one;
        private bool playerMountScaleCaptured;

        public bool IsActive => isActive;

        public DressUpActionSpinePresenter(
            RectTransform playerMount,
            RectTransform friendMount,
            Image playerImage,
            Image friendImage,
            SkeletonDataAsset playerSkeletonOverride,
            SkeletonDataAsset friendSkeletonOverride)
        {
            this.playerMount = playerMount;
            this.friendMount = friendMount;
            this.playerImage = playerImage;
            this.friendImage = friendImage;
            this.playerSkeletonOverride = playerSkeletonOverride;
            this.friendSkeletonOverride = friendSkeletonOverride;
        }

        public bool EnsureBuilt()
        {
            bool playerOk = EnsureRoleBuilt(
                playerMount, playerImage, ResolvePlayerSkeletonData(), mirrorPlayer: true, ref playerSpine);
            bool friendOk = EnsureRoleBuilt(
                friendMount, friendImage, ResolveFriendSkeletonData(), mirrorPlayer: false, ref friendSpine);

            isActive = playerOk && friendOk;
            if (!isActive)
                Teardown();
            return isActive;
        }

        public void ShowStandby()
        {
            if (!isActive)
                return;

            StopActionRoutine();
            PlayLoop(playerSpine, StandbyClip, StandbyFallbacks);
            PlayLoop(friendSpine, StandbyClip, StandbyFallbacks);
        }

        public void PlayActionForItem(string itemId, MonoBehaviour host)
        {
            if (!isActive || host == null)
                return;

            ShowStandby();

            if (!string.IsNullOrEmpty(itemId) &&
                ActionHandlers.TryGetValue(itemId, out var handler))
            {
                handler(this, host);
            }
        }

        public void PlayWork2Sequence(MonoBehaviour host)
        {
            if (!isActive || host == null)
                return;

            StopActionRoutine();
            coroutineHost = host;
            actionRoutine = host.StartCoroutine(Work2SequenceRoutine());
        }

        public void Teardown()
        {
            StopActionRoutine();
            DestroySpineChild(playerMount, ref playerSpine);
            DestroySpineChild(friendMount, ref friendSpine);
            RestoreMountScale(playerMount);
            RestoreImage(playerImage);
            RestoreImage(friendImage);
            isActive = false;
            playerMountScaleCaptured = false;
        }

        private IEnumerator Work2SequenceRoutine()
        {
            var playerEntry = PlayOnce(playerSpine, Work2Clip);
            if (playerEntry == null)
            {
                ShowStandby();
                yield break;
            }

            yield return new WaitForSeconds(FriendWork2DelaySec);

            var friendEntry = PlayOnce(friendSpine, Work2Clip);

            float elapsed = 0f;
            while (elapsed < ActionAnimTimeoutSec)
            {
                bool playerDone = playerEntry == null || playerEntry.IsComplete;
                bool friendDone = friendEntry == null || friendEntry.IsComplete;
                if (playerDone && friendDone)
                    break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            ShowStandby();
        }

        private void StopActionRoutine()
        {
            if (actionRoutine != null && coroutineHost != null)
                coroutineHost.StopCoroutine(actionRoutine);
            actionRoutine = null;
            coroutineHost = null;
        }

        private bool EnsureRoleBuilt(
            RectTransform mount,
            Image portraitImage,
            SkeletonDataAsset dataAsset,
            bool mirrorPlayer,
            ref SkeletonGraphic spineGraphic)
        {
            if (mount == null)
                return false;

            if (spineGraphic != null && spineGraphic.IsValid)
            {
                if (portraitImage != null)
                    portraitImage.enabled = false;
                CapturePlayerMountScale(mount, mirrorPlayer);
                if (mirrorPlayer)
                    GuildSpineCharacterBuilder.SetFacing(mount, faceRight: true);
                return true;
            }

            DestroySpineChild(mount, ref spineGraphic);

            if (dataAsset == null)
            {
                UnityEngine.Debug.LogWarning("[DressUpActionSpinePresenter] 缺少 SkeletonDataAsset，回退静态立绘。");
                RestoreImage(portraitImage);
                return false;
            }

            var shader = Shader.Find(SkeletonGraphicShaderName);
            if (shader == null)
            {
                UnityEngine.Debug.LogWarning("[DressUpActionSpinePresenter] Shader 未找到：" + SkeletonGraphicShaderName);
                RestoreImage(portraitImage);
                return false;
            }

            var spineGo = new GameObject(ActionSpineChildName, typeof(RectTransform));
            var spineRt = spineGo.GetComponent<RectTransform>();
            spineRt.SetParent(mount, false);
            spineRt.anchorMin = spineRt.anchorMax = new Vector2(0.5f, 0.5f);
            spineRt.pivot = new Vector2(0.5f, 0.5f);
            spineRt.anchoredPosition = RoleAnchoredPosition;
            spineRt.sizeDelta = GraphicSize;
            spineRt.localRotation = Quaternion.identity;
            spineRt.localScale = RoleLocalScale;

            var uiMaterial = SkeletonGraphicUiMaterialFactory.CreateForPmaVertexColors(shader);
            spineGraphic = SkeletonGraphic.AddSkeletonGraphicComponent(spineGo, dataAsset, uiMaterial);
            if (spineGraphic == null || !spineGraphic.IsValid)
            {
                UnityEngine.Object.Destroy(spineGo);
                spineGraphic = null;
                UnityEngine.Debug.LogWarning("[DressUpActionSpinePresenter] SkeletonGraphic 构建失败，回退静态立绘。");
                RestoreImage(portraitImage);
                return false;
            }

            spineGraphic.raycastTarget = false;
            CapturePlayerMountScale(mount, mirrorPlayer);
            if (mirrorPlayer)
                GuildSpineCharacterBuilder.SetFacing(mount, faceRight: true);

            if (portraitImage != null)
                portraitImage.enabled = false;
            return true;
        }

        private SkeletonDataAsset ResolvePlayerSkeletonData()
        {
            if (playerSkeletonOverride != null)
                return playerSkeletonOverride;

#if UNITY_EDITOR
            var fromEditor = LoadSkeletonFromEditor(PlayerSkeletonEditorPath);
            if (fromEditor != null)
                return fromEditor;
#endif

            return ResolveSkeletonFromPrefabResources(PlayerPrefabResourcesPath);
        }

        private SkeletonDataAsset ResolveFriendSkeletonData()
        {
            if (friendSkeletonOverride != null)
                return friendSkeletonOverride;

#if UNITY_EDITOR
            return LoadSkeletonFromEditor(FriendSkeletonEditorPath);
#else
            return null;
#endif
        }

        private static SkeletonDataAsset ResolveSkeletonFromPrefabResources(string resourcesPath)
        {
            var prefab = Resources.Load<GameObject>(resourcesPath);
            if (prefab == null)
                return null;

            var probe = UnityEngine.Object.Instantiate(prefab);
            probe.SetActive(false);
            var srcAnim = probe.GetComponent<SkeletonAnimation>()
                ?? probe.GetComponentInChildren<SkeletonAnimation>(true);
            var dataAsset = srcAnim != null ? srcAnim.skeletonDataAsset : null;
            UnityEngine.Object.Destroy(probe);
            return dataAsset;
        }

#if UNITY_EDITOR
        private static SkeletonDataAsset LoadSkeletonFromEditor(string assetPath)
        {
            return UnityEditor.AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(assetPath);
        }
#endif

        private static void DestroySpineChild(RectTransform mount, ref SkeletonGraphic spineGraphic)
        {
            spineGraphic = null;
            if (mount == null)
                return;

            var child = mount.Find(ActionSpineChildName);
            if (child != null)
                UnityEngine.Object.Destroy(child.gameObject);
        }

        private void CapturePlayerMountScale(RectTransform mount, bool mirrorPlayer)
        {
            if (!mirrorPlayer || mount == null || playerMountScaleCaptured)
                return;
            playerMountBaseScale = mount.localScale;
            playerMountScaleCaptured = true;
        }

        private void RestoreMountScale(RectTransform mount)
        {
            if (mount == null || !playerMountScaleCaptured)
                return;
            mount.localScale = playerMountBaseScale;
        }

        private static void RestoreImage(Image portraitImage)
        {
            if (portraitImage != null)
                portraitImage.enabled = true;
        }

        private static void PlayLoop(SkeletonGraphic sg, string configured, params string[] fallbacks)
        {
            if (sg == null)
                return;
            GuildSpineCharacterBuilder.PlayLoop(sg, configured, fallbacks);
        }

        private static TrackEntry PlayOnce(SkeletonGraphic sg, string clipName)
        {
            if (sg == null || sg.Skeleton == null || sg.Skeleton.Data == null)
                return null;

            var resolved = ResolveClipName(sg.Skeleton.Data, clipName);
            if (string.IsNullOrEmpty(resolved))
            {
                UnityEngine.Debug.LogWarning(
                    "[DressUpActionSpinePresenter] 动画未找到，跳过单次播放：" + clipName);
                return null;
            }

            try
            {
                return sg.AnimationState.SetAnimation(0, resolved, false);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[DressUpActionSpinePresenter] 单次动画失败：" + e.Message);
                return null;
            }
        }

        private static string ResolveClipName(SkeletonData data, string clipName)
        {
            if (data == null || string.IsNullOrEmpty(clipName))
                return null;

            var exact = data.FindAnimation(clipName);
            if (exact != null)
                return exact.Name;

            var anims = data.Animations;
            if (anims == null)
                return null;

            for (int i = 0; i < anims.Count; i++)
            {
                var anim = anims.Items[i];
                if (anim != null && string.Equals(anim.Name, clipName, StringComparison.OrdinalIgnoreCase))
                    return anim.Name;
            }

            return null;
        }
    }
}
