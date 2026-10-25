using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StreamPlug.Mod.Server
{
    /// <summary>
    /// Serveur WebSocket minimal (RFC 6455, messages texte) lié à 127.0.0.1. Sans dépendance externe.
    /// Les événements sont levés depuis des threads du pool : ils ne doivent pas toucher à l'API du jeu.
    /// </summary>
    public sealed class WsServer : IDisposable
    {
        private readonly ConcurrentDictionary<long, WsConnection> _clients = new ConcurrentDictionary<long, WsConnection>();
        private readonly int _requestedPort;
        private readonly int _maxClients;
        private readonly Action<string> _logInfo;
        private readonly Action<string> _logError;
        private readonly bool _verbose;
        private CancellationTokenSource _cts;
        private TcpListener _listener;
        private long _nextId;

        public WsServer(
            int port,
            int maxClients = 8,
            int maxMessageBytes = 4096,
            bool verbose = false,
            Action<string> logInfo = null,
            Action<string> logError = null)
        {
            _requestedPort = port;
            _maxClients = maxClients;
            MaxMessageBytes = maxMessageBytes;
            _verbose = verbose;
            _logInfo = logInfo;
            _logError = logError;
        }

        /// <summary>Port réellement utilisé (utile quand le port demandé est 0).</summary>
        public int Port { get; private set; }

        public int MaxMessageBytes { get; }

        public bool IsRunning => _listener != null;

        public int ClientCount => _clients.Count;

        public event Action<IWsClient> ClientConnected;

        public event Action<IWsClient> ClientDisconnected;

        public event Action<IWsClient, string> MessageReceived;

        /// <summary>Démarre l'écoute. Retourne false (sans lever d'exception) si le port est indisponible.</summary>
        public bool Start()
        {
            if (_listener != null)
                return true;

            try
            {
                var listener = new TcpListener(IPAddress.Loopback, _requestedPort);
                listener.Start();
                _listener = listener;
                Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            catch (SocketException exception)
            {
                LogError($"Impossible d'ouvrir le port {_requestedPort} : {exception.Message}");
                return false;
            }

            _cts = new CancellationTokenSource();
            var acceptListener = _listener;
            var acceptToken = _cts.Token;
            Task.Run(() => AcceptLoopAsync(acceptListener, acceptToken));
            LogInfo($"Serveur WebSocket à l'écoute sur ws://127.0.0.1:{Port}/");
            return true;
        }

        public void Stop()
        {
            var listener = _listener;
            if (listener == null)
                return;
            _listener = null;

            try { _cts.Cancel(); } catch (ObjectDisposedException) { }
            try { listener.Stop(); } catch (SocketException) { }

            foreach (var client in _clients.Values)
                client.Close(WebSocketFrames.CloseGoingAway, "Arrêt du serveur");

            // Laisse un court instant pour envoyer les trames de fermeture, puis coupe le reste.
            var deadline = DateTime.UtcNow.AddMilliseconds(500);
            while (_clients.Count > 0 && DateTime.UtcNow < deadline)
                Thread.Sleep(10);

            foreach (var client in _clients.Values)
                client.Terminate();

            LogInfo("Serveur WebSocket arrêté.");
        }

        public void Broadcast(string text)
        {
            foreach (var client in _clients.Values)
                client.Send(text);
        }

        public void Dispose() => Stop();

        internal void LogInfo(string message) => _logInfo?.Invoke(message);

        internal void LogError(string message) => _logError?.Invoke(message);

        internal void LogVerbose(string message)
        {
            if (_verbose)
                _logInfo?.Invoke(message);
        }

        internal void OnClientConnected(WsConnection connection)
        {
            // L'événement est levé avant l'ajout à la liste de diffusion : l'abonné peut envoyer
            // un message d'accueil qui sera garanti d'arriver avant toute diffusion.
            Raise(() => ClientConnected?.Invoke(connection));
            _clients[connection.Id] = connection;
            LogInfo($"Client #{connection.Id} connecté ({connection.RemoteEndPoint}), {_clients.Count} client(s).");
        }

        internal void OnClientDisconnected(WsConnection connection)
        {
            _clients.TryRemove(connection.Id, out _);
            LogInfo($"Client #{connection.Id} déconnecté, {_clients.Count} client(s).");
            Raise(() => ClientDisconnected?.Invoke(connection));
        }

        internal void OnMessageReceived(WsConnection connection, string text)
        {
            Raise(() => MessageReceived?.Invoke(connection, text));
        }

        private void Raise(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                // Un abonné défaillant ne doit jamais couper la connexion ni le serveur.
                LogError($"Erreur dans un gestionnaire d'événement : {exception}");
            }
        }

        private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient tcp;
                try
                {
                    tcp = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is ObjectDisposedException || exception is SocketException || exception is InvalidOperationException)
                {
                    return;
                }

                if (_clients.Count >= _maxClients)
                {
                    RefuseOverCapacity(tcp);
                    continue;
                }

                tcp.NoDelay = true;
                var connection = new WsConnection(this, tcp, Interlocked.Increment(ref _nextId));
                var ignored = connection.RunAsync();
            }
        }

        private void RefuseOverCapacity(TcpClient tcp)
        {
            LogError($"Connexion refusée : {_maxClients} clients déjà connectés.");

            // Hors de la boucle d'acceptation, pour ne pas la bloquer.
            Task.Run(async () =>
            {
                try
                {
                    var stream = tcp.GetStream();
                    using (var timeout = new CancellationTokenSource(1000))
                    {
                        // Lit la requête avant de répondre puis de fermer : fermer une connexion qui contient
                        // des données non lues provoque un reset TCP qui peut faire perdre la réponse.
                        await WebSocketHandshake.ReadRequestAsync(stream, timeout.Token).ConfigureAwait(false);

                        var bytes = Encoding.ASCII.GetBytes(WebSocketHandshake.ErrorResponse(503, "Service Unavailable"));
                        await stream.WriteAsync(bytes, 0, bytes.Length, timeout.Token).ConfigureAwait(false);
                        tcp.Client.Shutdown(SocketShutdown.Send);
                    }
                }
                catch
                {
                    // Le client est déjà parti ou trop lent.
                }
                finally
                {
                    tcp.Close();
                }
            });
        }
    }
}
