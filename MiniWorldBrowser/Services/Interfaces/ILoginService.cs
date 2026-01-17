using MiniWorldBrowser.Models;

namespace MiniWorldBrowser.Services.Interfaces;

/// <summary>
/// 登录服务接口
/// </summary>
public interface ILoginService
{
    /// <summary>
    /// 当前是否已登录
    /// </summary>
    bool IsLoggedIn { get; }

    /// <summary>
    /// 当前用户信息
    /// </summary>
    UserInfo? CurrentUser { get; }

    /// <summary>
    /// 异步检查登录状态
    /// </summary>
    Task<bool> CheckLoginAsync();

    /// <summary>
    /// 获取网页登录 URL
    /// </summary>
    Task<string> GetWebLoginUrlAsync(string encodedNonce);

    /// <summary>
    /// 开始登录流程（生成 Nonce 并返回登录 URL）
    /// </summary>
    Task<(string loginUrl, string encodedNonce)> PrepareLoginAsync();

    /// <summary>
    /// 轮询获取 Token
    /// </summary>
    Task<string?> PollTokenAsync(string encodedNonce, CancellationToken cancellationToken);

    /// <summary>
    /// 退出登录
    /// </summary>
    Task<bool> LogoutAsync();

    /// <summary>
    /// 刷新用户信息
    /// </summary>
    Task<UserInfo?> GetUserInfoAsync();

    /// <summary>
    /// 登录状态变更事件
    /// </summary>
    event Action? LoginStateChanged;
}
