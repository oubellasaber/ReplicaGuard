using FluentValidation.TestHelper;
using ReplicaGuard.Application.Assets.CreateAsset;

namespace ReplicaGuard.Application.Tests;

public class CreateAssetCommandValidatorTests
{
    private readonly CreateAssetCommandValidator _sut = new();

    [Fact]
    public void valid_command_passes()
    {
        var command = CreateCommand();
        var result = _sut.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void empty_source_fails(string? source)
    {
        var command = CreateCommand(source: source!);
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Source)
            .WithErrorMessage("Source is required.");
    }

    [Fact]
    public void source_exceeding_max_length_fails()
    {
        var command = CreateCommand(source: new string('a', 2049));
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Source)
            .WithErrorMessage("Source cannot exceed 2048 characters.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void empty_file_name_fails(string? fileName)
    {
        var command = CreateCommand(fileName: fileName!);
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.FileName)
            .WithErrorMessage("File name is required.");
    }

    [Fact]
    public void file_name_exceeding_max_length_fails()
    {
        var command = CreateCommand(fileName: new string('a', 256));
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.FileName)
            .WithErrorMessage("File name cannot exceed 255 characters.");
    }

    [Fact]
    public void file_name_with_invalid_chars_fails()
    {
        var command = CreateCommand(fileName: "file<>.txt");
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.FileName);
    }

    [Fact]
    public void file_name_with_directory_separator_fails()
    {
        var command = CreateCommand(fileName: "folder/file.zip");
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.FileName)
            .WithErrorMessage("File name contains invalid characters.");
    }

    [Fact]
    public void empty_hoster_account_ids_fails()
    {
        var command = CreateCommand(hosterAccountIds: []);
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.HosterAccountIds);
    }

    [Fact]
    public void empty_guid_in_hoster_account_ids_fails()
    {
        var command = CreateCommand(hosterAccountIds: [Guid.Empty]);
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor("HosterAccountIds[0]")
            .WithErrorMessage("HosterAccountId cannot be empty.");
    }

    [Fact]
    public void duplicate_hoster_account_ids_fails()
    {
        var id = Guid.NewGuid();
        var command = CreateCommand(hosterAccountIds: [id, id]);
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.HosterAccountIds)
            .WithErrorMessage("Duplicate hoster account IDs are not allowed.");
    }

    [Fact]
    public void too_many_hoster_account_ids_fails()
    {
        var ids = Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToList();
        var command = CreateCommand(hosterAccountIds: ids);
        var result = _sut.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.HosterAccountIds)
            .WithErrorMessage("Too many hoster account IDs specified. Maximum allowed is 10.");
    }

    private static CreateAssetCommand CreateCommand(
        string source = "https://example.com/file.zip",
        string fileName = "file.zip",
        List<Guid>? hosterAccountIds = null)
    {
        return new CreateAssetCommand(source, fileName, hosterAccountIds ?? [Guid.NewGuid()]);
    }
}
