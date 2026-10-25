using System.Net.Sockets;
using System.Text;
using StreamPlug.Mod.Server;

namespace StreamPlug.Mod.Tests;

public class WebSocketFramesTests
{
    /// <summary>Construit une trame telle qu'un client la masque (RFC 6455 §5.3).</summary>
    internal static byte[] ClientFrame(int opcode, byte[] payload, bool fin = true, bool masked = true, int rsv = 0)
    {
        var header = new List<byte> { (byte)((fin ? 0x80 : 0) | rsv | opcode) };
        var maskBit = masked ? 0x80 : 0;

        if (payload.Length < 126)
            header.Add((byte)(maskBit | payload.Length));
        else if (payload.Length <= ushort.MaxValue)
        {
            header.Add((byte)(maskBit | 126));
            header.Add((byte)(payload.Length >> 8));
            header.Add((byte)payload.Length);
        }
        else
        {
            header.Add((byte)(maskBit | 127));
            for (var i = 7; i >= 0; i--)
                header.Add((byte)((long)payload.Length >> (8 * i)));
        }

        var frame = new List<byte>(header);
        if (masked)
        {
            var mask = new byte[] { 0x12, 0x34, 0x56, 0x78 };
            frame.AddRange(mask);
            for (var i = 0; i < payload.Length; i++)
                frame.Add((byte)(payload[i] ^ mask[i % 4]));
        }
        else
        {
            frame.AddRange(payload);
        }

        return frame.ToArray();
    }

    private static Task<WebSocketFrame> Read(byte[] bytes, int max = 4096) =>
        WebSocketFrames.ReadAsync(new MemoryStream(bytes), max, CancellationToken.None);

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(125)]
    [InlineData(126)]
    [InlineData(4000)]
    public async Task Client_text_frames_roundtrip(int length)
    {
        var payload = Encoding.UTF8.GetBytes(new string('a', length));

        var frame = await Read(ClientFrame(WebSocketFrames.Text, payload));

        Assert.True(frame.Fin);
        Assert.Equal(WebSocketFrames.Text, frame.Opcode);
        Assert.Equal(payload, frame.Payload);
    }

    [Fact]
    public async Task Frame_with_64_bit_length_is_read()
    {
        var payload = new byte[70000];
        Array.Fill(payload, (byte)'x');

        var frame = await Read(ClientFrame(WebSocketFrames.Text, payload), max: 100000);

        Assert.Equal(70000, frame.Payload.Length);
    }

    [Fact]
    public async Task Unmasked_client_frame_is_a_protocol_error()
    {
        var exception = await Assert.ThrowsAsync<WebSocketProtocolException>(
            () => Read(ClientFrame(WebSocketFrames.Text, new byte[] { 1 }, masked: false)));

        Assert.Equal(WebSocketFrames.CloseProtocolError, exception.CloseCode);
    }

    [Fact]
    public async Task Reserved_bits_are_a_protocol_error()
    {
        var exception = await Assert.ThrowsAsync<WebSocketProtocolException>(
            () => Read(ClientFrame(WebSocketFrames.Text, new byte[] { 1 }, rsv: 0x40)));

        Assert.Equal(WebSocketFrames.CloseProtocolError, exception.CloseCode);
    }

    [Fact]
    public async Task Oversized_frame_is_rejected_with_1009()
    {
        var exception = await Assert.ThrowsAsync<WebSocketProtocolException>(
            () => Read(ClientFrame(WebSocketFrames.Text, new byte[5000]), max: 4096));

        Assert.Equal(WebSocketFrames.CloseMessageTooBig, exception.CloseCode);
    }

    [Fact]
    public async Task Fragmented_control_frame_is_a_protocol_error()
    {
        var exception = await Assert.ThrowsAsync<WebSocketProtocolException>(
            () => Read(ClientFrame(WebSocketFrames.Ping, new byte[] { 1 }, fin: false)));

        Assert.Equal(WebSocketFrames.CloseProtocolError, exception.CloseCode);
    }

    [Fact]
    public async Task Truncated_stream_signals_end_of_stream()
    {
        var bytes = ClientFrame(WebSocketFrames.Text, new byte[50]);

        await Assert.ThrowsAsync<EndOfStreamException>(() => Read(bytes.Take(10).ToArray()));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(125, 2)]
    [InlineData(126, 4)]
    [InlineData(65535, 4)]
    [InlineData(65536, 10)]
    public void Server_frames_use_the_right_header_size(int length, int headerSize)
    {
        var frame = WebSocketFrames.Build(WebSocketFrames.Text, new byte[length]);

        Assert.Equal(headerSize + length, frame.Length);
        Assert.Equal(0x81, frame[0]);
        Assert.Equal(0, frame[1] & 0x80);
    }

    [Fact]
    public void Close_frame_carries_code_and_reason()
    {
        var frame = WebSocketFrames.BuildClose(1009, "trop grand");

        Assert.Equal(0x88, frame[0]);
        Assert.Equal(1009, (frame[2] << 8) | frame[3]);
        Assert.Equal("trop grand", Encoding.UTF8.GetString(frame, 4, frame.Length - 4));
    }

    [Fact]
    public void Close_reason_is_truncated_to_fit_a_control_frame()
    {
        var frame = WebSocketFrames.BuildClose(1000, new string('x', 500));

        Assert.True(frame[1] <= 125);
    }
}

public class WebSocketHandshakeTests
{
    private static HandshakeRequest Request(Action<HandshakeRequest>? change = null, int port = 8765)
    {
        var request = new HandshakeRequest { Method = "GET", Path = "/" };
        request.Headers["Host"] = $"127.0.0.1:{port}";
        request.Headers["Upgrade"] = "websocket";
        request.Headers["Connection"] = "Upgrade";
        request.Headers["Sec-WebSocket-Version"] = "13";
        request.Headers["Sec-WebSocket-Key"] = "dGhlIHNhbXBsZSBub25jZQ==";
        change?.Invoke(request);
        return request;
    }

    [Fact]
    public void Accept_key_matches_rfc_6455_example()
    {
        Assert.Equal("s3pPLMBiTxaQ9kYGzzhZRbK+xOo=", WebSocketHandshake.ComputeAccept("dGhlIHNhbXBsZSBub25jZQ=="));
    }

    [Fact]
    public void Valid_request_is_accepted()
    {
        var response = WebSocketHandshake.Validate(Request(), 8765, out var accepted);

        Assert.True(accepted);
        Assert.StartsWith("HTTP/1.1 101", response);
        Assert.Contains("Sec-WebSocket-Accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=", response);
    }

    [Fact]
    public void Localhost_host_is_accepted()
    {
        WebSocketHandshake.Validate(Request(r => r.Headers["Host"] = "localhost:8765"), 8765, out var accepted);

        Assert.True(accepted);
    }

    [Theory]
    [InlineData("evil.example.com:8765")]
    [InlineData("127.0.0.1:9999")]
    [InlineData("192.168.1.10:8765")]
    [InlineData("127.0.0.1")]
    public void Foreign_host_is_forbidden(string host)
    {
        var response = WebSocketHandshake.Validate(Request(r => r.Headers["Host"] = host), 8765, out var accepted);

        Assert.False(accepted);
        Assert.StartsWith("HTTP/1.1 403", response);
    }

    [Fact]
    public void Request_with_origin_is_forbidden()
    {
        var response = WebSocketHandshake.Validate(Request(r => r.Headers["Origin"] = "https://evil.example.com"), 8765, out var accepted);

        Assert.False(accepted);
        Assert.StartsWith("HTTP/1.1 403", response);
    }

    [Fact]
    public void Missing_host_is_a_bad_request()
    {
        var response = WebSocketHandshake.Validate(Request(r => r.Headers.Remove("Host")), 8765, out var accepted);

        Assert.False(accepted);
        Assert.StartsWith("HTTP/1.1 400", response);
    }

    [Fact]
    public void Non_get_method_is_a_bad_request()
    {
        var response = WebSocketHandshake.Validate(Request(r => r.Method = "POST"), 8765, out var accepted);

        Assert.False(accepted);
        Assert.StartsWith("HTTP/1.1 400", response);
    }

    [Fact]
    public void Plain_http_request_requires_upgrade()
    {
        var response = WebSocketHandshake.Validate(
            Request(r => { r.Headers.Remove("Upgrade"); r.Headers.Remove("Connection"); }), 8765, out var accepted);

        Assert.False(accepted);
        Assert.StartsWith("HTTP/1.1 426", response);
    }

    [Fact]
    public void Wrong_websocket_version_requires_upgrade_and_announces_13()
    {
        var response = WebSocketHandshake.Validate(Request(r => r.Headers["Sec-WebSocket-Version"] = "8"), 8765, out var accepted);

        Assert.False(accepted);
        Assert.StartsWith("HTTP/1.1 426", response);
        Assert.Contains("Sec-WebSocket-Version: 13", response);
    }

    [Fact]
    public void Missing_key_is_a_bad_request()
    {
        var response = WebSocketHandshake.Validate(Request(r => r.Headers.Remove("Sec-WebSocket-Key")), 8765, out var accepted);

        Assert.False(accepted);
        Assert.StartsWith("HTTP/1.1 400", response);
    }

    [Fact]
    public void Null_request_is_a_bad_request()
    {
        var response = WebSocketHandshake.Validate(null, 8765, out var accepted);

        Assert.False(accepted);
        Assert.StartsWith("HTTP/1.1 400", response);
    }

    [Fact]
    public void Request_text_is_parsed_with_case_insensitive_headers()
    {
        var request = WebSocketHandshake.Parse("GET /chat HTTP/1.1\r\nHOST: 127.0.0.1:1\r\nupgrade: WebSocket\r\n\r\n");

        Assert.NotNull(request);
        Assert.Equal("GET", request!.Method);
        Assert.Equal("/chat", request.Path);
        Assert.Equal("127.0.0.1:1", request.Header("host"));
        Assert.Equal("WebSocket", request.Header("Upgrade"));
    }

    [Fact]
    public void Garbage_request_text_is_rejected()
    {
        Assert.Null(WebSocketHandshake.Parse("n'importe quoi\r\n\r\n"));
        Assert.Null(WebSocketHandshake.Parse("GET / HTTP/1.1\r\nen-tête sans deux-points\r\n\r\n"));
    }
}

public sealed class WsServerTests : IDisposable
{
    private readonly WsServer _server = new(0);

    public WsServerTests()
    {
        Assert.True(_server.Start());
    }

    public void Dispose() => _server.Stop();

    private static async Task<string> RawRequestAsync(int port, string request)
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync("127.0.0.1", port);
        var stream = tcp.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request));

        var buffer = new byte[2048];
        var read = await stream.ReadAsync(buffer);
        return Encoding.ASCII.GetString(buffer, 0, read);
    }

    private string Handshake(string extraHeaders = "", string? host = null) =>
        $"GET / HTTP/1.1\r\nHost: {host ?? $"127.0.0.1:{_server.Port}"}\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
        + "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\n" + extraHeaders + "\r\n";

    [Fact]
    public async Task Client_can_connect_and_is_counted()
    {
        using var client = await TestClient.ConnectAsync(_server.Port);

        Assert.True(SpinWait.SpinUntil(() => _server.ClientCount == 1, 2000));
    }

    [Fact]
    public async Task Disconnected_client_is_removed()
    {
        var client = await TestClient.ConnectAsync(_server.Port);
        Assert.True(SpinWait.SpinUntil(() => _server.ClientCount == 1, 2000));

        client.Dispose();

        Assert.True(SpinWait.SpinUntil(() => _server.ClientCount == 0, 3000));
    }

    [Fact]
    public async Task Broadcast_reaches_every_client()
    {
        using var a = await TestClient.ConnectAsync(_server.Port);
        using var b = await TestClient.ConnectAsync(_server.Port);
        Assert.True(SpinWait.SpinUntil(() => _server.ClientCount == 2, 2000));

        _server.Broadcast("{\"type\":\"test\"}");

        Assert.Equal("test", (string?)a.Next()!["type"]);
        Assert.Equal("test", (string?)b.Next()!["type"]);
    }

    [Fact]
    public async Task Messages_from_client_raise_the_event()
    {
        var received = new System.Collections.Concurrent.BlockingCollection<string>();
        _server.MessageReceived += (_, text) => received.Add(text);
        using var client = await TestClient.ConnectAsync(_server.Port);

        await client.SendAsync("bonjour é€");

        Assert.True(received.TryTake(out var text, 3000));
        Assert.Equal("bonjour é€", text);
    }

    [Fact]
    public async Task Server_can_reply_to_a_specific_client()
    {
        _server.MessageReceived += (client, text) => client.Send("{\"echo\":" + text + "}");
        using var a = await TestClient.ConnectAsync(_server.Port);
        using var b = await TestClient.ConnectAsync(_server.Port);

        await a.SendAsync("{\"x\":1}");

        Assert.Equal(1, (int)a.Next()!["echo"]!["x"]!);
        Assert.Null(b.Next(300));
    }

    [Fact]
    public async Task Oversized_message_closes_the_connection_with_1009()
    {
        using var client = await TestClient.ConnectAsync(_server.Port);

        await client.SendAsync(new string('x', 5000));

        Assert.True(client.WaitUntilClosed());
        Assert.Equal(System.Net.WebSockets.WebSocketCloseStatus.MessageTooBig, client.CloseStatus);
    }

    [Fact]
    public async Task Binary_message_closes_the_connection()
    {
        using var client = await TestClient.ConnectAsync(_server.Port);

        await client.SendBinaryAsync(new byte[] { 1, 2, 3 });

        Assert.True(client.WaitUntilClosed());
        Assert.Equal(System.Net.WebSockets.WebSocketCloseStatus.InvalidMessageType, client.CloseStatus);
    }

    [Fact]
    public async Task Stop_closes_clients_gracefully()
    {
        using var client = await TestClient.ConnectAsync(_server.Port);
        Assert.True(SpinWait.SpinUntil(() => _server.ClientCount == 1, 2000));

        _server.Stop();

        Assert.True(client.WaitUntilClosed());
        Assert.Equal(0, _server.ClientCount);
        Assert.False(_server.IsRunning);
    }

    [Fact]
    public async Task Valid_raw_handshake_gets_101()
    {
        var response = await RawRequestAsync(_server.Port, Handshake());

        Assert.StartsWith("HTTP/1.1 101", response);
    }

    [Fact]
    public async Task Request_with_origin_gets_403()
    {
        var response = await RawRequestAsync(_server.Port, Handshake("Origin: https://evil.example.com\r\n"));

        Assert.StartsWith("HTTP/1.1 403", response);
    }

    [Fact]
    public async Task Request_with_foreign_host_gets_403()
    {
        var response = await RawRequestAsync(_server.Port, Handshake(host: "attacker.example:" + _server.Port));

        Assert.StartsWith("HTTP/1.1 403", response);
    }

    [Fact]
    public async Task Plain_http_get_gets_426()
    {
        var response = await RawRequestAsync(_server.Port, $"GET / HTTP/1.1\r\nHost: 127.0.0.1:{_server.Port}\r\n\r\n");

        Assert.StartsWith("HTTP/1.1 426", response);
    }

    [Fact]
    public async Task Garbage_does_not_crash_the_server()
    {
        using (var tcp = new TcpClient())
        {
            await tcp.ConnectAsync("127.0.0.1", _server.Port);
            await tcp.GetStream().WriteAsync(new byte[] { 0, 1, 2, 3, 255, 254, 13, 10, 13, 10 });
        }

        using var client = await TestClient.ConnectAsync(_server.Port);
        Assert.True(SpinWait.SpinUntil(() => _server.ClientCount == 1, 2000));
    }

    [Fact]
    public async Task Too_many_clients_are_refused_with_503()
    {
        using var small = new WsServer(0, maxClients: 1);
        Assert.True(small.Start());
        using var first = await TestClient.ConnectAsync(small.Port);
        Assert.True(SpinWait.SpinUntil(() => small.ClientCount == 1, 2000));

        var response = await RawRequestAsync(small.Port,
            $"GET / HTTP/1.1\r\nHost: 127.0.0.1:{small.Port}\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
            + "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\n\r\n");

        Assert.StartsWith("HTTP/1.1 503", response);
    }

    [Fact]
    public void Port_already_in_use_makes_start_return_false_without_throwing()
    {
        var errors = new List<string>();
        using var other = new WsServer(_server.Port, logError: errors.Add);

        Assert.False(other.Start());
        Assert.False(other.IsRunning);
        Assert.Single(errors);
    }

    [Fact]
    public void A_throwing_event_handler_does_not_break_the_server()
    {
        _server.ClientConnected += _ => throw new InvalidOperationException("boum");
        var errors = new List<string>();
        using var server = new WsServer(0, logError: errors.Add);
        Assert.True(server.Start());
        server.ClientConnected += _ => throw new InvalidOperationException("boum");

        using var client = TestClient.ConnectAsync(server.Port).GetAwaiter().GetResult();

        Assert.True(SpinWait.SpinUntil(() => server.ClientCount == 1, 2000));
        Assert.True(SpinWait.SpinUntil(() => errors.Count > 0, 2000));
    }
}
