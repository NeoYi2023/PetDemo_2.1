#if UNITY_EDITOR
// SPEC §9.14.11 v3.276：绿幕视频 → 15fps 透明序列帧（ffmpeg colorkey 一键）。
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;
using Debug = UnityEngine.Debug;

namespace PetDemo.EditorTools
{
    public sealed class GreenScreenVideoPipelineWindow : EditorWindow
    {
        private const string PrefFfmpegPath = "PetDemo.GreenScreenPipeline.FfmpegPath";
        private const string PrefAnimName = "PetDemo.GreenScreenPipeline.AnimName";
        private const string PrefSimilarity = "PetDemo.GreenScreenPipeline.Similarity";
        private const string PrefBlend = "PetDemo.GreenScreenPipeline.Blend";
        private const string PrefFps = "PetDemo.GreenScreenPipeline.Fps";
        private const string PrefRebuildAtlas = "PetDemo.GreenScreenPipeline.RebuildAtlas";
        private const string PrefKeyColorR = "PetDemo.GreenScreenPipeline.KeyColor.R";
        private const string PrefKeyColorG = "PetDemo.GreenScreenPipeline.KeyColor.G";
        private const string PrefKeyColorB = "PetDemo.GreenScreenPipeline.KeyColor.B";

        private const string DefaultAnimName = "LangRen_DZ _1";
        private const float DefaultSimilarity = 0.3f;
        private const float DefaultBlend = 0.1f;
        private const int DefaultFps = 15;

        private string _videoPath = "";
        private VideoClip _videoClip;
        private string _animName = DefaultAnimName;
        private Color _keyColor = Color.green;
        private float _similarity = DefaultSimilarity;
        private float _blend = DefaultBlend;
        private int _fps = DefaultFps;
        private string _ffmpegPath = "";
        private bool _rebuildLangRenAtlas;
        private Vector2 _helpScroll;
        private string _status = "";

        [MenuItem("Tools/PetDemo/Green Screen Video To Frames")]
        public static void Open()
        {
            var window = GetWindow<GreenScreenVideoPipelineWindow>(true, "Green Screen → Frames", true);
            window.minSize = new Vector2(480, 520);
            window.Show();
        }

        private void OnEnable()
        {
            _ffmpegPath = EditorPrefs.GetString(PrefFfmpegPath, "");
            _animName = EditorPrefs.GetString(PrefAnimName, DefaultAnimName);
            _similarity = EditorPrefs.GetFloat(PrefSimilarity, DefaultSimilarity);
            _blend = EditorPrefs.GetFloat(PrefBlend, DefaultBlend);
            _fps = EditorPrefs.GetInt(PrefFps, DefaultFps);
            _rebuildLangRenAtlas = EditorPrefs.GetBool(PrefRebuildAtlas, false);
            _keyColor = new Color(
                EditorPrefs.GetFloat(PrefKeyColorR, 0f),
                EditorPrefs.GetFloat(PrefKeyColorG, 1f),
                EditorPrefs.GetFloat(PrefKeyColorB, 0f),
                1f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("绿幕视频 → 15fps 透明序列帧", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "依赖本机 ffmpeg（PATH 或下方自定义路径）。一次命令完成：拆帧 + colorkey 抠绿 + fps 抽帧。\n" +
                "默认滤镜：fps=15,colorkey=0x00FF00:0.3:0.1,format=rgba\n" +
                "输出：Assets/Resources/SpriteDicing/{AnimName}/frames_sprite/frame_%06d.png",
                MessageType.Info);

            EditorGUILayout.Space(6);
            EditorGUI.BeginChangeCheck();

            using (new EditorGUILayout.HorizontalScope())
            {
                _videoClip = (VideoClip)EditorGUILayout.ObjectField("VideoClip（可选）", _videoClip, typeof(VideoClip), false);
                if (GUILayout.Button("浏览…", GUILayout.Width(64)))
                {
                    string picked = EditorUtility.OpenFilePanel(
                        "选择绿幕视频",
                        string.IsNullOrEmpty(_videoPath) ? Application.dataPath : Path.GetDirectoryName(_videoPath),
                        "mp4,mov,avi,webm,mkv");
                    if (!string.IsNullOrEmpty(picked))
                    {
                        _videoPath = picked.Replace('\\', '/');
                        _videoClip = null;
                    }
                }
            }

            if (_videoClip != null)
            {
                string clipPath = AssetDatabase.GetAssetPath(_videoClip);
                if (!string.IsNullOrEmpty(clipPath))
                {
                    string abs = Path.GetFullPath(Path.Combine(Application.dataPath, "..", clipPath));
                    _videoPath = abs.Replace('\\', '/');
                }
            }

            EditorGUILayout.LabelField("视频路径", string.IsNullOrEmpty(_videoPath) ? "（未选择）" : _videoPath);

            _animName = EditorGUILayout.TextField("动画名 AnimName", _animName);
            string outAssetDir = BuildOutputAssetDir(_animName);
            EditorGUILayout.LabelField("输出目录", outAssetDir);

            _keyColor = EditorGUILayout.ColorField("绿幕 Key 色", _keyColor);
            _similarity = EditorGUILayout.Slider("colorkey similarity", _similarity, 0.01f, 1f);
            _blend = EditorGUILayout.Slider("colorkey blend", _blend, 0f, 1f);
            _fps = Mathf.Max(1, EditorGUILayout.IntField("目标 FPS", _fps));

            using (new EditorGUILayout.HorizontalScope())
            {
                _ffmpegPath = EditorGUILayout.TextField("ffmpeg 路径（可空=PATH）", _ffmpegPath);
                if (GUILayout.Button("浏览…", GUILayout.Width(64)))
                {
                    string picked = EditorUtility.OpenFilePanel("选择 ffmpeg", "", "exe");
                    if (!string.IsNullOrEmpty(picked))
                        _ffmpegPath = picked.Replace('\\', '/');
                }
            }

            bool isLangRenOut = string.Equals(
                NormalizeAssetPath(outAssetDir),
                NormalizeAssetPath(LangRenDzDicedAtlasBuilder.FramesSpriteFolder),
                StringComparison.OrdinalIgnoreCase);
            using (new EditorGUI.DisabledScope(!isLangRenOut))
            {
                _rebuildLangRenAtlas = EditorGUILayout.ToggleLeft(
                    "完成后重建 LangRen DZ Diced Atlas（仅输出为 LangRen frames_sprite 时可用）",
                    _rebuildLangRenAtlas && isLangRenOut);
            }

            if (!isLangRenOut && _rebuildLangRenAtlas)
                _rebuildLangRenAtlas = false;

            if (EditorGUI.EndChangeCheck())
                SavePrefs();

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_videoPath)))
            {
                if (GUILayout.Button("Run（一键拆帧 + 抠绿 + 抽帧）", GUILayout.Height(36)))
                    RunPipeline();
            }

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(_status, MessageType.None);
            }

            EditorGUILayout.Space(8);
            _helpScroll = EditorGUILayout.BeginScrollView(_helpScroll, GUILayout.MinHeight(120));
            EditorGUILayout.LabelField("说明", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "1. 安装 ffmpeg 并加入 PATH，或在上方指定 ffmpeg.exe。\n" +
                "2. 选择绿幕视频 → 填写 AnimName → Run。\n" +
                "3. 若 AnimName 为「LangRen_DZ _1」且勾选重建，将调用 Build LangRen DZ Diced Atlas。\n" +
                "4. 其他动画名仅生成 frames_sprite；Atlas 构建需另开需求泛化。\n" +
                "5. 抠绿参数不理想时，调高 similarity 或微调 Key 色。",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(PrefFfmpegPath, _ffmpegPath ?? "");
            EditorPrefs.SetString(PrefAnimName, _animName ?? "");
            EditorPrefs.SetFloat(PrefSimilarity, _similarity);
            EditorPrefs.SetFloat(PrefBlend, _blend);
            EditorPrefs.SetInt(PrefFps, _fps);
            EditorPrefs.SetBool(PrefRebuildAtlas, _rebuildLangRenAtlas);
            EditorPrefs.SetFloat(PrefKeyColorR, _keyColor.r);
            EditorPrefs.SetFloat(PrefKeyColorG, _keyColor.g);
            EditorPrefs.SetFloat(PrefKeyColorB, _keyColor.b);
        }

        private static string BuildOutputAssetDir(string animName)
        {
            string safe = string.IsNullOrWhiteSpace(animName) ? DefaultAnimName : animName.Trim();
            return "Assets/Resources/SpriteDicing/" + safe + "/frames_sprite";
        }

        private static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "";
            return path.Replace('\\', '/').TrimEnd('/');
        }

        private void RunPipeline()
        {
            SavePrefs();
            _status = "";

            if (string.IsNullOrEmpty(_videoPath) || !File.Exists(_videoPath))
            {
                EditorUtility.DisplayDialog("Green Screen Pipeline", "请先选择有效的视频文件。", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(_animName))
            {
                EditorUtility.DisplayDialog("Green Screen Pipeline", "AnimName 不能为空。", "OK");
                return;
            }

            string ffmpeg = ResolveFfmpegPath(_ffmpegPath);
            if (string.IsNullOrEmpty(ffmpeg))
            {
                EditorUtility.DisplayDialog(
                    "Green Screen Pipeline",
                    "未找到 ffmpeg。\n请安装 ffmpeg 并加入 PATH，或在窗口中指定可执行文件路径。\n下载：https://ffmpeg.org/download.html",
                    "OK");
                _status = "失败：未找到 ffmpeg。";
                return;
            }

            string outAssetDir = BuildOutputAssetDir(_animName);
            string outAbsDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outAssetDir));

            if (Directory.Exists(outAbsDir))
            {
                string[] existing = Directory.GetFiles(outAbsDir, "frame_*.png");
                if (existing != null && existing.Length > 0)
                {
                    if (!EditorUtility.DisplayDialog(
                            "Green Screen Pipeline",
                            "输出目录已有 " + existing.Length + " 个 frame_*.png，将删除后重新导出：\n" + outAssetDir,
                            "覆盖",
                            "取消"))
                        return;
                }
            }

            try
            {
                EditorUtility.DisplayProgressBar("Green Screen Pipeline", "准备输出目录…", 0.05f);
                EnsureAssetFolder(outAssetDir);
                ClearExistingFrames(outAbsDir);

                string keyHex = ColorToHexRgb(_keyColor);
                string sim = _similarity.ToString("0.###", CultureInfo.InvariantCulture);
                string blend = _blend.ToString("0.###", CultureInfo.InvariantCulture);
                string vf = string.Format(
                    CultureInfo.InvariantCulture,
                    "fps={0},colorkey=0x{1}:{2}:{3},format=rgba",
                    _fps,
                    keyHex,
                    sim,
                    blend);

                string pattern = Path.Combine(outAbsDir, "frame_%06d.png");
                string args = string.Format(
                    CultureInfo.InvariantCulture,
                    "-y -i \"{0}\" -vf \"{1}\" \"{2}\"",
                    _videoPath,
                    vf,
                    pattern.Replace('\\', '/'));

                EditorUtility.DisplayProgressBar("Green Screen Pipeline", "ffmpeg 导出中…", 0.2f);
                string log;
                int exitCode = RunProcess(ffmpeg, args, out log);
                if (exitCode != 0)
                {
                    Debug.LogError("[GreenScreenVideoPipeline] ffmpeg failed (exit " + exitCode + "):\n" + log);
                    EditorUtility.DisplayDialog(
                        "Green Screen Pipeline",
                        "ffmpeg 失败（exit " + exitCode + "）。详见 Console。",
                        "OK");
                    _status = "失败：ffmpeg exit " + exitCode;
                    return;
                }

                EditorUtility.DisplayProgressBar("Green Screen Pipeline", "导入 Unity 资源…", 0.7f);
                AssetDatabase.Refresh();

                string[] pngs = Directory.GetFiles(outAbsDir, "frame_*.png");
                int count = pngs != null ? pngs.Length : 0;
                if (count < 1)
                {
                    EditorUtility.DisplayDialog("Green Screen Pipeline", "ffmpeg 成功但未生成 frame_*.png。", "OK");
                    _status = "失败：无输出帧。";
                    return;
                }

                ConfigureTextureImporters(outAssetDir);
                AssetDatabase.Refresh();

                bool rebuilt = false;
                bool isLangRenOut = string.Equals(
                    NormalizeAssetPath(outAssetDir),
                    NormalizeAssetPath(LangRenDzDicedAtlasBuilder.FramesSpriteFolder),
                    StringComparison.OrdinalIgnoreCase);
                if (_rebuildLangRenAtlas && isLangRenOut)
                {
                    EditorUtility.DisplayProgressBar("Green Screen Pipeline", "重建 LangRen DZ Diced Atlas…", 0.9f);
                    LangRenDzDicedAtlasBuilder.Build();
                    rebuilt = true;
                }

                _status = "完成：导出 " + count + " 帧 → " + outAssetDir
                          + (rebuilt ? "；已重建 LangRen DZ Diced Atlas。" : "。");
                Debug.Log("[GreenScreenVideoPipeline] " + _status + "\nffmpeg vf=" + vf);
                EditorUtility.DisplayDialog("Green Screen Pipeline", _status, "OK");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                _status = "异常：" + ex.Message;
                EditorUtility.DisplayDialog("Green Screen Pipeline", _status, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static string ResolveFfmpegPath(string overridePath)
        {
            if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
                return overridePath;

            string fromPath = FindOnPath(Application.platform == RuntimePlatform.WindowsEditor ? "ffmpeg.exe" : "ffmpeg");
            if (!string.IsNullOrEmpty(fromPath))
                return fromPath;

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                string[] candidates =
                {
                    @"C:\ffmpeg\bin\ffmpeg.exe",
                    @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Links", "ffmpeg.exe")
                };
                for (int i = 0; i < candidates.Length; i++)
                {
                    if (File.Exists(candidates[i]))
                        return candidates[i];
                }
            }

            return null;
        }

        private static string FindOnPath(string fileName)
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            string[] parts = pathEnv.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                try
                {
                    string candidate = Path.Combine(parts[i].Trim().Trim('"'), fileName);
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch
                {
                    // ignore invalid PATH entries
                }
            }

            return null;
        }

        private static int RunProcess(string exe, string args, out string combinedLog)
        {
            var sb = new StringBuilder();
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory
            };

            using (var proc = new Process { StartInfo = psi })
            {
                proc.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        sb.AppendLine(e.Data);
                };
                proc.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        sb.AppendLine(e.Data);
                };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.WaitForExit();
                combinedLog = sb.ToString();
                return proc.ExitCode;
            }
        }

        private static string ColorToHexRgb(Color c)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
            int g = Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
            int b = Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
            return string.Format(CultureInfo.InvariantCulture, "{0:X2}{1:X2}{2:X2}", r, g, b);
        }

        private static void ClearExistingFrames(string absDir)
        {
            if (!Directory.Exists(absDir))
            {
                Directory.CreateDirectory(absDir);
                return;
            }

            string[] files = Directory.GetFiles(absDir, "frame_*.png");
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    File.Delete(files[i]);
                    string meta = files[i] + ".meta";
                    if (File.Exists(meta))
                        File.Delete(meta);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[GreenScreenVideoPipeline] Failed to delete " + files[i] + ": " + ex.Message);
                }
            }
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            assetPath = NormalizeAssetPath(assetPath);
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            string[] parts = assetPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }

            string abs = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            if (!Directory.Exists(abs))
                Directory.CreateDirectory(abs);
            AssetDatabase.Refresh();
        }

        private static void ConfigureTextureImporters(string assetFolder)
        {
            assetFolder = NormalizeAssetPath(assetFolder);
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
            string[] pngs = Directory.GetFiles(abs, "frame_*.png");
            for (int i = 0; i < pngs.Length; i++)
            {
                string full = Path.GetFullPath(pngs[i]);
                if (!full.StartsWith(dataAbs, StringComparison.OrdinalIgnoreCase))
                    continue;
                string relative = full.Substring(dataAbs.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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
