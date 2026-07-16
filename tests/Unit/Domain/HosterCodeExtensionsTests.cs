using ReplicaGuard.Domain.Hosters;

namespace ReplicaGuard.Domain.Tests;

public sealed class HosterCodeExtensionsTests
{
    [Fact]
    public void pixeldrain_to_friendly_string()
    {
        Assert.Equal("pixeldrain", HosterCode.Pixeldrain.ToFriendlyString());
    }

    [Fact]
    public void send_cm_to_friendly_string()
    {
        Assert.Equal("sendcm", HosterCode.SendCm.ToFriendlyString());
    }

    [Fact]
    public void from_friendly_string_pixeldrain()
    {
        var result = HosterCodeExtensions.FromFriendlyString("pixeldrain");
        Assert.Equal(HosterCode.Pixeldrain, result);
    }

    [Fact]
    public void from_friendly_string_sendcm()
    {
        var result = HosterCodeExtensions.FromFriendlyString("sendcm");
        Assert.Equal(HosterCode.SendCm, result);
    }

    [Fact]
    public void from_friendly_string_is_case_insensitive()
    {
        var result = HosterCodeExtensions.FromFriendlyString("PixeldRain");
        Assert.Equal(HosterCode.Pixeldrain, result);
    }

    [Fact]
    public void from_friendly_string_unknown_throws()
    {
        Assert.Throws<ArgumentException>(() =>
            HosterCodeExtensions.FromFriendlyString("unknown_hoster"));
    }
}
