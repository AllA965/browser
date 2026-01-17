using System.Collections.Concurrent;
using System.Drawing.Drawing2D;
using System.Net.Http;

namespace MiniWorldBrowser.Helpers;

/// <summary>
/// 图片获取和缓存辅助类
/// </summary>
public static class ImageHelper
{
    private static readonly ConcurrentDictionary<string, byte[]> _cache = new();
    private static readonly ConcurrentDictionary<string, Task<Image?>> _loadingTasks = new();
    private static readonly HttpClient _httpClient = CreateHttpClient();

    /// <summary>
    /// 异步获取网络图片
    /// </summary>
    public static Task<Image?> GetImageAsync(string? url)
    {
        if (string.IsNullOrEmpty(url)) return Task.FromResult<Image?>(null);

        // 1. 检查字节缓存
        if (_cache.TryGetValue(url, out var cachedBytes))
        {
            return Task.FromResult(CreateImageFromBytes(cachedBytes));
        }

        // 2. 检查是否有正在进行的下载任务（防止重复请求）
        return _loadingTasks.GetOrAdd(url, async (key) =>
        {
            try
            {
                var bytes = await _httpClient.GetByteArrayAsync(key);
                if (bytes != null && bytes.Length > 0)
                {
                    _cache[key] = bytes;
                    return CreateImageFromBytes(bytes);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ImageHelper] Load failed: {key}, {ex.Message}");
            }
            finally
            {
                _loadingTasks.TryRemove(key, out _);
            }
            return null;
        });
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

    public static Image? CreateImageFromBytes(byte[] bytes)
    {
        if (bytes.Length == 0) return null;

        try
        {
            using var ms = new MemoryStream(bytes);
            using var img = Image.FromStream(ms);
            return new Bitmap(img);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 将图片裁剪为圆形
    /// </summary>
    public static Image GetCircularImage(Image image, int size)
    {
        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        using var path = new GraphicsPath();
        path.AddEllipse(0, 0, size, size);
        g.SetClip(path);
        
        g.DrawImage(image, new Rectangle(0, 0, size, size));
        
        return bitmap;
    }
}
