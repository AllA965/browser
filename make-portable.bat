@echo off
setlocal enabledelayedexpansion

echo [提示] 正在准备打包便携版...

:: 设置输出目录
set "PUBLISH_DIR=publish_portable"
set "ZIP_FILE=鲲穹AI浏览器_便携版.zip"

:: 清理旧的发布目录
if exist "%PUBLISH_DIR%" (
    echo [提示] 正在清理旧的发布目录...
    rd /s /q "%PUBLISH_DIR%"
)

:: 执行 dotnet publish
echo [提示] 正在编译并发布项目 (Single-File, Self-Contained)...
dotnet publish MiniWorldBrowser\MiniWorldBrowser.csproj -c Release -r win-x64 --self-contained true -o "%PUBLISH_DIR%"

if %ERRORLEVEL% neq 0 (
    echo [错误] dotnet publish 失败。
    pause
    exit /b %ERRORLEVEL%
)

:: 复制 python_bridge
echo [提示] 正在复制 Python Bridge...
xcopy "python_bridge" "%PUBLISH_DIR%\python_bridge" /E /I /H /Y

:: 创建内置 Python 环境并安装依赖
echo [提示] 正在创建内置 Python 环境 (python_env) 并安装依赖...
set "VENV_DIR=%PUBLISH_DIR%\python_env"
if exist "%VENV_DIR%" rd /s /q "%VENV_DIR%"

where py >nul 2>nul
if %ERRORLEVEL%==0 (
    for /f "tokens=2 delims= " %%v in ('py -0p 2^>nul ^| findstr /r /c:".\*3\.1[1-9].\*"') do (
        set "PY311=%%v"
    )
)
if defined PY311 (
    echo 使用 Python: %PY311%
    "%PY311%" -m venv "%VENV_DIR%"
) else (
    echo 未检测到 py 启动器或 3.11+，尝试使用 python...
    python -V
    if %ERRORLEVEL% neq 0 (
        echo [错误] 未找到可用的 Python 3.11+，请在打包机上安装 Python 3.11 或更高版本。
        pause
        exit /b 1
    )
    python -m venv "%VENV_DIR%"
)

if not exist "%VENV_DIR%\Scripts\python.exe" (
    echo [错误] 虚拟环境创建失败。
    pause
    exit /b 1
)

echo 升级 pip ...
"%VENV_DIR%\Scripts\python.exe" -m pip install --upgrade pip

echo 安装 Python 依赖...
"%VENV_DIR%\Scripts\pip.exe" install -r "python_bridge\requirements.txt"
if %ERRORLEVEL% neq 0 (
    echo [错误] 依赖安装失败，请检查网络或 requirements.txt。
    pause
    exit /b 1
)

echo [提示] 正在精简 Python 环境...
:: Remove __pycache__
for /d /r "%VENV_DIR%" %%d in (__pycache__) do @if exist "%%d" rd /s /q "%%d"
:: Remove tests, docs, etc.
for /d /r "%VENV_DIR%\Lib\site-packages" %%d in (tests, test, docs, doc, examples, sample, samples) do @if exist "%%d" rd /s /q "%%d"
:: Remove .dist-info and .egg-info (metadata)
for /d /r "%VENV_DIR%\Lib\site-packages" %%d in (*.dist-info, *.egg-info) do @if exist "%%d" rd /s /q "%%d"
:: Remove compiled files .pyc .pyo
del /s /q "%VENV_DIR%\*.pyc" 2>nul
del /s /q "%VENV_DIR%\*.pyo" 2>nul
:: Remove pip and setuptools from venv to save space
"%VENV_DIR%\Scripts\python.exe" -m pip uninstall -y pip setuptools
:: Remove unnecessary directories in venv
if exist "%VENV_DIR%\include" rd /s /q "%VENV_DIR%\include"

:: 移除不必要的文件 (如果有)
if exist "%PUBLISH_DIR%\updater.pdb" del "%PUBLISH_DIR%\updater.pdb"
if exist "%PUBLISH_DIR%\鲲穹AI浏览器.pdb" del "%PUBLISH_DIR%\鲲穹AI浏览器.pdb"
:: Remove any remaining .pdb files
del /s /q "%PUBLISH_DIR%\*.pdb" 2>nul
:: Remove WebView2 runtime symbols if any
if exist "%PUBLISH_DIR%\WebView2\*.pdb" del /s /q "%PUBLISH_DIR%\WebView2\*.pdb" 2>nul

:: 创建 ZIP 压缩包 (使用 PowerShell)
echo [提示] 正在创建压缩包 %ZIP_FILE%...
if exist "%ZIP_FILE%" del "%ZIP_FILE%"
powershell -Command "Compress-Archive -Path '%PUBLISH_DIR%\*' -DestinationPath '%ZIP_FILE%' -Force"

if %ERRORLEVEL% equ 0 (
    echo.
    echo [成功] 便携版已生成：
    echo 1. 文件夹: %PUBLISH_DIR%
    echo 2. 压缩包: %ZIP_FILE%
) else (
    echo.
    echo [警告] 压缩包创建失败，但发布目录已就绪。
)

pause
