using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using MiniWorldBrowser.Services.Interfaces;
using System.Diagnostics;

namespace MiniWorldBrowser.Helpers
{
    /// <summary>
    /// WebView2 与 C# 之间的桥接类，用于处理 AI API 调用
    /// </summary>
    [ComVisible(true)]
    public class AiApiBridge
    {
        private readonly ISettingsService _settingsService;
        private readonly IBrowserController? _browserController;
        private static readonly HttpClient _httpClient;
        
        // 维护聊天上下文
        private List<object> _chatHistory = new List<object>();
        
        // 维护历史摘要
        private string _contextSummary = "";
        
        // 任务取消令牌源
        private CancellationTokenSource? _cts;
        
        // 流式输出事件 (content, type: "chunk"|"done"|"error")
        public event Action<string, string>? OnStreamChunk;

        private const string SystemPrompt = @"你是一个内置在浏览器中的智能助手。
请区分【浏览器操作指令】和【普通文本回答】：

**核心规则（必须严格遵守）**：
1. **ReAct 模式**：对于需要操作浏览器的复杂任务，请遵循以下循环：
   - **Thought**: 简要描述你当前的思考和下一步计划（例如：""我需要先阅读页面内容"" 或 ""没找到目标，我需要向下滚动""）。
   - **Action**: 输出 JSON 格式的指令。
   - **Observation**: 你将收到系统的执行结果。
2. **禁止 Markdown**：输出 JSON 时，不要使用 ```json 或 ``` 包裹，直接输出纯 JSON 字符串。
3. **任务结束**：当任务完成或只是闲聊时，请直接用自然语言回答用户，不要包含 Thought/Action 格式。
    4. **知识/规划类任务**：对于“旅游攻略”、“解释概念”等可以通过内部知识或简单搜索回答的问题，**优先直接回答**或使用 `search` 指令，**严禁**执行复杂的“导航->输入->点击”流程，除非用户明确要求。

    支持的操作指令格式：
1. 搜索: { ""command"": ""search"", ""content"": ""关键词"" }
2. 站内搜索: { ""command"": ""site_search"", ""content"": ""关键词"", ""site"": ""网站名"" }
3. 打开网页: { ""command"": ""navigate"", ""url"": ""网址"" }
4. 新建标签页: { ""command"": ""new_tab"", ""url"": ""网址(可选)"" }
5. 关闭当前标签: { ""command"": ""close_tab"" }
6. 阅读网页: { ""command"": ""read_page"" }
7. 后退: { ""command"": ""back"" }
8. 前进: { ""command"": ""forward"" }
9. 刷新: { ""command"": ""refresh"" }
10. 向下滚动: { ""command"": ""scroll_down"" }
11. 向上滚动: { ""command"": ""scroll_up"" }
12. 点击元素: { ""command"": ""click"", ""selector"": ""元素ID或选择器"" }
13. 输入文本: { ""command"": ""type"", ""selector"": ""元素ID或选择器"", ""text"": ""内容"" }

重要提示：
- **优先读取**：如果用户请求“总结”、“翻译”、“解释代码”或基于当前页面回答问题，**必须**先发送 { ""command"": ""read_page"" }。
- **捷径**：
  - B站历史: https://www.bilibili.com/account/history
  - B站动态: https://t.bilibili.com/
  - 知乎热榜: https://www.zhihu.com/hot

示例：
User: 帮我总结这个页面
Assistant: Thought: 我需要先获取页面内容才能进行总结。
{ ""command"": ""read_page"" }
";

        static AiApiBridge()
        {
            var handler = new HttpClientHandler
            {
                UseProxy = true,
                Proxy = null, // 使用系统默认代理
                UseDefaultCredentials = true
            };
            
            // 尝试忽略 SSL 证书错误（仅用于开发/测试环境，生产环境建议移除）
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(60)
            };

            try 
            { 
                // 确保启用现代 TLS 协议
                System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls13; 
            }
            catch { /* 忽略不支持的旧系统 */ }
        }

        public AiApiBridge(ISettingsService settingsService, IBrowserController? browserController = null)
        {
            _settingsService = settingsService;
            _browserController = browserController;
            
            // 初始化历史记录
            _chatHistory.Add(new { role = "system", content = SystemPrompt });
        }

        /// <summary>
        /// 获取当前配置的 AI 模型名称
        /// </summary>
        public string GetModelName()
        {
            try
            {
                var settings = _settingsService.Settings;
                return settings.AiModelName ?? "未知模型";
            }
            catch
            {
                return "未知模型";
            }
        }

        /// <summary>
        /// 取消当前的 AI 生成任务
        /// </summary>
        public void CancelGeneration()
        {
            try
            {
                _cts?.Cancel();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"取消任务失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 由 JavaScript 调用，发送消息给 AI API
        /// </summary>
        public async Task<string> CallAiApi(string message, string mode = "tool")
        {
            var settings = _settingsService.Settings;
            if (string.IsNullOrEmpty(settings.AiApiKey)) return "错误：未配置 API Key";
            if (string.IsNullOrWhiteSpace(settings.AiApiBaseUrl)) return "错误：未配置 API Base URL";

            // 初始化取消令牌
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            // 纯对话模式：使用简化 Prompt，启用流式输出
            if (mode == "chat")
            {
                return await CallAiApiChatMode(message, settings, token);
            }

            // 工具模式：原有逻辑
            // 1. 将用户消息加入历史
            _chatHistory.Add(new { role = "user", content = message });

            // 限制历史记录长度，保留最近的 20 条，但保留第一条 system prompt
            if (_chatHistory.Count > 21)
            {
                // 触发历史记录摘要生成
                await SummarizeHistoryAsync();
            }

            // 2. ReAct 循环
            int maxSteps = 15; // 增加最大步数，支持自动连续执行
            int currentStep = 0;
            string lastResponse = "";

            Debug.WriteLine($"[AiApiBridge] ToolMode Request - Model: {settings.AiModelName}");

            while (currentStep < maxSteps)
            {
                // 检查取消请求
                if (token.IsCancellationRequested) return "操作已由用户手动取消。";

                currentStep++;

                // 如果步数较多，自动进行历史摘要以避免上下文溢出
                if (currentStep % 5 == 0)
                {
                     await SummarizeHistoryAsync();
                }

                // 准备请求
                string baseUrl = settings.AiApiBaseUrl.Trim().TrimEnd('/');
                
                // 处理厂商特定的 URL 修正逻辑 (MiniMax / DashScope)
                bool isMiniMax = baseUrl.Contains("minimax.io", StringComparison.OrdinalIgnoreCase) || baseUrl.Contains("minimaxi.com", StringComparison.OrdinalIgnoreCase);
                bool isDashScope = baseUrl.Contains("dashscope", StringComparison.OrdinalIgnoreCase) || baseUrl.Contains("aliyuncs.com", StringComparison.OrdinalIgnoreCase);

                if (isMiniMax)
                {
                    try { if (!baseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)) baseUrl = "https://" + baseUrl; var uri = new Uri(baseUrl); baseUrl = $"{uri.Scheme}://{uri.Host}/v1"; } catch { }
                }
                if (isDashScope)
                {
                    try { if (!baseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)) baseUrl = "https://" + baseUrl; var uri = new Uri(baseUrl); var path = uri.AbsolutePath.TrimEnd('/'); if (!path.Contains("compatible-mode", StringComparison.OrdinalIgnoreCase)) baseUrl = $"{uri.Scheme}://{uri.Host}/compatible-mode/v1"; else if (!path.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) baseUrl = $"{uri.Scheme}://{uri.Host}{path}/v1"; } catch { }
                }

                var requestBody = new
                {
                    model = settings.AiModelName,
                    messages = BuildMessagesWithSummary(), // 使用带有摘要的历史记录
                    stream = false
                };

                // 发送请求
                try
                {
                    var json = JsonSerializer.Serialize(requestBody);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl.TrimEnd('/') + "/chat/completions");
                    request.Headers.Add("Authorization", $"Bearer {settings.AiApiKey}");
                    request.Content = content;

                    var response = await _httpClient.SendAsync(request, token);
                    var responseJson = await response.Content.ReadAsStringAsync(token);

                    if (!response.IsSuccessStatusCode) return $"API 错误: {responseJson}";

                    using var doc = JsonDocument.Parse(responseJson);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        var choice = choices[0];
                        if (choice.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var aiContent))
                        {
                            string responseText = aiContent.GetString() ?? "";
                            
                            // 过滤特殊 Token
                            responseText = responseText.Replace("<|endoftext|>", "").Replace("<|im_end|>", "").Replace("<|im_start|>", "").Trim();
                            
                            lastResponse = responseText;
                            
                            // 将 AI 回复加入历史
                            _chatHistory.Add(new { role = "assistant", content = responseText });

                            // 解析指令
                            if (_browserController != null)
                            {
                                var commands = ParseCommands(responseText);
                                if (commands.Count > 0)
                                {
                                    // 执行指令
                                    StringBuilder observationBuilder = new StringBuilder();
                                    bool hasReadPage = false;

                                    foreach (var cmd in commands)
                                    {
                                        if (token.IsCancellationRequested) return "操作已由用户手动取消。";
                                        
                                        string? result = await ExecuteCommandAsync(cmd);
                                        if (!string.IsNullOrEmpty(result))
                                        {
                                            observationBuilder.AppendLine($"Command '{cmd.Command}' result: {result}");
                                        }
                                        if (cmd.Command == "read_page") hasReadPage = true;
                                        if (commands.Count > 1) await Task.Delay(500, token);
                                    }

                                    string observation = observationBuilder.ToString();
                                    if (string.IsNullOrWhiteSpace(observation)) observation = "Action executed successfully.";
                                    
                                    // 将观察结果加入历史
                                    _chatHistory.Add(new { role = "user", content = $"Observation (系统反馈): {observation}" });
                                    
                                    // 如果包含 read_page，继续循环让 AI 分析页面
                                    if (hasReadPage) continue;
                                    
                                    // 如果有命令执行，继续循环让 AI 决定下一步
                                    continue; 
                                }
                                else
                                {
                                    // 没有解析出指令，说明是普通回复，任务结束
                                    return responseText;
                                }
                            }
                            return responseText;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return "操作已由用户手动取消。";
                }
                catch (Exception ex)
                {
                    return $"调用异常: {ex.Message}";
                }
            }

            return $"已自动执行 {maxSteps} 步，但任务似乎尚未结束。当前状态摘要：\n{_contextSummary}\n\n最后回复：\n{lastResponse}"; // 超过最大步数
        }

        private async Task<string> CallAiApiChatMode(string message, dynamic settings, CancellationToken token)
        {
            try
            {
                // 简单的 Chat 历史管理（仅内存，不持久化摘要，或者可以复用 _chatHistory 但不带 SystemPrompt）
                // 为了简单起见，这里复用 _chatHistory 但临时替换 SystemPrompt
                
                // 1. 记录用户消息
                _chatHistory.Add(new { role = "user", content = message });

                // 2. 构造请求
                string baseUrl = settings.AiApiBaseUrl.Trim().TrimEnd('/');
                
                // 处理厂商 URL
                bool isMiniMax = baseUrl.Contains("minimax.io", StringComparison.OrdinalIgnoreCase) || baseUrl.Contains("minimaxi.com", StringComparison.OrdinalIgnoreCase);
                bool isDashScope = baseUrl.Contains("dashscope", StringComparison.OrdinalIgnoreCase) || baseUrl.Contains("aliyuncs.com", StringComparison.OrdinalIgnoreCase);

                if (isMiniMax)
                {
                    try { if (!baseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)) baseUrl = "https://" + baseUrl; var uri = new Uri(baseUrl); baseUrl = $"{uri.Scheme}://{uri.Host}/v1"; } catch { }
                }
                if (isDashScope)
                {
                    try { if (!baseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)) baseUrl = "https://" + baseUrl; var uri = new Uri(baseUrl); var path = uri.AbsolutePath.TrimEnd('/'); if (!path.Contains("compatible-mode", StringComparison.OrdinalIgnoreCase)) baseUrl = $"{uri.Scheme}://{uri.Host}/compatible-mode/v1"; else if (!path.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) baseUrl = $"{uri.Scheme}://{uri.Host}{path}/v1"; } catch { }
                }

                // 构造专门用于对话的 Messages
                var chatMessages = new List<object>();
                chatMessages.Add(new { role = "system", content = "你是一个乐于助人的 AI 助手。请直接回答用户问题，无需输出 JSON 指令。" });
                // 添加最近 20 条历史（跳过旧的 System Prompt）
                chatMessages.AddRange(_chatHistory.Where(x => 
                {
                    // 简单的反射检查，排除旧的 System Prompt
                    dynamic d = x;
                    try { return d.role != "system"; } catch { return true; }
                }).TakeLast(20));

                var requestBody = new
                {
                    model = settings.AiModelName,
                    messages = chatMessages,
                    stream = true // 启用流式
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl.TrimEnd('/') + "/chat/completions");
                request.Headers.Add("Authorization", $"Bearer {settings.AiApiKey}");
                request.Content = content;

                // 3. 发送流式请求
                Debug.WriteLine($"[AiApiBridge] ChatMode Request - Model: {settings.AiModelName}");
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(token);
                    return $"API 错误: {error}";
                }

                using var stream = await response.Content.ReadAsStreamAsync(token);
                using var reader = new System.IO.StreamReader(stream);

                StringBuilder fullResponse = new StringBuilder();
                string? line;
                
                // 通知前端开始流式接收
                OnStreamChunk?.Invoke("", "start");

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (token.IsCancellationRequested) break;
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (line.StartsWith("data: "))
                    {
                        var data = line.Substring(6).Trim();
                        if (data == "[DONE]") break;

                        try
                        {
                            using var doc = JsonDocument.Parse(data);
                            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                            {
                                var choice = choices[0];
                                if (choice.TryGetProperty("delta", out var delta) && delta.TryGetProperty("content", out var chunk))
                                {
                                    string text = chunk.GetString() ?? "";
                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        fullResponse.Append(text);
                                        // 发送 chunk 给前端
                                        OnStreamChunk?.Invoke(text, "chunk");
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }

                string finalResponse = fullResponse.ToString();
                
                // 记录 AI 回复到历史
                _chatHistory.Add(new { role = "assistant", content = finalResponse });
                
                // 通知前端结束
                OnStreamChunk?.Invoke("", "done");

                return "__STREAMING__"; // 告诉前端已通过事件发送，无需显示返回值
            }
            catch (OperationCanceledException)
            {
                return "操作已由用户手动取消。";
            }
            catch (Exception ex)
            {
                return $"调用异常: {ex.Message}";
            }
        }

        private List<object> BuildMessagesWithSummary()
        {
            var messages = new List<object>();
            
            // 1. 添加 System Prompt
            messages.Add(_chatHistory[0]);

            // 2. 如果有历史摘要，作为第二条 System 消息插入
            if (!string.IsNullOrEmpty(_contextSummary))
            {
                messages.Add(new { role = "system", content = $"【历史任务摘要】之前的对话总结如下，请基于此继续执行任务：\n{_contextSummary}" });
            }

            // 3. 添加剩余的历史记录（跳过 SystemPrompt）
            if (_chatHistory.Count > 1)
            {
                messages.AddRange(_chatHistory.Skip(1));
            }

            return messages;
        }

        private async Task SummarizeHistoryAsync()
        {
            try
            {
                var settings = _settingsService.Settings;
                string baseUrl = settings.AiApiBaseUrl?.Trim().TrimEnd('/') ?? "";
                if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(settings.AiApiKey)) return;

                // 提取需要总结的历史（保留 System Prompt 和最近 10 条，总结中间的）
                if (_chatHistory.Count <= 12) return;

                int keepCount = 10;
                int summarizeCount = _chatHistory.Count - 1 - keepCount; // 减1是因为排除 System Prompt
                if (summarizeCount <= 0) return;

                var historyToSummarize = _chatHistory.Skip(1).Take(summarizeCount).ToList();
                
                // 构造总结请求
                var summaryPrompt = "请简要总结上述对话中用户的目标、已完成的操作步骤以及当前的执行状态。保留关键信息，忽略闲聊。";
                var summaryMessages = new List<object>(historyToSummarize);
                summaryMessages.Add(new { role = "user", content = summaryPrompt });

                // 调用 AI 生成摘要
                // 注意：这里复用 baseUrl 处理逻辑可能有点复杂，简化处理，假设 CallAiApi 里的逻辑是正确的
                // 为避免代码重复，最好重构 GetBaseUrl 逻辑，这里先简化复制
                bool isMiniMax = baseUrl.Contains("minimax.io") || baseUrl.Contains("minimaxi.com");
                bool isDashScope = baseUrl.Contains("dashscope") || baseUrl.Contains("aliyuncs.com");

                 if (isMiniMax)
                {
                    try { if (!baseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)) baseUrl = "https://" + baseUrl; var uri = new Uri(baseUrl); baseUrl = $"{uri.Scheme}://{uri.Host}/v1"; } catch { }
                }
                if (isDashScope)
                {
                    try { if (!baseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)) baseUrl = "https://" + baseUrl; var uri = new Uri(baseUrl); var path = uri.AbsolutePath.TrimEnd('/'); if (!path.Contains("compatible-mode", StringComparison.OrdinalIgnoreCase)) baseUrl = $"{uri.Scheme}://{uri.Host}/compatible-mode/v1"; else if (!path.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) baseUrl = $"{uri.Scheme}://{uri.Host}{path}/v1"; } catch { }
                }

                var requestBody = new
                {
                    model = settings.AiModelName,
                    messages = summaryMessages,
                    stream = false
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl.TrimEnd('/') + "/chat/completions");
                request.Headers.Add("Authorization", $"Bearer {settings.AiApiKey}");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseJson);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        var choice = choices[0];
                        if (choice.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var aiContent))
                        {
                            string newSummary = aiContent.GetString() ?? "";
                            
                            // 更新摘要：旧摘要 + 新摘要
                            if (!string.IsNullOrEmpty(_contextSummary))
                            {
                                _contextSummary = $"之前的阶段：{_contextSummary}\n新的阶段：{newSummary}";
                            }
                            else
                            {
                                _contextSummary = newSummary;
                            }

                            // 裁剪历史记录：保留 System Prompt + 最近 keepCount 条
                            var recentHistory = _chatHistory.Skip(_chatHistory.Count - keepCount).ToList();
                            _chatHistory.Clear();
                            _chatHistory.Add(new { role = "system", content = SystemPrompt });
                            _chatHistory.AddRange(recentHistory);
                            
                            Debug.WriteLine($"[Summarization] Context summarized. New summary length: {_contextSummary.Length}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Summarization] Failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 指令模型
        /// </summary>
        public class BrowserCommand
        {
            public string Command { get; set; } = "";
            public string? Url { get; set; }
            public string? Content { get; set; }
            public string? Site { get; set; }
            public string? Selector { get; set; }
            public string? Text { get; set; }
        }

        /// <summary>
        /// 解析 AI 返回的指令
        /// </summary>
        private List<BrowserCommand> ParseCommands(string content)
        {
            var commands = new List<BrowserCommand>();
            if (string.IsNullOrWhiteSpace(content)) return commands;

            try
            {
                // 1. 尝试寻找 Markdown 代码块
                var match = System.Text.RegularExpressions.Regex.Match(content, @"```(?:json)?\s*([\s\S]*?)```");
                string jsonToParse = match.Success ? match.Groups[1].Value.Trim() : content.Trim();

                // 2. 尝试直接解析为 JSON 数组
                if (jsonToParse.StartsWith("["))
                {
                    try
                    {
                        var list = JsonSerializer.Deserialize<List<BrowserCommand>>(jsonToParse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (list != null) return list;
                    }
                    catch { }
                }

                // 3. 尝试寻找所有的 JSON 对象 { ... }
                var matches = System.Text.RegularExpressions.Regex.Matches(jsonToParse, @"\{(?:[^{}]|(?<open>\{)|(?<-open>\}))+(?(open)(?!))\}");
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    try
                    {
                        var cmd = JsonSerializer.Deserialize<BrowserCommand>(m.Value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (cmd != null && !string.IsNullOrEmpty(cmd.Command))
                        {
                            commands.Add(cmd);
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析指令失败: {ex.Message}");
            }
            return commands;
        }

        private async Task<string?> ExecuteCommandAsync(BrowserCommand cmd)
        {
            if (_browserController == null) return null;
            
            try
            {
                switch (cmd.Command.ToLower())
                {
                    case "navigate":
                        if (!string.IsNullOrEmpty(cmd.Url)) _browserController.Navigate(cmd.Url);
                        break;
                    case "search":
                        if (!string.IsNullOrEmpty(cmd.Content)) _browserController.Search(cmd.Content);
                        break;
                    case "site_search":
                        if (!string.IsNullOrEmpty(cmd.Content) && !string.IsNullOrEmpty(cmd.Site)) 
                            _browserController.SearchOnSite(cmd.Content, cmd.Site);
                        else if (!string.IsNullOrEmpty(cmd.Content))
                            _browserController.Search(cmd.Content);
                        break;
                    case "new_tab":
                        _browserController.NewTab(cmd.Url ?? "");
                        break;
                    case "close_tab":
                        _browserController.CloseCurrentTab();
                        break;
                    case "read_page":
                        return await _browserController.GetPageContentAsync();
                    case "click":
                        if (!string.IsNullOrEmpty(cmd.Selector))
                        {
                            if (int.TryParse(cmd.Selector, out int id))
                                _browserController.ClickElementById(id.ToString());
                            else
                                _browserController.ClickElement(cmd.Selector);
                        }
                        break;
                    case "type":
                        if (!string.IsNullOrEmpty(cmd.Selector))
                        {
                            if (int.TryParse(cmd.Selector, out int id))
                                _browserController.TypeToElementById(id.ToString(), cmd.Text ?? "");
                            else
                                _browserController.TypeToElement(cmd.Selector, cmd.Text ?? "");
                        }
                        break;
                    case "back":
                        _browserController.GoBack();
                        break;
                    case "forward":
                        _browserController.GoForward();
                        break;
                    case "refresh":
                        _browserController.Refresh();
                        break;
                    case "scroll_down":
                        _browserController.Scroll(300);
                        break;
                    case "scroll_up":
                        _browserController.Scroll(-300);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"执行浏览器命令失败: {ex.Message}");
                return $"Error: {ex.Message}";
            }
            return null;
        }
    }
}
