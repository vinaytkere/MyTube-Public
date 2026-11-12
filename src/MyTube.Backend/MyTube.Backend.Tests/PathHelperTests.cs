using Xunit;

namespace MyTube.Backend.Tests;

public class PathHelperTests
{
    [Fact]
    public void TestPathGetFileNameOnLongPath()
    {
        var longPath = "/media/Downloads/test_video.mp4";
        var fn = System.IO.Path.GetFileName(longPath);
        Assert.Equal("test_video.mp4", fn);
    }
}
