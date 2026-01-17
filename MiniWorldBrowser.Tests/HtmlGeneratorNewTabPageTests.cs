using MiniWorldBrowser.Helpers;
using MiniWorldBrowser.Models;
using Xunit;

namespace MiniWorldBrowser.Tests;

public class HtmlGeneratorNewTabPageTests
{
    [Fact]
    public void GenerateNewTabPage_ContainsCoreLayoutAndStyles()
    {
        var html = HtmlGenerator.GenerateNewTabPage(new BrowserSettings());

        Assert.Contains("<title>新标签页</title>", html);
        Assert.Contains("watermark-container", html);
        Assert.Contains("shortcut-icon", html);
        Assert.Contains("backdrop-filter", html);
        Assert.Contains("linear-gradient(135deg", html);
    }
}

