using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MiniWorldBrowser.Helpers;

public static class Localization
{
    private static JsonElement _root;
    private static JsonElement _fallbackRoot;
    private static readonly object _lock = new();
    private static string _currentLang = "";
    private const string ProjectMarker = "MiniWorldBrowser";

    public static void Initialize(string? languageCode = null)
    {
        lock (_lock)
        {
            var lang = languageCode;
            if (string.Equals(lang, "auto", StringComparison.OrdinalIgnoreCase)) lang = null;
            if (string.IsNullOrWhiteSpace(lang))
            {
                var ui = CultureInfo.CurrentUICulture.Name;
                lang = ui switch
                {
                    string s when s.StartsWith("zh", StringComparison.OrdinalIgnoreCase) => "zh-CN",
                    _ => "en"
                };
            }
            if (_currentLang == lang && _root.ValueKind != JsonValueKind.Undefined) return;
            _currentLang = lang;

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(baseDir, "Resources", "i18n", $"{lang}.json");
            if (!File.Exists(path))
            {
                path = Path.Combine(baseDir, "Resources", "i18n", "en.json");
            }
            var json = File.Exists(path) ? File.ReadAllText(path) : "{}";
            var doc = JsonDocument.Parse(json);
            _root = doc.RootElement.Clone();

            // prepare zh-CN fallback for missing keys (including raw entries)
            var fallbackPath = Path.Combine(baseDir, "Resources", "i18n", "zh-CN.json");
            var fallbackJson = File.Exists(fallbackPath) ? File.ReadAllText(fallbackPath) : "{}";
            var fallbackDoc = JsonDocument.Parse(fallbackJson);
            _fallbackRoot = fallbackDoc.RootElement.Clone();
        }
    }

    public static string T(string key, IDictionary<string, string>? args = null)
    {
        if (_root.ValueKind == JsonValueKind.Undefined) Initialize();
        var value = Lookup(key) ?? LookupFallback(key) ?? key;
        if (args == null || args.Count == 0) return value;
        foreach (var kv in args)
        {
            var ph = "{{" + kv.Key + "}}";
            value = value.Replace(ph, kv.Value ?? string.Empty, StringComparison.Ordinal);
        }
        return value;
    }

    private static string? Lookup(string key)
    {
        var parts = key.Split('.');
        JsonElement cursor = _root;
        foreach (var part in parts)
        {
            if (cursor.ValueKind == JsonValueKind.Object && cursor.TryGetProperty(part, out var next))
            {
                cursor = next;
            }
            else
            {
                return null;
            }
        }
        return cursor.ValueKind == JsonValueKind.String ? cursor.GetString() : null;
    }

    private static string? LookupFallback(string key)
    {
        var parts = key.Split('.');
        JsonElement cursor = _fallbackRoot;
        foreach (var part in parts)
        {
            if (cursor.ValueKind == JsonValueKind.Object && cursor.TryGetProperty(part, out var next))
            {
                cursor = next;
            }
            else
            {
                return null;
            }
        }
        return cursor.ValueKind == JsonValueKind.String ? cursor.GetString() : null;
    }

    public static string Raw([System.Runtime.CompilerServices.CallerFilePath] string file = "", [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    {
        string rel = Path.GetFileName(file);
        try
        {
            var idx = file.IndexOf(ProjectMarker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var start = idx + ProjectMarker.Length;
                while (start < file.Length && (file[start] == '\\' || file[start] == '/')) start++;
                rel = file.Substring(start);
            }
        }
        catch { }
        rel = rel.Replace('\\', '/');
        var keyPath = rel.Replace("/", ".");
        var key = $"raw.{keyPath}.L{line}";
        return T(key);
    }
}
