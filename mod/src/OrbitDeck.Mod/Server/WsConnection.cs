using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OrbitDeck.Mod.Server
{
    /// <summary>Une connexion cliente : poignée de main, boucle de lecture, boucle d'écriture (seul écrivain).</summary>
    internal sealed class WsConnection : IWsClient
    {
        private const int MaxQueuedFrames = 64;
        private const int HandshakeTimeoutMs = 5000;
        private const int WriteTimeoutMs = 3000;
        private const int CloseGraceMs = 300;

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private readonly WsServer _server;
        private readonly TcpClient _tcp;
        private readonly NetworkStream _stream;
        private readonly ConcurrentQueue<Outbound> _outbound = new ConcurrentQueue<Outbound>();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private int _closeQueued;
        private int _disposed;

        public WsConnection(WsServer server, TcpClient tcp, long id)
        {
            _server = server;
            _tcp = tcp;
            _stream = tcp.GetStream();
            Id = id;
            RemoteEndPoint = tcp.Client.RemoteEndPoint?.ToString() ?? "?";
        }

        public long Id { get; }

        public string RemoteEndPoint { get; }

        public void Send(string text)
        {
            if (Volatile.Read(ref _closeQueued) != 0 || text == null)
                return;

            if (_outbound.Count >= MaxQueuedFrames)
            {
                // Client trop lent : on le déconnecte plutôt que de saturer la mémoire.
                _server.LogError($"Client #{Id} trop lent, déconnecté.");
                Terminate();
                return;
            }

            Enqueue(WebSocketFrames.BuildText(text), false);
        }

        public void Close(int code, string reason)
        {
            if (Interlocked.Exchange(ref _closeQueued, 1) != 0)
                return;
            Enqueue(WebSocketFrames.BuildClose(code, reason), true);
        }

        /// <summary>Coupe immédiatement la connexion.</summary>
        public void Terminate()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            try { _cts.Cancel(); } catch (ObjectDisposedException) { }
            try { _tcp.Close(); } catch { }
        }

        public async Task RunAsync()
        {
            var connected = false;
            try
            {
                if (!await HandshakeAsync().ConfigureAwait(false))
                    return;

                connected = true;
                _server.OnClientConnected(this);

                var sender = SendLoopAsync();
                var receiver = ReceiveLoopAsync();
                var first = await Task.WhenAny(sender, receiver).ConfigureAwait(false);

                if (first == sender)
                    await Task.WhenAny(receiver, Task.Delay(CloseGraceMs)).ConfigureAwait(false);
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                _server.LogVerbose($"Client #{Id} : {exception.GetType().Name} {exception.Message}");
            }
            finally
            {
                Terminate();
                if (connected)
                    _server.OnClientDisconnected(this);
            }
        }

        private async Task<bool> HandshakeAsync()
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token))
            {
                timeout.CancelAfter(HandshakeTimeoutMs);

                var request = await WebSocketHandshake.ReadRequestAsync(_stream, timeout.Token).ConfigureAwait(false);
                var response = WebSocketHandshake.Validate(request, _server.Port, out var accepted);

                var bytes = Encoding.ASCII.GetBytes(response);
                await _stream.WriteAsync(bytes, 0, bytes.Length, timeout.Token).ConfigureAwait(false);

                if (!accepted)
                    _server.LogVerbose($"Connexion refusée depuis {RemoteEndPoint} ({response.Split('\r')[0]}).");
                return accepted;
            }
        }

        private async Task ReceiveLoopAsync()
        {
            var message = new MemoryStream();
            var messageOpcode = -1;

            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    var frame = await WebSocketFrames.ReadAsync(_stream, _server.MaxMessageBytes, _cts.Token).ConfigureAwait(false);

                    switch (frame.Opcode)
                    {
                        case WebSocketFrames.Ping:
                            Enqueue(WebSocketFrames.Build(WebSocketFrames.Pong, frame.Payload), false);
                            break;

                        case WebSocketFrames.Pong:
                            break;

                        case WebSocketFrames.Close:
                            var code = frame.Payload.Length >= 2 ? (frame.Payload[0] << 8) | frame.Payload[1] : WebSocketFrames.CloseNormal;
                            Close(code == WebSocketFrames.CloseNoStatus ? WebSocketFrames.CloseNormal : code, string.Empty);
                            return;

                        case WebSocketFrames.Text:
                        case WebSocketFrames.Binary:
                        case WebSocketFrames.Continuation:
                            if (frame.Opcode == WebSocketFrames.Binary)
                                throw new WebSocketProtocolException(WebSocketFrames.CloseUnsupportedData, "Les messages binaires ne sont pas supportés.");

                            if (frame.Opcode == WebSocketFrames.Continuation)
                            {
                                if (messageOpcode < 0)
                                    throw new WebSocketProtocolException(WebSocketFrames.CloseProtocolError, "Trame de continuation inattendue.");
                            }
                            else
                            {
                                if (messageOpcode >= 0)
                                    throw new WebSocketProtocolException(WebSocketFrames.CloseProtocolError, "Nouveau message avant la fin du précédent.");
                                messageOpcode = frame.Opcode;
                                message.SetLength(0);
                            }

                            message.Write(frame.Payload, 0, frame.Payload.Length);
                            if (message.Length > _server.MaxMessageBytes)
                                throw new WebSocketProtocolException(WebSocketFrames.CloseMessageTooBig, "Message trop grand.");

                            if (frame.Fin)
                            {
                                string text;
                                try
                                {
                                    text = StrictUtf8.GetString(message.GetBuffer(), 0, (int)message.Length);
                                }
                                catch (ArgumentException)
                                {
                                    throw new WebSocketProtocolException(WebSocketFrames.CloseInvalidPayload, "UTF-8 invalide.");
                                }

                                messageOpcode = -1;
                                _server.OnMessageReceived(this, text);
                            }

                            break;

                        default:
                            throw new WebSocketProtocolException(WebSocketFrames.CloseProtocolError, "Opcode inconnu.");
                    }
                }
            }
            catch (WebSocketProtocolException exception)
            {
                _server.LogVerbose($"Client #{Id} : violation du protocole ({exception.Message}).");
                Close(exception.CloseCode, exception.Message);
                // Laisse l'écrivain envoyer la trame de fermeture avant de couper.
                await Task.Delay(CloseGraceMs).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsConnectionClosed(exception))
            {
                // Le client a fermé la connexion (ou nous l'avons coupée).
            }
        }

        private async Task SendLoopAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    await _signal.WaitAsync(_cts.Token).ConfigureAwait(false);

                    while (_outbound.TryDequeue(out var item))
                    {
                        var write = _stream.WriteAsync(item.Data, 0, item.Data.Length, _cts.Token);
                        if (await Task.WhenAny(write, Task.Delay(WriteTimeoutMs)).ConfigureAwait(false) != write)
                        {
                            _server.LogError($"Client #{Id} : délai d'écriture dépassé, déconnecté.");
                            Terminate();
                            return;
                        }

                        await write.ConfigureAwait(false);
                        if (item.IsClose)
                            return;
                    }
                }
            }
            catch (Exception exception) when (IsConnectionClosed(exception))
            {
                // Connexion coupée pendant l'écriture.
            }
        }

        private static bool IsConnectionClosed(Exception exception) =>
            exception is EndOfStreamException
            || exception is OperationCanceledException
            || exception is ObjectDisposedException
            || exception is IOException
            || exception is SocketException
            || exception is InvalidOperationException;

        private void Enqueue(byte[] data, bool isClose)
        {
            _outbound.Enqueue(new Outbound(data, isClose));
            try { _signal.Release(); } catch (ObjectDisposedException) { }
        }

        private readonly struct Outbound
        {
            public Outbound(byte[] data, bool isClose)
            {
                Data = data;
                IsClose = isClose;
            }

            public byte[] Data { get; }

            public bool IsClose { get; }
        }
    }
}
