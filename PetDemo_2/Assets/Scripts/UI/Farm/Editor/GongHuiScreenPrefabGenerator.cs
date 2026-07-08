#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using PetDemo.Core;
using PetDemo.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// SPEC §9.8.9.2 (v3.123；全景按钮 v3.183)：公会场景层预制体生成器。
    /// 产出 Assets/Resources/Prefabs/Farm/GongHuiScreenPanel.prefab，
    /// 内置示例碰撞体×3 / 建筑×2 / NPC×3，位置供人工在 Inspector 中调整。
    /// </summary>
    public static class GongHuiScreenPrefabGenerator
    {
        private const string PrefabDir = "Assets/Resources/Prefabs/Farm";
        private const string PrefabPath = PrefabDir + "/GongHuiScreenPanel.prefab";

        [MenuItem("Tools/PetDemo/Generate GongHui Screen Prefab")]
        public static void Generate()
        {
            EnsureDir(PrefabDir);
            BuildPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("GongHuiScreenPanel 预制件生成完成: " + PrefabPath);
        }

        private static void BuildPrefab()
        {
            var root = new GameObject("GongHuiScreenPanel", typeof(RectTransform));
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var view = root.AddComponent<GongHuiScreenView>();

            // 复用运行时骨架构建（Viewport/WorldContent/Background/分组根/PlayerSpawn/摇杆/全景按钮）。
            GongHuiScreenView.BuildSceneSkeleton(rootRt, view);

            var worldContent = rootRt.Find(
                "GongHuiViewport/GongHuiWorldContent") as RectTransform;
            if (worldContent != null)
                worldContent.localScale = GongHuiScreenView.WorldContentLocalScale;

            var obstaclesRoot = worldContent.Find("Obstacles") as RectTransform;
            var buildingsRoot = worldContent.Find("Buildings") as RectTransform;
            var npcsRoot = worldContent.Find("Npcs") as RectTransform;
            var responseAreasRoot = worldContent.Find("ResponseAreas") as RectTransform;

            // 示例碰撞体（无视觉，人工调整位置/大小）。
            BuildObstacle(obstaclesRoot, "Obstacle_1", new Vector2(-320f, 420f), new Vector2(360f, 220f));
            BuildObstacle(obstaclesRoot, "Obstacle_2", new Vector2(330f, 120f), new Vector2(260f, 260f));
            BuildObstacle(obstaclesRoot, "Obstacle_3", new Vector2(-60f, -520f), new Vector2(420f, 180f));

            // 示例建筑（占位色块 + 名称）。
            BuildBuilding(buildingsRoot, "Building_1", "公会大厅", new Vector2(-280f, 640f));
            BuildBuilding(buildingsRoot, "Building_2", "任务板", new Vector2(300f, 360f));

            // 示例 NPC（固定出生点，头像/名字默认取 FriendCatalog）。
            BuildNpc(npcsRoot, "Npc_1", "friend-01", GuildNpcSkeletonKind.LangMeiRen, true, new Vector2(-330f, -120f));
            BuildNpc(npcsRoot, "Npc_2", "friend-02", GuildNpcSkeletonKind.LangRen, false, new Vector2(280f, -320f));
            BuildNpc(npcsRoot, "Npc_3", "friend-03", GuildNpcSkeletonKind.LangRen, false, new Vector2(40f, 180f));

            // 示例响应区域（靠近显示名牌，走进半径自动触发占位跳转）。
            BuildResponseArea(responseAreasRoot, "ResponseArea_1", "portal_shop", "商店入口",
                "ShangDian", new Vector2(-120f, 520f));
            BuildResponseArea(responseAreasRoot, "ResponseArea_2", "portal_adventure", "冒险传送",
                "ZhuXian", new Vector2(420f, -180f));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            if (prefab != null)
                Selection.activeObject = prefab;
        }

        private static void BuildObstacle(
            RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size)
        {
            var rt = CreateCentered(parent, name, anchoredPosition, size);
            rt.gameObject.AddComponent<GuildObstacleArea>();
        }

        private static void BuildBuilding(
            RectTransform parent, string name, string buildingName, Vector2 anchoredPosition)
        {
            var rt = CreateCentered(parent, name, anchoredPosition, new Vector2(240f, 240f));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.62f, 0.46f, 0.28f, 0.92f);
            img.raycastTarget = false;

            var marker = rt.gameObject.AddComponent<GuildBuildingMarker>();
            marker.SetBuildingName(buildingName);

            // 编辑器内可辨识的占位文字（运行时名牌另行懒创建）。
            var labelRt = CreateCentered(rt, "EditorLabel", Vector2.zero, new Vector2(220f, 60f));
            var label = labelRt.gameObject.AddComponent<Text>();
            label.text = buildingName;
            label.font = GuildSceneUiFactory.LoadFont();
            label.fontSize = 32;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
        }

        private static void BuildNpc(
            RectTransform parent,
            string name,
            string npcId,
            GuildNpcSkeletonKind skeletonKind,
            bool showActionIcon,
            Vector2 anchoredPosition)
        {
            var rt = CreateCentered(parent, name, anchoredPosition, new Vector2(10f, 10f));
            var marker = rt.gameObject.AddComponent<GuildNpcMarker>();
            marker.SetNpcId(npcId);
            marker.SetSkeletonKind(skeletonKind);
            marker.SetShowActionIcon(showActionIcon);
            BakeNpcNamePlate(rt, npcId);
        }

        private static void BuildResponseArea(
            RectTransform parent,
            string nodeName,
            string areaId,
            string displayName,
            string navTargetKey,
            Vector2 anchoredPosition)
        {
            var rt = CreateCentered(parent, nodeName, anchoredPosition, new Vector2(10f, 10f));
            var marker = rt.gameObject.AddComponent<GuildResponseAreaMarker>();
            marker.SetAreaId(areaId);
            marker.SetDisplayName(displayName);
            marker.SetNavTargetKey(navTargetKey);
            BakeResponseAreaNamePlate(rt, displayName);
        }

        private static void BakeResponseAreaNamePlate(RectTransform areaRt, string displayName)
        {
            var plateRt = GuildSceneUiFactory.BuildResponseAreaNamePlate(
                areaRt, displayName, null,
                new Color(0.35f, 0.55f, 0.72f, 1f), 140f);
            plateRt.gameObject.SetActive(false);
        }

        /// <summary>预制体内烘焙 NamePlate/InteractButton/Label（默认隐藏，运行时复用）。</summary>
        private static void BakeNpcNamePlate(RectTransform npcRt, string npcId)
        {
            string displayName = ResolveNpcDisplayName(npcId);
            Sprite avatarSprite = ResolveNpcAvatarSprite(npcId);
            var plateRt = GuildSceneUiFactory.BuildNpcNamePlate(
                npcRt, displayName, avatarSprite,
                new Color(0.45f, 0.55f, 0.75f, 1f), 330f);
            plateRt.gameObject.SetActive(false);
        }

        private static string ResolveNpcDisplayName(string npcId)
        {
            List<FriendProfile> catalog = FriendCatalog.BuildDefault();
            for (int i = 0; i < catalog.Count; i++)
            {
                if (catalog[i] != null && string.Equals(catalog[i].id, npcId))
                    return catalog[i].displayName;
            }
            return string.IsNullOrEmpty(npcId) ? "公会成员" : npcId;
        }

        private static Sprite ResolveNpcAvatarSprite(string npcId)
        {
            List<FriendProfile> catalog = FriendCatalog.BuildDefault();
            for (int i = 0; i < catalog.Count; i++)
            {
                if (catalog[i] != null
                    && string.Equals(catalog[i].id, npcId)
                    && !string.IsNullOrEmpty(catalog[i].avatarResource))
                {
                    return Resources.Load<Sprite>(catalog[i].avatarResource);
                }
            }
            return null;
        }

        private static RectTransform CreateCentered(
            RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;
            return rt;
        }

        private static void EnsureDir(string dir)
        {
            if (Directory.Exists(dir))
                return;
            Directory.CreateDirectory(dir);
        }
    }
}
#endif
