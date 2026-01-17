using System.Text.Json.Serialization;

namespace MiniWorldBrowser.Models;

/// <summary>
/// 广告信息模型
/// </summary>
public class AdItem
{
    /// <summary>
    /// 软件编号
    /// </summary>
    [JsonPropertyName("soft_number")]
    public int SoftNumber { get; set; }

    /// <summary>
    /// 广告位置
    /// </summary>
    [JsonPropertyName("adv_position")]
    public string AdvPosition { get; set; } = string.Empty;

    /// <summary>
    /// 广告资源地址
    /// </summary>
    [JsonPropertyName("adv_url")]
    public string AdvUrl { get; set; } = string.Empty;

    /// <summary>
    /// 广告对应跳转地址
    /// </summary>
    [JsonPropertyName("target_url")]
    public string TargetUrl { get; set; } = string.Empty;

    /// <summary>
    /// 宽
    /// </summary>
    [JsonPropertyName("width")]
    public int Width { get; set; }

    /// <summary>
    /// 高
    /// </summary>
    [JsonPropertyName("height")]
    public int Height { get; set; }
}
