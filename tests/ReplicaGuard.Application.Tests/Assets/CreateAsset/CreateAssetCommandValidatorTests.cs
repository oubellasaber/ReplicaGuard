using FluentValidation.TestHelper;
using ReplicaGuard.Application.Assets.CreateAsset;

namespace ReplicaGuard.Application.Tests.Assets.CreateAsset;

public class CreateAssetCommandValidatorTests
{
    private readonly CreateAssetCommandValidator _sut = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        // Arrange
        var command = new CreateAssetCommand(
            "https://example.com/file.zip",
            "file.zip",
            new List<Guid> { Guid.NewGuid() });

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptySource_ShouldFail(string? source)
    {
        // Arrange
        var command = new CreateAssetCommand(
            source!,
            "file.zip",
            new List<Guid> { Guid.NewGuid() });

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Source)
            .WithErrorMessage("Source is required.");
    }

    [Fact]
    public void Validate_SourceExceeds2048Chars_ShouldFail()
    {
        // Arrange
        var command = new CreateAssetCommand(
            new string('a', 2049),
            "file.zip",
            new List<Guid> { Guid.NewGuid() });

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Source)
            .WithErrorMessage("Source cannot exceed 2048 characters.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyFileName_ShouldFail(string? fileName)
    {
        // Arrange
        var command = new CreateAssetCommand(
            "https://example.com/file.zip",
            fileName!,
            new List<Guid> { Guid.NewGuid() });

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FileName)
            .WithErrorMessage("File name is required.");
    }

    [Fact]
    public void Validate_FileNameExceeds255Chars_ShouldFail()
    {
        // Arrange
        var command = new CreateAssetCommand(
            "https://example.com/file.zip",
            new string('a', 256),
            new List<Guid> { Guid.NewGuid() });

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FileName)
            .WithErrorMessage("File name cannot exceed 255 characters.");
    }

    [Fact]
    public void Validate_EmptyHosterIds_ShouldFail()
    {
        // Arrange
        var command = new CreateAssetCommand(
            "https://example.com/file.zip",
            "file.zip",
            new List<Guid>());

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.HosterIds)
            .WithErrorMessage("At least one hoster is required.");
    }

    [Fact]
    public void Validate_HosterIdsContainsEmptyGuid_ShouldFail()
    {
        // Arrange
        var command = new CreateAssetCommand(
            "https://example.com/file.zip",
            "file.zip",
            new List<Guid> { Guid.Empty });

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("HosterIds[0]")
            .WithErrorMessage("Hoster ID cannot be empty.");
    }

    [Fact]
    public void Validate_DuplicateHosterIds_ShouldFail()
    {
        // Arrange
        Guid hosterId = Guid.NewGuid();
        var command = new CreateAssetCommand(
            "https://example.com/file.zip",
            "file.zip",
            new List<Guid> { hosterId, hosterId });

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.HosterIds)
            .WithErrorMessage("Duplicate hoster IDs are not allowed.");
    }
}
