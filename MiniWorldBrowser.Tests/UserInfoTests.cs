using MiniWorldBrowser.Models;
using Xunit;

namespace MiniWorldBrowser.Tests;

public class UserInfoTests
{
    [Fact]
    public void DisplayName_EmptyNickname_ReturnsDefault()
    {
        var user = new UserInfo { Nickname = "" };
        Assert.Equal("用户", user.DisplayName);
    }

    [Fact]
    public void DisplayInitial_EmptyNickname_ReturnsQuestionMark()
    {
        var user = new UserInfo { Nickname = "" };
        Assert.Equal("?", user.DisplayInitial);
    }

    [Fact]
    public void DisplayName_TrimNickname_ReturnsTrimmedValue()
    {
        var user = new UserInfo { Nickname = "  alice  " };
        Assert.Equal("alice", user.DisplayName);
    }

    [Fact]
    public void DisplayInitial_TrimNickname_ReturnsUpperFirstChar()
    {
        var user = new UserInfo { Nickname = "  alice  " };
        Assert.Equal("A", user.DisplayInitial);
    }
}

