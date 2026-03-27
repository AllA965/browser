# macOS x64 + arm64 Build Guide

`MiniWorldBrowser` is currently `WinForms + WebView2` and Windows-only.

This repository now includes two macOS targets:

1. `MiniWorldBrowser.MacLauncher` (lightweight .NET launcher)
2. `mac_app/KunQiongBrowserMac.swift` (native `AppKit + WKWebView` browser shell)

## Recommended: Native macOS app (real browser shell)

Build script:

- `scripts/build-macos-native.sh`
- `scripts/build-macos-native.ps1` (if you use PowerShell on macOS)

Requirements:

- macOS
- Xcode Command Line Tools (`swiftc`, `lipo`, `codesign`, `ditto`)

Run:

```bash
chmod +x ./scripts/build-macos-native.sh
./scripts/build-macos-native.sh
```

PowerShell:

```powershell
./scripts/build-macos-native.ps1
```

Output:

- `artifacts/macos/native/osx-x64/KunQiongBrowser.app`
- `artifacts/macos/native/osx-arm64/KunQiongBrowser.app`
- `artifacts/macos/native/universal/KunQiongBrowser.app`
- portable zip packages:
  - `artifacts/macos/portable/KunQiongBrowser-macos-x64-portable-v1.0.0.zip`
  - `artifacts/macos/portable/KunQiongBrowser-macos-arm64-portable-v1.0.0.zip`
  - `artifacts/macos/portable/KunQiongBrowser-macos-universal-portable-v1.0.0.zip`
- checksums:
  - `artifacts/macos/portable/SHA256SUMS.txt`

Settings file (native mac app):

- `~/Library/Application Support/MiniWorldBrowser/settings.json`

Settings file (portable package, recommended):

- `KunQiongBrowser.app/Contents/Resources/portable-data/settings.json`

Supported keys (compatible naming with Windows side):

- `HomePage`
- `SearchEngine`
- `StartupBehavior` (`0=new/home`, `1=restore last session`, `2=open StartupPages`)
- `StartupPages` (array)
- `LastSessionUrls` (array, auto-saved)

Startup URL options:

```bash
KUNQIONG_HOME_URL="https://example.com" ./artifacts/macos/native/universal/KunQiongBrowser.app/Contents/MacOS/KunQiongBrowser
```

or:

```bash
./artifacts/macos/native/universal/KunQiongBrowser.app/Contents/MacOS/KunQiongBrowser "https://example.com"
```

## Optional: .NET launcher target

Build scripts:

- `scripts/build-macos.ps1`
- `scripts/build-macos.sh`

Run:

```bash
chmod +x ./scripts/build-macos.sh
./scripts/build-macos.sh
```

Output:

- `artifacts/macos/osx-x64`
- `artifacts/macos/osx-arm64`
- `artifacts/macos/universal` (only when merged on macOS with `lipo`)
