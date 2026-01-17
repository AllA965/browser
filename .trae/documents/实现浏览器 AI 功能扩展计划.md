# 浏览器 AI 功能扩展实现计划

根据您的需求，我们将基于 Chromium 扩展架构 (Manifest V3) 为 MiniWorldBrowser 实现 AI 功能扩展。这将包括自然语言指令解析、Markdown 渲染支持以及基于 Content Script 的安全 DOM 操作。

## 1. 架构升级：启用 Chromium 扩展支持
我们将修改浏览器内核初始化逻辑，使其支持加载标准的 Chromium 扩展 (Unpacked Extension)。
- **修改文件**: `BrowserTab.cs`
- **操作**: 在 `CoreWebView2EnvironmentOptions` 中添加 `--load-extension` 参数，指向本地扩展目录。
- **目的**: 确保扩展在所有标签页中自动加载，并符合浏览器安全策略。

## 2. 核心扩展开发 (Phase 1)
创建一个标准的 Chromium 扩展结构，作为 AI 功能的载体。
- **目录**: `MiniWorldBrowser/Resources/AiExtension/`
- **文件**:
    - `manifest.json`: 定义权限 (Scripting, ActiveTab) 和 Content Scripts。
    - `content.js`: **DOM 操作引擎**。实现 `click`, `type`, `read`, `highlight` 等原子操作。
    - `background.js`: (可选) 用于处理跨标签页逻辑。
- **通信机制**: C# `BrowserController` 将通过 `ExecuteScriptAsync` 或 `PostWebMessage` 向 `content.js` 发送指令，不再直接注入未经封装的 JS 代码。

## 3. 自然语言指令解析与执行
增强现有的 AI 桥接器，使其支持复合指令的拆分与执行。
- **修改文件**: `AiApiBridge.cs`
- **功能**:
    - **Prompt 工程优化**: 更新 System Prompt，指导 AI 将 "打开B站历史记录" 拆解为 `["navigate:bilibili", "click:profile", "click:history"]` 的指令序列。
    - **指令队列执行**: 在 C# 中实现指令队列管理器，按顺序调度执行步骤，并处理步骤间的等待与重试。

## 4. Markdown 渲染升级
优化 AI 聊天界面的渲染能力。
- **修改文件**: `ai_chat.html`
- **功能**:
    - 升级 Markdown 渲染配置（基于现有的 `marked.js`，优化配置以接近 `markdown-it` 的表现，或在有网络条件下引入 `markdown-it`）。
    - 增加代码块语法高亮支持。
    - 优化 CSS 样式以支持表格和复杂排版。

## 5. 质量保证与测试
- 编写测试用例（在代码注释中提供），验证指令拆分逻辑和 DOM 操作的准确性。
- 确保所有操作都在 Content Script 的隔离环境中执行，符合安全规范。

---

### 执行步骤
1.  **创建扩展文件**: 建立 `manifest.json` 和 `content.js`。
2.  **后端集成**: 修改 `BrowserTab.cs` 加载扩展。
3.  **前端优化**: 更新 `ai_chat.html` 的渲染逻辑。
4.  **逻辑实现**: 更新 `AiApiBridge.cs` 和 `BrowserController.cs` 以适配新的扩展架构。

请确认是否开始执行此计划？