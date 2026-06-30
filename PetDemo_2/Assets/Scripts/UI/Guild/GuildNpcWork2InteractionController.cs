// SPEC §9.8.9.9：公会场景 Npc_1 动作图标 work_2 编排 — Player 单次 work_2 → 0.5s → Npc 单次 work_2 → 恢复待机。
using System.Collections;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class GuildNpcWork2InteractionController : MonoBehaviour
    {
        private const string Work2Clip = "work_2";
        private const string StandbyClip = "standby_1";
        private const float NpcWork2DelaySec = 0.5f;
        private const float ActionAnimTimeoutSec = 12f;

        private static readonly string[] StandbyFallbacks = { "animation", "idle", "exclusive_2" };

        private GuildPlayerController playerController;
        private GuildNpcFollowController followController;
        private bool initialized;
        private bool isPlaying;

        public void Initialize(GuildPlayerController player, GuildNpcFollowController follow)
        {
            playerController = player;
            followController = follow;
            initialized = true;
        }

        public void TryPlayWork2WithNpc(GuildNpcMarker npc)
        {
            if (!initialized || isPlaying || npc == null)
                return;

            var playerSkeleton = playerController != null ? playerController.PlayerSkeleton : null;
            var npcSkeleton = npc.NpcSkeleton;
            if (playerSkeleton == null && npcSkeleton == null)
                return;

            StartCoroutine(Work2SequenceRoutine(npc, playerSkeleton, npcSkeleton));
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (isPlaying)
            {
                playerController?.SetAnimationLocked(false);
                isPlaying = false;
            }
        }

        private IEnumerator Work2SequenceRoutine(
            GuildNpcMarker npc,
            SkeletonGraphic playerSkeleton,
            SkeletonGraphic npcSkeleton)
        {
            isPlaying = true;
            playerController?.SetAnimationLocked(true);
            followController?.SetActionAnimationLocked(npc, true);

            TrackEntry playerEntry = null;
            TrackEntry npcEntry = null;

            try
            {
                playerEntry = GuildSpineCharacterBuilder.PlayOnce(playerSkeleton, Work2Clip);
                if (playerEntry == null && npcSkeleton == null)
                    yield break;

                yield return new WaitForSeconds(NpcWork2DelaySec);

                npcEntry = GuildSpineCharacterBuilder.PlayOnce(npcSkeleton, Work2Clip);

                float elapsed = 0f;
                while (elapsed < ActionAnimTimeoutSec)
                {
                    bool playerDone = playerEntry == null || playerEntry.IsComplete;
                    bool npcDone = npcEntry == null || npcEntry.IsComplete;
                    if (playerDone && npcDone)
                        break;
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
            finally
            {
                followController?.SetActionAnimationLocked(npc, false);
                playerController?.SetAnimationLocked(false);
                playerController?.RefreshAnimation();

                if (npcSkeleton != null)
                {
                    if (followController != null)
                        followController.RefreshMarkerAnimation(npc);
                    else
                        GuildSpineCharacterBuilder.PlayLoop(npcSkeleton, StandbyClip, StandbyFallbacks);
                }

                isPlaying = false;
            }
        }
    }
}
