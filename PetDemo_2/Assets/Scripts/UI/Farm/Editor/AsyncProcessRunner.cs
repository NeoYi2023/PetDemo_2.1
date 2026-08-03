#if UNITY_EDITOR
// SPEC §9.14.11 v3.277：编辑器异步子进程执行器（输出逐行回调 + 可取消）。
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace PetDemo.EditorTools
{
    /// <summary>
    /// 在后台线程运行外部进程，主线程通过 <see cref="Pump"/> 派发输出行与退出事件，
    /// 保证 Editor 窗口在长时间任务（ffmpeg / diffusion 推理）期间保持可交互、可取消。
    /// </summary>
    public sealed class AsyncProcessRunner
    {
        public Action<string> OnOutputLine;
        public Action<int> OnExited;

        public bool IsRunning { get; private set; }
        public string FullLog => _log.ToString();

        private readonly ConcurrentQueue<string> _lines = new ConcurrentQueue<string>();
        private readonly StringBuilder _log = new StringBuilder();
        private Process _proc;
        private bool _exitedFlag;

        /// <param name="extraEnv">可选额外环境变量（如 MODELSCOPE_CACHE / HF_HOME）。</param>
        public bool Start(string exe, string args, string workingDir = null, System.Collections.Generic.IDictionary<string, string> extraEnv = null)
        {
            if (IsRunning)
                return false;

            _log.Length = 0;
            while (_lines.TryDequeue(out _)) { }
            _exitedFlag = false;

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = string.IsNullOrEmpty(workingDir)
                    ? Environment.CurrentDirectory
                    : workingDir
            };
            // 强制 Python 子进程使用 UTF-8 输出，避免 Windows GBK 控制台乱码。
            psi.EnvironmentVariables["PYTHONUTF8"] = "1";
            psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            if (extraEnv != null)
            {
                foreach (var kv in extraEnv)
                {
                    if (!string.IsNullOrEmpty(kv.Key))
                        psi.EnvironmentVariables[kv.Key] = kv.Value ?? "";
                }
            }

            try
            {
                _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            }
            catch (Exception ex)
            {
                _log.AppendLine("Failed to create process: " + ex.Message);
                _proc = null;
                return false;
            }

            _proc.OutputDataReceived += (_, e) => Enqueue(e.Data);
            _proc.ErrorDataReceived += (_, e) => Enqueue(e.Data);
            _proc.Exited += (_, __) => _exitedFlag = true;

            try
            {
                if (!_proc.Start())
                {
                    _proc.Dispose();
                    _proc = null;
                    return false;
                }
            }
            catch (Exception ex)
            {
                _log.AppendLine("Failed to start process: " + ex.Message);
                _proc.Dispose();
                _proc = null;
                return false;
            }

            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();
            IsRunning = true;
            return true;
        }

        /// <summary>主线程轮询（EditorApplication.update）：派发输出行；进程退出后触发 OnExited。</summary>
        public void Pump()
        {
            while (_lines.TryDequeue(out string line))
            {
                _log.AppendLine(line);
                OnOutputLine?.Invoke(line);
            }

            if (!IsRunning || !_exitedFlag)
                return;

            try
            {
                _proc.WaitForExit(); // 确保异步输出流全部冲刷完毕
            }
            catch (Exception ex)
            {
                _log.AppendLine("WaitForExit error: " + ex.Message);
            }

            while (_lines.TryDequeue(out string line))
            {
                _log.AppendLine(line);
                OnOutputLine?.Invoke(line);
            }

            int code;
            try
            {
                code = _proc.ExitCode;
            }
            catch
            {
                code = -1;
            }

            IsRunning = false;
            _proc.Dispose();
            _proc = null;
            OnExited?.Invoke(code);
        }

        public void Kill()
        {
            if (!IsRunning || _proc == null)
                return;

            try
            {
                _proc.Kill();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[AsyncProcessRunner] Kill failed: " + ex.Message);
            }
        }

        private void Enqueue(string data)
        {
            if (!string.IsNullOrEmpty(data))
                _lines.Enqueue(data);
        }
    }
}
#endif
