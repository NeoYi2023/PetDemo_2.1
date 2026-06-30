// SPEC §13：本地 JSON 存档读写。
using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace PetDemo.Save
{
    public static class GameSaveRepository
    {
        public const int FormatVersion = 1;

        public static bool Exists(int slotIndex)
        {
            return File.Exists(GetSlotPath(slotIndex));
        }

        public static GameSaveSnapshot Load(int slotIndex)
        {
            string path = GetSlotPath(slotIndex);
            if (!File.Exists(path))
                return null;

            try
            {
                string json = File.ReadAllText(path);
                var file = JsonUtility.FromJson<GameSaveFile>(json);
                if (file?.snapshot == null)
                {
                    UnityEngine.Debug.LogWarning("[GameSaveRepository] 存档解析为空，槽位=" + slotIndex);
                    return null;
                }

                if (file.formatVersion > FormatVersion)
                    UnityEngine.Debug.LogWarning("[GameSaveRepository] 存档版本较新，槽位=" + slotIndex + " version=" + file.formatVersion);

                return file.snapshot;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[GameSaveRepository] 读档失败，槽位=" + slotIndex + ":\n" + ex);
                return null;
            }
        }

        public static void Save(int slotIndex, GameSaveSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            snapshot.savedAtUtcTicks = DateTime.UtcNow.Ticks;
            var file = new GameSaveFile
            {
                formatVersion = FormatVersion,
                snapshot = snapshot,
            };

            string path = GetSlotPath(slotIndex);
            try
            {
                Directory.CreateDirectory(GetSaveDirectory());
                File.WriteAllText(path, JsonUtility.ToJson(file, true));
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[GameSaveRepository] 写档失败，槽位=" + slotIndex + ":\n" + ex);
            }
        }

        public static void Delete(int slotIndex)
        {
            string path = GetSlotPath(slotIndex);
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[GameSaveRepository] 删档失败，槽位=" + slotIndex + ":\n" + ex);
            }
        }

        public static SaveSlotDisplayInfo GetSlotDisplayInfo(int slotIndex)
        {
            int displayNumber = slotIndex + 1;
            if (!Exists(slotIndex))
            {
                return new SaveSlotDisplayInfo
                {
                    slotIndex = slotIndex,
                    hasSave = false,
                    enterLabel = "存档" + displayNumber + " · 空",
                    summary = string.Empty,
                };
            }

            var snapshot = Load(slotIndex);
            if (snapshot == null)
            {
                return new SaveSlotDisplayInfo
                {
                    slotIndex = slotIndex,
                    hasSave = false,
                    enterLabel = "存档" + displayNumber + " · 损坏",
                    summary = string.Empty,
                };
            }

            string timeText = string.Empty;
            if (snapshot.savedAtUtcTicks > 0)
            {
                var local = new DateTime(snapshot.savedAtUtcTicks, DateTimeKind.Utc).ToLocalTime();
                timeText = local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            }

            string roleName = snapshot.role != null && !string.IsNullOrEmpty(snapshot.role.displayName)
                ? snapshot.role.displayName
                : "Role";

            string enterLabel = string.IsNullOrEmpty(timeText)
                ? "存档" + displayNumber + " · " + roleName
                : "存档" + displayNumber + " · " + timeText;

            return new SaveSlotDisplayInfo
            {
                slotIndex = slotIndex,
                hasSave = true,
                enterLabel = enterLabel,
                summary = roleName + " 攻" + (snapshot.role?.atk ?? 0),
            };
        }

        public static string GetSaveDirectory()
        {
            return Path.Combine(Application.persistentDataPath, "PetDemo", "saves");
        }

        private static string GetSlotPath(int slotIndex)
        {
            return Path.Combine(GetSaveDirectory(), "slot_" + slotIndex + ".json");
        }
    }

    public struct SaveSlotDisplayInfo
    {
        public int slotIndex;
        public bool hasSave;
        public string enterLabel;
        public string summary;
    }

    [Serializable]
    public class GameSaveFile
    {
        public int formatVersion = GameSaveRepository.FormatVersion;
        public GameSaveSnapshot snapshot;
    }
}
