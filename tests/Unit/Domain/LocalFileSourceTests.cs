using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Domain.Tests;

public sealed class LocalFileSourceTests
{
    [Fact]
    public void create_with_valid_path_succeeds()
    {
        var result = LocalFileSource.Create("/home/user/file.bin");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsLocal);
        Assert.False(result.Value.IsRemote);
    }

    [Fact]
    public void create_with_empty_path_fails()
    {
        var result = LocalFileSource.Create("");

        Assert.True(result.IsFailure);
        Assert.Equal(ReplicationErrors.FilePathEmpty.Code, result.Error.Code);
    }

    [Fact]
    public void create_with_null_path_fails()
    {
        var result = LocalFileSource.Create(null!);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void get_file_name_extracts_from_path()
    {
        var source = LocalFileSource.Create("/home/user/document.pdf").Value;

        Assert.Equal("document.pdf", source.GetFileName());
    }
}
