using MiniWorldBrowser.Models;

namespace MiniWorldBrowser.Services.Interfaces;

/// <summary>
/// 广告服务接口
/// </summary>
public interface IAdService
{
    /// <summary>
    /// 获取广告列表
    /// </summary>
    /// <param name="softNumber">软件编号</param>
    /// <param name="advPosition">广告位置</param>
    /// <returns>广告列表</returns>
    Task<List<AdItem>> GetAdsAsync(string softNumber, string advPosition);

    /// <summary>
    /// 获取软件定制链接
    /// </summary>
    /// <returns>定制链接地址</returns>
    Task<string?> GetCustomUrlAsync();
}
