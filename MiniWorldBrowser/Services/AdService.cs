using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MiniWorldBrowser.Models;
using MiniWorldBrowser.Services.Interfaces;

namespace MiniWorldBrowser.Services;

/// <summary>
/// 广告服务实现
/// </summary>
public class AdService : IAdService, IDisposable
{
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private bool _disposed;
    private const string ApiBaseUrl = "https://api-web.kunqiongai.com";

    public AdService()
    {
    }

    public async Task<List<AdItem>> GetAdsAsync(string softNumber, string advPosition)
    {
        try
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("soft_number", softNumber),
                new KeyValuePair<string, string>("adv_position", advPosition)
            });

            using var response = await HttpClient.PostAsync($"{ApiBaseUrl}/soft_desktop/get_adv", content).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return new List<AdItem>();

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<AdItem>>>().ConfigureAwait(false);

            if (result?.Code == 1 && result.Data != null)
            {
                return result.Data;
            }
            return new List<AdItem>();
        }
        catch
        {
            return new List<AdItem>();
        }
    }

    public async Task<string?> GetCustomUrlAsync()
    {
        try
        {
            using var response = await HttpClient.PostAsync($"{ApiBaseUrl}/soft_desktop/get_custom_url", null).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<CustomUrlData>>().ConfigureAwait(false);
            return result?.Code == 1 ? result.Data?.Url : null;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    private class CustomUrlData
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                   System.Net.DecompressionMethods.Deflate |
                                   System.Net.DecompressionMethods.Brotli,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 10
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(20),
            DefaultRequestVersion = new Version(2, 0)
        };

        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("deflate");
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("br");

        return client;
    }

    private class ApiResponse<T>
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }
        [JsonPropertyName("msg")]
        public string Msg { get; set; } = string.Empty;
        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }
}
