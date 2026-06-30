// SPEC §9.8.9.4 / §9.8.9.6 ⑤：公会场景接近检测 — 0.1s 轮询主角与建筑/NPC 的
// 平方距离，进入半径显示名牌、离开隐藏。
using System.Collections.Generic;
using UnityEngine;

namespace PetDemo.UI
{
    [DisallowMultipleComponent]
    public sealed class GuildProximityController : MonoBehaviour
    {
        private const float PollIntervalSeconds = 0.1f;

        private RectTransform playerRt;
        private RectTransform worldContentRt;
        private readonly List<GuildBuildingMarker> buildings = new List<GuildBuildingMarker>();
        private readonly List<GuildNpcMarker> npcs = new List<GuildNpcMarker>();
        private float nextPollTime;
        private bool initialized;

        public void Initialize(
            RectTransform player,
            RectTransform worldContent,
            IList<GuildBuildingMarker> buildingMarkers,
            IList<GuildNpcMarker> npcMarkers)
        {
            playerRt = player;
            worldContentRt = worldContent;

            buildings.Clear();
            if (buildingMarkers != null)
            {
                for (int i = 0; i < buildingMarkers.Count; i++)
                {
                    if (buildingMarkers[i] != null)
                        buildings.Add(buildingMarkers[i]);
                }
            }

            npcs.Clear();
            if (npcMarkers != null)
            {
                for (int i = 0; i < npcMarkers.Count; i++)
                {
                    if (npcMarkers[i] != null)
                        npcs.Add(npcMarkers[i]);
                }
            }

            initialized = true;
            nextPollTime = 0f;
        }

        private void OnDisable()
        {
            // 离开公会 Tab 时隐藏全部名牌，避免下次进入残留。
            for (int i = 0; i < buildings.Count; i++)
                if (buildings[i] != null) buildings[i].SetPlateVisible(false);
            for (int i = 0; i < npcs.Count; i++)
                if (npcs[i] != null) npcs[i].SetPlateVisible(false);
        }

        private void Update()
        {
            if (!initialized || playerRt == null || worldContentRt == null)
                return;
            if (Time.unscaledTime < nextPollTime)
                return;
            nextPollTime = Time.unscaledTime + PollIntervalSeconds;

            var playerPos = GuildSceneGeometry.PointInContentSpace(playerRt, worldContentRt);

            for (int i = 0; i < buildings.Count; i++)
            {
                var marker = buildings[i];
                if (marker == null)
                    continue;
                var markerPos = GuildSceneGeometry.PointInContentSpace(marker.Rt, worldContentRt);
                float r = marker.InteractRadius;
                marker.SetPlateVisible((markerPos - playerPos).sqrMagnitude <= r * r);
            }

            for (int i = 0; i < npcs.Count; i++)
            {
                var marker = npcs[i];
                if (marker == null)
                    continue;
                var markerPos = GuildSceneGeometry.PointInContentSpace(marker.Rt, worldContentRt);
                float r = marker.InteractRadius;
                marker.SetPlateVisible((markerPos - playerPos).sqrMagnitude <= r * r);
            }
        }
    }
}
