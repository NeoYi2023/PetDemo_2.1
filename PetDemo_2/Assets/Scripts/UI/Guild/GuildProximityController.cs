// SPEC §9.8.9.4 / §9.8.9.6 / §9.8.9.11 / §9.8.9.12：公会场景接近检测 — 0.1s 轮询主角与建筑/NPC/响应区的
// 平方距离，进入半径显示名牌、离开隐藏；响应区沿边进入触发 Entered；全景模式暂停轮询并强制显名牌。
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
        private readonly List<GuildResponseAreaMarker> responseAreas = new List<GuildResponseAreaMarker>();
        private readonly Dictionary<GuildResponseAreaMarker, bool> wasInside = new Dictionary<GuildResponseAreaMarker, bool>();
        private float nextPollTime;
        private bool initialized;
        private bool panoramaMode;

        public void Initialize(
            RectTransform player,
            RectTransform worldContent,
            IList<GuildBuildingMarker> buildingMarkers,
            IList<GuildNpcMarker> npcMarkers,
            IList<GuildResponseAreaMarker> responseAreaMarkers = null)
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

            responseAreas.Clear();
            wasInside.Clear();
            if (responseAreaMarkers != null)
            {
                for (int i = 0; i < responseAreaMarkers.Count; i++)
                {
                    var marker = responseAreaMarkers[i];
                    if (marker == null)
                        continue;
                    responseAreas.Add(marker);
                    wasInside[marker] = false;
                }
            }

            initialized = true;
            nextPollTime = 0f;
        }

        /// <summary>SPEC §9.8.9.12：全景模式暂停接近轮询，进入时强制显示建筑/响应区名牌（NPC 隐藏）。</summary>
        public void SetPanoramaMode(bool active, float plateScaleCompensation = 1f, int plateFontSize = 0)
        {
            panoramaMode = active;
            if (active)
                ShowAllPlates(plateScaleCompensation, plateFontSize);
            else
                HideAllPlates();
        }

        private void ShowAllPlates(float plateScaleCompensation, int plateFontSize)
        {
            for (int i = 0; i < buildings.Count; i++)
            {
                var marker = buildings[i];
                if (marker == null)
                    continue;
                marker.SetPlateVisible(true, panoramaOverride: true);
                marker.ApplyPlateScaleCompensation(plateScaleCompensation);
                if (plateFontSize > 0)
                    marker.SetPanoramaNameFontSize(plateFontSize);
            }

            // SPEC §9.8.9.12：全景模式下 NPC 名牌不显示（仅建筑/响应区强制显示）。
            for (int i = 0; i < npcs.Count; i++)
            {
                var marker = npcs[i];
                if (marker == null)
                    continue;
                marker.ResetPlateScale();
                marker.SetPlateVisible(false);
            }

            for (int i = 0; i < responseAreas.Count; i++)
            {
                var marker = responseAreas[i];
                if (marker == null)
                    continue;
                marker.SetPlateVisible(true, panoramaOverride: true);
                marker.ApplyPlateScaleCompensation(plateScaleCompensation);
                if (plateFontSize > 0)
                    marker.SetPanoramaNameFontSize(plateFontSize);
            }
        }

        private void HideAllPlates()
        {
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] == null)
                    continue;
                buildings[i].ResetPlateScale();
                buildings[i].SetPlateVisible(false);
            }

            for (int i = 0; i < npcs.Count; i++)
            {
                if (npcs[i] == null)
                    continue;
                npcs[i].ExitPanoramaPlateState();
            }

            for (int i = 0; i < responseAreas.Count; i++)
            {
                var marker = responseAreas[i];
                if (marker == null)
                    continue;
                marker.ResetPlateScale();
                marker.SetPlateVisible(false);
                marker.ResetVisitState();
                wasInside[marker] = false;
            }

            nextPollTime = 0f;
        }

        private void OnDisable()
        {
            panoramaMode = false;
            HideAllPlates();
        }

        private void Update()
        {
            if (!initialized || panoramaMode || playerRt == null || worldContentRt == null)
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

            for (int i = 0; i < responseAreas.Count; i++)
            {
                var marker = responseAreas[i];
                if (marker == null)
                    continue;
                var markerPos = GuildSceneGeometry.PointInContentSpace(marker.Rt, worldContentRt);
                float r = marker.InteractRadius;
                bool inside = (markerPos - playerPos).sqrMagnitude <= r * r;
                marker.SetPlateVisible(inside);

                if (!wasInside.TryGetValue(marker, out bool prevInside))
                    prevInside = false;

                if (inside && !prevInside && marker.TryConsumeEnter())
                    marker.NotifyEntered();

                wasInside[marker] = inside;
            }
        }
    }
}
