using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OrbitDeck.Mod.Commands;
using OrbitDeck.Mod.Protocol;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Mod.Server
{
    /// <summary>
    /// Relie le serveur WebSocket au flux de télémétrie : accueille les clients (hello + état courant),
    /// diffuse les changements (10 messages/s au plus, battement de cœur toutes les secondes)
    /// et traite les messages entrants. Ne touche jamais à l'API du jeu.
    /// </summary>
    public sealed class OrbitDeckServer : IDisposable
    {
        public const int MinStateIntervalMs = 100;
        public const int HeartbeatIntervalMs = 1000;
        private const int TickIntervalMs = 25;

        private readonly WsServer _ws;
        private readonly ITelemetrySource _source;
        private readonly CommandQueue _commands;
        private readonly string _modVersion;
        private readonly string _gameVersion;
        private readonly Func<long> _clockMs;
        private readonly Action<string> _logError;
        private readonly object _sync = new object();
        private CancellationTokenSource _cts;
        private long _lastSentSequence = -1;
        private long _lastSendMs = long.MinValue / 2;

        public OrbitDeckServer(
            WsServer ws,
            ITelemetrySource source,
            CommandQueue commands,
            string modVersion,
            string gameVersion,
            Func<long> clockMs = null,
            Action<string> logError = null)
        {
            _ws = ws;
            _source = source;
            _commands = commands;
            _modVersion = modVersion;
            _gameVersion = gameVersion;
            _clockMs = clockMs ?? DefaultClock();
            _logError = logError;
        }

        public bool IsRunning => _cts != null;

        /// <summary>Démarre le serveur WebSocket puis la boucle de diffusion. Retourne false si le port est indisponible.</summary>
        public bool Start()
        {
            if (_cts != null)
                return true;

            _ws.ClientConnected += OnClientConnected;
            _ws.MessageReceived += OnMessageReceived;

            if (!_ws.Start())
            {
                _ws.ClientConnected -= OnClientConnected;
                _ws.MessageReceived -= OnMessageReceived;
                return false;
            }

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            Task.Run(() => PublishLoopAsync(token));
            return true;
        }

        public void Stop()
        {
            if (_cts == null)
                return;

            _cts.Cancel();
            _cts = null;
            _ws.ClientConnected -= OnClientConnected;
            _ws.MessageReceived -= OnMessageReceived;
            _ws.Stop();
        }

        public void Dispose() => Stop();

        /// <summary>Un cycle de diffusion. Appelé en boucle par le serveur ; public pour les tests.</summary>
        public void Tick()
        {
            lock (_sync)
            {
                if (_ws.ClientCount == 0)
                    return;

                var now = _clockMs();
                var elapsed = now - _lastSendMs;
                var sequence = _source.Sequence;
                var changed = sequence != _lastSentSequence;

                if ((changed && elapsed >= MinStateIntervalMs) || elapsed >= HeartbeatIntervalMs)
                {
                    _ws.Broadcast(ProtocolMessages.State(_source.Latest, sequence));
                    _lastSentSequence = sequence;
                    _lastSendMs = now;
                }
            }
        }

        private async Task PublishLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    Tick();
                    await Task.Delay(TickIntervalMs, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception exception)
                {
                    _logError?.Invoke($"Erreur de diffusion : {exception}");
                    await Task.Delay(1000).ConfigureAwait(false);
                }
            }
        }

        // Appelé avant que le client ne soit ajouté à la liste de diffusion : il reçoit donc toujours
        // `hello` puis un état complet avant tout autre message.
        private void OnClientConnected(IWsClient client)
        {
            lock (_sync)
            {
                var sequence = _source.Sequence;
                client.Send(ProtocolMessages.Hello(_modVersion, _gameVersion));
                client.Send(ProtocolMessages.State(_source.Latest, sequence));

                // Cet état vient d'être envoyé : inutile de le rediffuser à tout le monde au tick suivant.
                _lastSentSequence = sequence;
                _lastSendMs = _clockMs();
            }
        }

        private void OnMessageReceived(IWsClient client, string text)
        {
            var message = ProtocolParser.Parse(text);
            if (!message.IsValid)
            {
                client.Send(ProtocolMessages.Error(message.Id, message.ErrorCode, message.ErrorMessage));
                return;
            }

            switch (message.Kind)
            {
                case ClientMessageKind.Ping:
                    client.Send(ProtocolMessages.Pong());
                    break;

                case ClientMessageKind.Command:
                    HandleCommand(client, message);
                    break;
            }
        }

        // Validé ici (thread réseau), exécuté plus tard sur le thread principal du jeu, qui répond ack ou error.
        private void HandleCommand(IWsClient client, ParsedMessage message)
        {
            var validation = CommandValidator.Validate(message.CommandName, message.Args);
            if (!validation.IsValid)
            {
                client.Send(ProtocolMessages.Error(message.Id, validation.ErrorCode, validation.ErrorMessage));
                return;
            }

            if (!_commands.TryEnqueue(new PendingCommand(message.Id, validation.Command, client.Send)))
                client.Send(ProtocolMessages.Error(message.Id, ErrorCodes.Internal, "File de commandes pleine, réessayez."));
        }

        private static Func<long> DefaultClock()
        {
            var stopwatch = Stopwatch.StartNew();
            return () => stopwatch.ElapsedMilliseconds;
        }
    }
}
