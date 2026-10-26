using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SpaceWarp2.API.Mods;
using OrbitDeck.Mod.Commands;
using OrbitDeck.Mod.Config;
using OrbitDeck.Mod.Server;
using UnityEngine;

namespace OrbitDeck.Mod
{
    /// <summary>Point d'entrée du mod, chargé par le chargeur de mods de Redux.</summary>
    public class OrbitDeckMod : GeneralMod
    {
        private const int MaxRestarts = 5;
        private const long RestartWindowMs = 30_000;
        private const int BindAttempts = 3;
        private const int BindRetryDelayMs = 1000;

        private GameObject _host;
        private OrbitDeckConfig _config;
        private SynchronizationContext _mainThread;
        private readonly RestartGuard _restarts = new RestartGuard(MaxRestarts, RestartWindowMs);
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        public override void OnInitialized()
        {
            var modFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _config = OrbitDeckConfig.Load(
                Path.Combine(modFolder ?? ".", OrbitDeckConfig.FileName),
                message => SWLogger.LogWarning($"[OrbitDeck] {message}"));

            SWLogger.LogInfo(
                $"[OrbitDeck] Mod chargé (port {_config.Port}, {_config.SampleRateHz} Hz, logs détaillés : {_config.VerboseLogging}).");

            // Contexte du thread principal : permet de recréer le pont après que le jeu l'a détruit (voir OnHostDestroyed).
            _mainThread = SynchronizationContext.Current;
            StartBridge(BindAttempts);
        }

        /// <summary>Crée l'objet du mod, son serveur WebSocket et les relie. Réessaie si le port tarde à se libérer.</summary>
        private void StartBridge(int attemptsLeft)
        {
            var config = _config;

            _host = new GameObject("OrbitDeck");
            UnityEngine.Object.DontDestroyOnLoad(_host);
            var bridge = _host.AddComponent<GameBridge>();
            bridge.Logger = SWLogger;
            bridge.Config = config;
            bridge.HostDestroyed = OnHostDestroyed;

            var webSocket = new WsServer(
                config.Port,
                verbose: config.VerboseLogging,
                logInfo: message => SWLogger.LogInfo($"[OrbitDeck] {message}"),
                logError: message => SWLogger.LogError($"[OrbitDeck] {message}"));

            var commands = new CommandQueue(message => SWLogger.LogError($"[OrbitDeck] {message}"));
            if (config.VerboseLogging)
            {
                commands.Processed = (pending, result) => SWLogger.LogInfo(
                    $"[OrbitDeck] commande {pending.Id} {pending.Command.Kind} → "
                    + (result.Success ? "ok" : $"{result.ErrorCode} ({result.Message})"));
            }

            bridge.Commands = commands;

            var server = new OrbitDeckServer(
                webSocket,
                bridge,
                commands,
                modVersion: Assembly.GetExecutingAssembly().GetName().Version.ToString(3),
                gameVersion: Application.version,
                logError: message => SWLogger.LogError($"[OrbitDeck] {message}"));

            if (server.Start())
            {
                bridge.Server = server;
                return;
            }

            // Après un redémarrage, le port peut mettre un instant à se libérer : on réessaie avant d'abandonner.
            if (attemptsLeft > 1 && _mainThread != null)
            {
                bridge.HostDestroyed = null;
                UnityEngine.Object.Destroy(_host);
                SWLogger.LogWarning($"[OrbitDeck] Port {config.Port} indisponible, nouvel essai dans {BindRetryDelayMs} ms.");
                Task.Delay(BindRetryDelayMs).ContinueWith(_ => _mainThread.Post(__ => StartBridgeSafely(attemptsLeft - 1), null));
                return;
            }

            SWLogger.LogError($"[OrbitDeck] Le serveur n'a pas pu démarrer (port {config.Port}). Le mod continue sans Stream Deck.");
        }

        private void StartBridgeSafely(int attemptsLeft)
        {
            try
            {
                StartBridge(attemptsLeft);
            }
            catch (Exception exception)
            {
                SWLogger.LogError($"[OrbitDeck] Redémarrage du pont impossible : {exception}");
            }
        }

        /// <summary>
        /// Le jeu détruit l'objet du mod à la fin de « Game Shutdown » (retour au menu principal), ce qui arrêtait le
        /// serveur jusqu'au prochain lancement du jeu. On recrée donc le pont dès que possible, sur le thread principal.
        /// </summary>
        private void OnHostDestroyed()
        {
            if (_mainThread == null)
            {
                SWLogger.LogError("[OrbitDeck] L'objet du mod a été détruit par le jeu et ne peut pas être recréé : le Stream Deck ne sera plus servi avant le prochain lancement.");
                return;
            }

            if (!_restarts.TryRestart(_clock.ElapsedMilliseconds))
            {
                SWLogger.LogError($"[OrbitDeck] L'objet du mod est détruit en boucle ({MaxRestarts} fois en {RestartWindowMs / 1000} s) : abandon du redémarrage.");
                return;
            }

            SWLogger.LogWarning("[OrbitDeck] L'objet du mod a été détruit par le jeu : redémarrage du serveur.");
            _mainThread.Post(_ => StartBridgeSafely(BindAttempts), null);
        }
    }
}
