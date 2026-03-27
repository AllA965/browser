# KunQiongBrowser mac_app

Native macOS browser shell source:

- `KunQiongBrowserMac.swift`

Tech stack:

- `AppKit`
- `WKWebView`

Current features:

- Address bar (`URL` or keyword search)
- Back / Forward / Reload
- New window
- Startup behavior from settings (`StartupBehavior`)
- Session restore (`LastSessionUrls`)

Settings file:

- `~/Library/Application Support/MiniWorldBrowser/settings.json`
- Example template: `settings.example.json`

Portable mode:

- auto-enabled when `portable.mode` exists in app bundle resources
- can also force with env: `KUNQIONG_PORTABLE=1`
- portable settings path: `KunQiongBrowser.app/Contents/Resources/portable-data/settings.json`

Build with:

- `../scripts/build-macos-native.sh`
