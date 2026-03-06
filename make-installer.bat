@echo off
setlocal enabledelayedexpansion

set "PUBLISH_DIR=publish_installer"
set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

echo [1/4] Cleaning old publish directory...
if exist "%PUBLISH_DIR%" rd /s /q "%PUBLISH_DIR%"
mkdir "%PUBLISH_DIR%"

echo [2/4] Publishing project (Win-x64, Self-Contained)...
dotnet publish MiniWorldBrowser\MiniWorldBrowser.csproj -c Release -r win-x64 --self-contained true -o "%PUBLISH_DIR%"

if %ERRORLEVEL% neq 0 (
    echo [ERROR] dotnet publish failed.
    pause
    exit /b %ERRORLEVEL%
)

echo [3/4] Copying Python Bridge...
xcopy "python_bridge" "%PUBLISH_DIR%\python_bridge" /E /I /H /Y

echo [3.1/4] Creating embedded Python environment (python_env)...
set "VENV_DIR=%PUBLISH_DIR%\python_env"
if exist "%VENV_DIR%" rd /s /q "%VENV_DIR%"

:: Try py -3.11 first, then python
where py >nul 2>nul
if %ERRORLEVEL%==0 (
    for /f "tokens=2 delims= " %%v in ('py -0p 2^>nul ^| findstr /r /c:".\*3\.1[1-9].\*"') do (
        set "PY311=%%v"
    )
)
if defined PY311 (
    echo Using Python: %PY311%
    "%PY311%" -m venv "%VENV_DIR%"
) else (
    echo No py launcher or 3.11+, trying python...
    python -V
    if %ERRORLEVEL% neq 0 (
        echo [ERROR] Python 3.11+ not found. Please install Python 3.11 or higher.
        pause
        exit /b 1
    )
    python -m venv "%VENV_DIR%"
)

if not exist "%VENV_DIR%\Scripts\python.exe" (
    echo [ERROR] Virtual environment creation failed.
    pause
    exit /b 1
)

echo Upgrading pip...
"%VENV_DIR%\Scripts\python.exe" -m pip install --upgrade pip
if %ERRORLEVEL% neq 0 (
    echo [WARNING] pip upgrade failed, continuing...
)

echo Installing Python dependencies...
"%VENV_DIR%\Scripts\pip.exe" install -r "python_bridge\requirements.txt"
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Dependencies installation failed.
    pause
    exit /b 1
)

echo [3.2/4] Cleaning up Python environment...
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

:: Remove unnecessary files in publish directory
if exist "%PUBLISH_DIR%\updater.pdb" del "%PUBLISH_DIR%\updater.pdb"
if exist "%PUBLISH_DIR%\鲲穹AI浏览器.pdb" del "%PUBLISH_DIR%\鲲穹AI浏览器.pdb"
:: Remove any remaining .pdb files
del /s /q "%PUBLISH_DIR%\*.pdb" 2>nul
:: Remove WebView2 runtime symbols if any
if exist "%PUBLISH_DIR%\WebView2\*.pdb" del /s /q "%PUBLISH_DIR%\WebView2\*.pdb" 2>nul

echo [4/4] Generating installer with Inno Setup 6...
if not exist "%ISCC%" (
    echo [ERROR] Inno Setup 6 compiler not found: "%ISCC%"
    pause
    exit /b 1
)

"%ISCC%" installer.iss

if %ERRORLEVEL% equ 0 (
    echo.
    echo [SUCCESS] Installer generated successfully.
) else (
    echo.
    echo [FAILURE] Error occurred during packaging.
)

pause
