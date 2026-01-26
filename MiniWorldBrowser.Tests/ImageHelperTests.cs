using System;
using MiniWorldBrowser.Helpers;
using Xunit;

namespace MiniWorldBrowser.Tests;

public class ImageHelperTests
{
    [Fact]
    public void CreateImageFromBytes_DisposeDoesNotAffectNextImage()
    {
        var pngBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO7+3kcAAAAASUVORK5CYII=");

        using (var img1 = ImageHelper.CreateImageFromBytes(pngBytes))
        {
            Assert.NotNull(img1);
            Assert.Equal(1, img1!.Width);
            Assert.Equal(1, img1.Height);
        }

        using (var img2 = ImageHelper.CreateImageFromBytes(pngBytes))
        {
            Assert.NotNull(img2);
            Assert.Equal(1, img2!.Width);
            _ = img2.RawFormat;
        }
    }
}
