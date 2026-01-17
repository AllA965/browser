using MiniWorldBrowser.Models;
using System.Reflection;
using Xunit;

namespace MiniWorldBrowser.Tests;

public class FontSettingsScriptTests
{
    [Fact]
    public void GenerateFontSettingsScript_DoesNotForceFontFamilyOnAllElements()
    {
        var settings = new BrowserSettings
        {
            StandardFont = "宋体",
            SerifFont = "宋体",
            SansSerifFont = "宋体",
            FixedWidthFont = "新宋体",
            StandardFontSize = 16,
            MinimumFontSize = 12
        };

        var type = typeof(MiniWorldBrowser.Browser.BrowserTabManager);
        var method = type.GetMethod("GenerateFontSettingsScript", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var script = (string?)method!.Invoke(null, new object[] { settings });
        Assert.False(string.IsNullOrWhiteSpace(script));

        Assert.Contains("miniworld-font-settings", script);
        Assert.Contains("html, body", script);
        Assert.DoesNotContain("* {\n", script);
        Assert.DoesNotContain("* {\r\n", script);
        Assert.Contains("code, pre, kbd, samp, tt", script);
    }
}

