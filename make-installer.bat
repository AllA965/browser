@echo off
setlocal enabledelayedexpansion

set "PUBLISH_DIR=publish_installer"
set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

echo [1/4] Cleaning old publish directory...
if exist "%PUBLISH_DIR%" rd /s /q "%PUBLISH_DIR%"
mkdir "%PUBLISH_DIR%"

rem Create exclude list for xcopy
(
echo .git\
echo .vscode\
echo __pycache__\
echo .pytest_cache\
echo venv\
echo .env
echo *.pyc
echo *.pyo
echo *.pdb
echo *.log
echo *.md
echo LICENSE
echo LICENSE.txt
) > exclude_list.txt

echo [2/4] Publishing project (Win-x64, Self-Contained, Optimized)...
dotnet publish MiniWorldBrowser\MiniWorldBrowser.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%PUBLISH_DIR%"

if %ERRORLEVEL% neq 0 (
    echo [ERROR] dotnet publish failed.
    pause
    exit /b %ERRORLEVEL%
)

echo [3/4] Copying Python Bridge (Excluding unnecessary files)...
xcopy "python_bridge" "%PUBLISH_DIR%\python_bridge" /E /I /H /Y /EXCLUDE:exclude_list.txt

echo [3.1/4] Creating embedded Python environment (python_env)...
set "VENV_DIR=%PUBLISH_DIR%\python_env"
if exist "%VENV_DIR%" rd /s /q "%VENV_DIR%"

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

rem Skipping pip upgrade to avoid lock issues on some Windows systems
rem echo Upgrading pip...
rem "%VENV_DIR%\Scripts\python.exe" -m pip install --upgrade pip
rem if %ERRORLEVEL% neq 0 (
rem     echo [WARNING] pip upgrade failed, continuing...
rem )

echo Installing Python dependencies...
"%VENV_DIR%\Scripts\python.exe" -m pip install -r "python_bridge\requirements.txt"
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Dependencies installation failed.
    pause
    exit /b 1
)

echo [3.2/4] Optimizing Python environment (Installing only Chromium and cleaning up)...
set "PLAYWRIGHT_BROWSERS_PATH=%PUBLISH_DIR%\python_browsers"
if not exist "%PLAYWRIGHT_BROWSERS_PATH%" mkdir "%PLAYWRIGHT_BROWSERS_PATH%"

"%VENV_DIR%\Scripts\python.exe" -m playwright install chromium
if %ERRORLEVEL% neq 0 (
    echo [WARNING] Playwright browser installation failed.
)

echo Cleaning up Python environment...
for /d /r "%VENV_DIR%" %%d in (__pycache__) do if exist "%%d" rd /s /q "%%d"
del /s /q "%VENV_DIR%\*.pyc" 2>nul
del /s /q "%VENV_DIR%\*.pyo" 2>nul
del /s /q "%VENV_DIR%\*.pdb" 2>nul
for /d /r "%VENV_DIR%\Lib\site-packages" %%d in (tests test docs examples) do if exist "%%d" rd /s /q "%%d"

echo [3.3/4] Removing all debug symbols (.pdb) and extra files...
del /s /q "%PUBLISH_DIR%\*.pdb" 2>nul
if exist "exclude_list.txt" del "exclude_list.txt"

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
