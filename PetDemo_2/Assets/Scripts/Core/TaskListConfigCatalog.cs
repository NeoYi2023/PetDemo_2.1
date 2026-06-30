// 加好感页签（ZhuanQianPopup）任务列表配置目录，对应 SPEC §9.14.8 v3.121「加好感」赚钱图任务列表 Demo。
// 运行时优先从 Resources/Configs/Farm/farm_tasks.csv 装载；缺表或解析失败时回退到 BuildDefaultTaskConfigs 内置默认值，
// 确保无配置也能跑通交互闭环。CSV 列：id,iconResource,description,rewardIconResource,rewardCount,navKey。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PetDemo.Core
{
    /// <summary>加好感任务列表单条任务配置。</summary>
    public struct TaskConfig
    {
        public string id;                  // 任务唯一标识
        public string iconResource;        // 任务图标 Resources 路径（如 "AirUI/Game_NongChang"）
        public string description;         // 任务文字描述
        public string rewardIconResource;  // 奖励道具图标 Resources 路径（如 "AirUI/Xing_2"）
        public int rewardCount;            // 奖励数量
        public string navKey;              // 跳转目标（GongHui/JiaYuan/ZhuXian/LangLai/FenZheng/XiuXian）
    }

    public static class TaskListConfigCatalog
    {
        // CSV 装载路径（Resources.Load 不带扩展名）。
        public const string FarmTasksCsvResourcePath = "Configs/Farm/farm_tasks";

        // ============================================================
        // CSV 装载入口（带 fallback）
        // ============================================================
        public static List<TaskConfig> LoadTaskConfigs()
        {
            var ta = Resources.Load<TextAsset>(FarmTasksCsvResourcePath);
            if (ta == null || string.IsNullOrEmpty(ta.text))
            {
                UnityEngine.Debug.LogWarning("[TaskListConfigCatalog] farm_tasks.csv 未找到，回退到 BuildDefaultTaskConfigs。");
                return BuildDefaultTaskConfigs();
            }

            var table = CsvTable.Parse(ta.text);
            int idxId = table.IndexOfHeader("id");
            int idxIcon = table.IndexOfHeader("iconResource");
            int idxDesc = table.IndexOfHeader("description");
            int idxRewardIcon = table.IndexOfHeader("rewardIconResource");
            int idxRewardCount = table.IndexOfHeader("rewardCount");
            int idxNavKey = table.IndexOfHeader("navKey");
            if (idxId < 0 || idxIcon < 0 || idxDesc < 0 || idxRewardIcon < 0
                || idxRewardCount < 0 || idxNavKey < 0)
            {
                UnityEngine.Debug.LogWarning("[TaskListConfigCatalog] farm_tasks.csv 缺少必需列，回退到 BuildDefaultTaskConfigs。");
                return BuildDefaultTaskConfigs();
            }

            var list = new List<TaskConfig>(table.rows.Count);
            for (int i = 0; i < table.rows.Count; i++)
            {
                var row = table.rows[i];
                string id = row.Get(idxId);
                string desc = row.Get(idxDesc);
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(desc))
                {
                    UnityEngine.Debug.LogWarning($"[TaskListConfigCatalog] farm_tasks.csv 第 {row.lineNumber} 行字段缺失，跳过。");
                    continue;
                }

                if (!int.TryParse(row.Get(idxRewardCount), out int rewardCount))
                    rewardCount = 0;

                list.Add(new TaskConfig
                {
                    id = id,
                    iconResource = row.Get(idxIcon),
                    description = desc,
                    rewardIconResource = row.Get(idxRewardIcon),
                    rewardCount = rewardCount,
                    navKey = row.Get(idxNavKey),
                });
            }

            if (list.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[TaskListConfigCatalog] farm_tasks.csv 无有效行，回退到 BuildDefaultTaskConfigs。");
                return BuildDefaultTaskConfigs();
            }
            return list;
        }

        // ============================================================
        // 内置默认值（缺表 fallback）
        // ============================================================
        private static List<TaskConfig> BuildDefaultTaskConfigs()
        {
            return new List<TaskConfig>
            {
                new TaskConfig
                {
                    id = "task_jiaYuan",
                    iconResource = "AirUI/Game_NongChang",
                    description = "前往农场收获一批作物",
                    rewardIconResource = "AirUI/Xing_2",
                    rewardCount = 100,
                    navKey = "JiaYuan",
                },
                new TaskConfig
                {
                    id = "task_gongHui",
                    iconResource = "AirUI/Game_ZuDui",
                    description = "前往社区拜访邻居",
                    rewardIconResource = "AirUI/Xing_2",
                    rewardCount = 80,
                    navKey = "GongHui",
                },
                new TaskConfig
                {
                    id = "task_zhuXian",
                    iconResource = "AirUI/Game_MaoXian",
                    description = "进入冒险关卡挑战一次",
                    rewardIconResource = "AirUI/Xing_2",
                    rewardCount = 120,
                    navKey = "ZhuXian",
                },
                new TaskConfig
                {
                    id = "task_langLai",
                    iconResource = "AirUI/Game_LangLai",
                    description = "参与一次狼来了玩法",
                    rewardIconResource = "AirUI/Xing_2",
                    rewardCount = 150,
                    navKey = "LangLai",
                },
                new TaskConfig
                {
                    id = "task_fenZheng",
                    iconResource = "AirUI/Game_FenZheng",
                    description = "体验人狼纷争玩法",
                    rewardIconResource = "AirUI/Xing_2",
                    rewardCount = 200,
                    navKey = "FenZheng",
                },
                new TaskConfig
                {
                    id = "task_xiuXian",
                    iconResource = "AirUI/Game_XiuXian",
                    description = "进入修仙玩法修炼",
                    rewardIconResource = "AirUI/Xing_2",
                    rewardCount = 180,
                    navKey = "XiuXian",
                },
            };
        }
    }
}
