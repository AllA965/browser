using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MiniWorldBrowser.Models;
using MiniWorldBrowser.Services.Interfaces;

namespace MiniWorldBrowser.Services;

/// <summary>
/// 登录服务实现
/// </summary>
public class LoginService : ILoginService, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly HttpClient _httpClient;
    private bool _disposed;
    
    // Reference: 登录接口文档.md - 4. 登录示例代码
    private static readonly byte[] SecretKey = Encoding.UTF8.GetBytes("7530bfb1ad6c41627b0f0620078fa5ed");
    private const string ApiBaseUrl = "https://api-web.kunqiongai.com";

    public bool IsLoggedIn => !string.IsNullOrEmpty(_settingsService.Settings.AuthToken);
    public UserInfo? CurrentUser => _settingsService.Settings.UserInfo;

    public event Action? LoginStateChanged;

    public LoginService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _httpClient = new HttpClient();
        // 设置默认超时，防止挂死
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<bool> CheckLoginAsync()
    {
        if (!IsLoggedIn) return false;

        try
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("token", _settingsService.Settings.AuthToken)
            });

            var response = await _httpClient.PostAsync($"{ApiBaseUrl}/user/check_login", content);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

            if (result?.Code == 1)
            {
                return true;
            }
            
            // 如果检查失败，清除 Token
            _settingsService.Settings.AuthToken = string.Empty;
            _settingsService.Settings.UserInfo = null;
            _settingsService.Save();
            LoginStateChanged?.Invoke();
            return false;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient.Dispose();
            _disposed = true;
        }
    }

    public async Task<string> GetWebLoginUrlAsync(string encodedNonce)
    {
        var response = await _httpClient.PostAsync($"{ApiBaseUrl}/soft_desktop/get_web_login_url", null);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginUrlData>>();

        if (result?.Code == 1 && result.Data != null)
        {
            return $"{result.Data.LoginUrl}?client_type=desktop&client_nonce={encodedNonce}";
        }

        throw new Exception($"获取登录地址失败：{result?.Msg}");
    }

    public async Task<(string loginUrl, string encodedNonce)> PrepareLoginAsync()
    {
        var signedNonce = GenerateSignedNonce();
        var encodedNonce = EncodeSignedNonce(signedNonce);
        var loginUrl = await GetWebLoginUrlAsync(encodedNonce);
        return (loginUrl, encodedNonce);
    }

    public async Task<string?> PollTokenAsync(string encodedNonce, CancellationToken cancellationToken)
    {
        var pollUrl = $"{ApiBaseUrl}/user/desktop_get_token";
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_type", "desktop"),
                    new KeyValuePair<string, string>("client_nonce", encodedNonce)
                });

                var response = await _httpClient.PostAsync(pollUrl, content, cancellationToken);
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<TokenData>>();

                if (result?.Code == 1 && result.Data != null)
                {
                    _settingsService.Settings.AuthToken = result.Data.Token;
                    _settingsService.Save();
                    
                    // 获取用户信息
                    await GetUserInfoAsync();
                    
                    LoginStateChanged?.Invoke();
                    return result.Data.Token;
                }
                
                await Task.Delay(2000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(2000, cancellationToken);
            }
        }

        return null;
    }

    public async Task<bool> LogoutAsync()
    {
        if (!IsLoggedIn) return true;

        var token = _settingsService.Settings.AuthToken;
        
        // 无论服务器注销是否成功，本地都先清除状态，保证用户感知的响应速度
        _settingsService.Settings.AuthToken = string.Empty;
        _settingsService.Settings.UserInfo = null;
        _settingsService.Save();
        LoginStateChanged?.Invoke();

        try
        {
            // 同时在 Header 和 Body 中发送 token，以兼容不同的后端处理逻辑
            var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/logout");
            request.Headers.Add("token", token);
            
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("token", token)
            });
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

            return result?.Code == 1;
        }
        catch
        {
            return false;
        }
    }

    public async Task<UserInfo?> GetUserInfoAsync()
    {
        if (!IsLoggedIn) return null;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/soft_desktop/get_user_info");
            request.Headers.Add("token", _settingsService.Settings.AuthToken);

            var response = await _httpClient.SendAsync(request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserInfoData>>();

            if (result?.Code == 1 && result.Data?.UserInfo != null)
            {
                _settingsService.Settings.UserInfo = result.Data.UserInfo;
                _settingsService.Save();
                LoginStateChanged?.Invoke();
                return result.Data.UserInfo;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private SignedNonce GenerateSignedNonce()
    {
        var nonce = Guid.NewGuid().ToString("n");
        var timestamp = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
        var message = $"{nonce}|{timestamp}";
        
        using var hmac = new HMACSHA256(SecretKey);
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        var signature = Convert.ToBase64String(signatureBytes);

        return new SignedNonce
        {
            Nonce = nonce,
            Timestamp = timestamp,
            Signature = signature
        };
    }

    private string EncodeSignedNonce(SignedNonce signedNonce)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // The python example uses lowercase keys in JSON
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        
        // Actually, let's match the Python dict keys exactly
        var dict = new Dictionary<string, object>
        {
            { "nonce", signedNonce.Nonce },
            { "timestamp", signedNonce.Timestamp },
            { "signature", signedNonce.Signature }
        };

        var jsonStr = JsonSerializer.Serialize(dict);
        var bytes = Encoding.UTF8.GetBytes(jsonStr);
        var base64 = Convert.ToBase64String(bytes);
        
        return base64.Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    #region Helper Models

    private class SignedNonce
    {
        public string Nonce { get; set; } = string.Empty;
        public long Timestamp { get; set; }
        public string Signature { get; set; } = string.Empty;
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

    private class LoginUrlData
    {
        [JsonPropertyName("login_url")]
        public string LoginUrl { get; set; } = string.Empty;
    }

    private class TokenData
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;
    }

    private class UserInfoData
    {
        [JsonPropertyName("user_info")]
        public UserInfo? UserInfo { get; set; }
    }

    #endregion
}
