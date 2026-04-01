using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Core.Domain.Credentials;

public class HosterCredentials : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public Guid HosterId { get; private set; }
    public Hoster.Credentials PrimaryCredentials { get; private set; }

    public string? ApiKey { get; private set; }
    public string? Email { get; private set; }
    public string? Password { get; private set; }
    public string? Username { get; private set; }
    public CredentialsSyncStatus SyncStatus { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Incremented on every modification. Used for optimistic concurrency control.
    /// </summary>
    public uint Version { get; private set; }

    private HosterCredentials() : base(Guid.NewGuid()) { }

    /// <summary>
    /// Creates a new HosterCredentials entity.
    /// Primary credentials are required; secondary credentials are optional and stored for async validation.
    /// </summary>
    internal static Result<HosterCredentials> Create(
        Guid userId,
        Guid hosterId,
        Hoster.Credentials primaryCredentials,
        string? apiKey = null,
        string? email = null,
        string? username = null,
        string? password = null)
    {
        // Validate PRIMARY credentials are present
        var validationResult = ValidatePrimaryCredentials(primaryCredentials, apiKey, email, username, password);
        if (validationResult.IsFailure)
            return Result.Failure<HosterCredentials>(validationResult.Error);

        var cred = new HosterCredentials
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            HosterId = hosterId,
            PrimaryCredentials = primaryCredentials,
            SyncStatus = CredentialsSyncStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            Version = 1
        };

        // Store ALL provided credentials (primary + secondary)
        // Worker will validate secondary belongs to same account later
        cred.SetAllProvidedCredentials(apiKey, email, username, password);

        cred.RaiseDomainEvent(new HosterCredentialsCreated(cred.Id, cred.Version));
        return Result.Success(cred);
    }

    /// <summary>
    /// Updates credentials. Primary credentials must remain present after update.
    /// Secondary credentials are optional and validated async by worker.
    /// </summary>
    internal Result Update(
        string? apiKey = null,
        string? email = null,
        string? username = null,
        string? password = null)
    {
        // Ensure at least one credential is being updated
        bool hasAnyUpdate = !string.IsNullOrWhiteSpace(apiKey) ||
                            !string.IsNullOrWhiteSpace(email) ||
                            !string.IsNullOrWhiteSpace(username);

        if (!hasAnyUpdate)
            return Result.Failure(HosterCredentialsErrors.NoCredentialsProvided());

        // Validate that PRIMARY credentials will still be present after update
        var validationResult = ValidatePrimaryAfterUpdate(apiKey, email, username, password);
        if (validationResult.IsFailure)
            return validationResult;

        // Apply ALL provided updates (primary + secondary)
        SetAllProvidedCredentials(apiKey, email, username, password);

        IncrementVersion();
        MarkCredentialAsPending();
        return Result.Success();
    }

    /// <summary>
    /// Marks as synced. Only succeeds if version matches (no concurrent updates).
    /// </summary>
    public Result MarkCredentialAsSynced(uint expectedVersion)
    {
        if (Version != expectedVersion)
            return Result.Failure(HosterCredentialsErrors.VersionMismatch());

        SyncStatus = CredentialsSyncStatus.Synced;
        UpdatedAtUtc = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// Marks as failed. Only succeeds if version matches.
    /// </summary>
    public Result MarkCredentialAsFailed(uint expectedVersion)
    {
        if (Version != expectedVersion)
            return Result.Failure(HosterCredentialsErrors.VersionMismatch());

        SyncStatus = CredentialsSyncStatus.Failed;
        UpdatedAtUtc = DateTime.UtcNow;
        return Result.Success();
    }

    internal void MarkCredentialAsPending()
    {
        SyncStatus = CredentialsSyncStatus.Pending;
        UpdatedAtUtc = DateTime.UtcNow;
        RaiseDomainEvent(new HosterCredentialsAreOutOfSync(Id, Version));
    }

    /// <summary>
    /// Applies validated credentials from the validation worker.
    /// This may include enriched data (e.g., API key retrieved from login).
    /// Does NOT increment version or change sync status - caller handles that.
    /// </summary>
    public void ApplyValidatedCredentials(
        string? apiKey = null,
        string? email = null,
        string? username = null,
        string? password = null)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
            ApiKey = apiKey.Trim();

        if (!string.IsNullOrWhiteSpace(email))
            Email = email.Trim();

        if (!string.IsNullOrWhiteSpace(username))
            Username = username.Trim();

        if (!string.IsNullOrWhiteSpace(password))
            Password = password;

        UpdatedAtUtc = DateTime.UtcNow;
    }

    private void IncrementVersion()
    {
        Version++;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private bool IsPrimary(Hoster.Credentials credType) =>
        (PrimaryCredentials & credType) != 0;

    private void SetAllProvidedCredentials(
        string? apiKey,
        string? email,
        string? username,
        string? password)
    {
        // Set ApiKey if provided
        if (!string.IsNullOrWhiteSpace(apiKey))
            ApiKey = apiKey.Trim();

        // Set Email if provided
        if (!string.IsNullOrWhiteSpace(email))
            Email = email.Trim();

        // Set Username if provided
        if (!string.IsNullOrWhiteSpace(username))
            Username = username.Trim();

        // Set Password if email or username provided
        if (!string.IsNullOrWhiteSpace(password) &&
            (!string.IsNullOrWhiteSpace(email) || !string.IsNullOrWhiteSpace(username)))
            Password = password;
    }

    private Result ValidatePrimaryAfterUpdate(
        string? newApiKey,
        string? newEmail,
        string? newUsername,
        string? newPassword)
    {
        // Determine what values will exist after update (new value or keep existing)
        string? effectiveApiKey = !string.IsNullOrWhiteSpace(newApiKey) ? newApiKey : ApiKey;
        string? effectiveEmail = !string.IsNullOrWhiteSpace(newEmail) ? newEmail : Email;
        string? effectiveUsername = !string.IsNullOrWhiteSpace(newUsername) ? newUsername : Username;
        string? effectivePassword = !string.IsNullOrWhiteSpace(newPassword) ? newPassword : Password;

        // Validate primary will still be present
        return ValidatePrimaryCredentials(
            PrimaryCredentials,
            effectiveApiKey,
            effectiveEmail,
            effectiveUsername,
            effectivePassword);
    }

    private static Result ValidatePrimaryCredentials(
        Hoster.Credentials primaryCredentials,
        string? apiKey,
        string? email,
        string? username,
        string? password)
    {
        // ApiKey required if it's a primary type
        if ((primaryCredentials & Hoster.Credentials.ApiKey) != 0 &&
            string.IsNullOrWhiteSpace(apiKey))
            return Result.Failure(HosterCredentialsErrors.MissingApiKey());

        // Email + Password required if EmailPassword is primary
        if ((primaryCredentials & Hoster.Credentials.EmailPassword) != 0)
        {
            if (string.IsNullOrWhiteSpace(email))
                return Result.Failure(HosterCredentialsErrors.MissingEmail());
            if (string.IsNullOrWhiteSpace(password))
                return Result.Failure(HosterCredentialsErrors.MissingPassword());
        }

        // Username + Password required if UsernamePassword is primary
        if ((primaryCredentials & Hoster.Credentials.UsernamePassword) != 0)
        {
            if (string.IsNullOrWhiteSpace(username))
                return Result.Failure(HosterCredentialsErrors.MissingUsername());
            if (string.IsNullOrWhiteSpace(password))
                return Result.Failure(HosterCredentialsErrors.MissingPassword());
        }

        return Result.Success();
    }
}
