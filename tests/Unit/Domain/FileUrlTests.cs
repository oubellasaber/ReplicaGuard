using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Domain.Tests;

public sealed class FileUrlTests
{
    [Fact]
    public void creating_file_url_with_https_succeeds()
    {
        var result = FileUrl.Create("https://example.com/file.zip");

        Assert.True(result.IsSuccess);
        Assert.Equal("https://example.com/file.zip", result.Value.Value.ToString());
    }

    [Fact]
    public void creating_file_url_with_http_succeeds()
    {
        var result = FileUrl.Create("http://example.com/file.zip");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void creating_file_url_with_null_fails()
    {
        var result = FileUrl.Create(null!);

        Assert.True(result.IsFailure);
        Assert.Equal(ReplicationErrors.FileUrlEmpty.Code, result.Error.Code);
    }

    [Fact]
    public void creating_file_url_with_empty_string_fails()
    {
        var result = FileUrl.Create("");

        Assert.True(result.IsFailure);
        Assert.Equal(ReplicationErrors.FileUrlEmpty.Code, result.Error.Code);
    }

    [Fact]
    public void creating_file_url_with_invalid_uri_fails()
    {
        var result = FileUrl.Create("not a url");

        Assert.True(result.IsFailure);
        Assert.Equal(ReplicationErrors.FileUrlInvalid("not a url").Code, result.Error.Code);
    }

    [Fact]
    public void creating_file_url_with_unsupported_scheme_fails()
    {
        var result = FileUrl.Create("ftp://files.example.com/file.zip");

        Assert.True(result.IsFailure);
        Assert.Equal(ReplicationErrors.FileUrlUnsupportedScheme("ftp").Code, result.Error.Code);
    }
}
