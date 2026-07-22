// SPEC §9.8.19.2.2 / §9.8.19.6（v3.263）：庄园悬浮框接近显隐控制。
using System.Collections.Generic;
using UnityEngine;

namespace PetDemo.UI.Companion
{
    [DisallowMultipleComponent]
    public sealed class ManorFloatingFrameProximityController : MonoBehaviour
    {
        private const float PollIntervalSeconds = 0.1f;

        private readonly List<ManorFloatingFrameMarker> frames =
            new List<ManorFloatingFrameMarker>();
        private RectTransform playerRt;
        private RectTransform worldContentRt;
        private float nextPollTime;
        private bool initialized;

        public void Initialize(
            RectTransform player,
            RectTransform worldContent,
            IList<ManorFloatingFrameMarker> markers)
        {
            playerRt = player;
            worldContentRt = worldContent;
            frames.Clear();

            if (markers != null)
            {
                for (int i = 0; i < markers.Count; i++)
                {
                    var marker = markers[i];
                    if (marker == null)
                        continue;
                    frames.Add(marker);
                    marker.InitializeHidden();
                }
            }

            initialized = true;
            nextPollTime = 0f;
        }

        private void Update()
        {
            if (!initialized || playerRt == null || worldContentRt == null)
                return;
            if (Time.unscaledTime < nextPollTime)
                return;

            nextPollTime = Time.unscaledTime + PollIntervalSeconds;
            Vector2 playerPos = GuildSceneGeometry.PointInContentSpace(playerRt, worldContentRt);

            for (int i = 0; i < frames.Count; i++)
            {
                var marker = frames[i];
                if (marker == null)
                    continue;

                Vector2 framePos = GuildSceneGeometry.PointInContentSpace(marker.Rt, worldContentRt);
                float radius = marker.ShowRadius;
                marker.SetVisible((framePos - playerPos).sqrMagnitude <= radius * radius);
            }
        }

        private void OnDisable()
        {
            HideAll();
        }

        private void HideAll()
        {
            for (int i = 0; i < frames.Count; i++)
            {
                if (frames[i] != null)
                    frames[i].SetVisible(false);
            }
            nextPollTime = 0f;
        }
    }
}
