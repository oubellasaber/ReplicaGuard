using ReplicaGuard.Infrastructure.Hosters.Capabilities;
using ReplicaGuard.Infrastructure.Hosters.Pixeldrain.CopyFile;

namespace ReplicaGuard.Infrastructure.IntegrationTests;

public sealed class PixeldrainCopyFileHandlerTests
{
    [Fact]
    [Trait("Category", "TargetTest")]
    public async Task DownloadAsync_ShouldReturnCopiedFileCode_WhenFileExists()
    {
        // Arrange
        var pxCopyHandler = new PixeldrainCopyFileHandler();
        // Act
        var result = await pxCopyHandler.HandleAsync(new CopyFileRequest(new Uri("https://pixeldrain.com/u/WMsxv6RT")));
        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Console.WriteLine($"Copied file code: {result.Value!.FileCode}");
    }
}
