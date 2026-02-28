using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace MiniWorldBrowser.Services
{
    /// <summary>
    /// 媒体资源下载服务，专门处理图片和视频的提取与下载
    /// </summary>
    public class MediaDownloadService
    {
        private readonly HttpClient _httpClient;

        public MediaDownloadService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        /// <summary>
        /// 从网页中提取所有图片并批量下载
        /// </summary>
        public async Task<int> ExtractAndDownloadImagesAsync(CoreWebView2 webView, string targetFolder)
        {
            if (!Directory.Exists(targetFolder))
                Directory.CreateDirectory(targetFolder);

            // 提取脚本：获取 img 标签和 CSS background-image
            const string script = @"(function() {
                const urls = new Set();
                
                // 1. 获取所有 img 标签
                document.querySelectorAll('img').forEach(img => {
                    if (img.src && img.src.startsWith('http')) urls.add(img.src);
                    if (img.dataset.src && img.dataset.src.startsWith('http')) urls.add(img.dataset.src);
                });

                // 2. 获取背景图片
                document.querySelectorAll('*').forEach(el => {
                    const style = window.getComputedStyle(el);
                    const bg = style.backgroundImage;
                    if (bg && bg !== 'none' && bg.includes('url(')) {
                        const match = bg.match(/url\(['""]?(.*?)['""]?\)/);
                        if (match && match[1] && match[1].startsWith('http')) {
                            urls.add(match[1]);
                        }
                    }
                });

                return JSON.stringify(Array.from(urls));
            })();";

            var result = await webView.ExecuteScriptAsync(script);
            var json = UnescapeJsString(result);
            
            var imageUrls = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            int count = 0;

            var tasks = imageUrls.Select(async url =>
            {
                try
                {
                    var uri = new Uri(url);
                    var fileName = Path.GetFileName(uri.LocalPath);
                    if (string.IsNullOrEmpty(fileName) || !Path.HasExtension(fileName))
                    {
                        fileName = Guid.NewGuid().ToString("N")[..8] + ".jpg";
                    }

                    var filePath = Path.Combine(targetFolder, fileName);
                    
                    // 简单的并发下载
                    var data = await _httpClient.GetByteArrayAsync(url);
                    await File.WriteAllBytesAsync(filePath, data);
                    count++;
                }
                catch
                {
                    // 忽略单个下载失败
                }
            });

            await Task.WhenAll(tasks);
            return count;
        }

        /// <summary>
        /// 反转义 WebView2 返回的字符串
        /// </summary>
        private static string UnescapeJsString(string str)
        {
            if (str.StartsWith("\"") && str.EndsWith("\""))
            {
                str = str[1..^1];
                str = System.Text.RegularExpressions.Regex.Unescape(str);
            }
            return str;
        }
    }
}
