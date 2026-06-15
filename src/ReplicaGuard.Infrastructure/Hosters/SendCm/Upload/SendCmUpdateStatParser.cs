using System.Text.Json;
using ReplicaGuard.Core.Abstractions;

namespace ReplicaGuard.Infrastructure.Hosters.SendCm.Upload;

internal static class SendCmUpdateStatParser
{
    private const string Prefix = "update_stat(";

    public static Result<UpdateStat> Parse(string input)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));

        ReadOnlySpan<char> span = input.AsSpan().Trim();

        if (span.IsEmpty || !span.StartsWith(Prefix) || span[^1] != ')')
            throw new FormatException("Expected update_stat(...)");

        int start = input.AsSpan().IndexOf(span);
        ReadOnlyMemory<char> trimmed = input.AsMemory(start, span.Length);

        ReadOnlyMemory<char> json =
            trimmed.Slice(Prefix.Length, trimmed.Length - Prefix.Length - 1);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        return new UpdateStat(
            long.Parse(GetString(root, "loaded")),
            GetInt(root, "pid"),
            long.Parse(GetString(root, "total")),
            int.Parse(GetString(root, "files_done")),
            GetString(root, "state"));
    }

    private static int GetInt(JsonElement root, string name)
        => root.GetProperty(name).GetInt32();

    private static string GetString(JsonElement root, string name)
        => root.GetProperty(name).GetString()!;

    internal readonly record struct UpdateStat(
        long Loaded,
        int Pid,
        long Total,
        int FilesDone,
        string State);
}
