using StreamPlug.Mod.Server;
using StreamPlug.Mod.Telemetry;

namespace StreamPlug.Mod.Tests;

public sealed class StreamPlugServerTests : IDisposable
{
    private readonly FakeTelemetrySource _source = new();
    private readonly WsServer _ws = new(0);
    private readonly StreamPlugServer _server;
    private long _now;

    public StreamPlugServerTests()
    {
        _server = new StreamPlugServer(_ws, _source, new StreamPlug.Mod.Commands.CommandQueue(), "0.1.0", "0.2.8.5", () => Interlocked.Read(ref _now));
        Assert.True(_server.Start());
    }

    public void Dispose() => _server.Stop();

    private async Task<TestClient> ConnectAsync()
    {
        var client = await TestClient.ConnectAsync(_ws.Port);
        Assert.True(SpinWait.SpinUntil(() => _ws.ClientCount >= 1, 2000));
        return client;
    }

    private void AdvanceTo(long ms)
    {
        Interlocked.Exchange(ref _now, ms);
        _server.Tick();
    }

    [Fact]
    public async Task New_client_receives_hello_then_current_state()
    {
        _source.Publish(Snapshots.InFlight(altitude: 2500));
        using var client = await ConnectAsync();

        var hello = client.Expect("hello");
        var state = client.Expect("state");

        Assert.Equal("0.1.0", (string?)hello["mod"]);
        Assert.Equal("0.2.8.5", (string?)hello["game"]);
        Assert.Equal(1, (long)state["seq"]!);
        Assert.Equal("connected", (string?)state["status"]);
        Assert.Equal(2500, (double)state["altitude"]!["seaLevel"]!);
    }

    [Fact]
    public async Task Initial_state_without_vessel_is_reported_as_no_vessel()
    {
        using var client = await ConnectAsync();

        client.Expect("hello");
        Assert.Equal("noVessel", (string?)client.Expect("state")["status"]);
    }

    [Fact]
    public async Task A_change_is_broadcast_with_the_new_sequence()
    {
        using var client = await ConnectAsync();
        client.Expect("hello");
        client.Expect("state");

        _source.Publish(Snapshots.InFlight(altitude: 3000));
        AdvanceTo(1000);

        var state = client.Expect("state");
        Assert.Equal(1, (long)state["seq"]!);
        Assert.Equal(3000, (double)state["altitude"]!["seaLevel"]!);
    }

    [Fact]
    public async Task State_messages_are_limited_to_ten_per_second()
    {
        using var client = await ConnectAsync();
        client.Expect("hello");
        client.Expect("state");

        _source.Publish(Snapshots.InFlight(altitude: 1));
        AdvanceTo(2000);
        Assert.Equal(1, (long)client.Expect("state")["seq"]!);

        _source.Publish(Snapshots.InFlight(altitude: 2));
        AdvanceTo(2050);
        Assert.Null(client.Next(300));

        AdvanceTo(2100);
        var state = client.Expect("state");
        Assert.Equal(2, (long)state["seq"]!);
        Assert.Equal(2, (double)state["altitude"]!["seaLevel"]!);
    }

    [Fact]
    public async Task Several_changes_between_two_sends_are_coalesced()
    {
        using var client = await ConnectAsync();
        client.Expect("hello");
        client.Expect("state");

        _source.Publish(Snapshots.InFlight(altitude: 1));
        AdvanceTo(2000);
        client.Expect("state");

        _source.Publish(Snapshots.InFlight(altitude: 2));
        _source.Publish(Snapshots.InFlight(altitude: 3));
        _source.Publish(Snapshots.InFlight(altitude: 4));
        AdvanceTo(2200);

        var state = client.Expect("state");
        Assert.Equal(4, (long)state["seq"]!);
        Assert.Equal(4, (double)state["altitude"]!["seaLevel"]!);
        Assert.Null(client.Next(300));
    }

    [Fact]
    public async Task Heartbeat_resends_the_state_after_one_second_without_change()
    {
        _source.Publish(Snapshots.InFlight());
        using var client = await ConnectAsync();
        client.Expect("hello");
        client.Expect("state");

        AdvanceTo(2000);
        var first = client.Expect("state");

        AdvanceTo(2500);
        Assert.Null(client.Next(300));

        AdvanceTo(3000);
        var second = client.Expect("state");

        Assert.Equal((long)first["seq"]!, (long)second["seq"]!);
    }

    [Fact]
    public async Task Nothing_is_sent_when_no_client_is_connected()
    {
        _source.Publish(Snapshots.InFlight());
        AdvanceTo(5000);

        using var client = await ConnectAsync();
        client.Expect("hello");
        client.Expect("state");
        Assert.Null(client.Next(300));
    }

    [Fact]
    public async Task Ping_gets_pong()
    {
        using var client = await ConnectAsync();
        client.Expect("hello");
        client.Expect("state");

        await client.SendAsync("{\"v\":1,\"type\":\"ping\"}");

        client.Expect("pong");
    }

    [Fact]
    public async Task Invalid_json_gets_bad_request_and_connection_stays_open()
    {
        using var client = await ConnectAsync();
        client.Expect("hello");
        client.Expect("state");

        await client.SendAsync("ceci n'est pas du json");

        var error = client.Expect("error");
        Assert.Equal("BAD_REQUEST", (string?)error["code"]);
        Assert.Null(error["id"]);

        await client.SendAsync("{\"v\":1,\"type\":\"ping\"}");
        client.Expect("pong");
    }

    [Fact]
    public async Task Command_with_missing_name_gets_bad_request_with_id()
    {
        using var client = await ConnectAsync();
        client.Expect("hello");
        client.Expect("state");

        await client.SendAsync("{\"v\":1,\"type\":\"command\",\"id\":\"c-5\"}");

        var error = client.Expect("error");
        Assert.Equal("c-5", (string?)error["id"]);
        Assert.Equal("BAD_REQUEST", (string?)error["code"]);
    }

    [Fact]
    public async Task Two_clients_receive_the_same_updates()
    {
        using var a = await ConnectAsync();
        using var b = await TestClient.ConnectAsync(_ws.Port);
        Assert.True(SpinWait.SpinUntil(() => _ws.ClientCount == 2, 2000));
        foreach (var client in new[] { a, b })
        {
            client.Expect("hello");
            client.Expect("state");
        }

        _source.Publish(Snapshots.InFlight(altitude: 777));
        AdvanceTo(2000);

        Assert.Equal(777, (double)a.Expect("state")["altitude"]!["seaLevel"]!);
        Assert.Equal(777, (double)b.Expect("state")["altitude"]!["seaLevel"]!);
    }

    [Fact]
    public void Start_returns_false_when_the_port_is_taken()
    {
        using var other = new StreamPlugServer(new WsServer(_ws.Port), _source, new StreamPlug.Mod.Commands.CommandQueue(), "0.1.0", "x");

        Assert.False(other.Start());
        Assert.False(other.IsRunning);
    }

    [Fact]
    public async Task Stop_then_dispose_is_safe_and_closes_clients()
    {
        using var client = await ConnectAsync();
        client.Expect("hello");

        _server.Stop();
        _server.Stop();

        Assert.True(client.WaitUntilClosed());
        Assert.False(_server.IsRunning);
    }
}
