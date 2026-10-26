using System.Net.Sockets;
using OrbitDeck.Mod.Server;
using Xunit;

namespace OrbitDeck.Mod.Tests;

public class BindFailureTests
{
    [Fact]
    public void Access_denied_points_to_windows_reserved_ports()
    {
        var message = WsServer.DescribeBindFailure(new SocketException((int)SocketError.AccessDenied));

        Assert.Contains("excludedportrange", message);
        Assert.Contains("orbitdeck-config.json", message);
    }

    [Fact]
    public void Address_in_use_suggests_closing_the_other_game_or_changing_the_port()
    {
        var message = WsServer.DescribeBindFailure(new SocketException((int)SocketError.AddressAlreadyInUse));

        Assert.Contains("déjà utilisé", message);
    }

    [Fact]
    public void Other_errors_add_nothing()
    {
        Assert.Equal(string.Empty, WsServer.DescribeBindFailure(new SocketException((int)SocketError.NetworkDown)));
    }
}
