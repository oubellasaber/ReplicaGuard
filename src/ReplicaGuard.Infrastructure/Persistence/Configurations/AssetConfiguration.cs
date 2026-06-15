using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReplicaGuard.Core.Replication;

namespace ReplicaGuard.Infrastructure.Persistence.Configurations;

internal sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("assets");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired();

        // Store FileSource as JSON (polymorphic - can be Remote or Local)
        builder.Property(x => x.Source)
            .HasConversion(
                source => SerializeFileSource(source),
                json => DeserializeFileSource(json))
            .HasColumnType("jsonb");

        builder.Property(x => x.FileName)
            .HasConversion(
                fn => fn.Value,
                value => FileName.Create(value).Value)
            .HasMaxLength(255)
            .IsRequired();

        builder.Ignore(a => a.Status);

        builder.Property(x => x.SizeBytes);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Indexes for performance
        builder.HasIndex(x => x.UserId);

        // Replicas collection
        builder.HasMany(a => a.Replicas)
            .WithOne()
            .HasForeignKey(r => r.AssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Replicas)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_replicas");
    }

    private static string SerializeFileSource(FileSource source)
    {
        return source switch
        {
            RemoteFileSource remote => JsonConvert.SerializeObject(new
            {
                Type = "Remote",
                Url = remote.Url.Value.ToString(),
                Headers = remote.Headers,
                Body = remote.Body
            }),
            LocalFileSource local => JsonConvert.SerializeObject(new
            {
                Type = "Local",
                FilePath = local.FilePath
            }),
            _ => throw new InvalidOperationException($"Unknown FileSource type: {source.GetType()}")
        };
    }

    private static FileSource DeserializeFileSource(string? json)
    {
        if (string.IsNullOrEmpty(json))
            throw new InvalidOperationException("FileSource JSON is null or empty.");

        JObject? data = JsonConvert.DeserializeObject<JObject>(json);
        if (data == null)
            throw new InvalidOperationException("Failed to deserialize FileSource JSON.");

        string? type = data["Type"]?.ToString();

        return type switch
        {
            "Remote" => DeserializeRemoteFileSource(data),
            "Local" => DeserializeLocalFileSource(data),
            _ => throw new InvalidOperationException($"Unknown FileSource type in JSON: {type}")
        };
    }

    private static RemoteFileSource DeserializeRemoteFileSource(JObject data)
    {
        string url = data["Url"]!.ToString();
        Dictionary<string, string> headers = data["Headers"]?.ToObject<Dictionary<string, string>>()
            ?? new Dictionary<string, string>();
        object? body = data["Body"]?.ToObject<object>();

        return RemoteFileSource.Create(url, headers, body).Value;
    }

    private static LocalFileSource DeserializeLocalFileSource(JObject data)
    {
        string filePath = data["FilePath"]!.ToString();
        return LocalFileSource.Create(filePath).Value;
    }
}
