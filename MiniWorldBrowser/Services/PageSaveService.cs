using Microsoft.Web.WebView2.Core;

namespace MiniWorldBrowser.Services;

/// <summary>
/// 页面保存服务
/// </summary>
public class PageSaveService
{
    /// <summary>
    /// 保存为纯 HTML
    /// </summary>
    public async Task SaveAsHtmlOnlyAsync(CoreWebView2 webView, string filePath)
    {
        var html = await webView.ExecuteScriptAsync("document.documentElement.outerHTML");
        html = UnescapeJsString(html);
        await File.WriteAllTextAsync(filePath, html, System.Text.Encoding.UTF8);
    }
    
    /// <summary>
    /// 保存为完整 HTML（包含资源）
    /// </summary>
    public async Task SaveAsHtmlCompleteAsync(CoreWebView2 webView, string filePath)
    {
        var html = await webView.ExecuteScriptAsync("document.documentElement.outerHTML");
        html = UnescapeJsString(html);
        
        var resourceFolder = Path.Combine(
            Path.GetDirectoryName(filePath) ?? "",
            Path.GetFileNameWithoutExtension(filePath) + "_files");
        
        if (!Directory.Exists(resourceFolder))
            Directory.CreateDirectory(resourceFolder);
        
        // 获取图片 URL
        var imgScript = @"(function(){
            var imgs=document.querySelectorAll('img[src]');
            var srcs=[];
            for(var i=0;i<imgs.length;i++){
                if(imgs[i].src&&imgs[i].src.startsWith('http')){
                    srcs.push(imgs[i].src);
                }
            }
            return JSON.stringify(srcs);
        })();";
        
        var imgResult = await webView.ExecuteScriptAsync(imgScript);
        imgResult = UnescapeJsString(imgResult);
        
        try
        {
            var imgUrls = System.Text.Json.JsonSerializer.Deserialize<List<string>>(imgResult) ?? new();
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var urlToLocalPath = new Dictionary<string, string>();
            
            foreach (var imgUrl in imgUrls.Take(50))
            {
                try
                {
                    var uri = new Uri(imgUrl);
                    var fileName = Path.GetFileName(uri.LocalPath);
                    if (string.IsNullOrEmpty(fileName) || fileName.Length > 100)
                        fileName = Guid.NewGuid().ToString("N")[..8] + ".jpg";
                    
                    var localPath = Path.Combine(resourceFolder, fileName);
                    var relativePath = Path.GetFileName(resourceFolder) + "/" + fileName;
                    
                    var imgData = await httpClient.GetByteArrayAsync(imgUrl);
                    await File.WriteAllBytesAsync(localPath, imgData);
                    urlToLocalPath[imgUrl] = relativePath;
                }
                catch
                {
                    // 忽略单个图片下载失败
                }
            }
            
            foreach (var kvp in urlToLocalPath)
                html = html.Replace(kvp.Key, kvp.Value);
        }
        catch
        {
            // 忽略资源下载错误
        }
        
        await File.WriteAllTextAsync(filePath, html, System.Text.Encoding.UTF8);
    }
    
    /// <summary>
    /// 保存为 MHTML
    /// </summary>
    public async Task SaveAsMhtmlAsync(CoreWebView2 webView, string filePath)
    {
        try
        {
            var result = await webView.CallDevToolsProtocolMethodAsync(
                "Page.captureSnapshot", "{\"format\":\"mhtml\"}");
            
            var json = System.Text.Json.JsonDocument.Parse(result);
            if (json.RootElement.TryGetProperty("data", out var dataElement))
            {
                var mhtmlContent = dataElement.GetString();
                if (!string.IsNullOrEmpty(mhtmlContent))
                {
                    await File.WriteAllTextAsync(filePath, mhtmlContent, System.Text.Encoding.UTF8);
                    return;
                }
            }
        }
        catch
        {
            // 忽略错误
        }
        
        throw new Exception("MHTML 格式保存失败");
    }
    
    /// <summary>
    /// 保存为 PDF
    /// </summary>
    public async Task SaveAsPdfAsync(CoreWebView2 webView, string filePath)
    {
        var printSettings = webView.Environment.CreatePrintSettings();
        printSettings.ShouldPrintBackgrounds = true;
        printSettings.ShouldPrintHeaderAndFooter = false;
        
        var success = await webView.PrintToPdfAsync(filePath, printSettings);
        if (!success)
            throw new Exception("PDF 保存失败");
    }
    
    /// <summary>
    /// 反转义 JavaScript 字符串
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
