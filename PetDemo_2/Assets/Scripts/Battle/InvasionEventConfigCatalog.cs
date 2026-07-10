// SPEC §12.11 / §B.16 / §B.17：InvasionBattleModal_2「下一天」事件静态配置目录（双表）。
// 天数表 Resources/Configs/Battle/invasion_event_days.csv：按精确天数加权随机抽 eventId。
// 事件表 Resources/Configs/Battle/invasion_events.csv：事件类型 / 文本(/n 多条 + 富文本) / 奖励 / 背景框。
// 解析约定与 §B.2.1 / Core/CsvTable 一致：UTF-8、header、`#` 注释、空行跳过、字段 Trim()、非法行 Warning 跳过。
using System;
using System.Collections.Generic;
using PetDemo.Core;
using UnityEngine;

namespace PetDemo.Battle
{
    /// <summary>SPEC §12.11.6：事件类型。</summary>
    public enum InvasionEventType
    {
        AdjustAttr, // 调整属性
        Battle,     // 战斗
        Lottery,    // 抽奖
        Adventure,  // 奇遇
    }

    /// <summary>SPEC §12.11.6 / §B.17.2：事件奖励类型。</summary>
    public enum InvasionEventRewardKind
    {
        AttrPercent, // attr:hp|atk|speed:±%（本期落地）
        BattleSmall, // battle_small（占位）
        BattleBoss,  // battle_boss（占位）
        Slot3,       // slot3（占位）
        Slot5,       // slot5（占位）
        PickThree,   // pick3（占位）
    }

    /// <summary>SPEC §12.11.6 / §B.17.2：单条事件奖励。</summary>
    public sealed class InvasionEventReward
    {
        public InvasionEventRewardKind kind;
        public string target;  // 仅 AttrPercent 使用：hp|atk|speed
        public int percent;    // 仅 AttrPercent 使用：带符号百分比
        public SkillQuality skillQuality; // 仅 PickThree 使用：Normal(领悟)/Legendary(顿悟)
    }

    /// <summary>SPEC §B.16：天数表一行。</summary>
    public sealed class InvasionEventDayEntry
    {
        public int day;
        public string eventId;
        public int weight;
    }

    /// <summary>SPEC §12.11.6 / §B.17：事件明细配置。</summary>
    public sealed class InvasionEventConfig
    {
        public string eventId;
        public InvasionEventType eventType;
        public List<string> textSegments;          // eventText 按 "/n" 拆分
        public List<InvasionEventReward> rewards;  // 可空
        public int backgroundIndex;                // 1~5 → AirUI/ShiJian_{n}
    }

    public static class InvasionEventConfigCatalog
    {
        public const string InvasionEventsCsvResourcePath = "Configs/Battle/invasion_events";
        public const string InvasionEventDaysCsvResourcePath = "Configs/Battle/invasion_event_days";

        public const int MinBackgroundIndex = 1;
        public const int MaxBackgroundIndex = 5;
        public const string TextSegmentSeparator = "/n";

        // ============================================================
        // 事件明细表（§B.17）
        // ============================================================
        public static Dictionary<string, InvasionEventConfig> LoadEventsFromCsv()
        {
            var ta = Resources.Load<TextAsset>(InvasionEventsCsvResourcePath);
            if (ta == null || string.IsNullOrEmpty(ta.text))
            {
                UnityEngine.Debug.LogWarning("[InvasionEventConfigCatalog] invasion_events.csv 未找到，回退到 BuildDefaultEvents。");
                return BuildDefaultEvents();
            }

            var table = CsvTable.Parse(ta.text);
            int idxId = table.IndexOfHeader("eventId");
            int idxType = table.IndexOfHeader("eventType");
            int idxText = table.IndexOfHeader("eventText");
            int idxReward = table.IndexOfHeader("eventReward");
            int idxBg = table.IndexOfHeader("background");
            if (idxId < 0 || idxType < 0 || idxText < 0)
            {
                UnityEngine.Debug.LogWarning("[InvasionEventConfigCatalog] invasion_events.csv 缺少必需列（eventId/eventType/eventText），回退到 BuildDefaultEvents。");
                return BuildDefaultEvents();
            }

            var dict = new Dictionary<string, InvasionEventConfig>(table.rows.Count);
            for (int i = 0; i < table.rows.Count; i++)
            {
                var row = table.rows[i];
                string id = row.Get(idxId);
                if (string.IsNullOrEmpty(id))
                {
                    UnityEngine.Debug.LogWarning($"[InvasionEventConfigCatalog] invasion_events.csv 第 {row.lineNumber} 行 eventId 缺失，跳过。");
                    continue;
                }

                InvasionEventType eventType = ParseEventType(row.Get(idxType), row.lineNumber);

                var segments = SplitTextSegments(row.Get(idxText));
                if (segments.Count == 0)
                    segments.Add(id);

                var rewards = idxReward >= 0
                    ? ParseRewards(row.Get(idxReward), row.lineNumber)
                    : new List<InvasionEventReward>();

                int bg = ParseBackground(idxBg >= 0 ? row.Get(idxBg) : null);

                dict[id] = new InvasionEventConfig
                {
                    eventId = id,
                    eventType = eventType,
                    textSegments = segments,
                    rewards = rewards,
                    backgroundIndex = bg,
                };
            }

            if (dict.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[InvasionEventConfigCatalog] invasion_events.csv 全部行非法，回退到 BuildDefaultEvents。");
                return BuildDefaultEvents();
            }
            return dict;
        }

        // ============================================================
        // 天数表（§B.16）
        // ============================================================
        public static List<InvasionEventDayEntry> LoadDayTableFromCsv()
        {
            var ta = Resources.Load<TextAsset>(InvasionEventDaysCsvResourcePath);
            if (ta == null || string.IsNullOrEmpty(ta.text))
            {
                UnityEngine.Debug.LogWarning("[InvasionEventConfigCatalog] invasion_event_days.csv 未找到，回退到 BuildDefaultDayEntries。");
                return BuildDefaultDayEntries();
            }

            var table = CsvTable.Parse(ta.text);
            int idxDay = table.IndexOfHeader("day");
            int idxEventId = table.IndexOfHeader("eventId");
            int idxWeight = table.IndexOfHeader("weight");
            if (idxDay < 0 || idxEventId < 0 || idxWeight < 0)
            {
                UnityEngine.Debug.LogWarning("[InvasionEventConfigCatalog] invasion_event_days.csv 缺少必需列（day/eventId/weight），回退到 BuildDefaultDayEntries。");
                return BuildDefaultDayEntries();
            }

            var list = new List<InvasionEventDayEntry>(table.rows.Count);
            for (int i = 0; i < table.rows.Count; i++)
            {
                var row = table.rows[i];
                if (!int.TryParse(row.Get(idxDay), out int day) || day < 1)
                {
                    UnityEngine.Debug.LogWarning($"[InvasionEventConfigCatalog] invasion_event_days.csv 第 {row.lineNumber} 行 day 非法（须 >= 1），跳过。");
                    continue;
                }
                string eventId = row.Get(idxEventId);
                if (string.IsNullOrEmpty(eventId))
                {
                    UnityEngine.Debug.LogWarning($"[InvasionEventConfigCatalog] invasion_event_days.csv 第 {row.lineNumber} 行 eventId 缺失，跳过。");
                    continue;
                }
                if (!int.TryParse(row.Get(idxWeight), out int weight) || weight <= 0)
                {
                    UnityEngine.Debug.LogWarning($"[InvasionEventConfigCatalog] invasion_event_days.csv 第 {row.lineNumber} 行 weight 非法（须 > 0），跳过。");
                    continue;
                }

                list.Add(new InvasionEventDayEntry { day = day, eventId = eventId, weight = weight });
            }

            if (list.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[InvasionEventConfigCatalog] invasion_event_days.csv 全部行非法，回退到 BuildDefaultDayEntries。");
                return BuildDefaultDayEntries();
            }
            return list;
        }

        /// <summary>
        /// SPEC §12.11.5 / §B.16.3：先按 entry.day==day 过滤，再按 weight 加权随机抽 1 个 eventId；无可用返回 null。
        /// </summary>
        public static string PickWeightedByDay(List<InvasionEventDayEntry> dayEntries, int day)
        {
            if (dayEntries == null || dayEntries.Count == 0)
                return null;

            int totalWeight = 0;
            for (int i = 0; i < dayEntries.Count; i++)
            {
                var e = dayEntries[i];
                if (e == null || e.weight <= 0 || e.day != day)
                    continue;
                totalWeight += e.weight;
            }

            if (totalWeight <= 0)
                return null;

            int roll = UnityEngine.Random.Range(0, totalWeight);
            for (int i = 0; i < dayEntries.Count; i++)
            {
                var e = dayEntries[i];
                if (e == null || e.weight <= 0 || e.day != day)
                    continue;
                roll -= e.weight;
                if (roll < 0)
                    return e.eventId;
            }
            return null;
        }

        // ============================================================
        // 解析辅助
        // ============================================================
        public static InvasionEventType ParseEventType(string raw, int lineNumber = -1)
        {
            string s = raw != null ? raw.Trim() : string.Empty;
            switch (s)
            {
                case "调整属性":
                case "AdjustAttr":
                    return InvasionEventType.AdjustAttr;
                case "战斗":
                case "Battle":
                    return InvasionEventType.Battle;
                case "抽奖":
                case "Lottery":
                    return InvasionEventType.Lottery;
                case "奇遇":
                case "Adventure":
                    return InvasionEventType.Adventure;
            }
            if (Enum.TryParse<InvasionEventType>(s, true, out var parsed))
                return parsed;
            UnityEngine.Debug.LogWarning($"[InvasionEventConfigCatalog] 事件表第 {lineNumber} 行 eventType 非法（{raw}），回退为 Adventure。");
            return InvasionEventType.Adventure;
        }

        public static List<string> SplitTextSegments(string raw)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(raw))
                return result;
            var parts = raw.Split(new[] { TextSegmentSeparator }, StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++)
            {
                string seg = parts[i] != null ? parts[i].Trim() : string.Empty;
                if (seg.Length > 0)
                    result.Add(seg);
            }
            return result;
        }

        public static int ParseBackground(string raw)
        {
            if (int.TryParse(raw, out int n) && n >= MinBackgroundIndex && n <= MaxBackgroundIndex)
                return n;
            return MinBackgroundIndex;
        }

        public static List<InvasionEventReward> ParseRewards(string encoded, int lineNumber = -1)
        {
            var list = new List<InvasionEventReward>();
            if (string.IsNullOrWhiteSpace(encoded))
                return list;

            var tokens = encoded.Split(';');
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i] != null ? tokens[i].Trim() : string.Empty;
                if (token.Length == 0)
                    continue;

                if (token.StartsWith("attr:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = token.Split(':');
                    if (parts.Length < 3)
                    {
                        UnityEngine.Debug.LogWarning($"[InvasionEventConfigCatalog] 事件表第 {lineNumber} 行奖励条目格式非法（须 attr:hp|atk|speed:±%）：{token}");
                        continue;
                    }
                    string target = parts[1].Trim().ToLowerInvariant();
                    if (target != "hp" && target != "atk" && target != "speed")
                    {
                        UnityEngine.Debug.LogWarning($"[InvasionEventConfigCatalog] 事件表第 {lineNumber} 行奖励目标非法（仅 hp/atk/speed）：{token}");
                        continue;
                    }
                    string percentRaw = parts[2].Trim().TrimEnd('%');
                    if (!int.TryParse(percentRaw, out int percent))
                    {
                        UnityEngine.Debug.LogWarning($"[InvasionEventConfigCatalog] 事件表第 {lineNumber} 行奖励百分比非法：{token}");
                        continue;
                    }
                    list.Add(new InvasionEventReward { kind = InvasionEventRewardKind.AttrPercent, target = target, percent = percent });
                    continue;
                }

                // SPEC §12.11.9 / §B.17.2：pick3:normal|legendary（裸 pick3 默认 normal）
                if (token.StartsWith("pick3", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = token.Split(':');
                    SkillQuality quality = SkillQuality.Normal;
                    if (parts.Length >= 2)
                    {
                        string q = parts[1].Trim().ToLowerInvariant();
                        if (q == "legendary" || q == "传说")
                            quality = SkillQuality.Legendary;
                        else if (q == "normal" || q == "普通" || q.Length == 0)
                            quality = SkillQuality.Normal;
                        else
                            UnityEngine.Debug.LogWarning($"[InvasionEventConfigCatalog] 事件表第 {lineNumber} 行 pick3 品质非法（仅 normal/legendary）：{token}，回退 normal。");
                    }
                    list.Add(new InvasionEventReward { kind = InvasionEventRewardKind.PickThree, skillQuality = quality });
                    continue;
                }

                switch (token.ToLowerInvariant())
                {
                    case "battle_small":
                        list.Add(new InvasionEventReward { kind = InvasionEventRewardKind.BattleSmall });
                        break;
                    case "battle_boss":
                        list.Add(new InvasionEventReward { kind = InvasionEventRewardKind.BattleBoss });
                        break;
                    case "slot3":
                        list.Add(new InvasionEventReward { kind = InvasionEventRewardKind.Slot3 });
                        break;
                    case "slot5":
                        list.Add(new InvasionEventReward { kind = InvasionEventRewardKind.Slot5 });
                        break;
                    default:
                        UnityEngine.Debug.LogWarning($"[InvasionEventConfigCatalog] 事件表第 {lineNumber} 行奖励类型未知：{token}");
                        break;
                }
            }
            return list;
        }

        // ============================================================
        // Demo 默认数据（与 CSV 等价，§B.16.2 / §B.17.3）
        // ============================================================
        public static Dictionary<string, InvasionEventConfig> BuildDefaultEvents()
        {
            var dict = new Dictionary<string, InvasionEventConfig>();
            AddDefault(dict, "evt_calm", InvasionEventType.Adventure, "平静的一天，你稍作休整。", null, 1);
            AddDefault(dict, "evt_boost_atk", InvasionEventType.AdjustAttr, "你找到一柄利器，<color=#FF3B30>攻击提升 10%</color>！", "attr:atk:+10", 2);
            AddDefault(dict, "evt_boost_hp", InvasionEventType.AdjustAttr, "温泉让你恢复元气，<color=#33CC33>生命提升 15%</color>。", "attr:hp:+15", 2);
            AddDefault(dict, "evt_curse_speed", InvasionEventType.Adventure, "沼泽拖慢了脚步，<color=#3399FF>速度下降 10%</color>。/n但你发现了一条捷径。", "attr:speed:-10", 3);
            AddDefault(dict, "evt_forage", InvasionEventType.AdjustAttr, "发现补给。/n<color=#33CC33>生命 +5%</color>、<color=#FF3B30>攻击 +5%</color>。", "attr:hp:+5;attr:atk:+5", 4);
            AddDefault(dict, "evt_fight_small_1", InvasionEventType.Battle, "前方出现一只小怪！", "battle_small", 5);
            AddDefault(dict, "evt_fight_small_2", InvasionEventType.Battle, "前方出现一群小怪！", "battle_small", 5);
            AddDefault(dict, "evt_fight_boss", InvasionEventType.Battle, "<color=#FF3B30>最终 BOSS 出现了！</color>", "battle_boss", 5);
            AddDefault(dict, "evt_lottery", InvasionEventType.Lottery, "你发现一个神秘宝箱。", "slot3", 4);
            AddDefault(dict, "evt_lottery5", InvasionEventType.Lottery, "一台华丽的五轴宝机出现在眼前！", "slot5", 4);
            AddDefault(dict, "evt_insight", InvasionEventType.Adventure, "你静心参悟，<color=#33CC33>领悟</color>了新的招式。", "pick3:normal", 4);
            AddDefault(dict, "evt_epiphany", InvasionEventType.Adventure, "灵光乍现，你<color=#FFB300>顿悟</color>了传说级奥义！", "pick3:legendary", 4);
            return dict;
        }

        private static void AddDefault(Dictionary<string, InvasionEventConfig> dict, string id,
            InvasionEventType type, string text, string reward, int bg)
        {
            dict[id] = new InvasionEventConfig
            {
                eventId = id,
                eventType = type,
                textSegments = SplitTextSegments(text),
                rewards = ParseRewards(reward),
                backgroundIndex = bg,
            };
        }

        public static List<InvasionEventDayEntry> BuildDefaultDayEntries()
        {
            return new List<InvasionEventDayEntry>
            {
                new InvasionEventDayEntry { day = 1, eventId = "evt_calm", weight = 50 },
                new InvasionEventDayEntry { day = 1, eventId = "evt_boost_atk", weight = 30 },
                new InvasionEventDayEntry { day = 1, eventId = "evt_forage", weight = 20 },
                new InvasionEventDayEntry { day = 2, eventId = "evt_calm", weight = 40 },
                new InvasionEventDayEntry { day = 2, eventId = "evt_boost_hp", weight = 30 },
                new InvasionEventDayEntry { day = 4, eventId = "evt_fight_small_1", weight = 40 },
                new InvasionEventDayEntry { day = 8, eventId = "evt_fight_small_2", weight = 40 },
                new InvasionEventDayEntry { day = 3, eventId = "evt_lottery", weight = 40 },
                new InvasionEventDayEntry { day = 3, eventId = "evt_curse_speed", weight = 30 },
                new InvasionEventDayEntry { day = 3, eventId = "evt_fight_boss", weight = 30 },
                new InvasionEventDayEntry { day = 1, eventId = "evt_insight", weight = 40 },
                new InvasionEventDayEntry { day = 2, eventId = "evt_insight", weight = 40 },
                new InvasionEventDayEntry { day = 3, eventId = "evt_epiphany", weight = 40 },
                new InvasionEventDayEntry { day = 2, eventId = "evt_lottery5", weight = 30 },
                new InvasionEventDayEntry { day = 3, eventId = "evt_lottery5", weight = 30 },
            };
        }
    }
}
