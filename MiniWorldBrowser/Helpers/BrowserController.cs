using System;
using MiniWorldBrowser.Browser;
using MiniWorldBrowser.Services.Interfaces;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace MiniWorldBrowser.Helpers
{
    public class BrowserController : IBrowserController
    {
        private readonly BrowserTabManager _tabManager;
        private readonly Control _syncContext;
        private readonly ISettingsService _settingsService;

        public BrowserController(BrowserTabManager tabManager, Control syncContext, ISettingsService settingsService)
        {
            _tabManager = tabManager;
            _syncContext = syncContext;
            _settingsService = settingsService;
        }

        public void Navigate(string url)
        {
            _syncContext.Invoke(new Action(() => {
                if (!string.IsNullOrEmpty(url))
                {
                    if (!url.StartsWith("http") && !url.StartsWith("about:") && !url.StartsWith("file:"))
                    {
                        url = "https://" + url;
                    }
                    _tabManager.ActiveTab?.Navigate(url);
                }
            }));
        }

        public void Search(string query)
        {
            _syncContext.Invoke(new Action(async () => {
                if (!string.IsNullOrEmpty(query))
                {
                    var searchEngine = _settingsService?.Settings?.SearchEngine 
                        ?? MiniWorldBrowser.Constants.AppConstants.DefaultSearchEngine;
                    string url = searchEngine + Uri.EscapeDataString(query);
                    await _tabManager.CreateTabAsync(url);
                }
            }));
        }

        public void SearchOnSite(string query, string siteName)
        {
            _syncContext.Invoke(new Action(async () => {
                if (!string.IsNullOrEmpty(query))
                {
                    string urlTemplate = GetSiteSearchUrl(siteName);
                    if (!string.IsNullOrEmpty(urlTemplate))
                    {
                        string url = string.Format(urlTemplate, Uri.EscapeDataString(query));
                        await _tabManager.CreateTabAsync(url);
                    }
                    else
                    {
                        // 如果找不到特定站点，回退到普通搜索，但带上站点名
                        Search(query + " " + siteName);
                    }
                }
            }));
        }

        private string GetSiteSearchUrl(string siteName)
        {
            siteName = siteName.ToLower().Trim();
            
            // 常见网站搜索 URL 映射
            if (siteName.Contains("bilibili") || siteName.Contains("哔哩哔哩") || siteName.Contains("b站"))
                return "https://search.bilibili.com/all?keyword={0}";
            
            if (siteName.Contains("youtube") || siteName.Contains("油管"))
                return "https://www.youtube.com/results?search_query={0}";
            
            if (siteName.Contains("github"))
                return "https://github.com/search?q={0}";
                
            if (siteName.Contains("csdn"))
                return "https://so.csdn.net/so/search?q={0}";
                
            if (siteName.Contains("知乎") || siteName.Contains("zhihu"))
                return "https://www.zhihu.com/search?type=content&q={0}";
                
            if (siteName.Contains("微博") || siteName.Contains("weibo"))
                return "https://s.weibo.com/weibo?q={0}";
                
            if (siteName.Contains("淘宝") || siteName.Contains("taobao"))
                return "https://s.taobao.com/search?q={0}";
                
            if (siteName.Contains("京东") || siteName.Contains("jd"))
                return "https://search.jd.com/Search?keyword={0}";
            
            if (siteName.Contains("google") || siteName.Contains("谷歌"))
                return "https://www.google.com/search?q={0}";
                
            if (siteName.Contains("baidu") || siteName.Contains("百度"))
                return "https://www.baidu.com/s?wd={0}";
                
            if (siteName.Contains("bing") || siteName.Contains("必应"))
                return "https://www.bing.com/search?q={0}";

            return "";
        }

        public void NewTab(string url)
        {
            _syncContext.Invoke(new Action(async () => {
                string targetUrl = string.IsNullOrEmpty(url) ? (_settingsService?.Settings?.HomePage ?? "about:newtab") : url;
                await _tabManager.CreateTabAsync(targetUrl);
            }));
        }

        public void CloseCurrentTab()
        {
            _syncContext.Invoke(new Action(() => {
                if (_tabManager.ActiveTab != null)
                {
                    _tabManager.CloseTab(_tabManager.ActiveTab);
                }
            }));
        }

        public void GoBack()
        {
            _syncContext.Invoke(new Action(() => {
                _tabManager.ActiveTab?.GoBack();
            }));
        }

        public void GoForward()
        {
            _syncContext.Invoke(new Action(() => {
                _tabManager.ActiveTab?.GoForward();
            }));
        }

        public void Refresh()
        {
            _syncContext.Invoke(new Action(() => {
                _tabManager.ActiveTab?.Refresh();
            }));
        }

        public void Scroll(int deltaY)
        {
             _syncContext.Invoke(new Action(async () => {
                if (_tabManager.ActiveTab?.WebView != null) {
                    string script = $"window.__AiAssistant ? window.__AiAssistant.scroll({deltaY}) : window.scrollBy(0, {deltaY});";
                    await _tabManager.ActiveTab.WebView.ExecuteScriptAsync(script);
                }
            }));
        }

        public string GetCurrentUrl()
        {
            string url = "";
            _syncContext.Invoke(new Action(() => {
                url = _tabManager.ActiveTab?.Url ?? "";
            }));
            return url;
        }

        public async Task<string> GetPageContentAsync()
        {
            var tcs = new TaskCompletionSource<string>();
            
            _syncContext.Invoke(new Action(async () => {
                try 
                {
                    if (_tabManager.ActiveTab?.WebView != null)
                    {
                        // 优先使用 Content Script 提供的增强读取功能
                        string script = "window.__AiAssistant ? window.__AiAssistant.readPage() : null";
                        string result = await _tabManager.ActiveTab.WebView.ExecuteScriptAsync(script);

                        if (string.IsNullOrEmpty(result) || result == "null")
                        {
                            // 降级到旧逻辑
                            result = await _tabManager.ActiveTab.WebView.ExecuteScriptAsync(PageContentExtractor.ExtractionScript);
                        }

                        // WebView2 返回的结果是 JSON 字符串，需要反序列化一次（去除首尾引号和转义）
                        if (!string.IsNullOrEmpty(result) && result != "null")
                        {
                            try 
                            {
                                // 简单的去引号处理，或者使用 JSON 解析
                                if (result.StartsWith("\"") && result.EndsWith("\""))
                                {
                                    result = System.Text.RegularExpressions.Regex.Unescape(result.Substring(1, result.Length - 2));
                                }
                                tcs.SetResult(result);
                            }
                            catch
                            {
                                tcs.SetResult(result);
                            }
                        }
                        else
                        {
                            tcs.SetResult("");
                        }
                    }
                    else
                    {
                        tcs.SetResult("");
                    }
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }));

            return await tcs.Task;
        }

        public void ClickElement(string selector)
        {
            _syncContext.Invoke(new Action(async () => {
                if (_tabManager.ActiveTab?.WebView != null)
                {
                    // 使用 Content Script 引擎
                    string script = $"window.__AiAssistant ? window.__AiAssistant.click('{selector}') : document.querySelector('{selector}')?.click();";
                    await _tabManager.ActiveTab.WebView.ExecuteScriptAsync(script);
                }
            }));
        }

        public void TypeToElement(string selector, string text)
        {
            _syncContext.Invoke(new Action(async () => {
                if (_tabManager.ActiveTab?.WebView != null)
                {
                    // 使用 Content Script 引擎
                    string script = $"window.__AiAssistant ? window.__AiAssistant.type('{selector}', '{text}') : (function() {{ var el = document.querySelector('{selector}'); if(el) {{ el.value = '{text}'; el.dispatchEvent(new Event('input', {{ bubbles: true }})); }} }})()";
                    await _tabManager.ActiveTab.WebView.ExecuteScriptAsync(script);
                }
            }));
        }

        public void ClickElementById(string id)
        {
            _syncContext.Invoke(new Action(async () => {
                if (_tabManager.ActiveTab?.WebView != null)
                {
                    string script = $"window.__AiAssistant ? window.__AiAssistant.click('{id}') : document.querySelector('[data-ai-id=\"{id}\"]')?.click();";
                    await _tabManager.ActiveTab.WebView.ExecuteScriptAsync(script);
                }
            }));
        }

        public void TypeToElementById(string id, string text)
        {
            _syncContext.Invoke(new Action(async () => {
                if (_tabManager.ActiveTab?.WebView != null)
                {
                    string script = $"window.__AiAssistant ? window.__AiAssistant.type('{id}', '{text}') : (function() {{ var el = document.querySelector('[data-ai-id=\"{id}\"]'); if(el) {{ el.value = '{text}'; el.dispatchEvent(new Event('input', {{ bubbles: true }})); }} }})()";
                    await _tabManager.ActiveTab.WebView.ExecuteScriptAsync(script);
                }
            }));
        }
    }
}
