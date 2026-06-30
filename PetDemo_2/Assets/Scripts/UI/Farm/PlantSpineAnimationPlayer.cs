// SPEC §9.1 (v3.94)：农田 Spine 植物动画播放（Grow / idle / work_1 / work_2）。
using System;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace PetDemo.UI.Farm
{
    public sealed class PlantSpineAnimationPlayer
    {
        private const string IdleClip = "idle";
        private const string GrowClip = "Grow";
        private const string Work1Clip = "work_1";
        private const string Work2Clip = "work_2";

        private SkeletonGraphic boundGraphic;
        private Action pendingComplete;

        public bool IsOneShotPlaying => pendingComplete != null;

        public void PlayIdle(SkeletonGraphic graphic)
        {
            if (!Bind(graphic))
                return;
            PlayLoop(IdleClip);
        }

        public void PlayGrowOnce(SkeletonGraphic graphic, Action onComplete)
        {
            if (!Bind(graphic))
            {
                onComplete?.Invoke();
                return;
            }
            PlayOnce(GrowClip, onComplete);
        }

        public void PlayWork1Once(SkeletonGraphic graphic, Action onComplete)
        {
            if (!Bind(graphic))
            {
                onComplete?.Invoke();
                return;
            }
            PlayOnce(Work1Clip, onComplete);
        }

        public void PlayWork2Loop(SkeletonGraphic graphic)
        {
            if (!Bind(graphic))
                return;
            PlayLoop(Work2Clip);
        }

        public void ClearTrack(SkeletonGraphic graphic)
        {
            if (graphic == null || graphic.AnimationState == null)
                return;
            graphic.AnimationState.ClearTracks();
            pendingComplete = null;
        }

        public void Unbind(SkeletonGraphic graphic)
        {
            if (boundGraphic == graphic)
            {
                pendingComplete = null;
                boundGraphic = null;
            }
        }

        private bool Bind(SkeletonGraphic graphic)
        {
            if (graphic == null || !graphic.IsValid)
                return false;
            boundGraphic = graphic;
            return true;
        }

        private void PlayLoop(string clipName)
        {
            pendingComplete = null;
            var resolved = ResolveClipName(boundGraphic.SkeletonData, clipName);
            if (string.IsNullOrEmpty(resolved))
            {
                UnityEngine.Debug.LogWarning(
                    "[PlantSpineAnimationPlayer] 动画未找到，回退 idle：" + clipName);
                resolved = ResolveClipName(boundGraphic.SkeletonData, IdleClip);
                if (string.IsNullOrEmpty(resolved))
                    return;
            }

            try
            {
                boundGraphic.AnimationState.SetAnimation(0, resolved, true);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[PlantSpineAnimationPlayer] 循环动画失败：" + e.Message);
            }
        }

        private void PlayOnce(string clipName, Action onComplete)
        {
            pendingComplete = onComplete;
            var resolved = ResolveClipName(boundGraphic.SkeletonData, clipName);
            if (string.IsNullOrEmpty(resolved))
            {
                UnityEngine.Debug.LogWarning(
                    "[PlantSpineAnimationPlayer] 动画未找到，跳过单次播放：" + clipName);
                InvokePendingComplete();
                PlayIdle(boundGraphic);
                return;
            }

            try
            {
                var entry = boundGraphic.AnimationState.SetAnimation(0, resolved, false);
                if (entry == null)
                {
                    InvokePendingComplete();
                    PlayIdle(boundGraphic);
                    return;
                }

                entry.Complete += OnTrackComplete;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[PlantSpineAnimationPlayer] 单次动画失败：" + e.Message);
                InvokePendingComplete();
                PlayIdle(boundGraphic);
            }
        }

        private void OnTrackComplete(TrackEntry entry)
        {
            if (entry != null)
                entry.Complete -= OnTrackComplete;
            InvokePendingComplete();
        }

        private void InvokePendingComplete()
        {
            var cb = pendingComplete;
            pendingComplete = null;
            cb?.Invoke();
        }

        private static string ResolveClipName(SkeletonData data, string clipName)
        {
            var anim = FindAnimationCaseInsensitive(data, clipName);
            return anim?.Name;
        }

        private static Spine.Animation FindAnimationCaseInsensitive(SkeletonData data, string animationName)
        {
            if (data == null || string.IsNullOrEmpty(animationName))
                return null;
            var exact = data.FindAnimation(animationName);
            if (exact != null)
                return exact;
            var anims = data.Animations;
            if (anims == null)
                return null;
            for (int i = 0; i < anims.Count; i++)
            {
                var a = anims.Items[i];
                if (a != null && string.Equals(a.Name, animationName, StringComparison.OrdinalIgnoreCase))
                    return a;
            }
            return null;
        }
    }
}
