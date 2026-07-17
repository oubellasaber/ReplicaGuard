using FluentAssertions;
using NSubstitute;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.HosterAccounts.CreateHosterAccount;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Application.Tests;

public class CreateHosterAccountCommandHandlerTests
{
    private readonly IHosterDefinitionResolver _resolver;
    private readonly IUserContext _userContext;
    private readonly IHosterRepository _hosters;
    private readonly IHosterAccountRepository _accounts;
    private readonly ISecretEncryptionService _crypto;
    private readonly IUnitOfWork _uow;
    private readonly CreateHosterAccountHandler _sut;

    public CreateHosterAccountCommandHandlerTests()
    {
        _resolver = Substitute.For<IHosterDefinitionResolver>();
        _userContext = Substitute.For<IUserContext>();
        _hosters = Substitute.For<IHosterRepository>();
        _accounts = Substitute.For<IHosterAccountRepository>();
        _crypto = new FakeEncryptionService();
        _uow = Substitute.For<IUnitOfWork>();

        _sut = new CreateHosterAccountHandler(
            _resolver,
            _userContext,
            _hosters,
            _accounts,
            _crypto,
            _uow);
    }

    [Fact]
    public async Task account_creation_returns_failure_when_hoster_not_found()
    {
        var hosterId = Guid.NewGuid();
        _userContext.UserId.Returns(Guid.NewGuid());
        _hosters.GetByIdAsync(hosterId, Arg.Any<CancellationToken>())
            .Returns((Hoster?)null);

        var command = new CreateHosterAccountCommand(
            hosterId, "Test", null,
            new List<IdentityDto>
            {
                new(IdentityType.ApiKey, null,
                    new Dictionary<SecretType, string> { { SecretType.ApiKeyPair, "key" } })
            });

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(HosterErrors.NotFound(hosterId).Code);
    }

    [Fact]
    public async Task account_creation_returns_failure_when_primary_identities_not_satisfied()
    {
        var hosterId = Guid.NewGuid();
        _userContext.UserId.Returns(Guid.NewGuid());

        var hoster = new Hoster(HosterCode.Pixeldrain, "Pixeldrain");
        _hosters.GetByIdAsync(hosterId, Arg.Any<CancellationToken>()).Returns(hoster);
        _resolver.Resolve(hoster.Code).Returns(new Pixeldrain());

        // Email does not satisfy Pixeldrain's ApiKey-only primary requirement
        var command = new CreateHosterAccountCommand(
            hosterId, "Test", null,
            new List<IdentityDto>
            {
                new(IdentityType.Email, "test@example.com",
                    new Dictionary<SecretType, string> { { SecretType.Password, "pass" } })
            });

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task account_creation_returns_success_when_api_key_identity_provided()
    {
        var hosterId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _userContext.UserId.Returns(userId);

        var hoster = new Hoster(HosterCode.Pixeldrain, "Pixeldrain");
        typeof(Hoster).GetProperty(nameof(Hoster.Id))!.SetValue(hoster, hosterId);

        _hosters.GetByIdAsync(hosterId, Arg.Any<CancellationToken>()).Returns(hoster);
        _resolver.Resolve(hoster.Code).Returns(new Pixeldrain());

        var command = new CreateHosterAccountCommand(
            hosterId, "My Account", null,
            new List<IdentityDto>
            {
                new(IdentityType.ApiKey, null,
                    new Dictionary<SecretType, string> { { SecretType.ApiKeyPair, "my-api-key" } })
            });

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Alias.Should().Be("My Account");
        result.Value.TotalIdentities.Should().Be(1);
    }
}
