namespace MiniWorldBrowser.Models;

/// <summary>
/// 保存的密码模型
/// </summary>
public class SavedPassword
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Host { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public DateTime SavedTime { get; set; } = DateTime.Now;
}

/// <summary>
/// 一律不保存密码的网站
/// </summary>
public class NeverSavePassword
{
    public string Host { get; set; } = "";
    public DateTime AddedTime { get; set; } = DateTime.Now;
}
