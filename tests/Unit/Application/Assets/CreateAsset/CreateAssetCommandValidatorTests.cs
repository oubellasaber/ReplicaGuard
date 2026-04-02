using FluentValidation.TestHelper;
using ReplicaGuard.Application.Assets.CreateAsset;

namespace ReplicaGuard.Application.Tests.Assets.CreateAsset;

public class CreateAssetCommandValidatorTests
{
    private readonly CreateAssetCommandValidator _sut = new();

    [Fact]
    public void Validate_Passes_WhenCommandIsValid()
    {
        // Arrange
        CreateAssetCommand command = CreateCommand();

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_Fails_WhenSourceIsEmpty(string? source)
    {
        // Arrange
        CreateAssetCommand command = CreateCommand(source: source!);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Source)
            .WithErrorMessage("Source is required.");
    }

    [Fact]
    public void Validate_Fails_WhenSourceExceedsMaxLength()
    {
        // Arrange
        CreateAssetCommand command = CreateCommand(source: new string('a', 2049));

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Source)
            .WithErrorMessage("Source cannot exceed 2048 characters.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_Fails_WhenFileNameIsEmpty(string? fileName)
    {
        // Arrange
        CreateAssetCommand command = CreateCommand(fileName: fileName!);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FileName)
            .WithErrorMessage("File name is required.");
    }

    [Fact]
    public void Validate_Fails_WhenFileNameExceedsMaxLength()
    {
        // Arrange
        CreateAssetCommand command = CreateCommand(fileName: new string('a', 256));

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FileName)
            .WithErrorMessage("File name cannot exceed 255 characters.");
    }

    [Fact]
    public void Validate_Fails_WhenHosterIdsListIsEmpty()
    {
        // Arrange
        CreateAssetCommand command = CreateCommand(hosterIds: []);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.HosterIds)
            .WithErrorMessage("At least one hoster is required.");
    }

    [Fact]
    public void Validate_Fails_WhenHosterIdsContainsEmptyGuid()
    {
        // Arrange
        CreateAssetCommand command = CreateCommand(hosterIds: [Guid.Empty]);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("HosterIds[0]")
            .WithErrorMessage("Hoster ID cannot be empty.");
    }

    [Fact]
    public void Validate_Fails_WhenHosterIdsContainDuplicates()
    {
        // Arrange
        Guid hosterId = Guid.NewGuid();
        CreateAssetCommand command = CreateCommand(hosterIds: [hosterId, hosterId]);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.HosterIds)
            .WithErrorMessage("Duplicate hoster IDs are not allowed.");
    }

    private static CreateAssetCommand CreateCommand(
        string source = "https://example.com/file.zip",
        string fileName = "file.zip",
        List<Guid>? hosterIds = null)
    {
        return new CreateAssetCommand(source, fileName, hosterIds ?? [Guid.NewGuid()]);
    }
}
