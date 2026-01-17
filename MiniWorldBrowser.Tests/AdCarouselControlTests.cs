using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MiniWorldBrowser.Controls;
using MiniWorldBrowser.Models;
using Xunit;

namespace MiniWorldBrowser.Tests;

public class AdCarouselControlTests
{
    [Fact]
    public void MergeAdsByUrl_RemovesDuplicatesAndSkipsEmpty()
    {
        var target = new List<AdItem>
        {
            new() { AdvUrl = "https://example.com/a.png" },
            new() { AdvUrl = "https://example.com/b.png" }
        };

        var incoming = new List<AdItem>
        {
            new() { AdvUrl = "https://example.com/b.png" },
            new() { AdvUrl = "https://example.com/c.png" },
            new() { AdvUrl = "" },
            new() { AdvUrl = null! }
        };

        var added = AdCarouselControl.MergeAdsByUrl(target, incoming);

        Assert.Equal(1, added);
        Assert.Equal(3, target.Count);
        Assert.Contains(target, a => a.AdvUrl == "https://example.com/c.png");
    }

    [Fact]
    public void InvokeAsync_DefersUntilHandleCreated()
    {
        Exception? captured = null;
        var wasCompletedImmediately = false;
        var executed = false;

        var thread = new Thread(() =>
        {
            try
            {
                using var form = new Form();
                var control = new AdCarouselControl();

                var method = typeof(AdCarouselControl).GetMethod(
                    "InvokeAsync",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.NotNull(method);

                var task = (Task)method!.Invoke(control, new object[] { (Action)(() => executed = true) })!;
                wasCompletedImmediately = task.IsCompleted;

                form.Controls.Add(control);
                form.Show();
                Application.DoEvents();
                control.CreateControl();
                Application.DoEvents();

                Assert.True(task.Wait(TimeSpan.FromSeconds(5)));
                Assert.True(task.IsCompletedSuccessfully);
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured != null) throw captured;
        Assert.False(wasCompletedImmediately);
        Assert.True(executed);
    }
}
