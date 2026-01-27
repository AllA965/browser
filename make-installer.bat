@echo off
set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

if not exist "%ISCC%" (
    echo [错误] 找不到 Inno Setup 6 编译器: "%ISCC%"
    echo 请确认您的安装路径是否正确。
    pause
    exit /b 1
)

echo [提示] 正在使用 Inno Setup 6 打包...
"%ISCC%" installer.iss

if %ERRORLEVEL% equ 0 (
    echo.
    echo [成功] 安装程序已生成在当前目录下。
) else (
    echo.
    echo [失败] 打包过程中出现错误。
)

