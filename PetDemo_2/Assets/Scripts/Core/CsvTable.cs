// 通用 CSV 解析器：header + 注释行（# 起始）+ 空行跳过 + trim 字段。
// SPEC §B.2.1 解析约定（与 §B.4 / §B.5 共用）。
// 仅按逗号切分，不处理双引号转义；字段内禁止嵌入逗号或换行（plants.csv 的 spine 列须单行占位）。
using System;
using System.Collections.Generic;

namespace PetDemo.Core
{
    public class CsvRow
    {
        public int lineNumber;
        public string[] cells;

        public string Get(int index)
        {
            if (cells == null || index < 0 || index >= cells.Length)
                return null;
            return cells[index];
        }

        public bool TryGet(int index, out string value)
        {
            value = Get(index);
            return value != null;
        }
    }

    public class CsvTable
    {
        public string[] headers;
        public List<CsvRow> rows = new List<CsvRow>();

        public int IndexOfHeader(string headerName)
        {
            if (headers == null || string.IsNullOrEmpty(headerName))
                return -1;
            for (int i = 0; i < headers.Length; i++)
                if (string.Equals(headers[i], headerName, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        public static CsvTable Parse(string text)
        {
            var table = new CsvTable();
            if (string.IsNullOrEmpty(text))
                return table;

            var lines = text.Split('\n');
            bool headerSeen = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i];
                if (raw.EndsWith("\r"))
                    raw = raw.Substring(0, raw.Length - 1);

                string trimmed = raw.Trim();
                if (trimmed.Length == 0)
                    continue;
                if (trimmed.StartsWith("#"))
                    continue;

                var cells = SplitAndTrim(raw);
                if (IsAllCellsEmpty(cells))
                    continue;
                if (!headerSeen)
                {
                    table.headers = cells;
                    headerSeen = true;
                    continue;
                }

                table.rows.Add(new CsvRow
                {
                    lineNumber = i + 1,
                    cells = cells,
                });
            }
            return table;
        }

        private static string[] SplitAndTrim(string line)
        {
            var parts = line.Split(',');
            for (int i = 0; i < parts.Length; i++)
                parts[i] = parts[i] != null ? parts[i].Trim() : string.Empty;
            return parts;
        }

        private static bool IsAllCellsEmpty(string[] cells)
        {
            if (cells == null || cells.Length == 0)
                return true;
            for (int i = 0; i < cells.Length; i++)
            {
                if (!string.IsNullOrEmpty(cells[i]))
                    return false;
            }
            return true;
        }
    }
}
