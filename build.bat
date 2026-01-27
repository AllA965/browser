@echo off
chcp 65001 >nul
echo ========================================
echo   MiniWorld Browser 构建脚本
echo ========================================
echo.

:: 检查 .NET SDK
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo [错误] 未检测到 .NET SDK
    echo.
    echo 请先运行 install-deps.bat 检查依赖
    echo.
    pause
    exit /b 1
)

echo [✓] .NET SDK 已检测到
dotnet --version
echo.

cd MiniWorldBrowser

echo [1/2] 清理旧构建...
dotnet clean -c Release >nul 2>&1
if exist "..\publish" rmdir /s /q "..\publish"
echo [✓] 清理完成
echo.

echo [2/2] 发布应用 (包含 WebView2 Runtime)...
dotnet publish -c Release -r win-x64 --self-contained true -o ..\publish
if errorlevel 1 (
    echo [错误] 发布失败
    pause
    exit /b 1
)

echo.
echo ========================================
echo   构建完成！
echo ========================================
echo.
echo 输出目录: publish\
echo 可执行文件: publish\鲲穹AI浏览器.exe
echo.
echo 已包含 WebView2 Runtime，用户可直接运行！
echo.
