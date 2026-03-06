; 鲲穹AI浏览器 Inno Setup 脚本
#define MyAppName "鲲穹AI浏览器"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "鲲穹AI"
#define MyAppExeName "鲲穹AI浏览器.exe"
#define MySourcePath "MiniWorldBrowser\\bin\\Release\\net8.0-windows\\win-x64\\publish"

[Setup]
AppId={{D8C9C8C0-7B3E-4B7E-AF9A-9B3B8B8B8B8B}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
OutputDir=.
OutputBaseFilename=鲲穹AI浏览器_安装包
SetupIconFile=MiniWorldBrowser\Resources\鲲穹AI浏览器.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"

[CustomMessages]
chinesesimplified.CreateDesktopIcon=创建桌面快捷方式(&D)
chinesesimplified.AdditionalIcons=附加快捷方式:
chinesesimplified.LaunchProgram=运行 %1

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "MiniWorldBrowser\Resources\鲲穹01.ico"; Flags: dontcopy
; 主程序与 WebView2、自包含运行时，从 publish 目录复制
Source: "{#MySourcePath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Python Bridge 源码，从项目根目录复制到安装目录的 python_bridge 子目录
Source: "python_bridge\*"; DestDir: "{app}\python_bridge"; Flags: ignoreversion recursesubdirs createallsubdirs
; 可选：桥接启动脚本，方便手动调试
Source: "run_bridge.bat"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
procedure InitializeWizard();
begin
end;
