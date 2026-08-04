#if UNITY_EDITOR
// SPEC_VideoMattingAtlas.md §5.3 / SPEC_FarmBattleDemo.md §9.14.11 v3.283：
// 将 VideoMatting output 的 frames_sprite 导入 SpriteDicing，并构建 Unity-package diced_sprites（不覆盖 CLI 方案）。
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PetDemo.EditorTools
{
    public static class ZjdhUnityPackageAtlasBuilder
    {
        public static readonly string[] AnimNames = { "ZJDH_rest_2", "ZJDH_study_2" };

        private const string LogTag = "ZjdhUnityPackageAtlasBuilder";

        [MenuItem("Tools/PetDemo/Build ZJDH Unity Package Atlases")]
        public static void BuildAllFromMenu()
        {
            var report = BuildAll();
            EditorUtility.DisplayDialog("ZJDH Unity Package Atlases", report, "OK");
        }

        /// <summary>
        /// 对 ZJDH_rest_2 / ZJDH_study_2：必要时从 Tools/VideoMatting/output 拷贝 frames_sprite，
        /// 应用 Sprite importer，再调用 DicedAtlasBuilder。返回可读报告。
        /// </summary>
        public static string BuildAll()
        {
            var lines = new System.Text.StringBuilder();
            lines.AppendLine("ZJDH Unity-package 路径构建（不改 Resources/VideoMatting CLI 产物）");
            lines.AppendLine("完成后请在 Play 中对比；HomeTab UseZjdhUnityPackagePath 默认 true。");
            lines.AppendLine("回退 CLI bake：将该常量改为 false。");
            lines.AppendLine();

            int okAnims = 0;
            for (int i = 0; i < AnimNames.Length; i++)
            {
                string anim = AnimNames[i];
                int sprites = BuildOne(anim, out string detail);
                lines.AppendLine(detail);
                if (sprites > 0)
                    okAnims++;
            }

            lines.AppendLine();
            lines.AppendLine("成功动画数：" + okAnims + "/" + AnimNames.Length);
            if (okAnims < AnimNames.Length)
            {
                lines.AppendLine("若 sprite=0：确认 Tools/VideoMatting/output/{Anim}/frames_sprite 存在 frame_*.png，");
                lines.AppendLine("并在 Unity 中重新执行本菜单。");
            }

            string report = lines.ToString();
            Debug.Log("[" + LogTag + "]\n" + report);
            return report;
        }

        public static int BuildOne(string animName, out string detail)
        {
            detail = animName + ": failed";
            if (string.IsNullOrWhiteSpace(animName))
                return -1;

            string name = animName.Trim();
            string framesFolder = DicedAtlasBuilder.GetFramesFolder(name);
            string sourceDir = ResolvePipelineFramesDir(name);

            try
            {
                EnsureCopiedFrames(name, framesFolder, sourceDir);
            }
            catch (Exception ex)
            {
                detail = name + ": 拷贝/导入失败 — " + ex.Message;
                Debug.LogError("[" + LogTag + "] " + detail);
                return -1;
            }

            ApplyImportersInFolder(framesFolder);
            AssetDatabase.Refresh();

            int frameCount = CountPngFrames(framesFolder);
            if (frameCount < 1)
            {
                detail = name + ": frames_sprite 为空（源=" + sourceDir + "）";
                Debug.LogError("[" + LogTag + "] " + detail);
                return -1;
            }

            int sprites = DicedAtlasBuilder.BuildForAnim(name);
            detail = name + ": frames=" + frameCount + ", diced_sprites=" + sprites +
                     ", out=" + DicedAtlasBuilder.GetDicedFolder(name);
            return sprites;
        }

        private static string ResolvePipelineFramesDir(string animName)
        {
            // Assets/Scripts/... → Application.dataPath = .../PetDemo_2/Assets
            string toolsOutput = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Tools",
                "VideoMatting",
                "output",
                animName,
                "frames_sprite"));
            return toolsOutput;
        }

        private static void EnsureCopiedFrames(string animName, string framesAssetFolder, string sourceAbsDir)
        {
            DicedAtlasBuilder.EnsureFolder(framesAssetFolder);

            int existing = CountPngFrames(framesAssetFolder);
            if (existing > 0)
            {
                Debug.Log("[" + LogTag + "] " + animName + " 已有 " + existing + " 帧，跳过拷贝。");
                return;
            }

            if (!Directory.Exists(sourceAbsDir))
                throw new DirectoryNotFoundException("缺少流水线 frames_sprite: " + sourceAbsDir);

            string[] pngs = Directory.GetFiles(sourceAbsDir, "frame_*.png");
            if (pngs == null || pngs.Length == 0)
                throw new FileNotFoundException("源目录无 frame_*.png: " + sourceAbsDir);

            string destAbs = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                framesAssetFolder.Replace('/', Path.DirectorySeparatorChar)));

            Directory.CreateDirectory(destAbs);
            int copied = 0;
            for (int i = 0; i < pngs.Length; i++)
            {
                string src = pngs[i];
                string dest = Path.Combine(destAbs, Path.GetFileName(src));
                File.Copy(src, dest, true);
                copied++;
            }

            AssetDatabase.Refresh();
            Debug.Log("[" + LogTag + "] " + animName + " 已拷贝 " + copied + " 帧 → " + framesAssetFolder);
        }

        private static int CountPngFrames(string framesAssetFolder)
        {
            string abs = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                framesAssetFolder.Replace('/', Path.DirectorySeparatorChar)));
            if (!Directory.Exists(abs))
                return 0;
            string[] files = Directory.GetFiles(abs, "frame_*.png");
            return files != null ? files.Length : 0;
        }

        private static void ApplyImportersInFolder(string assetFolder)
        {
            assetFolder = assetFolder.Replace('\\', '/');
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { assetFolder });
            if (guids != null && guids.Length > 0)
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrEmpty(path) || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                        continue;
                    ApplyImporter(path);
                }

                AssetDatabase.SaveAssets();
                return;
            }

            string abs = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetFolder));
            string dataAbs = Path.GetFullPath(Application.dataPath);
            if (!Directory.Exists(abs))
                return;

            string[] pngs = Directory.GetFiles(abs, "frame_*.png");
            for (int i = 0; i < pngs.Length; i++)
            {
                string full = Path.GetFullPath(pngs[i]);
                if (!full.StartsWith(dataAbs, StringComparison.OrdinalIgnoreCase))
                    continue;
                string relative = full.Substring(dataAbs.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string assetPath = "Assets/" + relative.Replace('\\', '/');
                ApplyImporter(assetPath);
            }

            AssetDatabase.SaveAssets();
        }

        private static void ApplyImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return;

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }

            if (!importer.isReadable)
            {
                importer.isReadable = true;
                dirty = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }

            if (Math.Abs(importer.spritePixelsPerUnit - 100f) > 0.01f)
            {
                importer.spritePixelsPerUnit = 100f;
                dirty = true;
            }

            if (importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                dirty = true;
            }

            if (dirty)
                importer.SaveAndReimport();
        }
    }
}
#endif
