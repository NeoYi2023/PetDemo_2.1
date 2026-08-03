@echo off
REM SPEC_VideoMattingAtlas.md v1.0: create VideoMatting venv and install deps.
REM Usage: setup_venv.bat [venv_dir] [base_python_cmd]
REM   Default venv_dir = .venv beside this script; default base python = py -3.11
REM   Use Python 3.10~3.12 only (avoid 3.14; no matching torch wheels).
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

echo [setup] installing requirements...
"%VPY%" -m pip install -r "%~dp0requirements.txt" || goto :fail

echo [setup] verifying torch...
"%VPY%" -c "import torch; print('torch', torch.__version__, 'cuda', torch.cuda.is_available())" || goto :fail

echo [setup] OK - venv ready at "%VENV_DIR%"
exit /b 0

:fail
echo [setup] FAILED (exit %errorlevel%)
exit /b 1
