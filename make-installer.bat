@echo off
setlocal enabledelayedexpansion

set "PUBLISH_DIR=publish_installer"
set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

echo [1/4] 清理旧的发布目录...
if exist "%PUBLISH_DIR%" rd /s /q "%PUBLISH_DIR%"
mkdir "%PUBLISH_DIR%"

echo [2/4] 正在编译并发布项目 (Win-x64, Self-Contained)...
dotnet publish MiniWorldBrowser\MiniWorldBrowser.csproj -c Release -r win-x64 --self-contained true -o "%PUBLISH_DIR%"

if %ERRORLEVEL% neq 0 (
    echo [错误] dotnet publish 失败。
    pause
    exit /b %ERRORLEVEL%
)

echo [3/4] 正在复制 Python Bridge...
xcopy "python_bridge" "%PUBLISH_DIR%\python_bridge" /E /I /H /Y

:: 移除不必要的文件
if exist "%PUBLISH_DIR%\updater.pdb" del "%PUBLISH_DIR%\updater.pdb"
if exist "%PUBLISH_DIR%\鲲穹AI浏览器.pdb" del "%PUBLISH_DIR%\鲲穹AI浏览器.pdb"

echo [4/4] 正在使用 Inno Setup 6 生成安装程序...
if not exist "%ISCC%" (
    echo [错误] 找不到 Inno Setup 6 编译器: "%ISCC%"
    echo 请确认您的安装路径是否正确，或者手动运行 installer.iss。
    pause
    exit /b 1
)

"%ISCC%" installer.iss

if %ERRORLEVEL% equ 0 (
    echo.
    echo [成功] 安装程序已生成在当前目录下。
) else (
    echo.
    echo [失败] 打包过程中出现错误。
)

pause
