; 鲲穹AI浏览器 Inno Setup 脚本
#define MyAppName "鲲穹AI浏览器"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "鲲穹AI"
#define MyAppExeName "鲲穹AI浏览器.exe"

[Setup]
; AppId 是唯一标识符，建议保持不变
AppId={{D8C9C8C0-7B3E-4B7E-AF9A-9B3B8B8B8B8B}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
; 导出安装包的位置
OutputDir=.
OutputBaseFilename=鲲穹AI浏览器_安装包
; 安装包图标
SetupIconFile=MiniWorldBrowser\Resources\鲲穹AI浏览器.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
; 使用您提供的完整中文语言包
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"

[CustomMessages]
; 补充语言包中可能缺失的自定义消息
chinesesimplified.CreateDesktopIcon=创建桌面快捷方式(&D)
chinesesimplified.AdditionalIcons=附加快捷方式:
chinesesimplified.LaunchProgram=运行 %1

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; 包含图标文件用于 UI 显示
Source: "MiniWorldBrowser\Resources\鲲穹01.ico"; Flags: dontcopy
; 包含 publish 目录下的所有文件
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
procedure InitializeWizard();
begin
  // 已移除自定义文本
end;
