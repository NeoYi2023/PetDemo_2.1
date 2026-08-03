#if UNITY_EDITOR
// SPEC §9.14.11 v3.277：绿幕视频 → 15fps 透明序列帧（ffmpeg 拆帧+抽帧 → Qwen-Image-Layered AI 分层抠图）。
using System;
using System.Globalization;
using System.IO;
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
        private const string PrefFps = "PetDemo.GreenScreenPipeline.Fps";
        private const string PrefRebuildAtlas = "PetDemo.GreenScreenPipeline.RebuildAtlas";
        private const string PrefPythonExe = "PetDemo.GreenScreenPipeline.PythonExe";
        private const string PrefLayers = "PetDemo.GreenScreenPipeline.Layers";
        private const string PrefSteps = "PetDemo.GreenScreenPipeline.Steps";
        private const string PrefResolution = "PetDemo.GreenScreenPipeline.Resolution";
        private const string PrefSubjectLayer = "PetDemo.GreenScreenPipeline.SubjectLayerIndex";
        private const string PrefSeed = "PetDemo.GreenScreenPipeline.Seed";
        private const string PrefOffload = "PetDemo.GreenScreenPipeline.Offload";
        private const string PrefSource = "PetDemo.GreenScreenPipeline.Source";
        private const string PrefModelPath = "PetDemo.GreenScreenPipeline.ModelPath";

        private const string DefaultAnimName = "LangRen_DZ _1";
        private const int DefaultFps = 15;
        private const int DefaultLayers = 4;
        private const int DefaultSteps = 50;
        private const int DefaultSeed = 777;
        private const int DefaultSubjectLayer = 1;

        private static readonly int[] ResolutionOptions = { 640, 1024 };
        private static readonly string[] OffloadOptions = { "model", "sequential", "none" };
        private static readonly string[] SourceOptions = { "modelscope", "hf", "hf-mirror", "local" };

        private enum Stage { None, SetupVenv, Extract, PreviewExtract, PreviewPython, BatchPython, CheckEnv, PurgeCache }

        private string _videoPath = "";
        private VideoClip _videoClip;
        private string _animName = DefaultAnimName;
        private int _fps = DefaultFps;
        private string _ffmpegPath = "";
        private string _pythonExe = "";
        private int _layers = DefaultLayers;
        private int _steps = DefaultSteps;
        private int _resolution = 640;
        private int _subjectLayer = DefaultSubjectLayer;
        private int _seed = DefaultSeed;
        private int _offloadIndex;
        private int _sourceIndex;
        private string _modelPath = "";
        private bool _rebuildAtlas;

        private Stage _stage = Stage.None;
        private bool _cancelRequested;
        private readonly AsyncProcessRunner _proc = new AsyncProcessRunner();
        private float _progress01;
        private string _progressText = "";
        private string _status = "";
        private string _logTail = "";
        private Texture2D _previewTex;
        private Vector2 _scroll;

        [MenuItem("Tools/PetDemo/Green Screen Video To Frames")]
        public static void Open()
        {
            var window = GetWindow<GreenScreenVideoPipelineWindow>(true, "Green Screen → Frames (AI)", true);
            window.minSize = new Vector2(520, 700);
            window.Show();
        }

        private bool IsRunning => _stage != Stage.None;

        private void OnEnable()
        {
            _ffmpegPath = EditorPrefs.GetString(PrefFfmpegPath, "");
            _animName = EditorPrefs.GetString(PrefAnimName, DefaultAnimName);
            _fps = EditorPrefs.GetInt(PrefFps, DefaultFps);
            _rebuildAtlas = EditorPrefs.GetBool(PrefRebuildAtlas, false);
            _pythonExe = EditorPrefs.GetString(PrefPythonExe, "");
            _layers = EditorPrefs.GetInt(PrefLayers, DefaultLayers);
            _steps = EditorPrefs.GetInt(PrefSteps, DefaultSteps);
            _resolution = EditorPrefs.GetInt(PrefResolution, 640);
            _subjectLayer = EditorPrefs.GetInt(PrefSubjectLayer, DefaultSubjectLayer);
            _seed = EditorPrefs.GetInt(PrefSeed, DefaultSeed);
            _offloadIndex = Mathf.Clamp(EditorPrefs.GetInt(PrefOffload, 0), 0, OffloadOptions.Length - 1);
            _sourceIndex = Mathf.Clamp(EditorPrefs.GetInt(PrefSource, 0), 0, SourceOptions.Length - 1);
            _modelPath = EditorPrefs.GetString(PrefModelPath, "");

            _proc.OnOutputLine = OnProcLine;
            _proc.OnExited = OnProcExited;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Pump;
            if (_proc.IsRunning)
                _proc.Kill();
            if (_previewTex != null)
            {
                DestroyImmediate(_previewTex);
                _previewTex = null;
            }
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(PrefFfmpegPath, _ffmpegPath ?? "");
            EditorPrefs.SetString(PrefAnimName, _animName ?? "");
            EditorPrefs.SetInt(PrefFps, _fps);
            EditorPrefs.SetBool(PrefRebuildAtlas, _rebuildAtlas);
            EditorPrefs.SetString(PrefPythonExe, _pythonExe ?? "");
            EditorPrefs.SetInt(PrefLayers, _layers);
            EditorPrefs.SetInt(PrefSteps, _steps);
            EditorPrefs.SetInt(PrefResolution, _resolution);
            EditorPrefs.SetInt(PrefSubjectLayer, _subjectLayer);
            EditorPrefs.SetInt(PrefSeed, _seed);
            EditorPrefs.SetInt(PrefOffload, _offloadIndex);
            EditorPrefs.SetInt(PrefSource, _sourceIndex);
            EditorPrefs.SetString(PrefModelPath, _modelPath ?? "");
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("绿幕视频 → 15fps 透明序列帧（AI 分层抠图）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "流水线：ffmpeg 拆帧+抽帧 → Qwen-Image-Layered RGBA 分层（取主体层，去绿幕与「AI生成」水印）。\n" +
                "输出：Assets/Resources/SpriteDicing/{AnimName}/frames_sprite/frame_%06d.png",
                MessageType.Info);

            DrawVideoSection();
            DrawFfmpegSection();
            DrawPythonSection();
            DrawQwenSection();
            DrawRunSection();
            DrawPreviewSection();

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(_status, MessageType.None);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("说明", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "1. 首次使用先点「创建/修复 venv」（需 Python 3.10~3.12；下载 torch CUDA 版约数 GB）。\n" +
                "2. 模型权重首次运行自动下载到工程盘 Library/PetDemoQwenMatting/model_cache（约数十 GB；需 ≥40GB 空闲）。\n" +
                "3. 若遇「磁盘已满 / *.incomplete / model.safetensors 找不到」，先点「清理残缺模型缓存」再重试。\n" +
                "4. 推荐流程：先点「① 首帧分层预览」查看图层拼图 → 填写主体层序号 → 再 Run。\n" +
                "5. 修改层数后请重新预览；序号存 EditorPrefs，下次一键 Run 直接使用。\n" +
                "6. diffusion 逐帧推理慢（RTX 3060 12GB 约分钟级/帧），Run 期间可中途取消。",
                EditorStyles.wordWrappedLabel);

            EditorGUILayout.EndScrollView();
        }

        private void DrawVideoSection()
        {
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
            EditorGUILayout.LabelField("输出目录", BuildOutputAssetDir(_animName));
            _fps = Mathf.Max(1, EditorGUILayout.IntField("目标 FPS", _fps));

            if (EditorGUI.EndChangeCheck())
                SavePrefs();
        }

        private void DrawFfmpegSection()
        {
            EditorGUI.BeginChangeCheck();
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
            if (EditorGUI.EndChangeCheck())
                SavePrefs();
        }

        private void DrawPythonSection()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Python venv（QwenMatting）", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                _pythonExe = EditorGUILayout.TextField("python.exe（可空=默认 venv）", _pythonExe);
                if (GUILayout.Button("浏览…", GUILayout.Width(64)))
                {
                    string picked = EditorUtility.OpenFilePanel("选择 venv 的 python.exe", "", "exe");
                    if (!string.IsNullOrEmpty(picked))
                        _pythonExe = picked.Replace('\\', '/');
                }
            }
            if (EditorGUI.EndChangeCheck())
                SavePrefs();

            string resolved = ResolvePythonExe(_pythonExe);
            bool ok = !string.IsNullOrEmpty(resolved);
            EditorGUILayout.LabelField("解析结果", ok ? resolved : "（未找到 venv，请先创建）");
            if (!ok)
                EditorGUILayout.HelpBox("未找到 Python venv。点击「创建/修复 venv」自动安装依赖（不污染全局 Python）。", MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(IsRunning))
                {
                    if (GUILayout.Button("创建/修复 venv（安装依赖）"))
                        StartSetupVenv();
                    if (GUILayout.Button("检查环境", GUILayout.Width(80)))
                        StartCheckEnv();
                }
            }

            EditorGUILayout.LabelField("模型缓存目录", GetModelCacheDir());
            using (new EditorGUI.DisabledScope(IsRunning))
            {
                if (GUILayout.Button("清理残缺模型缓存（工程盘 + 系统盘 C:\\Users\\…\\.cache）"))
                    StartPurgeCache();
            }
        }

        private void DrawQwenSection()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Qwen-Image-Layered 参数", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            _layers = EditorGUILayout.IntSlider("分解层数 layers", _layers, 3, 8);
            _steps = Mathf.Max(1, EditorGUILayout.IntField("推理步数 steps", _steps));
            int resIdx = Array.IndexOf(ResolutionOptions, _resolution);
            if (resIdx < 0) resIdx = 0;
            resIdx = EditorGUILayout.Popup("分辨率 resolution", resIdx, new[] { "640（推荐）", "1024" });
            _resolution = ResolutionOptions[Mathf.Clamp(resIdx, 0, ResolutionOptions.Length - 1)];
            _seed = EditorGUILayout.IntField("随机种子 seed", _seed);
            _subjectLayer = Mathf.Max(0, EditorGUILayout.IntField("主体层序号（0=最底层）", _subjectLayer));
            _offloadIndex = EditorGUILayout.Popup("显存策略 offload", _offloadIndex,
                new[] { "model（12GB 推荐）", "sequential（更低显存/更慢）", "none（整载 GPU）" });
            _sourceIndex = EditorGUILayout.Popup("权重源 source", _sourceIndex,
                new[] { "modelscope（国内推荐）", "hf（HuggingFace）", "hf-mirror", "local（本地目录）" });
            if (SourceOptions[_sourceIndex] == "local")
                _modelPath = EditorGUILayout.TextField("本地权重目录", _modelPath);

            if (EditorGUI.EndChangeCheck())
                SavePrefs();
        }

        private void DrawRunSection()
        {
            EditorGUILayout.Space(8);

            EditorGUI.BeginChangeCheck();
            _rebuildAtlas = EditorGUILayout.ToggleLeft("完成后构建 Diced Atlas（转图集，任意 AnimName）", _rebuildAtlas);
            if (EditorGUI.EndChangeCheck())
                SavePrefs();

            using (new EditorGUI.DisabledScope(IsRunning || string.IsNullOrEmpty(_videoPath)))
            {
                if (GUILayout.Button("① 首帧分层预览（选主体层）", GUILayout.Height(28)))
                    StartPreviewFlow();
                if (GUILayout.Button("Run（一键：拆帧+抽帧 → AI 抠图批处理）", GUILayout.Height(36)))
                    StartRunFlow();
            }

            if (IsRunning)
            {
                EditorGUILayout.Space(4);
                var rect = EditorGUILayout.GetControlRect(false, 22);
                EditorGUI.ProgressBar(rect, _progress01, _progressText);
                if (GUILayout.Button("取消", GUILayout.Height(24)))
                {
                    _cancelRequested = true;
                    _proc.Kill();
                    _progressText = "正在取消…";
                }
            }
        }

        private void DrawPreviewSection()
        {
            if (_previewTex == null)
                return;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("图层预览（自底向顶：Layer 0 … N；请把「主体层序号」填为角色所在层）", EditorStyles.boldLabel);
            float width = Mathf.Min(position.width - 24, 720);
            float aspect = (float)_previewTex.height / Mathf.Max(1, _previewTex.width);
            var rect = GUILayoutUtility.GetRect(width, width * aspect, GUILayout.ExpandWidth(false));
            GUI.DrawTexture(rect, _previewTex, ScaleMode.StretchToFill);
        }

        // ---------- 流程编排 ----------

        private void StartSetupVenv()
        {
            SavePrefs();
            string toolsDir = GetToolsDir();
            string bat = Path.Combine(toolsDir, "setup_venv.bat");
            if (!File.Exists(bat))
            {
                Fail("缺少 setup_venv.bat：\n" + bat);
                return;
            }

            _status = "正在创建/修复 venv（下载 torch CUDA 版约数 GB，请耐心等待；详细输出见 Console）…";
            BeginStage(Stage.SetupVenv, 0f, "创建 venv 中…");
            string cmdArgs = "/c " + Quote(Quote(bat) + " " + Quote(GetDefaultVenvDir()));
            if (!_proc.Start("cmd.exe", cmdArgs, toolsDir))
                EndStageWithFail("无法启动 setup_venv.bat。");
        }

        private void StartCheckEnv()
        {
            string python = ValidatePythonAndScript();
            if (python == null)
                return;

            _status = "环境检查中…";
            BeginStage(Stage.CheckEnv, 0f, "环境检查…");
            if (!_proc.Start(python, Quote(GetScriptPath()) + " check --cache-dir " + Quote(GetModelCacheDir()),
                    GetToolsDir(), BuildPythonEnv()))
                EndStageWithFail("无法启动 Python。");
        }

        private void StartPurgeCache()
        {
            SavePrefs();
            string python = ValidatePythonAndScript();
            if (python == null)
                return;

            if (!EditorUtility.DisplayDialog(
                    "Green Screen Pipeline",
                    "将删除：\n1) 工程缓存 " + GetModelCacheDir() + "\n"
                    + "2) 系统盘残缺缓存 %USERPROFILE%\\.cache\\modelscope\\models\\Qwen--Qwen-Image-Layered\n\n"
                    + "完整权重需重新下载（约数十 GB）。确认清理？",
                    "清理",
                    "取消"))
                return;

            _status = "正在清理残缺模型缓存…";
            BeginStage(Stage.PurgeCache, 0f, "清理缓存…");
            string args = Quote(GetScriptPath()) + " purge-cache --also-system --cache-dir " + Quote(GetModelCacheDir());
            if (!_proc.Start(python, args, GetToolsDir(), BuildPythonEnv()))
                EndStageWithFail("无法启动 Python。");
        }

        private void StartPreviewFlow()
        {
            SavePrefs();
            _status = "";
            string ffmpeg = ValidateVideoAnimFfmpeg();
            if (ffmpeg == null)
                return;
            string python = ValidatePythonAndScript();
            if (python == null)
                return;

            string rawDir = GetRawFramesDir();
            try
            {
                if (Directory.Exists(rawDir))
                    Directory.Delete(rawDir, true);
                Directory.CreateDirectory(rawDir);
            }
            catch (Exception ex)
            {
                Fail("无法清理临时目录：" + ex.Message);
                return;
            }

            _status = "① 首帧分层预览：ffmpeg 抽取第 1 帧…";
            BeginStage(Stage.PreviewExtract, 0.05f, "ffmpeg 抽取首帧…");
            string pattern = Path.Combine(rawDir, "frame_%06d.png").Replace('\\', '/');
            string args = string.Format(CultureInfo.InvariantCulture,
                "-y -i {0} -vf \"fps={1}\" -frames:v 1 {2}",
                Quote(_videoPath), _fps, Quote(pattern));
            if (!_proc.Start(ffmpeg, args, Path.GetDirectoryName(ffmpeg)))
                EndStageWithFail("无法启动 ffmpeg。");
        }

        private void StartRunFlow()
        {
            SavePrefs();
            _status = "";
            string ffmpeg = ValidateVideoAnimFfmpeg();
            if (ffmpeg == null)
                return;
            string python = ValidatePythonAndScript();
            if (python == null)
                return;

            string outAssetDir = BuildOutputAssetDir(_animName);
            string outAbsDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outAssetDir));
            if (Directory.Exists(outAbsDir))
            {
                string[] existing = Directory.GetFiles(outAbsDir, "frame_*.png");
                if (existing != null && existing.Length > 0 &&
                    !EditorUtility.DisplayDialog("Green Screen Pipeline",
                        "输出目录已有 " + existing.Length + " 个 frame_*.png，将删除后重新导出：\n" + outAssetDir,
                        "覆盖", "取消"))
                    return;
            }

            try
            {
                EnsureAssetFolder(outAssetDir);
                ClearExistingFrames(outAbsDir);
                string rawDir = GetRawFramesDir();
                if (Directory.Exists(rawDir))
                    Directory.Delete(rawDir, true);
                Directory.CreateDirectory(rawDir);
            }
            catch (Exception ex)
            {
                Fail("准备输出/临时目录失败：" + ex.Message);
                return;
            }

            _status = "Run：ffmpeg 拆帧+抽帧中…";
            BeginStage(Stage.Extract, 0.02f, "ffmpeg 拆帧+抽帧…");
            string pattern = Path.Combine(GetRawFramesDir(), "frame_%06d.png").Replace('\\', '/');
            string args = string.Format(CultureInfo.InvariantCulture,
                "-y -i {0} -vf \"fps={1}\" {2}",
                Quote(_videoPath), _fps, Quote(pattern));
            if (!_proc.Start(ffmpeg, args, Path.GetDirectoryName(ffmpeg)))
                EndStageWithFail("无法启动 ffmpeg。");
        }

        private void OnProcLine(string line)
        {
            _logTail = (_logTail + line + "\n");
            if (_logTail.Length > 1200)
                _logTail = _logTail.Substring(_logTail.Length - 1200);

            if (_stage == Stage.BatchPython && line.StartsWith("PROGRESS ", StringComparison.Ordinal))
            {
                string payload = line.Substring("PROGRESS ".Length);
                int slash = payload.IndexOf('/');
                if (slash > 0 &&
                    int.TryParse(payload.Substring(0, slash), out int n) &&
                    int.TryParse(payload.Substring(slash + 1), out int total) && total > 0)
                {
                    _progress01 = Mathf.Lerp(0.1f, 0.9f, (float)n / total);
                    _progressText = "AI 抠图 第 " + n + "/" + total + " 帧";
                }
            }
            else if (line.StartsWith("FATAL ", StringComparison.Ordinal))
            {
                _progressText = line;
            }

            Repaint();
        }

        private void OnProcExited(int code)
        {
            switch (_stage)
            {
                case Stage.SetupVenv:
                    if (code == 0)
                    {
                        _pythonExe = "";
                        SavePrefs();
                        EndStage("venv 创建/修复完成。", null);
                    }
                    else EndStageWithFail("venv 创建失败（exit " + code + "）。详见 Console。\n" + _logTail);
                    break;

                case Stage.CheckEnv:
                    EndStage(code == 0 ? "环境检查通过。" : "环境检查未通过（exit " + code + "）。",
                        _logTail.Trim());
                    if (code != 0)
                        Fail("环境检查未通过：\n" + _logTail.Trim());
                    break;

                case Stage.PurgeCache:
                    if (code == 0)
                        EndStage("缓存清理完成。C 盘空间已释放；下次 preview/batch 将下载到工程盘缓存目录。", _logTail.Trim());
                    else
                        EndStageWithFail("缓存清理失败（exit " + code + "）。\n" + _logTail);
                    break;

                case Stage.PreviewExtract:
                    if (_cancelRequested) { EndStage("已取消。", null); break; }
                    if (code != 0) { EndStageWithFail("ffmpeg 抽取首帧失败（exit " + code + "）。详见 Console。"); break; }
                    RunPreviewPython();
                    break;

                case Stage.PreviewPython:
                    if (_cancelRequested) { EndStage("已取消。", null); break; }
                    if (code == 4) { EndStageWithFail("磁盘空间不足或模型缓存残缺（exit 4）。\n请先点「清理残缺模型缓存」，确保工程盘有 ≥40GB 空闲后再预览。\n" + _logTail); break; }
                    if (code != 0) { EndStageWithFail("分层预览失败（exit " + code + "）。详见 Console。\n" + _logTail); break; }
                    LoadPreviewTexture();
                    EndStage("预览完成。请查看图层拼图并填写主体层序号，然后 Run。", null);
                    break;

                case Stage.Extract:
                    if (_cancelRequested) { EndStage("已取消。", null); break; }
                    if (code != 0) { EndStageWithFail("ffmpeg 拆帧失败（exit " + code + "）。详见 Console。"); break; }
                    RunBatchPython();
                    break;

                case Stage.BatchPython:
                    if (_cancelRequested) { EndStage("已取消（已写出的帧保留在输出目录）。", null); break; }
                    if (code == 3)
                    {
                        EndStageWithFail("主体层序号超出范围。请先点「① 首帧分层预览」确认层数与序号。\n" + _logTail);
                        break;
                    }
                    if (code == 4)
                    {
                        EndStageWithFail("磁盘空间不足或模型缓存残缺（exit 4）。\n请先点「清理残缺模型缓存」，确保工程盘有 ≥40GB 空闲后再 Run。\n" + _logTail);
                        break;
                    }
                    if (code != 0) { EndStageWithFail("AI 抠图批处理失败（exit " + code + "）。详见 Console。\n" + _logTail); break; }
                    FinishRunFlow();
                    break;
            }
        }

        private void RunPreviewPython()
        {
            string python = ResolvePythonExe(_pythonExe);
            string frame1 = Path.Combine(GetRawFramesDir(), "frame_000001.png");
            if (!File.Exists(frame1))
            {
                EndStageWithFail("ffmpeg 成功但未生成首帧：" + frame1);
                return;
            }

            BeginStage(Stage.PreviewPython, 0.1f, "Qwen 分层分解首帧（首次需下载模型权重）…");
            string args = Quote(GetScriptPath()) + " preview"
                + " --image " + Quote(frame1)
                + " --out " + Quote(GetPreviewPngPath())
                + BuildModelArgs();
            if (!_proc.Start(python, args, GetToolsDir(), BuildPythonEnv()))
                EndStageWithFail("无法启动 Python。");
        }

        private void RunBatchPython()
        {
            string[] raw = Directory.GetFiles(GetRawFramesDir(), "frame_*.png");
            if (raw == null || raw.Length < 1)
            {
                EndStageWithFail("ffmpeg 成功但未生成 frame_*.png。");
                return;
            }

            string outAssetDir = BuildOutputAssetDir(_animName);
            string outAbsDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outAssetDir));

            BeginStage(Stage.BatchPython, 0.1f, "AI 抠图 0/" + raw.Length + "（首次需下载模型权重）…");
            string args = Quote(GetScriptPath()) + " batch"
                + " --input-dir " + Quote(GetRawFramesDir())
                + " --out-dir " + Quote(outAbsDir)
                + " --layer-index " + _subjectLayer.ToString(CultureInfo.InvariantCulture)
                + BuildModelArgs();
            if (!_proc.Start(ResolvePythonExe(_pythonExe), args, GetToolsDir(), BuildPythonEnv()))
                EndStageWithFail("无法启动 Python。");
        }

        private void FinishRunFlow()
        {
            _progressText = "导入 Unity 资源…";
            _progress01 = 0.92f;
            Repaint();

            string outAssetDir = BuildOutputAssetDir(_animName);
            string outAbsDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outAssetDir));
            AssetDatabase.Refresh();

            string[] pngs = Directory.GetFiles(outAbsDir, "frame_*.png");
            int count = pngs != null ? pngs.Length : 0;
            if (count < 1)
            {
                EndStageWithFail("AI 抠图成功但未生成 frame_*.png。");
                return;
            }

            ConfigureTextureImporters(outAssetDir);
            AssetDatabase.Refresh();

            string atlasNote = "";
            if (_rebuildAtlas)
            {
                _progressText = "构建 Diced Atlas…";
                _progress01 = 0.97f;
                Repaint();
                if (string.Equals(
                        NormalizeAssetPath(outAssetDir),
                        NormalizeAssetPath(LangRenDzDicedAtlasBuilder.FramesSpriteFolder),
                        StringComparison.OrdinalIgnoreCase))
                {
                    LangRenDzDicedAtlasBuilder.Build();
                    atlasNote = "；已重建 LangRen DZ Diced Atlas。";
                }
                else
                {
                    int sprites = DicedAtlasBuilder.BuildForAnim(_animName.Trim());
                    atlasNote = sprites >= 0
                        ? "；已构建 Diced Atlas（sprites=" + sprites + "）。"
                        : "；Diced Atlas 构建失败（详见 Console）。";
                }
            }

            EndStage("完成：导出 " + count + " 帧 → " + outAssetDir + atlasNote, null);
            EditorUtility.DisplayDialog("Green Screen Pipeline", _status, "OK");
        }

        // ---------- 阶段/状态工具 ----------

        private void BeginStage(Stage stage, float progress, string text)
        {
            _stage = stage;
            _cancelRequested = false;
            _progress01 = progress;
            _progressText = text;
            _logTail = "";
            EditorApplication.update -= Pump;
            EditorApplication.update += Pump;
            Repaint();
        }

        private void Pump()
        {
            _proc.Pump();
        }

        private void EndStage(string status, string extraLog)
        {
            _stage = Stage.None;
            EditorApplication.update -= Pump;
            _status = status + (string.IsNullOrEmpty(extraLog) ? "" : "\n" + extraLog);
            if (!string.IsNullOrEmpty(status))
                Debug.Log("[GreenScreenVideoPipeline] " + status);
            Repaint();
        }

        private void EndStageWithFail(string message)
        {
            Debug.LogError("[GreenScreenVideoPipeline] " + message + "\n--- process log ---\n" + _proc.FullLog);
            _stage = Stage.None;
            EditorApplication.update -= Pump;
            _status = "失败：" + message;
            EditorUtility.DisplayDialog("Green Screen Pipeline", _status, "OK");
            Repaint();
        }

        private static void Fail(string message)
        {
            EditorUtility.DisplayDialog("Green Screen Pipeline", message, "OK");
        }

        // ---------- 校验与路径 ----------

        private string ValidateVideoAnimFfmpeg()
        {
            if (string.IsNullOrEmpty(_videoPath) || !File.Exists(_videoPath))
            {
                Fail("请先选择有效的视频文件。");
                return null;
            }

            if (string.IsNullOrWhiteSpace(_animName))
            {
                Fail("AnimName 不能为空。");
                return null;
            }

            string ffmpeg = ResolveFfmpegPath(_ffmpegPath);
            if (string.IsNullOrEmpty(ffmpeg))
            {
                Fail("未找到 ffmpeg。\n请安装 ffmpeg 并加入 PATH，或在窗口中指定可执行文件路径。\n下载：https://ffmpeg.org/download.html");
                _status = "失败：未找到 ffmpeg。";
                return null;
            }

            return ffmpeg;
        }

        private string ValidatePythonAndScript()
        {
            string script = GetScriptPath();
            if (!File.Exists(script))
            {
                Fail("缺少 Python 脚本：\n" + script);
                return null;
            }

            string python = ResolvePythonExe(_pythonExe);
            if (string.IsNullOrEmpty(python))
            {
                Fail("未找到 Python venv。\n请点击「创建/修复 venv」自动安装（Python 3.10~3.12），\n或在窗口中指定 venv 的 python.exe。");
                _status = "失败：未找到 Python venv。";
                return null;
            }

            return python;
        }

        private static string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string GetToolsDir()
        {
            return Path.Combine(GetProjectRoot(), "Tools", "QwenMatting");
        }

        private static string GetScriptPath()
        {
            return Path.Combine(GetToolsDir(), "qwen_matting.py");
        }

        private static string GetDefaultVenvDir()
        {
            return Path.Combine(GetToolsDir(), ".venv");
        }

        private static string ResolvePythonExe(string overridePath)
        {
            if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
                return overridePath;

            string candidate = Path.Combine(GetDefaultVenvDir(), "Scripts", "python.exe");
            return File.Exists(candidate) ? candidate : null;
        }

        private string GetRawFramesDir()
        {
            string safe = string.IsNullOrWhiteSpace(_animName) ? DefaultAnimName : _animName.Trim();
            return Path.Combine(GetProjectRoot(), "Library", "PetDemoQwenMatting", safe, "raw_frames");
        }

        private string GetPreviewPngPath()
        {
            string safe = string.IsNullOrWhiteSpace(_animName) ? DefaultAnimName : _animName.Trim();
            return Path.Combine(GetProjectRoot(), "Library", "PetDemoQwenMatting", safe, "layer_preview.png");
        }

        /// <summary>SPEC v3.278：权重缓存固定在工程盘 Library，避免写满系统盘 C:。</summary>
        private static string GetModelCacheDir()
        {
            return Path.Combine(GetProjectRoot(), "Library", "PetDemoQwenMatting", "model_cache");
        }

        private static System.Collections.Generic.Dictionary<string, string> BuildPythonEnv()
        {
            string cache = GetModelCacheDir();
            return new System.Collections.Generic.Dictionary<string, string>
            {
                { "MODELSCOPE_CACHE", cache },
                { "HF_HOME", cache },
                { "HUGGINGFACE_HUB_CACHE", Path.Combine(cache, "hub") },
                { "TRANSFORMERS_CACHE", Path.Combine(cache, "transformers") }
            };
        }

        private string BuildModelArgs()
        {
            string args = string.Format(CultureInfo.InvariantCulture,
                " --layers {0} --steps {1} --resolution {2} --true-cfg-scale 4.0 --seed {3} --source {4} --offload {5} --cache-dir {6}",
                _layers, _steps, _resolution, _seed,
                SourceOptions[_sourceIndex], OffloadOptions[_offloadIndex],
                Quote(GetModelCacheDir()));
            if (SourceOptions[_sourceIndex] == "local" && !string.IsNullOrWhiteSpace(_modelPath))
                args += " --model-path " + Quote(_modelPath);
            return args;
        }

        private void LoadPreviewTexture()
        {
            string path = GetPreviewPngPath();
            if (!File.Exists(path))
                return;

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes))
                {
                    if (_previewTex != null)
                        DestroyImmediate(_previewTex);
                    _previewTex = tex;
                }
                else
                {
                    DestroyImmediate(tex);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GreenScreenVideoPipeline] 预览图加载失败: " + ex.Message);
            }
        }

        // ---------- 既有工具方法（ffmpeg 解析 / 资源导入设置） ----------

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

        private static string Quote(string path)
        {
            return "\"" + path + "\"";
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
