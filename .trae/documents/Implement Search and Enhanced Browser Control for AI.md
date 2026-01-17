I will implement the requested search functionality and additional browser control commands by extending the `IBrowserController` interface and updating the AI bridge.

**Plan:**

1.  **Update Interface (`IBrowserController.cs`)**:
    *   Add `Search(string query)` method for performing searches.
    *   Add `NewTab(string url)` method for opening new tabs.
    *   Add `CloseCurrentTab()` method for closing the active tab.

2.  **Update Implementation (`BrowserController.cs`)**:
    *   Inject `ISettingsService` into the constructor to access the user's preferred search engine.
    *   Implement `Search`: Construct the search URL using the configured search engine and the query, then open it in a new tab.
    *   Implement `NewTab`: Open a new tab with the specified URL (or default "new tab" page).
    *   Implement `CloseCurrentTab`: Close the currently active tab.

3.  **Update Main Form (`MainForm.cs`)**:
    *   Pass the `_settingsService` instance when initializing `BrowserController`.

4.  **Update AI Bridge (`AiApiBridge.cs`)**:
    *   **System Prompt**: Add instructions for the new commands (`search`, `new_tab`, `close_tab`) so the AI knows how to use them.
    *   **Command Parsing**: Enhance `TryParseCommand` to support extracting a `content` field (for search queries).
    *   **Command Execution**: Update `ExecuteCommand` to call the new methods on `BrowserController`.

**Result:**
The AI will be able to respond to commands like "Search for [keyword]", "Open a new tab", and "Close this tab" by directly controlling the browser.