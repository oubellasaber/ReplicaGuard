using ReplicaGuard.Domain.Abstractions;

namespace ReplicaGuard.Domain.Tests;

public sealed class ErrorTests
{
    [Fact]
    public void constructor_throws_when_code_is_empty()
    {
        Assert.Throws<ArgumentException>(() => new Error("", "message"));
    }

    [Fact]
    public void constructor_throws_when_message_is_empty()
    {
        Assert.Throws<ArgumentException>(() => new Error("code", ""));
    }

    [Fact]
    public void none_has_empty_code_and_message()
    {
        Assert.Equal("", Error.None.Code);
        Assert.Equal("", Error.None.Message);
    }

    [Fact]
    public void with_detail_returns_new_error_with_detail()
    {
        var error = new Error("Test.Code", "Test message");

        var withDetail = error.WithDetail("Additional detail");

        Assert.Null(error.Detail);
        Assert.Equal("Additional detail", withDetail.Detail);
        Assert.Equal(error.Code, withDetail.Code);
        Assert.Equal(error.Message, withDetail.Message);
    }

    [Fact]
    public void with_metadata_returns_new_error_with_metadata()
    {
        var error = new Error("Test.Code", "Test message");

        var withMeta = error.WithMetadata("key", "value");

        Assert.Empty(error.Metadata);
        Assert.Single(withMeta.Metadata);
        Assert.Equal("value", withMeta.Metadata["key"]);
    }

    [Fact]
    public void with_type_returns_new_error_with_type()
    {
        var error = new Error("Test.Code", "Test message");

        var withType = error.WithType(ErrorType.Validation);

        Assert.Equal(ErrorType.Failure, error.Type);
        Assert.Equal(ErrorType.Validation, withType.Type);
    }

    [Fact]
    public void as_permanent_creates_permanent_error()
    {
        var error = new Error("Test.Code", "Test message");

        var permanent = error.AsPermanent();

        Assert.False(error.IsPermanent);
        Assert.True(error.IsTransient);
        Assert.True(permanent.IsPermanent);
        Assert.False(permanent.IsTransient);
    }
}
