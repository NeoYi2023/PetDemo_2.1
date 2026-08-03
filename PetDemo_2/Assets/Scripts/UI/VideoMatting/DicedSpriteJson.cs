// SPEC_VideoMattingAtlas.md v1.0 — sprites.json DTOs for CLI mesh playback.
using System;
using UnityEngine;

namespace PetDemo.UI.VideoMatting
{
    [Serializable]
    public class DicedSpriteVec2
    {
        public float x;
        public float y;
    }

    [Serializable]
    public class DicedSpriteUv
    {
        public float u;
        public float v;
    }

    [Serializable]
    public class DicedSpriteRect
    {
        public float x;
        public float y;
        public float width;
        public float height;
    }

    [Serializable]
    public class DicedSpriteEntry
    {
        public string id;
        public int atlas;
        public DicedSpriteRect rect;
        public DicedSpriteVec2[] vertices;
        public DicedSpriteUv[] uvs;
        public int[] indices;
    }

    [Serializable]
    public class DicedSpriteEntryList
    {
        public DicedSpriteEntry[] items;
    }

    public static class DicedSpriteJson
    {
        /// <summary>
        /// Parse sprites.json root array into entries. Sorts by frame index parsed from id.
        /// </summary>
        public static DicedSpriteEntry[] ParseAndSort(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Array.Empty<DicedSpriteEntry>();

            string trimmed = json.TrimStart();
            string wrapped;
            if (trimmed.StartsWith("[", StringComparison.Ordinal))
                wrapped = "{\"items\":" + json + "}";
            else
                wrapped = json;

            var list = JsonUtility.FromJson<DicedSpriteEntryList>(wrapped);
            if (list == null || list.items == null)
                return Array.Empty<DicedSpriteEntry>();

            Array.Sort(list.items, CompareByFrameId);
            return list.items;
        }

        public static int CompareByFrameId(DicedSpriteEntry a, DicedSpriteEntry b)
        {
            int ia = ParseFrameIndex(a != null ? a.id : null);
            int ib = ParseFrameIndex(b != null ? b.id : null);
            return ia.CompareTo(ib);
        }

        public static int ParseFrameIndex(string id)
        {
            if (string.IsNullOrEmpty(id))
                return int.MaxValue;

            // Prefer trailing digits: frame_000012 → 12
            int end = id.Length - 1;
            while (end >= 0 && !char.IsDigit(id[end]))
                end--;
            if (end < 0)
                return int.MaxValue;
            int start = end;
            while (start >= 0 && char.IsDigit(id[start]))
                start--;
            start++;
            if (int.TryParse(id.Substring(start, end - start + 1), out int value))
                return value;
            return int.MaxValue - 1;
        }
    }
}
