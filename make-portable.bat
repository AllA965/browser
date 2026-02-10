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

:: 移除不必要的文件 (如果有)
if exist "%PUBLISH_DIR%\updater.pdb" del "%PUBLISH_DIR%\updater.pdb"
if exist "%PUBLISH_DIR%\鲲穹AI浏览器.pdb" del "%PUBLISH_DIR%\鲲穹AI浏览器.pdb"

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
