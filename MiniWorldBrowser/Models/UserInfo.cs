using System.Text.Json.Serialization;

namespace MiniWorldBrowser.Models;

/// <summary>
/// 用户基本信息模型
/// </summary>
public class UserInfo
{
    /// <summary>
    /// 头像地址
    /// </summary>
    [JsonPropertyName("avatar")]
    public string Avatar { get; set; } = string.Empty;

    /// <summary>
    /// 昵称
    /// </summary>
    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = string.Empty;

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Nickname) ? "用户" : Nickname.Trim();

    [JsonIgnore]
    public string DisplayInitial
    {
        get
        {
            var name = Nickname?.Trim();
            if (string.IsNullOrEmpty(name)) return "?";
            return name[0].ToString().ToUpperInvariant();
        }
    }
}
