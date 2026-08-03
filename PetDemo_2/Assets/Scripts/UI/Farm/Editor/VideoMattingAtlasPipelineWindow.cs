#if UNITY_EDITOR
// SPEC_VideoMattingAtlas.md v1.0 — EditorWindow for Video Matting Atlas pipeline.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using PetDemo.UI.VideoMatting;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// Collects parameters, invokes Tools/VideoMatting/run_pipeline.py, imports atlas assets, builds preview Prefab.
    /// Does not reimplement pipeline algorithms.
    /// </summary>
    public sealed class VideoMattingAtlasPipelineWindow : EditorWindow
    {
        private const string PrefPrefix = "PetDemo.VideoMatting.";
        private const string MenuPath = "Tools/PetDemo/Video Matting Atlas Pipeline";

        // Defaults must match SPEC / CLI
        private const float DefaultFps = 15f;
        private const int DefaultMaxSide = 512;
        private const int DefaultPreparePad = 4;
        private const int DefaultAlphaThreshold = 8;
        private const int DefaultUnitSize = 64;
        private const int DefaultDicePad = 2;
        private const int DefaultAtlasLimit = 2048;
        private const string DefaultModelId = "ZhengPeng7/BiRefNet";
        private const string DefaultAtlasFormat = "png";
        private const string DefaultKeyColor = "0,255,0";
        private const float DefaultKeyTolerance = 40f;

        private static readonly string[] DeviceOptions = { "auto", "cuda", "cpu" };
        private static readonly string[] AtlasFormatOptions = { "png", "webp", "tga" };

        private string _videoPath = "";
        private string _outRoot = "";
        private float _fps = DefaultFps;
        private int _deviceIndex;
        private string _modelId = DefaultModelId;
        private bool _chromaKey;
        private string _keyColor = DefaultKeyColor;
        private float _keyTolerance = DefaultKeyTolerance;
        private int _maxSide = DefaultMaxSide;
        private int _preparePad = DefaultPreparePad;
        private int _alphaThreshold = DefaultAlphaThreshold;
        private int _unitSize = DefaultUnitSize;
        private int _dicePad = DefaultDicePad;
        private int _atlasLimit = DefaultAtlasLimit;
        private int _atlasFormatIndex;
        private bool _trim;
        private bool _skipExtract;
        private bool _skipMatte;
        private bool _skipPrepare;
        private bool _skipDice;
        private string _pythonExe = "";
        private string _pipelineScript = "";
        private string _diceCli = "";
        private float _playFps = DefaultFps;
        private string _importDir = "";
        private bool _autoImport = true;

        private enum RunKind { None, Pipeline, SetupVenv }

        private readonly AsyncProcessRunner _proc = new AsyncProcessRunner();
        private bool _busy;
        private RunKind _runKind = RunKind.None;
        private string _status = "";
        private string _logTail = "";
        private readonly StringBuilder _log = new StringBuilder();
        private Vector2 _scroll;
        private Vector2 _logScroll;
        private double _runStarted;
        private string _lastOutRoot = "";

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var w = GetWindow<VideoMattingAtlasPipelineWindow>(true, "Video Matting Atlas", true);
            w.minSize = new Vector2(560, 720);
            w.Show();
        }

        private void OnEnable()
        {
            LoadPrefs();
            _proc.OnOutputLine = OnProcLine;
            _proc.OnExited = OnProcExited;
            if (string.IsNullOrEmpty(_pipelineScript))
                _pipelineScript = GetDefaultPipelineScript();
            if (string.IsNullOrEmpty(_pythonExe))
                _pythonExe = GetDefaultPython();
        }

        private void OnDisable()
        {
            EditorApplication.update -= Pump;
            if (_proc.IsRunning)
                _proc.Kill();
            SavePrefs();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("绿幕/实拍 → 抽帧 → 抠图 → 统一裁切 → SpriteDicing → Prefab", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "算法在 Python 流水线（Tools/VideoMatting）。本窗口只收集参数、调用 CLI、导入资源并生成播放 Prefab。\n" +
                "默认参数与 SPEC_VideoMattingAtlas.md / run_pipeline.py 一致。与 Qwen 绿幕窗口并行共存。",
                MessageType.Info);

            DrawInputSection();
            DrawExtractSection();
            DrawMatteSection();
            DrawPrepareSection();
            DrawDiceSection();
            DrawSkipSection();
            DrawEnvSection();
            DrawActions();
            DrawFeedback();

            EditorGUILayout.EndScrollView();

            if (GUI.changed)
                SavePrefs();
        }

        private void DrawInputSection()
        {
            EditorGUILayout.LabelField("输入", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _videoPath = EditorGUILayout.TextField("视频路径", _videoPath);
            if (GUILayout.Button("浏览…", GUILayout.Width(64)))
            {
                string picked = EditorUtility.OpenFilePanel("选择视频", string.IsNullOrEmpty(_videoPath) ? "" : Path.GetDirectoryName(_videoPath), "mp4,mov,webm,MP4,MOV,WEBM");
                if (!string.IsNullOrEmpty(picked))
                {
                    _videoPath = picked;
                    if (string.IsNullOrEmpty(_outRoot))
                        _outRoot = "";
                    if (string.IsNullOrEmpty(_importDir))
                        _importDir = SuggestImportDir(Path.GetFileNameWithoutExtension(_videoPath));
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _outRoot = EditorGUILayout.TextField("输出根目录", _outRoot);
            if (GUILayout.Button("浏览…", GUILayout.Width(64)))
            {
                string picked = EditorUtility.OpenFolderPanel("输出根目录", ResolveOutRootPreview(), "");
                if (!string.IsNullOrEmpty(picked))
                    _outRoot = picked;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(
                "留空则默认：PetDemo_2/Tools/VideoMatting/output/<视频stem>/（绝对路径）。也可填绝对路径。",
                MessageType.None);
        }

        private void DrawExtractSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("抽帧", EditorStyles.boldLabel);
            _fps = EditorGUILayout.FloatField("Target FPS", _fps);
        }

        private void DrawMatteSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("抠图", EditorStyles.boldLabel);
            _deviceIndex = EditorGUILayout.Popup("设备", _deviceIndex, DeviceOptions);
            _modelId = EditorGUILayout.TextField("模型 ID", _modelId);
            _chromaKey = EditorGUILayout.Toggle("使用绿幕色键", _chromaKey);
            if (_chromaKey)
            {
                _keyColor = EditorGUILayout.TextField("Key Color (R,G,B)", _keyColor);
                _keyTolerance = EditorGUILayout.FloatField("Tolerance", _keyTolerance);
            }
        }

        private void DrawPrepareSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("精灵预处理", EditorStyles.boldLabel);
            _maxSide = EditorGUILayout.IntField("Max Side", _maxSide);
            _preparePad = EditorGUILayout.IntField("BBox Pad", _preparePad);
            _alphaThreshold = EditorGUILayout.IntField("Alpha Threshold", _alphaThreshold);
        }

        private void DrawDiceSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("SpriteDicing", EditorStyles.boldLabel);
            _unitSize = EditorGUILayout.IntField("Unit Size", _unitSize);
            _dicePad = EditorGUILayout.IntField("Dice Pad", _dicePad);
            _atlasLimit = EditorGUILayout.IntField("Atlas Limit", _atlasLimit);
            _atlasFormatIndex = EditorGUILayout.Popup("Atlas Format", _atlasFormatIndex, AtlasFormatOptions);
            _trim = EditorGUILayout.Toggle("Trim（动画默认关）", _trim);
        }

        private void DrawSkipSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("分步控制", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _skipExtract = EditorGUILayout.ToggleLeft("Skip Extract", _skipExtract, GUILayout.Width(120));
            _skipMatte = EditorGUILayout.ToggleLeft("Skip Matte", _skipMatte, GUILayout.Width(110));
            _skipPrepare = EditorGUILayout.ToggleLeft("Skip Prepare", _skipPrepare, GUILayout.Width(120));
            _skipDice = EditorGUILayout.ToggleLeft("Skip Dice", _skipDice, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginDisabledGroup(_busy);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("仅抽帧"))
                RunPartial(skipMatte: true, skipPrepare: true, skipDice: true);
            if (GUILayout.Button("仅抠图"))
                RunPartial(skipExtract: true, skipPrepare: true, skipDice: true);
            if (GUILayout.Button("仅预处理"))
                RunPartial(skipExtract: true, skipMatte: true, skipDice: true);
            if (GUILayout.Button("仅打图集"))
                RunPartial(skipExtract: true, skipMatte: true, skipPrepare: true);
            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();
        }

        private void DrawEnvSection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("环境与导入", EditorStyles.boldLabel);

            DrawPathRow("Python", ref _pythonExe, false, "exe", "py");
            DrawPathRow("run_pipeline.py", ref _pipelineScript, false, "py", "py");
            DrawPathRow("Dice CLI", ref _diceCli, false, "exe", "exe");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("检测环境", GUILayout.Width(100)))
                CheckEnvironment();
            if (GUILayout.Button("创建/修复 venv", GUILayout.Width(120)))
                RunSetupVenv();
            EditorGUILayout.EndHorizontal();

            _playFps = EditorGUILayout.FloatField("播放 FPS", _playFps);
            _importDir = EditorGUILayout.TextField("Unity 导入目录", _importDir);
            EditorGUILayout.HelpBox("例如 Assets/Art/VFX/<Name>/（相对工程 Assets 的路径或 Assets 开头路径）。", MessageType.None);
            _autoImport = EditorGUILayout.Toggle("完成后自动导入", _autoImport);
        }

        private void DrawPathRow(string label, ref string value, bool folder, string ext, string filter)
        {
            EditorGUILayout.BeginHorizontal();
            value = EditorGUILayout.TextField(label, value);
            if (GUILayout.Button("浏览…", GUILayout.Width(64)))
            {
                if (folder)
                {
                    string picked = EditorUtility.OpenFolderPanel(label, "", "");
                    if (!string.IsNullOrEmpty(picked))
                        value = picked;
                }
                else
                {
                    string picked = EditorUtility.OpenFilePanel(label, "", filter);
                    if (!string.IsNullOrEmpty(picked))
                        value = picked;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawActions()
        {
            EditorGUILayout.Space(8);
            EditorGUI.BeginDisabledGroup(_busy);
            if (GUILayout.Button("运行全流程", GUILayout.Height(32)))
                RunFull();
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(!_busy);
            if (GUILayout.Button("停止"))
            {
                _proc.Kill();
                AppendLog("[editor] stop requested");
                _status = "正在停止…";
            }
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("打开输出目录"))
                OpenOutDir();

            if (GUILayout.Button("导入/刷新到工程"))
            {
                string err;
                if (!TryImportToProject(out err))
                    EditorUtility.DisplayDialog("导入失败", err, "确定");
                else
                    _status = "导入完成：" + ResolveImportAssetPath();
            }

            if (GUILayout.Button("生成/更新播放 Prefab"))
            {
                string err;
                if (!TryCreateOrUpdatePrefab(out err))
                    EditorUtility.DisplayDialog("Prefab 失败", err, "确定");
                else
                    _status = "Prefab 已更新";
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFeedback()
        {
            EditorGUILayout.Space(8);
            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, MessageType.None);

            EditorGUILayout.LabelField("日志", EditorStyles.boldLabel);
            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.MinHeight(180));
            EditorGUILayout.TextArea(_logTail, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void RunFull()
        {
            RunWithSkips(_skipExtract, _skipMatte, _skipPrepare, _skipDice);
        }

        private void RunPartial(bool skipExtract = false, bool skipMatte = false, bool skipPrepare = false, bool skipDice = false)
        {
            RunWithSkips(skipExtract, skipMatte, skipPrepare, skipDice);
        }

        private void RunWithSkips(bool skipExtract, bool skipMatte, bool skipPrepare, bool skipDice)
        {
            if (_busy)
            {
                EditorUtility.DisplayDialog("忙碌", "流水线正在运行，请先停止或等待完成。", "确定");
                return;
            }

            string err;
            if (!ValidateBeforeRun(out err))
            {
                EditorUtility.DisplayDialog("参数错误", err, "确定");
                return;
            }

            string outRoot = ResolveOutRoot();
            _lastOutRoot = outRoot;
            string args = BuildCliArgs(outRoot, skipExtract, skipMatte, skipPrepare, skipDice);

            _log.Length = 0;
            _logTail = "";
            AppendLog("[editor] " + Quote(_pythonExe) + " " + args);
            _busy = true;
            _runKind = RunKind.Pipeline;
            _status = "运行中…";
            _runStarted = EditorApplication.timeSinceStartup;
            SavePrefs();

            string workDir = Path.GetDirectoryName(_pipelineScript);
            if (!_proc.Start(_pythonExe, args, workDir))
            {
                _busy = false;
                _runKind = RunKind.None;
                _status = "启动失败：无法创建 Python 进程（检查解释器路径）";
                EditorUtility.DisplayDialog("启动失败", _status, "确定");
                return;
            }

            EditorApplication.update -= Pump;
            EditorApplication.update += Pump;
        }

        private bool ValidateBeforeRun(out string err)
        {
            if (string.IsNullOrWhiteSpace(_videoPath) || !File.Exists(_videoPath))
            {
                err = "视频路径不存在：" + _videoPath;
                return false;
            }
            if (_fps <= 0f)
            {
                err = "FPS 必须 > 0";
                return false;
            }
            if (_maxSide <= 0)
            {
                err = "Max Side 必须 > 0";
                return false;
            }
            if (string.IsNullOrWhiteSpace(_pythonExe) || !File.Exists(_pythonExe))
            {
                err = "Python 解释器不存在。请先「创建/修复 venv」或指定路径。\n" + _pythonExe;
                return false;
            }
            if (string.IsNullOrWhiteSpace(_pipelineScript) || !File.Exists(_pipelineScript))
            {
                err = "run_pipeline.py 不存在：" + _pipelineScript;
                return false;
            }
            if (!string.IsNullOrWhiteSpace(_diceCli) && !File.Exists(_diceCli))
            {
                err = "Dice CLI 路径无效：" + _diceCli;
                return false;
            }
            err = null;
            return true;
        }

        private string BuildCliArgs(string outRoot, bool skipExtract, bool skipMatte, bool skipPrepare, bool skipDice)
        {
            var sb = new StringBuilder();
            sb.Append(Quote(_pipelineScript));
            sb.Append(" --video ").Append(Quote(_videoPath));
            sb.Append(" --out-root ").Append(Quote(outRoot));
            sb.Append(" --fps ").Append(_fps.ToString(CultureInfo.InvariantCulture));
            sb.Append(" --device ").Append(DeviceOptions[Mathf.Clamp(_deviceIndex, 0, DeviceOptions.Length - 1)]);
            sb.Append(" --model-id ").Append(Quote(_modelId));
            if (_chromaKey)
            {
                sb.Append(" --chroma-key");
                sb.Append(" --key-color ").Append(Quote(_keyColor));
                sb.Append(" --key-tolerance ").Append(_keyTolerance.ToString(CultureInfo.InvariantCulture));
            }
            sb.Append(" --max-side ").Append(_maxSide);
            sb.Append(" --prepare-pad ").Append(_preparePad);
            sb.Append(" --alpha-threshold ").Append(_alphaThreshold);
            sb.Append(" --unit-size ").Append(_unitSize);
            sb.Append(" --dice-pad ").Append(_dicePad);
            sb.Append(" --atlas-limit ").Append(_atlasLimit);
            sb.Append(" --atlas-format ").Append(AtlasFormatOptions[Mathf.Clamp(_atlasFormatIndex, 0, AtlasFormatOptions.Length - 1)]);
            if (_trim)
                sb.Append(" --trim");
            if (!string.IsNullOrWhiteSpace(_diceCli))
                sb.Append(" --dice-cli ").Append(Quote(_diceCli));
            if (skipExtract) sb.Append(" --skip-extract");
            if (skipMatte) sb.Append(" --skip-matte");
            if (skipPrepare) sb.Append(" --skip-prepare");
            if (skipDice) sb.Append(" --skip-dice");
            return sb.ToString();
        }

        private static string Quote(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "\"\"";
            return "\"" + path.Replace("\"", "\\\"") + "\"";
        }

        private void Pump()
        {
            _proc.Pump();
            Repaint();
        }

        private void OnProcLine(string line)
        {
            AppendLog(line);
        }

        private void OnProcExited(int code)
        {
            EditorApplication.update -= Pump;
            _busy = false;
            var kind = _runKind;
            _runKind = RunKind.None;
            double elapsed = EditorApplication.timeSinceStartup - _runStarted;

            if (kind == RunKind.SetupVenv)
            {
                if (code == 0)
                {
                    string venvPy = GetDefaultPython();
                    if (!string.IsNullOrEmpty(venvPy))
                        _pythonExe = venvPy;
                    SavePrefs();
                    _status = string.Format(CultureInfo.InvariantCulture, "venv 就绪（耗时 {0:F1}s）", elapsed);
                    AppendLog("[editor] setup_venv exit 0 → " + _pythonExe);
                }
                else
                {
                    _status = "venv 安装失败 exit=" + code;
                    AppendLog("[editor] setup_venv exit " + code);
                    EditorUtility.DisplayDialog("venv 失败", _status + "\n请查看窗口日志。", "确定");
                }
                Repaint();
                return;
            }

            if (code == 0)
            {
                _status = string.Format(CultureInfo.InvariantCulture, "成功（耗时 {0:F1}s）", elapsed);
                AppendLog("[editor] exit 0");
                if (_autoImport)
                {
                    string err;
                    if (TryImportToProject(out err))
                    {
                        string prefabErr;
                        TryCreateOrUpdatePrefab(out prefabErr);
                        if (!string.IsNullOrEmpty(prefabErr))
                            AppendLog("[editor] prefab: " + prefabErr);
                    }
                    else
                    {
                        AppendLog("[editor] import failed: " + err);
                        _status += "；导入失败：" + err;
                    }
                }
            }
            else
            {
                _status = string.Format(CultureInfo.InvariantCulture, "失败 exit={0}（耗时 {1:F1}s）", code, elapsed);
                AppendLog("[editor] exit " + code);
                EditorUtility.DisplayDialog("流水线失败", _status + "\n请查看窗口日志。", "确定");
            }
            Repaint();
        }

        private void AppendLog(string line)
        {
            _log.AppendLine(line);
            // Keep tail for UI
            string full = _log.ToString();
            if (full.Length > 20000)
                _logTail = full.Substring(full.Length - 20000);
            else
                _logTail = full;
            _logScroll.y = float.MaxValue;
        }

        private void OpenOutDir()
        {
            string dir = ResolveOutRoot();
            if (!Directory.Exists(dir))
            {
                EditorUtility.DisplayDialog("目录不存在", dir, "确定");
                return;
            }
            EditorUtility.RevealInFinder(dir);
        }

        private string ResolveOutRoot()
        {
            if (!string.IsNullOrWhiteSpace(_outRoot))
                return Path.GetFullPath(_outRoot);
            string stem = Path.GetFileNameWithoutExtension(_videoPath);
            if (string.IsNullOrEmpty(stem))
                stem = "output";
            return Path.GetFullPath(Path.Combine(GetToolsRoot(), "output", stem));
        }

        private string ResolveOutRootPreview()
        {
            try { return ResolveOutRoot(); }
            catch { return GetToolsRoot(); }
        }

        private string ResolveAtlasDir()
        {
            string root = !string.IsNullOrEmpty(_lastOutRoot) ? _lastOutRoot : ResolveOutRoot();
            return Path.Combine(root, "atlas");
        }

        private string ResolveImportAssetPath()
        {
            string dir = _importDir;
            if (string.IsNullOrWhiteSpace(dir))
            {
                string stem = Path.GetFileNameWithoutExtension(_videoPath);
                if (string.IsNullOrEmpty(stem) && !string.IsNullOrEmpty(_lastOutRoot))
                    stem = Path.GetFileName(_lastOutRoot.TrimEnd('/', '\\'));
                dir = SuggestImportDir(stem);
            }
            dir = dir.Replace('\\', '/').Trim();
            if (!dir.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                !dir.Equals("Assets", StringComparison.OrdinalIgnoreCase))
                dir = "Assets/" + dir.TrimStart('/');
            return dir.TrimEnd('/');
        }

        private static string SuggestImportDir(string name)
        {
            if (string.IsNullOrEmpty(name))
                name = "Anim";
            // Sanitize for Unity path
            name = Regex.Replace(name, @"[^\w\-\u4e00-\u9fff ]+", "_").Trim();
            return "Assets/Art/VFX/" + name;
        }

        private bool TryImportToProject(out string err)
        {
            string atlasDir = ResolveAtlasDir();
            if (!Directory.Exists(atlasDir))
            {
                err = "atlas 目录不存在：" + atlasDir;
                return false;
            }

            string spritesJson = Path.Combine(atlasDir, "sprites.json");
            if (!File.Exists(spritesJson))
            {
                err = "缺少 sprites.json：" + spritesJson;
                return false;
            }

            string assetDir = ResolveImportAssetPath();
            string absAssetDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetDir));
            Directory.CreateDirectory(absAssetDir);

            // Copy atlas_*.* and sprites.json
            foreach (string file in Directory.GetFiles(atlasDir))
            {
                string name = Path.GetFileName(file);
                string lower = name.ToLowerInvariant();
                if (lower == "sprites.json" || (lower.StartsWith("atlas_") &&
                    (lower.EndsWith(".png") || lower.EndsWith(".webp") || lower.EndsWith(".tga"))))
                {
                    File.Copy(file, Path.Combine(absAssetDir, name), true);
                }
            }

            AssetDatabase.Refresh();

            // Configure texture importers
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { assetDir });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileName(path);
                if (fileName == null || !fileName.StartsWith("atlas_", StringComparison.OrdinalIgnoreCase))
                    continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    continue;
                bool dirty = false;
                if (importer.textureType != TextureImporterType.Default)
                {
                    importer.textureType = TextureImporterType.Default;
                    dirty = true;
                }
                if (importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = false;
                    dirty = true;
                }
                if (!importer.alphaIsTransparency)
                {
                    importer.alphaIsTransparency = true;
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

            err = null;
            AppendLog("[editor] imported → " + assetDir);
            return true;
        }

        private bool TryCreateOrUpdatePrefab(out string err)
        {
            string assetDir = ResolveImportAssetPath();
            string jsonPath = assetDir + "/sprites.json";
            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
            if (textAsset == null)
            {
                err = "未找到 " + jsonPath + "，请先导入。";
                return false;
            }

            // Load atlases sorted by index
            var atlasList = new List<Texture2D>();
            for (int i = 0; i < 64; i++)
            {
                string p = string.Format(CultureInfo.InvariantCulture, "{0}/atlas_{1}.png", assetDir, i);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                if (tex == null)
                {
                    // try other formats
                    tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetDir + "/atlas_" + i + ".webp");
                    if (tex == null)
                        tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetDir + "/atlas_" + i + ".tga");
                }
                if (tex == null)
                    break;
                atlasList.Add(tex);
            }
            if (atlasList.Count == 0)
            {
                err = "未找到 atlas_*.png/webp/tga：" + assetDir;
                return false;
            }

            string prefabPath = assetDir + "/DicedSpriteAnimation.prefab";
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject instance;
            bool isNew = root == null;
            if (isNew)
            {
                instance = new GameObject("DicedSpriteAnimation");
            }
            else
            {
                instance = PrefabUtility.LoadPrefabContents(prefabPath);
            }

            try
            {
                var player = instance.GetComponent<DicedSpriteAnimationPlayer>();
                if (player == null)
                    player = instance.AddComponent<DicedSpriteAnimationPlayer>();
                if (instance.GetComponent<MeshFilter>() == null)
                    instance.AddComponent<MeshFilter>();
                if (instance.GetComponent<MeshRenderer>() == null)
                    instance.AddComponent<MeshRenderer>();

                var so = new SerializedObject(player);
                so.FindProperty("spritesJson").objectReferenceValue = textAsset;
                var atlasesProp = so.FindProperty("atlases");
                atlasesProp.arraySize = atlasList.Count;
                for (int i = 0; i < atlasList.Count; i++)
                    atlasesProp.GetArrayElementAtIndex(i).objectReferenceValue = atlasList[i];
                so.FindProperty("defaultFps").floatValue = _playFps > 0 ? _playFps : DefaultFps;
                so.FindProperty("playOnEnable").boolValue = true;
                so.FindProperty("loopOnEnable").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();

                if (isNew)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(Path.Combine(Application.dataPath, "..", prefabPath))));
                    PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                    DestroyImmediate(instance);
                }
                else
                {
                    PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                    PrefabUtility.UnloadPrefabContents(instance);
                }
            }
            catch (Exception ex)
            {
                if (!isNew && instance != null)
                    PrefabUtility.UnloadPrefabContents(instance);
                if (isNew && instance != null)
                    DestroyImmediate(instance);
                err = ex.Message;
                return false;
            }

            AssetDatabase.Refresh();
            AppendLog("[editor] prefab → " + prefabPath);
            err = null;
            return true;
        }

        private void CheckEnvironment()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Python: " + (File.Exists(_pythonExe) ? "OK " + _pythonExe : "MISSING " + _pythonExe));
            sb.AppendLine("Pipeline: " + (File.Exists(_pipelineScript) ? "OK " + _pipelineScript : "MISSING " + _pipelineScript));
            string dice = string.IsNullOrWhiteSpace(_diceCli)
                ? Path.Combine(GetToolsRoot(), "bin", "dice-windows-x64.exe")
                : _diceCli;
            sb.AppendLine("Dice CLI: " + (File.Exists(dice) ? "OK " + dice : "MISSING（首次打图集时可自动下载） " + dice));
            if (File.Exists(_pythonExe))
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = _pythonExe,
                        Arguments = "-c \"import torch; print('torch', torch.__version__, 'cuda', torch.cuda.is_available())\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    using (var p = Process.Start(psi))
                    {
                        string o = p.StandardOutput.ReadToEnd();
                        string e = p.StandardError.ReadToEnd();
                        p.WaitForExit(15000);
                        sb.AppendLine(o.Trim());
                        if (!string.IsNullOrWhiteSpace(e))
                            sb.AppendLine(e.Trim());
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine("torch check failed: " + ex.Message);
                }
            }
            AppendLog(sb.ToString());
            EditorUtility.DisplayDialog("环境检测", sb.ToString(), "确定");
        }

        private void RunSetupVenv()
        {
            if (_busy)
            {
                EditorUtility.DisplayDialog("忙碌", "请等待当前任务结束。", "确定");
                return;
            }
            string bat = Path.Combine(GetToolsRoot(), "setup_venv.bat");
            if (!File.Exists(bat))
            {
                EditorUtility.DisplayDialog("缺少脚本", bat, "确定");
                return;
            }
            _log.Length = 0;
            _logTail = "";
            _busy = true;
            _runKind = RunKind.SetupVenv;
            _status = "正在创建 venv…";
            _runStarted = EditorApplication.timeSinceStartup;
            if (!_proc.Start("cmd.exe", "/c " + Quote(bat), GetToolsRoot()))
            {
                _busy = false;
                _runKind = RunKind.None;
                _status = "无法启动 setup_venv.bat";
                return;
            }
            EditorApplication.update -= Pump;
            EditorApplication.update += Pump;
        }

        private static string GetToolsRoot()
        {
            // Assets/../Tools/VideoMatting
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "VideoMatting"));
        }

        private static string GetDefaultPipelineScript()
        {
            return Path.Combine(GetToolsRoot(), "run_pipeline.py");
        }

        private static string GetDefaultPython()
        {
            string venv = Path.Combine(GetToolsRoot(), ".venv", "Scripts", "python.exe");
            if (File.Exists(venv))
                return venv;
            return "";
        }

        private void LoadPrefs()
        {
            _videoPath = EditorPrefs.GetString(PrefPrefix + "VideoPath", "");
            _outRoot = EditorPrefs.GetString(PrefPrefix + "OutRoot", "");
            _fps = EditorPrefs.GetFloat(PrefPrefix + "Fps", DefaultFps);
            _deviceIndex = Mathf.Clamp(EditorPrefs.GetInt(PrefPrefix + "Device", 0), 0, DeviceOptions.Length - 1);
            _modelId = EditorPrefs.GetString(PrefPrefix + "ModelId", DefaultModelId);
            _chromaKey = EditorPrefs.GetBool(PrefPrefix + "ChromaKey", false);
            _keyColor = EditorPrefs.GetString(PrefPrefix + "KeyColor", DefaultKeyColor);
            _keyTolerance = EditorPrefs.GetFloat(PrefPrefix + "KeyTolerance", DefaultKeyTolerance);
            _maxSide = EditorPrefs.GetInt(PrefPrefix + "MaxSide", DefaultMaxSide);
            _preparePad = EditorPrefs.GetInt(PrefPrefix + "PreparePad", DefaultPreparePad);
            _alphaThreshold = EditorPrefs.GetInt(PrefPrefix + "AlphaThreshold", DefaultAlphaThreshold);
            _unitSize = EditorPrefs.GetInt(PrefPrefix + "UnitSize", DefaultUnitSize);
            _dicePad = EditorPrefs.GetInt(PrefPrefix + "DicePad", DefaultDicePad);
            _atlasLimit = EditorPrefs.GetInt(PrefPrefix + "AtlasLimit", DefaultAtlasLimit);
            _atlasFormatIndex = Mathf.Clamp(EditorPrefs.GetInt(PrefPrefix + "AtlasFormat", 0), 0, AtlasFormatOptions.Length - 1);
            _trim = EditorPrefs.GetBool(PrefPrefix + "Trim", false);
            _skipExtract = EditorPrefs.GetBool(PrefPrefix + "SkipExtract", false);
            _skipMatte = EditorPrefs.GetBool(PrefPrefix + "SkipMatte", false);
            _skipPrepare = EditorPrefs.GetBool(PrefPrefix + "SkipPrepare", false);
            _skipDice = EditorPrefs.GetBool(PrefPrefix + "SkipDice", false);
            _pythonExe = EditorPrefs.GetString(PrefPrefix + "PythonExe", "");
            _pipelineScript = EditorPrefs.GetString(PrefPrefix + "PipelineScript", "");
            _diceCli = EditorPrefs.GetString(PrefPrefix + "DiceCli", "");
            _playFps = EditorPrefs.GetFloat(PrefPrefix + "PlayFps", DefaultFps);
            _importDir = EditorPrefs.GetString(PrefPrefix + "ImportDir", "");
            _autoImport = EditorPrefs.GetBool(PrefPrefix + "AutoImport", true);
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(PrefPrefix + "VideoPath", _videoPath ?? "");
            EditorPrefs.SetString(PrefPrefix + "OutRoot", _outRoot ?? "");
            EditorPrefs.SetFloat(PrefPrefix + "Fps", _fps);
            EditorPrefs.SetInt(PrefPrefix + "Device", _deviceIndex);
            EditorPrefs.SetString(PrefPrefix + "ModelId", _modelId ?? "");
            EditorPrefs.SetBool(PrefPrefix + "ChromaKey", _chromaKey);
            EditorPrefs.SetString(PrefPrefix + "KeyColor", _keyColor ?? "");
            EditorPrefs.SetFloat(PrefPrefix + "KeyTolerance", _keyTolerance);
            EditorPrefs.SetInt(PrefPrefix + "MaxSide", _maxSide);
            EditorPrefs.SetInt(PrefPrefix + "PreparePad", _preparePad);
            EditorPrefs.SetInt(PrefPrefix + "AlphaThreshold", _alphaThreshold);
            EditorPrefs.SetInt(PrefPrefix + "UnitSize", _unitSize);
            EditorPrefs.SetInt(PrefPrefix + "DicePad", _dicePad);
            EditorPrefs.SetInt(PrefPrefix + "AtlasLimit", _atlasLimit);
            EditorPrefs.SetInt(PrefPrefix + "AtlasFormat", _atlasFormatIndex);
            EditorPrefs.SetBool(PrefPrefix + "Trim", _trim);
            EditorPrefs.SetBool(PrefPrefix + "SkipExtract", _skipExtract);
            EditorPrefs.SetBool(PrefPrefix + "SkipMatte", _skipMatte);
            EditorPrefs.SetBool(PrefPrefix + "SkipPrepare", _skipPrepare);
            EditorPrefs.SetBool(PrefPrefix + "SkipDice", _skipDice);
            EditorPrefs.SetString(PrefPrefix + "PythonExe", _pythonExe ?? "");
            EditorPrefs.SetString(PrefPrefix + "PipelineScript", _pipelineScript ?? "");
            EditorPrefs.SetString(PrefPrefix + "DiceCli", _diceCli ?? "");
            EditorPrefs.SetFloat(PrefPrefix + "PlayFps", _playFps);
            EditorPrefs.SetString(PrefPrefix + "ImportDir", _importDir ?? "");
            EditorPrefs.SetBool(PrefPrefix + "AutoImport", _autoImport);
        }
    }
}
#endif
