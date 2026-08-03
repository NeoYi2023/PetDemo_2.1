@echo off
REM SPEC §9.14.11 v3.277：创建 QwenMatting venv 并安装依赖。
REM 用法: setup_venv.bat [venv_dir] [base_python_cmd]
REM   默认 venv_dir = 脚本旁 .venv；默认 base python = py -3.11（3.10~3.12 均可，勿用 3.14）。
REM 国内网络可将下方两个 pip 索引替换为镜像，例如：
REM   torch:  https://mirrors.aliyun.com/pytorch-wheels/cu121
REM   pypi :  -i https://pypi.tuna.tsinghua.edu.cn/simple
setlocal EnableExtensions
set "VENV_DIR=%~1"
if not defined VENV_DIR set "VENV_DIR=%~dp0.venv"
set "BASE_PY=%~2"
if not defined BASE_PY set "BASE_PY=py -3.11"

echo [setup] creating venv at "%VENV_DIR%" with: %BASE_PY%
%BASE_PY% -m venv "%VENV_DIR%" || goto :fail

set "VPY=%VENV_DIR%\Scripts\python.exe"
if not exist "%VPY%" goto :fail

echo [setup] upgrading pip...
"%VPY%" -m pip install --upgrade pip || goto :fail

echo [setup] installing torch + torchvision (CUDA cu121)...
"%VPY%" -m pip install torch torchvision --index-url https://download.pytorch.org/whl/cu121 || goto :fail

echo [setup] installing requirements (diffusers from git, transformers, modelscope...)...
"%VPY%" -m pip install -r "%~dp0requirements.txt" || goto :fail

echo [setup] verifying environment...
"%VPY%" "%~dp0qwen_matting.py" check || goto :fail

echo [setup] OK - venv ready at "%VENV_DIR%"
exit /b 0

:fail
echo [setup] FAILED (exit %errorlevel%)
exit /b 1
