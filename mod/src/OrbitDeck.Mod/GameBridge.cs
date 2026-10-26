using System;
using System.Globalization;
using OrbitDeck.Mod.Commands;
using OrbitDeck.Mod.Config;
using OrbitDeck.Mod.Server;
using OrbitDeck.Mod.Telemetry;
using UnityEngine;
using ILogger = ReduxLib.Logging.ILogger;

namespace OrbitDeck.Mod
{
    /// <summary>
    /// Seul composant qui échantillonne le jeu. Tourne sur le thread principal Unity et publie
    /// le dernier <see cref="TelemetrySnapshot"/> pour les autres threads (serveur réseau, T3).
    /// </summary>
    public class GameBridge : MonoBehaviour, ITelemetrySource
    {
        private const float VerboseLogIntervalSeconds = 1f;
        private const float ErrorLogIntervalSeconds = 5f;

        private IGameState _gameState = new GameState();
        private GameEventWatcher _watcher;
        private volatile TelemetrySnapshot _latest = TelemetrySnapshot.NoVessel;
        private long _sequence;

        private int _forcedSamples;
        private float _nextSampleTime;
        private float _nextVerboseLogTime;
        private float _nextErrorLogTime;

        public ILogger Logger { get; set; }

        public OrbitDeckConfig Config { get; set; } = new OrbitDeckConfig();

        /// <summary>Dernier snapshot publié (lisible depuis n'importe quel thread).</summary>
        public TelemetrySnapshot Latest => _latest;

        /// <summary>Numéro du dernier snapshot publié ; croît à chaque changement.</summary>
        public long Sequence => System.Threading.Interlocked.Read(ref _sequence);

        /// <summary>Remplace la source de données (tests).</summary>
        internal void UseGameState(IGameState gameState) => _gameState = gameState;

        private void Awake()
        {
            _watcher = new GameEventWatcher();
        }

        /// <summary>Serveur à arrêter proprement quand le jeu se ferme ou que le composant est détruit.</summary>
        internal OrbitDeckServer Server { get; set; }

        /// <summary>Commandes reçues du Stream Deck, exécutées ici sur le thread principal.</summary>
        internal CommandQueue Commands { get; set; }

        /// <summary>Remplace l'exécuteur de commandes (tests).</summary>
        internal IGameControls Controls { get; set; } = new GameControls();

        /// <summary>
        /// Appelé quand le jeu détruit ce composant alors qu'il continue de tourner (par exemple au retour au menu
        /// principal, à la fin de « Game Shutdown ») : le serveur est alors arrêté, et le mod doit le relancer.
        /// </summary>
        internal Action HostDestroyed { get; set; }

        private bool _quitting;

        private void OnDestroy()
        {
            _watcher?.Dispose();
            Server?.Stop();

            if (!_quitting)
                HostDestroyed?.Invoke();
        }

        private void OnApplicationQuit()
        {
            _quitting = true;
            Server?.Stop();
        }

        private void Update()
        {
            try
            {
                _watcher.Refresh();

                // Les commandes d'abord, puis une relecture immédiate (et celle de l'image suivante, car certains
                // effets du jeu ne sont visibles qu'après sa mise à jour) pour que le Stream Deck voie le résultat.
                if (Commands != null && Commands.ProcessPending(Controls) > 0)
                    _forcedSamples = 2;

                var eventPending = _watcher.ConsumeEvent() || _forcedSamples > 0;
                if (_forcedSamples > 0)
                    _forcedSamples--;

                var now = Time.unscaledTime;
                if (!eventPending && now < _nextSampleTime)
                    return;
                _nextSampleTime = now + 1f / Config.SampleRateHz;

                Sample(now);
            }
            catch (Exception exception)
            {
                // Ne jamais laisser une exception sortir d'Update() (cf. SPECS-TECHNIQUES §4.4)
                LogErrorThrottled(exception);
            }
        }

        private void Sample(float now)
        {
            var next = _gameState.Read();
            var previous = _latest;

            if (!SnapshotComparer.HasChanged(previous, next))
                return;

            // Seules les bascules (SAS, RCS, véhicule, mode…) sont journalisées immédiatement ;
            // la poussée et les valeurs continues le sont au plus une fois par seconde.
            var modeChange = SnapshotComparer.HasModeChange(previous, next);
            _latest = next;
            var sequence = System.Threading.Interlocked.Increment(ref _sequence);

            if (Config.VerboseLogging && (modeChange || now >= _nextVerboseLogTime))
            {
                _nextVerboseLogTime = now + VerboseLogIntervalSeconds;
                Logger?.LogInfo(Describe(sequence, next));
            }
        }

        private void LogErrorThrottled(Exception exception)
        {
            var now = Time.unscaledTime;
            if (now < _nextErrorLogTime)
                return;
            _nextErrorLogTime = now + ErrorLogIntervalSeconds;
            Logger?.LogError($"[OrbitDeck] Erreur de lecture : {exception}");
        }

        private static string Describe(long sequence, TelemetrySnapshot s)
        {
            var culture = CultureInfo.InvariantCulture;
            if (s.Status == VesselStatus.NoVessel)
                return string.Format(culture, "[OrbitDeck] #{0} aucun véhicule (état du jeu : {1})", sequence, s.GameStateName ?? "?");

            return string.Format(
                culture,
                "[OrbitDeck] #{0} {1} '{2}' (état du jeu : " + (s.GameStateName ?? "?") + ") | alt mer={3:F1} surface={4:F1} m | vitesse sol={5:F1} orbite={6:F1} cible={7} (affichée: {8}) m/s | poussée={9:P0} | SAS={10} ({11}) | RCS={12}",
                sequence,
                s.Status,
                s.VesselName,
                s.AltitudeSeaLevel,
                s.AltitudeSurface,
                s.SpeedSurface,
                s.SpeedOrbital,
                s.SpeedTarget.HasValue ? s.SpeedTarget.Value.ToString("F1", culture) : "-",
                s.SpeedMode,
                s.Throttle,
                s.SasEnabled ? "on" : "off",
                s.SasMode ?? "-",
                s.RcsEnabled ? "on" : "off");
        }
    }
}
