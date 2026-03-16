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
    private static readonly HashSet<string> SupportedLanguageCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "zh-CN", "zh_TW", "en", "hi", "es", "fr", "ar", "bn", "pt_BR", "pt", "ru", "id",
        "ur", "de", "ja", "tr", "vi", "ko", "th", "it", "fa", "sw", "tl", "ta", "ms", "ha", "jv"
    };

    private static readonly Dictionary<string, string> CultureToLanguageCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ar"] = "ar",
        ["bn"] = "bn",
        ["de"] = "de",
        ["en"] = "en",
        ["es"] = "es",
        ["fa"] = "fa",
        ["fr"] = "fr",
        ["ha"] = "ha",
        ["hi"] = "hi",
        ["id"] = "id",
        ["it"] = "it",
        ["ja"] = "ja",
        ["jv"] = "jv",
        ["ko"] = "ko",
        ["ms"] = "ms",
        ["pt"] = "pt",
        ["ru"] = "ru",
        ["sw"] = "sw",
        ["ta"] = "ta",
        ["th"] = "th",
        ["tl"] = "tl",
        ["tr"] = "tr",
        ["ur"] = "ur",
        ["vi"] = "vi",
    };
    public static void Initialize(string? languageCode = null)
    {
        lock (_lock)
        {
            var lang = ResolveLanguageCode(languageCode);
            if (_currentLang == lang && _root.ValueKind != JsonValueKind.Undefined) return;
            _currentLang = lang;

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _root = LoadLanguageRoot(baseDir, lang);

            // Use English as stable fallback to avoid locale-specific fallback overriding selected language.
            _fallbackRoot = LoadLanguageRoot(baseDir, "en");
        }
    }

    private static JsonElement LoadLanguageRoot(string baseDir, string languageCode)
    {
        foreach (var path in EnumerateLanguageCandidates(baseDir, languageCode))
        {
            if (TryLoadJsonRoot(path, out var root))
                return root;
        }

        using var empty = JsonDocument.Parse("{}");
        return empty.RootElement.Clone();
    }

    private static IEnumerable<string> EnumerateLanguageCandidates(string baseDir, string languageCode)
    {
        if (string.Equals(languageCode, "zh-CN", StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(baseDir, "Resources", "i18n", "zh_CN.json");
            yield return Path.Combine(baseDir, "Resources", "i18n", "zh-CN.json");
        }
        else if (string.Equals(languageCode, "zh_CN", StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(baseDir, "Resources", "i18n", "zh_CN.json");
            yield return Path.Combine(baseDir, "Resources", "i18n", "zh-CN.json");
        }
        else
        {
            yield return Path.Combine(baseDir, "Resources", "i18n", $"{languageCode}.json");
        }

        if (!string.Equals(languageCode, "en", StringComparison.OrdinalIgnoreCase))
            yield return Path.Combine(baseDir, "Resources", "i18n", "en.json");
    }

    private static bool TryLoadJsonRoot(string path, out JsonElement root)
    {
        root = default;
        if (!File.Exists(path))
            return false;

        try
        {
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            root = doc.RootElement.Clone();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode) || string.Equals(languageCode, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return MapFromCurrentUiCulture();
        }

        var normalized = languageCode.Trim();
        if (string.Equals(normalized, "zh_CN", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "zh-CN", StringComparison.OrdinalIgnoreCase))
            return "zh-CN";
        if (string.Equals(normalized, "zh_TW", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "zh-TW", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "zh_HK", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "zh-HK", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "zh_MO", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "zh-MO", StringComparison.OrdinalIgnoreCase))
            return "zh_TW";
        if (string.Equals(normalized, "pt_BR", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "pt-BR", StringComparison.OrdinalIgnoreCase))
            return "pt_BR";

        return SupportedLanguageCodes.Contains(normalized) ? normalized : "en";
    }

    private static string MapFromCurrentUiCulture()
    {
        var ui = CultureInfo.CurrentUICulture;
        var uiName = ui.Name.Replace('-', '_');
        if (uiName.StartsWith("zh_TW", StringComparison.OrdinalIgnoreCase) ||
            uiName.StartsWith("zh_HK", StringComparison.OrdinalIgnoreCase) ||
            uiName.StartsWith("zh_MO", StringComparison.OrdinalIgnoreCase))
            return "zh_TW";
        if (uiName.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            return "zh-CN";
        if (uiName.StartsWith("pt_BR", StringComparison.OrdinalIgnoreCase))
            return "pt_BR";

        var twoLetter = ui.TwoLetterISOLanguageName;
        return CultureToLanguageCode.TryGetValue(twoLetter, out var code) ? code : "en";
    }

    public static string T(string key, IDictionary<string, string>? args = null)
    {
        if (_root.ValueKind == JsonValueKind.Undefined) Initialize();
        var primary = Lookup(key);
        var fallback = LookupFallback(key);
        var value = PickPreferredValue(primary, fallback) ?? key;
        if (args == null || args.Count == 0) return value;
        foreach (var kv in args)
        {
            var ph = "{{" + kv.Key + "}}";
            value = value.Replace(ph, kv.Value ?? string.Empty, StringComparison.Ordinal);
        }
        return value;
    }

    private static string? PickPreferredValue(string? primary, string? fallback)
    {
        var primaryUsable = !string.IsNullOrWhiteSpace(primary) && !LooksCorrupted(primary);
        if (primaryUsable) return primary;

        var fallbackUsable = !string.IsNullOrWhiteSpace(fallback) && !LooksCorrupted(fallback);
        if (fallbackUsable) return fallback;

        return primary ?? fallback;
    }

    private static bool LooksCorrupted(string value)
    {
        if (value.Contains("???", StringComparison.Ordinal)) return true;

        int questionCount = 0;
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '?') questionCount++;
        }

        return questionCount >= 3 && questionCount * 2 >= value.Length;
    }

    private static string? Lookup(string key)
    {
        var parts = key.Split('.');
        JsonElement cursor = _root;
        for (int i = 0; i < parts.Length; i++)
        {
            if (cursor.ValueKind != JsonValueKind.Object) return null;

            // Support flat keys like "input_mode.enter" stored in the same object.
            var rest = string.Join('.', parts, i, parts.Length - i);
            if (cursor.TryGetProperty(rest, out var flat))
            {
                return flat.ValueKind == JsonValueKind.String ? flat.GetString() : null;
            }

            var part = parts[i];
            if (cursor.TryGetProperty(part, out var next))
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
        for (int i = 0; i < parts.Length; i++)
        {
            if (cursor.ValueKind != JsonValueKind.Object) return null;

            // Keep fallback lookup behavior consistent with main lookup.
            var rest = string.Join('.', parts, i, parts.Length - i);
            if (cursor.TryGetProperty(rest, out var flat))
            {
                return flat.ValueKind == JsonValueKind.String ? flat.GetString() : null;
            }

            var part = parts[i];
            if (cursor.TryGetProperty(part, out var next))
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
