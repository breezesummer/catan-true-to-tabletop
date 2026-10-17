using Catan.Server;
using Xunit;

namespace Catan.Server.Tests;

public sealed class ServerBoundaryTests
{
    [Theory]
    [InlineData("--port", "0")]
    [InlineData("--port", "65536")]
    [InlineData("--mode", "http")]
    [InlineData("--ai-interval-ms", "0")]
    [InlineData("--request-limit-per-10s", "0")]
    [InlineData("--unknown", "value")]
    public void InvalidServerSettingsFailBeforeStarting(string option, string value)
        => Assert.Throws<ArgumentException>(() => ServerOptions.Parse(new[] { option, value }));

    [Fact]
    public void OnlineModeNeverFallsBackToSelfSignedCertificate()
        => Assert.Throws<ArgumentException>(() => ServerOptions.Parse(new[] { "--mode", "online" }));

    [Fact]
    public void DiscoveryDisabledAliasAndExplicitTestRateAreApplied()
    {
        var options = ServerOptions.Parse(new[] { "--no-discovery", "--port", "47800", "--request-limit-per-10s", "100000" });
        Assert.False(options.EnableDiscovery);
        Assert.Equal(47800, options.Port);
        Assert.Equal(100000, options.RequestLimitPer10Seconds);
    }

    [Fact]
    public void RateLimitRejectsOverQuotaAndDoesNotMergePeers()
    {
        var limits = new PeerRateLimiter();
        Assert.True(limits.Allow("10.0.0.1", 2, 10));
        Assert.True(limits.Allow("10.0.0.1", 2, 10));
        Assert.False(limits.Allow("10.0.0.1", 2, 10));
        Assert.True(limits.Allow("10.0.0.2", 2, 10));
    }

    [Fact]
    public void DefaultRateBudgetAllowsFourClientsSharingOneAddress()
    {
        var options = ServerOptions.Parse(Array.Empty<string>());
        var limits = new PeerRateLimiter();
        // 四位玩家每秒各一次同步和一次历史，以及当前玩家每秒十二次交互预览。
        // 同一公网出口在十秒内的这组正常请求不应被限流。
        for (var i = 0; i < (4 * 2 + 12) * 10; ++i)
            Assert.True(limits.Allow("shared-player-address", options.RequestLimitPer10Seconds, 10));
    }

    [Fact]
    public void RateLimiterDoesNotGrowUnboundedForNewPeerAddresses()
    {
        var limits = new PeerRateLimiter(2);
        Assert.True(limits.Allow("a", 2, 10));
        Assert.True(limits.Allow("b", 2, 10));
        Assert.False(limits.Allow("c", 2, 10));
        Assert.True(limits.Allow("a", 2, 10));
    }

    [Fact]
    public void CertificateFingerprintSurvivesRestartAndAnotherWriterIsRejected()
    {
        var directory = Path.Combine(Path.GetTempPath(), "catan-server-test-" + Guid.NewGuid().ToString("N"));
        var options = new ServerOptions { DataDirectory = directory };
        try
        {
            string first;
            using (var files = new PrivateServerFiles(options))
            {
                first = files.Fingerprint;
                Assert.Equal(64, first.Length);
                Assert.True(files.Certificate.HasPrivateKey);
                Assert.Equal(first, File.ReadAllText(Path.Combine(directory, "server-fingerprint.txt")).Trim());
                Assert.ThrowsAny<IOException>(() => new PrivateServerFiles(options));
            }
            using var restored = new PrivateServerFiles(options);
            Assert.Equal(first, restored.Fingerprint);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void MissingCertificateSecretDoesNotSilentlyReplaceTrustedIdentity()
    {
        var directory = Path.Combine(Path.GetTempPath(), "catan-server-test-" + Guid.NewGuid().ToString("N"));
        var options = new ServerOptions { DataDirectory = directory };
        try
        {
            using (var files = new PrivateServerFiles(options)) { Assert.NotEmpty(files.Fingerprint); }
            var certificateBytes = File.ReadAllBytes(Path.Combine(directory, "lan-server.pfx"));
            File.Delete(Path.Combine(directory, "lan-certificate.key"));
            Assert.Throws<InvalidOperationException>(() => new PrivateServerFiles(options));
            Assert.Equal(certificateBytes, File.ReadAllBytes(Path.Combine(directory, "lan-server.pfx")));
        }
        finally { Directory.Delete(directory, true); }
    }
}
