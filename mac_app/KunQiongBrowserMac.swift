import Cocoa
import WebKit

private let defaultHomeURL = "https://www.bing.com"
private let defaultSearchURL = "https://www.bing.com/search?q="

final class SettingsStore {
    static let shared = SettingsStore()

    private let fileManager = FileManager.default
    private let portableMode: Bool
    private let appSupportDirectory: URL
    private let settingsFileURL: URL
    private var raw: [String: Any] = [:]

    private init() {
        let resolved = SettingsStore.resolveSettingsDirectory(fileManager: fileManager)
        portableMode = resolved.portable
        appSupportDirectory = resolved.directory
        settingsFileURL = appSupportDirectory.appendingPathComponent("settings.json", isDirectory: false)
        load()
    }

    var homePage: String {
        get { string(forKey: "HomePage", defaultValue: "about:newtab") }
        set { raw["HomePage"] = newValue }
    }

    var searchEngine: String {
        get { string(forKey: "SearchEngine", defaultValue: defaultSearchURL) }
        set { raw["SearchEngine"] = newValue }
    }

    var startupBehavior: Int {
        get { int(forKey: "StartupBehavior", defaultValue: 0) }
        set { raw["StartupBehavior"] = newValue }
    }

    var startupPages: [String] {
        get { stringArray(forKey: "StartupPages") }
        set { raw["StartupPages"] = newValue }
    }

    var lastSessionUrls: [String] {
        get { stringArray(forKey: "LastSessionUrls") }
        set { raw["LastSessionUrls"] = newValue }
    }

    func load() {
        do {
            if fileManager.fileExists(atPath: settingsFileURL.path) {
                let data = try Data(contentsOf: settingsFileURL)
                if let object = try JSONSerialization.jsonObject(with: data) as? [String: Any] {
                    raw = object
                }
            }
        } catch {
            raw = [:]
        }
    }

    func save() {
        do {
            if !fileManager.fileExists(atPath: appSupportDirectory.path) {
                try fileManager.createDirectory(at: appSupportDirectory, withIntermediateDirectories: true, attributes: nil)
            }
            let data = try JSONSerialization.data(withJSONObject: raw, options: [.prettyPrinted, .sortedKeys])
            try data.write(to: settingsFileURL, options: .atomic)
        } catch {
            NSLog("Failed to save settings: \(error.localizedDescription)")
        }
    }

    func isPortableModeEnabled() -> Bool {
        portableMode
    }

    func normalizedHomeURL() -> URL {
        let rawHome = homePage.trimmingCharacters(in: .whitespacesAndNewlines)
        if rawHome.isEmpty || rawHome == "about:newtab" || rawHome == "about:home" {
            return URL(string: defaultHomeURL)!
        }

        if let direct = URL(string: rawHome), isNavigableURL(direct) {
            return direct
        }

        if !rawHome.contains(" "), rawHome.contains(".") {
            if let webURL = URL(string: "https://\(rawHome)") {
                return webURL
            }
        }

        return URL(string: defaultHomeURL)!
    }

    private func string(forKey key: String, defaultValue: String) -> String {
        if let value = raw[key] as? String, !value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            return value
        }
        return defaultValue
    }

    private func int(forKey key: String, defaultValue: Int) -> Int {
        if let value = raw[key] as? Int {
            return value
        }
        if let number = raw[key] as? NSNumber {
            return number.intValue
        }
        return defaultValue
    }

    private func stringArray(forKey key: String) -> [String] {
        guard let array = raw[key] as? [Any] else {
            return []
        }
        return array.compactMap { item in
            guard let text = item as? String else { return nil }
            let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
            return trimmed.isEmpty ? nil : trimmed
        }
    }

    private static func resolveSettingsDirectory(fileManager: FileManager) -> (directory: URL, portable: Bool) {
        let envPortable = ProcessInfo.processInfo.environment["KUNQIONG_PORTABLE"] == "1"

        let executableURL = URL(fileURLWithPath: CommandLine.arguments[0]).standardizedFileURL
        let executableDirectory = executableURL.deletingLastPathComponent()
        let bundleURL = Bundle.main.bundleURL
        let resourcesURL = Bundle.main.resourceURL
        let bundleParent = bundleURL.deletingLastPathComponent()

        let markerCandidates: [URL?] = [
            resourcesURL?.appendingPathComponent("portable.mode", isDirectory: false),
            executableDirectory.appendingPathComponent("portable.mode", isDirectory: false),
            bundleParent.appendingPathComponent("portable.mode", isDirectory: false)
        ]

        let hasPortableMarker = markerCandidates
            .compactMap { $0 }
            .contains { fileManager.fileExists(atPath: $0.path) }

        if envPortable || hasPortableMarker {
            let portableBase = resourcesURL?.appendingPathComponent("portable-data", isDirectory: true)
                ?? executableDirectory.appendingPathComponent("portable-data", isDirectory: true)
            return (portableBase, true)
        }

        let base = fileManager.urls(for: .applicationSupportDirectory, in: .userDomainMask).first
            ?? URL(fileURLWithPath: NSHomeDirectory()).appendingPathComponent("Library/Application Support", isDirectory: true)
        return (base.appendingPathComponent("MiniWorldBrowser", isDirectory: true), false)
    }
}

final class BrowserWindowController: NSWindowController, NSWindowDelegate, NSTextFieldDelegate, WKNavigationDelegate, WKUIDelegate {
    private let webView: WKWebView
    private let settings: SettingsStore
    private let addressField = NSTextField(string: "")
    private let backButton = NSButton(title: "<", target: nil, action: nil)
    private let forwardButton = NSButton(title: ">", target: nil, action: nil)
    private let reloadButton = NSButton(title: "R", target: nil, action: nil)
    private let newWindowButton = NSButton(title: "+", target: nil, action: nil)
    private var urlObservation: NSKeyValueObservation?
    private var titleObservation: NSKeyValueObservation?

    var onClose: (() -> Void)?
    var currentURL: URL? {
        webView.url
    }

    init(initialURL: URL, settings: SettingsStore) {
        self.settings = settings
        let configuration = WKWebViewConfiguration()
        configuration.defaultWebpagePreferences.allowsContentJavaScript = true
        configuration.websiteDataStore = .default()
        self.webView = WKWebView(frame: .zero, configuration: configuration)

        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 1320, height: 860),
            styleMask: [.titled, .closable, .miniaturizable, .resizable],
            backing: .buffered,
            defer: false
        )
        window.title = "KunQiong Browser"
        window.minSize = NSSize(width: 920, height: 640)
        super.init(window: window)

        window.delegate = self
        setupLayout()
        configureEvents()
        load(url: initialURL)
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    func windowWillClose(_ notification: Notification) {
        onClose?()
    }

    private func setupLayout() {
        guard let contentView = window?.contentView else { return }

        let rootStack = NSStackView()
        rootStack.translatesAutoresizingMaskIntoConstraints = false
        rootStack.orientation = .vertical
        rootStack.spacing = 0
        rootStack.alignment = .leading
        rootStack.distribution = .fill
        contentView.addSubview(rootStack)

        NSLayoutConstraint.activate([
            rootStack.leadingAnchor.constraint(equalTo: contentView.leadingAnchor),
            rootStack.trailingAnchor.constraint(equalTo: contentView.trailingAnchor),
            rootStack.topAnchor.constraint(equalTo: contentView.topAnchor),
            rootStack.bottomAnchor.constraint(equalTo: contentView.bottomAnchor)
        ])

        let toolbarView = NSView()
        toolbarView.translatesAutoresizingMaskIntoConstraints = false
        toolbarView.wantsLayer = true
        toolbarView.layer?.backgroundColor = NSColor.windowBackgroundColor.cgColor
        toolbarView.heightAnchor.constraint(equalToConstant: 48).isActive = true

        let toolbarStack = NSStackView()
        toolbarStack.translatesAutoresizingMaskIntoConstraints = false
        toolbarStack.orientation = .horizontal
        toolbarStack.spacing = 8
        toolbarStack.alignment = .centerY
        toolbarView.addSubview(toolbarStack)

        NSLayoutConstraint.activate([
            toolbarStack.leadingAnchor.constraint(equalTo: toolbarView.leadingAnchor, constant: 10),
            toolbarStack.trailingAnchor.constraint(equalTo: toolbarView.trailingAnchor, constant: -10),
            toolbarStack.topAnchor.constraint(equalTo: toolbarView.topAnchor, constant: 8),
            toolbarStack.bottomAnchor.constraint(equalTo: toolbarView.bottomAnchor, constant: -8)
        ])

        configureToolbarButton(backButton, action: #selector(goBack))
        configureToolbarButton(forwardButton, action: #selector(goForward))
        configureToolbarButton(reloadButton, action: #selector(reloadPage))
        configureToolbarButton(newWindowButton, action: #selector(openNewWindow))

        addressField.translatesAutoresizingMaskIntoConstraints = false
        addressField.delegate = self
        addressField.placeholderString = "Enter URL or search keywords"
        addressField.font = NSFont.systemFont(ofSize: 14)
        addressField.isBezeled = true
        addressField.bezelStyle = .roundedBezel
        addressField.setContentHuggingPriority(.defaultLow, for: .horizontal)
        addressField.setContentCompressionResistancePriority(.defaultLow, for: .horizontal)

        toolbarStack.addArrangedSubview(backButton)
        toolbarStack.addArrangedSubview(forwardButton)
        toolbarStack.addArrangedSubview(reloadButton)
        toolbarStack.addArrangedSubview(newWindowButton)
        toolbarStack.addArrangedSubview(addressField)

        backButton.widthAnchor.constraint(equalToConstant: 34).isActive = true
        forwardButton.widthAnchor.constraint(equalToConstant: 34).isActive = true
        reloadButton.widthAnchor.constraint(equalToConstant: 34).isActive = true
        newWindowButton.widthAnchor.constraint(equalToConstant: 34).isActive = true
        addressField.heightAnchor.constraint(equalToConstant: 30).isActive = true

        webView.translatesAutoresizingMaskIntoConstraints = false
        webView.allowsMagnification = true

        rootStack.addArrangedSubview(toolbarView)
        rootStack.addArrangedSubview(webView)
    }

    private func configureToolbarButton(_ button: NSButton, action: Selector) {
        button.target = self
        button.action = action
        button.bezelStyle = .texturedRounded
        button.font = NSFont.systemFont(ofSize: 14, weight: .medium)
        button.setButtonType(.momentaryPushIn)
    }

    private func configureEvents() {
        webView.navigationDelegate = self
        webView.uiDelegate = self

        urlObservation = webView.observe(\.url, options: [.new]) { [weak self] view, _ in
            guard let self else { return }
            self.addressField.stringValue = view.url?.absoluteString ?? ""
        }

        titleObservation = webView.observe(\.title, options: [.new]) { [weak self] view, _ in
            guard let self else { return }
            let title = view.title?.trimmingCharacters(in: .whitespacesAndNewlines)
            if let title, !title.isEmpty {
                self.window?.title = title
            } else {
                self.window?.title = "KunQiong Browser"
            }
        }
    }

    @objc private func goBack() {
        if webView.canGoBack {
            webView.goBack()
        }
    }

    @objc private func goForward() {
        if webView.canGoForward {
            webView.goForward()
        }
    }

    @objc private func reloadPage() {
        if webView.url == nil {
            load(string: defaultHomeURL)
        } else {
            webView.reload()
        }
    }

    @objc private func openNewWindow() {
        AppCoordinator.shared.openWindow(with: webView.url)
    }

    func control(_ control: NSControl, textView: NSTextView, doCommandBy commandSelector: Selector) -> Bool {
        if commandSelector == #selector(NSResponder.insertNewline(_:)) {
            load(string: addressField.stringValue)
            return true
        }
        return false
    }

    func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
        backButton.isEnabled = webView.canGoBack
        forwardButton.isEnabled = webView.canGoForward
        if let current = webView.url?.absoluteString, !current.isEmpty {
            addressField.stringValue = current
        }
    }

    func webView(_ webView: WKWebView, createWebViewWith configuration: WKWebViewConfiguration, for navigationAction: WKNavigationAction, windowFeatures: WKWindowFeatures) -> WKWebView? {
        if navigationAction.targetFrame == nil, let url = navigationAction.request.url {
            AppCoordinator.shared.openWindow(with: url)
        }
        return nil
    }

    func webView(_ webView: WKWebView, decidePolicyFor navigationAction: WKNavigationAction, decisionHandler: @escaping (WKNavigationActionPolicy) -> Void) {
        decisionHandler(.allow)
    }

    private func load(url: URL) {
        let request = URLRequest(url: url, cachePolicy: .useProtocolCachePolicy, timeoutInterval: 30)
        webView.load(request)
        addressField.stringValue = url.absoluteString
    }

    private func load(string: String) {
        guard let url = normalizedURL(from: string) else { return }
        load(url: url)
    }

    private func normalizedURL(from raw: String) -> URL? {
        let input = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        if input.isEmpty {
            return settings.normalizedHomeURL()
        }

        if input == "about:newtab" || input == "about:home" {
            return settings.normalizedHomeURL()
        }

        if let direct = URL(string: input), let scheme = direct.scheme?.lowercased(), scheme == "http" || scheme == "https" {
            return direct
        }

        if !input.contains(" "), input.contains(".") {
            return URL(string: "https://\(input)")
        }

        guard let encoded = input.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) else {
            return settings.normalizedHomeURL()
        }

        let engine = settings.searchEngine.trimmingCharacters(in: .whitespacesAndNewlines)
        if engine.contains("{q}") {
            let built = engine.replacingOccurrences(of: "{q}", with: encoded)
            return URL(string: built)
        }

        let separator = engine.contains("?") ? "&" : "?"
        let base = engine.isEmpty ? defaultSearchURL : engine
        if base.hasSuffix("=") || base.hasSuffix("/") {
            return URL(string: "\(base)\(encoded)")
        }

        return URL(string: "\(base)\(separator)q=\(encoded)")
    }
}

final class AppCoordinator: NSObject, NSApplicationDelegate {
    static let shared = AppCoordinator()

    private let settings = SettingsStore.shared
    private var windows: [BrowserWindowController] = []

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.activate(ignoringOtherApps: true)
        setupMenu()
        openStartupWindows()
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        true
    }

    func applicationWillTerminate(_ notification: Notification) {
        persistSession()
    }

    @objc func createWindowFromMenu(_ sender: Any?) {
        openWindow(with: nil)
    }

    @objc func openHomeFromMenu(_ sender: Any?) {
        openWindow(with: settings.normalizedHomeURL())
    }

    @objc func setCurrentAsHome(_ sender: Any?) {
        guard let controller = activeBrowserController(), let url = controller.currentURL else { return }
        settings.homePage = url.absoluteString
        settings.save()
    }

    func openWindow(with url: URL?) {
        let initialURL = url ?? settings.normalizedHomeURL()
        let controller = BrowserWindowController(initialURL: initialURL, settings: settings)
        controller.onClose = { [weak self, weak controller] in
            guard let self, let controller else { return }
            self.windows.removeAll { $0 === controller }
            self.persistSession()
        }
        windows.append(controller)
        controller.showWindow(nil)
        controller.window?.makeKeyAndOrderFront(nil)
    }

    private func openStartupWindows() {
        if let external = resolveExternalStartupURL() {
            openWindow(with: external)
            return
        }

        switch settings.startupBehavior {
        case 1:
            let restored = validUrls(from: settings.lastSessionUrls)
            if restored.isEmpty {
                openWindow(with: settings.normalizedHomeURL())
            } else {
                for url in restored {
                    openWindow(with: url)
                }
            }
        case 2:
            let configured = validUrls(from: settings.startupPages)
            if configured.isEmpty {
                openWindow(with: settings.normalizedHomeURL())
            } else {
                for url in configured {
                    openWindow(with: url)
                }
            }
        default:
            openWindow(with: settings.normalizedHomeURL())
        }
    }

    private func resolveExternalStartupURL() -> URL? {
        if CommandLine.arguments.count > 1 {
            let candidate = CommandLine.arguments[1]
            if let url = URL(string: candidate), isNavigableURL(url) {
                return url
            }
        }

        if let envURL = ProcessInfo.processInfo.environment["KUNQIONG_HOME_URL"],
           let url = URL(string: envURL),
           isNavigableURL(url) {
            return url
        }

        return nil
    }

    private func validUrls(from items: [String]) -> [URL] {
        return items.compactMap { item in
            guard let url = URL(string: item), isNavigableURL(url) else { return nil }
            return url
        }
    }

    private func persistSession() {
        let urls = windows.compactMap { $0.currentURL }
            .filter { isNavigableURL($0) }
            .map { $0.absoluteString }
        settings.lastSessionUrls = urls
        settings.save()
    }

    private func activeBrowserController() -> BrowserWindowController? {
        guard let keyWindow = NSApp.keyWindow else { return nil }
        return windows.first { $0.window == keyWindow }
    }

    private func setupMenu() {
        let mainMenu = NSMenu()

        let appMenuItem = NSMenuItem()
        let appMenu = NSMenu()
        let appName = ProcessInfo.processInfo.processName
        appMenu.addItem(withTitle: "About \(appName)", action: nil, keyEquivalent: "")
        appMenu.addItem(NSMenuItem.separator())
        appMenu.addItem(withTitle: "Quit \(appName)", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        appMenuItem.submenu = appMenu
        mainMenu.addItem(appMenuItem)

        let fileMenuItem = NSMenuItem()
        let fileMenu = NSMenu(title: "File")
        let newWindow = NSMenuItem(title: "New Window", action: #selector(createWindowFromMenu(_:)), keyEquivalent: "n")
        newWindow.target = self
        fileMenu.addItem(newWindow)
        let openHome = NSMenuItem(title: "Open Home", action: #selector(openHomeFromMenu(_:)), keyEquivalent: "h")
        openHome.target = self
        fileMenu.addItem(openHome)
        let setHome = NSMenuItem(title: "Set Current As Home", action: #selector(setCurrentAsHome(_:)), keyEquivalent: "")
        setHome.target = self
        fileMenu.addItem(setHome)
        fileMenuItem.submenu = fileMenu
        mainMenu.addItem(fileMenuItem)

        NSApp.mainMenu = mainMenu
    }
}

private func isNavigableURL(_ url: URL) -> Bool {
    guard let scheme = url.scheme?.lowercased() else { return false }
    return scheme == "http" || scheme == "https"
}

let app = NSApplication.shared
app.setActivationPolicy(.regular)
app.delegate = AppCoordinator.shared
app.run()
