using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ReplicaGuard.Application.Abstractions.Authentication;
using ReplicaGuard.Application.Abstractions.Clock;
using ReplicaGuard.Application.Assets.CreateAsset;
using ReplicaGuard.Domain.Abstractions;
using ReplicaGuard.Domain.HosterAccounts;
using ReplicaGuard.Domain.Hosters;
using ReplicaGuard.Domain.Replication;

namespace ReplicaGuard.Application.Tests;

public class CreateAssetCommandHandlerTests
{
    private readonly IUserContext _userContext;
    private readonly IHosterDefinitionResolver _resolver;
    private readonly IHosterAccountRepository _accountRepository;
    private readonly IAssetRepository _assetRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly CreateAssetCommandHandler _sut;

    public CreateAssetCommandHandlerTests()
    {
        _userContext = Substitute.For<IUserContext>();
        _resolver = Substitute.For<IHosterDefinitionResolver>();
        _accountRepository = Substitute.For<IHosterAccountRepository>();
        _assetRepository = Substitute.For<IAssetRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _dateTimeProvider = Substitute.For<IDateTimeProvider>();

        _sut = new CreateAssetCommandHandler(
            _userContext,
            _resolver,
            _accountRepository,
            _assetRepository,
            _unitOfWork,
            _dateTimeProvider,
            Substitute.For<ILogger<CreateAssetCommandHandler>>());
    }

    [Fact]
    public async Task creation_returns_failure_when_hoster_account_not_found()
    {
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        _userContext.UserId.Returns(userId);

        var command = new CreateAssetCommand(
            "https://example.com/file.zip",
            "file.zip",
            new[] { accountId });

        _accountRepository.GetAccountsByIds(
                userId, Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(accountId)),
                Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<HosterAccount>());

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task creation_returns_created_response_when_request_is_valid()
    {
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        _userContext.UserId.Returns(userId);

        var hoster = new Hoster(HosterCode.Pixeldrain, "Pixeldrain");
        var definition = new Pixeldrain();
        var account = CreateAccount(accountId, hoster, definition);

        _accountRepository.GetAccountsByIds(
                userId, Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(accountId)),
                Arg.Any<CancellationToken>())
            .Returns(new[] { account });

        _resolver.Resolve(account.HosterCode).Returns(definition);

        var command = new CreateAssetCommand(
            "https://example.com/file.zip",
            "file.zip",
            new[] { accountId });

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().Be("file.zip");
        result.Value.ReplicaCount.Should().Be(1);
        // TODO: Use DateTimeProvider
        result.Value.CreatedAtUtc.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
    }

    private static HosterAccount CreateAccount(Guid id, Hoster hoster, IHosterDefinition definition)
    {
        var encryption = new FakeEncryptionService();
        var account = HosterAccount.Create(
            definition,
            hoster,
            Guid.NewGuid(),
            "test",
            null,
            new[] { new IdentityPayload.ApiKeyPayload("test-key") },
            encryption).Value;

        account.Identities.Single().MarkAsVerified();

        typeof(HosterAccount).GetProperty(nameof(HosterAccount.Hoster))!
            .SetValue(account, hoster);
        typeof(Entity<Guid>).GetProperty(nameof(Entity<Guid>.Id))!
            .SetValue(account, id);

        return account;
    }
}
