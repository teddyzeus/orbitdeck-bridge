using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json.Linq;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Mod.Tests;

/// <summary>Source de télémétrie contrôlée par le test.</summary>
internal sealed class FakeTelemetrySource : ITelemetrySource
{
    private long _sequence;
    private volatile TelemetrySnapshot _latest = TelemetrySnapshot.NoVessel;

    public TelemetrySnapshot Latest => _latest;

    public long Sequence => Interlocked.Read(ref _sequence);

    public void Publish(TelemetrySnapshot snapshot)
    {
        _latest = snapshot;
        Interlocked.Increment(ref _sequence);
    }
}

internal static class Snapshots
{
    public static TelemetrySnapshot InFlight(double altitude = 1000, float throttle = 0.5f) => new()
    {
        Status = VesselStatus.Connected,
        GameStateName = "FlightView",
        VesselName = "Kerbal X",
        AltitudeSeaLevel = altitude,
        AltitudeSurface = altitude - 10,
        SpeedSurface = 100,
        SpeedOrbital = 200,
        SpeedMode = SpeedMode.Surface,
        Throttle = throttle,
        SasEnabled = true,
        SasMode = "StabilityAssist",
        RcsEnabled = false
    };
}

/// <summary>Client WebSocket de test : reçoit en continu dans une file, sans jamais annuler une lecture en cours.</summary>
internal sealed class TestClient : IDisposable
{
    private readonly ClientWebSocket _socket = new();
    private readonly BlockingCollection<string> _messages = new();
    private Task? _receiveLoop;

    public WebSocketState State => _socket.State;

    public WebSocketCloseStatus? CloseStatus => _socket.CloseStatus;

    public static async Task<TestClient> ConnectAsync(int port)
    {
        var client = new TestClient();
        await client._socket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/"), CancellationToken.None);
        client._receiveLoop = Task.Run(client.ReceiveLoopAsync);
        return client;
    }

    public Task SendAsync(string text) =>
        _socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, CancellationToken.None);

    public Task SendBinaryAsync(byte[] data) =>
        _socket.SendAsync(data, WebSocketMessageType.Binary, true, CancellationToken.None);

    /// <summary>Message suivant (JSON), ou null s'il n'arrive rien dans le délai.</summary>
    public JObject? Next(int timeoutMs = 3000) =>
        _messages.TryTake(out var text, timeoutMs) ? JObject.Parse(text) : null;

    public JObject Expect(string type, int timeoutMs = 3000)
    {
        var message = Next(timeoutMs) ?? throw new TimeoutException($"Aucun message « {type} » reçu.");
        Assert.Equal(type, (string?)message["type"]);
        return message;
    }

    public bool WaitUntilClosed(int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (_socket.State is WebSocketState.Closed or WebSocketState.Aborted or WebSocketState.CloseReceived)
                return true;
            Thread.Sleep(10);
        }

        return false;
    }

    public void Dispose()
    {
        _socket.Abort();
        _socket.Dispose();
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[8192];
        try
        {
            while (_socket.State == WebSocketState.Open)
            {
                var builder = new StringBuilder();
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket.ReceiveAsync(buffer, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        if (_socket.State == WebSocketState.CloseReceived)
                            await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                        return;
                    }

                    builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);

                _messages.Add(builder.ToString());
            }
        }
        catch (Exception)
        {
            // Connexion fermée ou abandonnée : la file reste lisible.
        }
    }
}
