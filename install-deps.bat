@echo off
chcp 65001 >nul
echo ========================================
echo   MiniWorld Browser 依赖安装
echo ========================================
echo.

:: 检查 .NET SDK
echo [检查] .NET 6.0 SDK...
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo.
    echo [错误] 未检测到 .NET 6.0 SDK
    echo.
    echo 请手动下载并安装：
    echo https://dotnet.microsoft.com/download/dotnet/6.0
    echo.
    echo 选择 "SDK" 版本下载安装程序，安装完成后重新运行此脚本
    echo.
    pause
    exit /b 1
) else (
    echo [✓] .NET 6.0 SDK 已安装
    dotnet --version
)

echo.

:: 检查 WebView2 Runtime
echo [检查] WebView2 Runtime...
reg query "HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" >nul 2>&1
if errorlevel 1 (
    reg query "HKLM\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" >nul 2>&1
    if errorlevel 1 (
        echo [✗] WebView2 Runtime 未安装
        echo.
        echo 请下载并安装：
        echo https://developer.microsoft.com/microsoft-edge/webview2/
        echo.
        echo 选择 "Evergreen Standalone Installer" 版本
        echo.
        pause
        exit /b 1
    ) else (
        echo [✓] WebView2 Runtime 已安装
    )
) else (
    echo [✓] WebView2 Runtime 已安装
)

echo.
echo ========================================
echo   依赖检查完成！
echo ========================================
echo.
echo 现在可以运行 build.bat 构建项目
echo.
pause
