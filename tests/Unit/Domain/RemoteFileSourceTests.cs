using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Domain.Tests;

public sealed class RemoteFileSourceTests
{
    [Fact]
    public void create_with_url_only_succeeds()
    {
        var result = RemoteFileSource.Create("https://example.com/file.bin");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsRemote);
        Assert.False(result.Value.IsLocal);
        Assert.Empty(result.Value.Headers);
        Assert.Null(result.Value.Body);
    }

    [Fact]
    public void create_with_url_and_headers_succeeds()
    {
        var headers = new Dictionary<string, string> { { "Authorization", "Bearer token" } };
        var result = RemoteFileSource.Create("https://example.com/file.bin", headers);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Headers);
        Assert.Equal("Bearer token", result.Value.Headers["Authorization"]);
    }

    [Fact]
    public void create_with_null_headers_fails()
    {
        var result = RemoteFileSource.Create("https://example.com/file.bin", null!);

        Assert.True(result.IsFailure);
        Assert.Equal(ReplicationErrors.HeadersCannotBeEmpty.Code, result.Error.Code);
    }

    [Fact]
    public void create_with_invalid_url_fails()
    {
        var result = RemoteFileSource.Create("invalid");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void with_headers_merges_new_headers()
    {
        var source = RemoteFileSource.Create("https://example.com/file.bin",
            new Dictionary<string, string> { { "Accept", "application/json" } }).Value;

        var merged = source.WithHeaders(new Dictionary<string, string> { { "Authorization", "Bearer x" } });

        Assert.Equal(2, merged.Headers.Count);
        Assert.Equal("application/json", merged.Headers["Accept"]);
        Assert.Equal("Bearer x", merged.Headers["Authorization"]);
    }

    [Fact]
    public void with_headers_overwrites_existing_header()
    {
        var source = RemoteFileSource.Create("https://example.com/file.bin",
            new Dictionary<string, string> { { "Accept", "text/plain" } }).Value;

        var merged = source.WithHeaders(new Dictionary<string, string> { { "Accept", "application/json" } });

        Assert.Equal("application/json", merged.Headers["Accept"]);
    }

    [Fact]
    public void with_body_replaces_body()
    {
        var source = RemoteFileSource.Create("https://example.com/file.bin").Value;

        var withBody = source.WithBody(new { key = "value" });

        Assert.NotNull(withBody.Body);
    }
}
