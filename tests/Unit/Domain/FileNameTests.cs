using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Domain.Tests;

public sealed class FileNameTests
{
    [Fact]
    public void creating_file_name_with_valid_value_succeeds()
    {
        var fileName = "report.pdf";
        
        var result = FileName.Create(fileName);

        Assert.True(result.IsSuccess);
        Assert.Equal(fileName, result.Value.Value);
    }

    [Fact]
    public void creating_file_name_with_null_fails()
    {
        var result = FileName.Create(null!);

        Assert.True(result.IsFailure);
        Assert.Equal(ReplicationErrors.FileNameEmpty.Code, result.Error.Code);
    }

    [Fact]
    public void creating_file_name_with_empty_string_fails()
    {
        var result = FileName.Create("");

        Assert.True(result.IsFailure);
        Assert.Equal(ReplicationErrors.FileNameEmpty.Code, result.Error.Code);
    }

    [Fact]
    public void creating_file_name_with_invalid_chars_fails()
    {
        var result = FileName.Create("file<script>.txt");

        Assert.True(result.IsFailure);
        Assert.Equal(ReplicationErrors.FileNameInvalidChars.Code, result.Error.Code);
    }

    [Fact]
    public void creating_file_name_with_more_than_255_chars_fails()
    {
        var result = FileName.Create(new string('a', 256));

        Assert.True(result.IsFailure);
        Assert.Equal(ReplicationErrors.FileNameTooLong(256).Code, result.Error.Code);
    }

    [Fact]
    public void file_name_to_string_returns_value()
    {
        var fileName = FileName.Create("readme.md").Value;

        Assert.Equal("readme.md", fileName.ToString());
        Assert.Equal("readme.md", (string)fileName);
    }
}
