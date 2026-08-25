using AwesomeAssertions;
using ServiceLib.Manager;
using Xunit;

namespace ServiceLib.Tests.Manager;

public class AmneziaWg31ManagerTests
{
    private const string Key = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    [Fact]
    public void InspectConfig_Awg3Parameters_ShouldReportVersion3()
    {
        var config =
            $"""
            [Interface]
            Address = 10.88.0.2/32
            PrivateKey = {Key}
            S1 = 9
            S2 = 9
            S3 = 9
            S4 = 9
            HeaderProtectionKey = {Key}
            ContentPaddingAddition = 16-96
            RekeyAfterTime = 120-180
            RekeyTimeout = 5-8
            RejectAfterTime = 240-300
            KeepaliveTimeout = 10-15
            MaxHandshakeAttempts = 10
            [Peer]
            PublicKey = {Key}
            AllowedIPs = 0.0.0.0/0, ::/0
            Endpoint = 192.0.2.20:586
            PersistentKeepalive = 22-30
            """;

        AmneziaWgManager.HasAmneziaParameterMarkers(config).Should().BeTrue();
        AmneziaWgManager.IsAmneziaConfig(config).Should().BeTrue();
        AmneziaWgManager.Instance.InspectConfig(config).Protocol.Should().Be("AmneziaWG 3.0");
    }

    [Fact]
    public void InspectConfig_Awg31Parameters_ShouldReportVersion31()
    {
        var config =
            $"""
            [Interface]
            Address = 10.88.0.2/32
            PrivateKey = {Key}
            S1 = 9
            S2 = 9
            S3 = 9
            S4 = 9
            HeaderProtectionKey = {Key}
            RandomTrailers = 1
            DisableCookies = off
            [Peer]
            PublicKey = {Key}
            AllowedIPs = 0.0.0.0/0, ::/0
            Endpoint = 192.0.2.31:587
            """;

        AmneziaWgManager.HasAmneziaParameterMarkers(config).Should().BeTrue();
        AmneziaWgManager.IsAmneziaConfig(config).Should().BeTrue();
        AmneziaWgManager.Instance.InspectConfig(config).Protocol.Should().Be("AmneziaWG 3.1");
    }

    [Theory]
    [InlineData("RandomTrailers", "on")]
    [InlineData("RandomTrailers", "0")]
    [InlineData("DisableCookies", "ON")]
    [InlineData("DisableCookies", "1")]
    public void InspectConfig_SingleAwg31Parameter_ShouldReportVersion31(string field, string value)
    {
        var config =
            $"""
            [Interface]
            Address = 10.88.0.2/32
            PrivateKey = {Key}
            S1 = 9
            S2 = 9
            S3 = 9
            S4 = 9
            HeaderProtectionKey = {Key}
            {field} = {value}
            [Peer]
            PublicKey = {Key}
            AllowedIPs = 0.0.0.0/0
            Endpoint = 192.0.2.31:587
            """;

        AmneziaWgManager.Instance.InspectConfig(config).Protocol.Should().Be("AmneziaWG 3.1");
    }

    [Fact]
    public void IsAmneziaConfig_CollapsedAwg31Text_ShouldBeAccepted()
    {
        var config =
            $"[Interface] Address = 10.88.0.2/32 PrivateKey = {Key} "
            + $"S1 = 9 S2 = 9 S3 = 9 S4 = 9 HeaderProtectionKey = {Key} "
            + "RandomTrailers = 1 DisableCookies = off [Peer] "
            + $"PublicKey = {Key} AllowedIPs = 0.0.0.0/0 Endpoint = 192.0.2.31:587";

        AmneziaWgManager.IsAmneziaConfig(config).Should().BeTrue();
        AmneziaWgManager.Instance.InspectConfig(config).Protocol.Should().Be("AmneziaWG 3.1");
    }

    [Theory]
    [InlineData("RandomTrailers", "")]
    [InlineData("RandomTrailers", "true")]
    [InlineData("DisableCookies", "false")]
    [InlineData("DisableCookies", "yes")]
    public void TryValidateAmneziaConfig_InvalidAwg31Switch_ShouldBeRejected(string field, string value)
    {
        var config =
            $"""
            [Interface]
            Address = 10.88.0.2/32
            PrivateKey = {Key}
            Jc = 4
            {field} = {value}
            [Peer]
            PublicKey = {Key}
            AllowedIPs = 0.0.0.0/0
            Endpoint = 192.0.2.31:587
            """;

        AmneziaWgManager.TryValidateAmneziaConfig(config, out _, out var error)
            .Should().BeFalse();
        error.Should().Contain(field);
        error.Should().Contain("on, off, 0 или 1");
    }

    [Theory]
    [InlineData("Endpoint = 192.0.2.30:586")]
    [InlineData("Endpoint = 192.0.2.30:587")]
    public void GetRuntimeFolderName_Awg30_ShouldKeepLegacyRuntimeRegardlessOfPort(string endpoint)
    {
        var config =
            $"""
            [Interface]
            PrivateKey = {Key}
            HeaderProtectionKey = {Key}
            S1 = 9
            S2 = 9
            S3 = 9
            S4 = 9
            [Peer]
            PublicKey = {Key}
            AllowedIPs = 0.0.0.0/0
            {endpoint}
            """;

        AmneziaWgManager.GetRuntimeFolderName(config).Should().Be("awg");
    }

    [Fact]
    public void GetRuntimeFolderName_CommentedAwg31Field_ShouldKeepLegacyRuntime()
    {
        var config =
            $"""
            [Interface]
            PrivateKey = {Key}
            HeaderProtectionKey = {Key}
            # RandomTrailers = 1
            ; DisableCookies = off
            [Peer]
            PublicKey = {Key}
            AllowedIPs = 0.0.0.0/0
            Endpoint = 192.0.2.30:586
            """;

        AmneziaWgManager.GetRuntimeFolderName(config).Should().Be("awg");
    }

    [Theory]
    [InlineData("Endpoint = 192.0.2.31:587", "RandomTrailers = 1")]
    [InlineData("Endpoint = 192.0.2.31:586", "DisableCookies = off")]
    public void GetRuntimeFolderName_Awg31_ShouldUseSeparateRuntimeRegardlessOfPort(string endpoint, string awg31Field)
    {
        var config =
            $"""
            [Interface]
            PrivateKey = {Key}
            HeaderProtectionKey = {Key}
            S1 = 9
            S2 = 9
            S3 = 9
            S4 = 9
            {awg31Field}
            [Peer]
            PublicKey = {Key}
            AllowedIPs = 0.0.0.0/0
            {endpoint}
            """;

        AmneziaWgManager.GetRuntimeFolderName(config).Should().Be("awg31");
    }

    [Fact]
    public void IsAmneziaConfig_Awg3HeaderProtectionWithSmallPadding_ShouldBeRejected()
    {
        var config =
            $"""
            [Interface]
            Address = 10.88.0.2/32
            PrivateKey = {Key}
            S1 = 8
            S2 = 9
            S3 = 9
            S4 = 9
            HeaderProtectionKey = {Key}
            [Peer]
            PublicKey = {Key}
            AllowedIPs = 0.0.0.0/0
            Endpoint = 192.0.2.20:586
            """;

        AmneziaWgManager.TryValidateAmneziaConfig(config, out _, out var error)
            .Should().BeFalse();
        error.Should().Contain("S1");
        error.Should().Contain("8");
    }

    [Fact]
    public void InspectConfig_Awg2Parameters_ShouldRemainVersion2()
    {
        var config =
            $"""
            [Interface]
            Address = 10.77.0.2/32
            PrivateKey = {Key}
            Jc = 4
            Jmin = 40
            Jmax = 70
            S1 = 0
            S2 = 0
            H1 = 2211719691
            H2 = 2488385896
            H3 = 947123974
            H4 = 4290447877
            [Peer]
            PublicKey = {Key}
            AllowedIPs = 0.0.0.0/0
            Endpoint = 192.0.2.10:585
            """;

        AmneziaWgManager.Instance.InspectConfig(config).Protocol.Should().Be("AmneziaWG 2.0");
    }

    [Fact]
    public void IsAmneziaConfig_PlainWireGuardWithKeepalive_ShouldRemainWireGuard()
    {
        var config =
            $"""
            [Interface]
            Address = 10.99.0.2/32
            PrivateKey = {Key}
            [Peer]
            PublicKey = {Key}
            AllowedIPs = 0.0.0.0/0
            Endpoint = 192.0.2.99:51820
            PersistentKeepalive = 25
            """;

        AmneziaWgManager.HasAmneziaParameterMarkers(config).Should().BeFalse();
        AmneziaWgManager.IsAmneziaConfig(config).Should().BeFalse();
    }
}
