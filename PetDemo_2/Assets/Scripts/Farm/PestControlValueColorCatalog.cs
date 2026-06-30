// SPEC §9.11.10 / §B.15：灭虫小游戏分值底色配置。
using System.Collections.Generic;
using PetDemo.Core;
using UnityEngine;

namespace PetDemo.Farm
{
    public struct PestControlValueColorEntry
    {
        public int value;
        public Color werewolfBg;
        public Color bugBg;
    }

    public static class PestControlValueColorCatalog
    {
        public const string ValueColorsCsvResourcePath = "Configs/Farm/pest_control_value_colors";

        private static List<PestControlValueColorEntry> cachedEntries;

        public static IReadOnlyList<PestControlValueColorEntry> LoadEntriesFromCsv()
        {
            if (cachedEntries != null)
                return cachedEntries;

            var ta = Resources.Load<TextAsset>(ValueColorsCsvResourcePath);
            if (ta == null || string.IsNullOrEmpty(ta.text))
            {
                Debug.LogWarning("[PestControlValueColorCatalog] pest_control_value_colors.csv 未找到，回退默认。");
                cachedEntries = BuildDefaultEntries();
                return cachedEntries;
            }

            var table = CsvTable.Parse(ta.text);
            int idxValue = IndexOfHeader(table, "分值", "value");
            int idxWolf = IndexOfHeader(table, "狼人底色色号", "werewolfColorHex");
            int idxBug = IndexOfHeader(table, "虫子底色色号", "bugColorHex");
            if (idxValue < 0 || idxWolf < 0 || idxBug < 0)
            {
                Debug.LogWarning("[PestControlValueColorCatalog] 缺少必需列，回退默认。");
                cachedEntries = BuildDefaultEntries();
                return cachedEntries;
            }

            var list = new List<PestControlValueColorEntry>(table.rows.Count);
            for (int i = 0; i < table.rows.Count; i++)
            {
                var row = table.rows[i];
                if (!int.TryParse(row.Get(idxValue), out int value) || value < 2)
                    continue;

                list.Add(new PestControlValueColorEntry
                {
                    value = value,
                    werewolfBg = ParseHex(row.Get(idxWolf), new Color(0.55f, 0.30f, 0.75f, 1f)),
                    bugBg = ParseHex(row.Get(idxBug), new Color(0.55f, 0.75f, 0.20f, 1f)),
                });
            }

            if (list.Count == 0)
            {
                cachedEntries = BuildDefaultEntries();
                return cachedEntries;
            }

            list.Sort((a, b) => a.value.CompareTo(b.value));
            cachedEntries = list;
            return cachedEntries;
        }

        public static void GetColors(int value, out Color werewolfBg, out Color bugBg)
        {
            var entries = LoadEntriesFromCsv();
            werewolfBg = new Color(0.55f, 0.30f, 0.75f, 1f);
            bugBg = new Color(0.55f, 0.75f, 0.20f, 1f);
            if (entries.Count == 0 || value < 2)
                return;

            PestControlValueColorEntry? exact = null;
            PestControlValueColorEntry? floor = null;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].value == value)
                {
                    exact = entries[i];
                    break;
                }

                if (entries[i].value < value)
                    floor = entries[i];
            }

            var pick = exact ?? floor;
            if (pick.HasValue)
            {
                werewolfBg = pick.Value.werewolfBg;
                bugBg = pick.Value.bugBg;
            }
        }

        public static List<PestControlValueColorEntry> BuildDefaultEntries()
        {
            return new List<PestControlValueColorEntry>
            {
                new PestControlValueColorEntry { value = 2, werewolfBg = ParseHex("#5C3D7A", default), bugBg = ParseHex("#4A6B2F", default) },
                new PestControlValueColorEntry { value = 4, werewolfBg = ParseHex("#6E4A92", default), bugBg = ParseHex("#5A8340", default) },
                new PestControlValueColorEntry { value = 8, werewolfBg = ParseHex("#8058AA", default), bugBg = ParseHex("#6A9B51", default) },
                new PestControlValueColorEntry { value = 16, werewolfBg = ParseHex("#9264C2", default), bugBg = ParseHex("#7AB362", default) },
                new PestControlValueColorEntry { value = 32, werewolfBg = ParseHex("#A470DA", default), bugBg = ParseHex("#8ACB73", default) },
            };
        }

        private static int IndexOfHeader(CsvTable table, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                int idx = table.IndexOfHeader(names[i]);
                if (idx >= 0)
                    return idx;
            }

            return -1;
        }

        private static Color ParseHex(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex))
                return fallback;
            string trimmed = hex.Trim();
            if (!trimmed.StartsWith("#"))
                trimmed = "#" + trimmed;
            if (ColorUtility.TryParseHtmlString(trimmed, out var color))
                return color;
            return fallback;
        }
    }
}
