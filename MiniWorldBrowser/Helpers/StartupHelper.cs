using MiniWorldBrowser.Browser;
using MiniWorldBrowser.Services.Interfaces;

namespace MiniWorldBrowser.Helpers;

public static class StartupHelper
{
    public static async Task HandleStartupAsync(
        ISettingsService settingsService,
        BrowserTabManager tabManager,
        bool isIncognito,
        Func<string, Task> createTabAction)
    {
        if (settingsService?.Settings == null || tabManager == null)
            return;

        if (isIncognito)
        {
            // 隐身模式直接打开主页
            var homePage = settingsService.Settings.HomePage ?? "about:newtab";
            await createTabAction(homePage);
            return;
        }

        // 根据 StartupBehavior 决定启动时打开什么页面
        // 0 = 打开新标签页, 1 = 继续上次浏览, 2 = 打开特定网页
        var startupBehavior = settingsService.Settings.StartupBehavior;
        
        switch (startupBehavior)
        {
            case 0: // 打开新标签页
                await createTabAction("about:newtab");
                break;
                
            case 1: // 继续上次浏览
                var lastUrls = settingsService.Settings.LastSessionUrls;
                if (lastUrls != null && lastUrls.Count > 0)
                {
                    foreach (var url in lastUrls)
                    {
                        await createTabAction(url);
                    }
                }
                else
                {
                    await createTabAction("about:newtab");
                }
                break;
                
            case 2: // 打开特定网页
                var startupPages = settingsService.Settings.StartupPages;
                if (startupPages != null && startupPages.Count > 0)
                {
                    foreach (var url in startupPages)
                    {
                        await createTabAction(url);
                    }
                }
                else
                {
                    // 如果没有设置特定网页，则打开主页
                    var startupUrl = settingsService.Settings.HomePage ?? "about:newtab";
                    if (string.IsNullOrEmpty(startupUrl) || startupUrl == "about:newtab")
                    {
                        startupUrl = "about:newtab";
                    }
                    await createTabAction(startupUrl);
                }
                break;
                
            default:
                await createTabAction("about:newtab");
                break;
        }
    }
}
