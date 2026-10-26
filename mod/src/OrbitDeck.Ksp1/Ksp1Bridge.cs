using System;
using System.Globalization;
using System.Threading;
using OrbitDeck.Mod.Commands;
using OrbitDeck.Mod.Config;
using OrbitDeck.Mod.Server;
using OrbitDeck.Mod.Telemetry;
using UnityEngine;

namespace OrbitDeck.Ksp1
{
    /// <summary>
    /// Seul composant qui touche au jeu. Tourne sur le thread principal Unity : exécute les commandes reçues,
    /// échantillonne l'état et publie le dernier <see cref="TelemetrySnapshot"/> pour le serveur réseau.
    /// </summary>
    public class Ksp1Bridge : MonoBehaviour, ITelemetrySource
    {
        private const float VerboseLogIntervalSeconds = 1f;
        private const float ErrorLogIntervalSeconds = 5f;

        private readonly IGameState _gameState = new Ksp1GameState();
        private volatile TelemetrySnapshot _latest = TelemetrySnapshot.NoVessel;
        private long _sequence;

        private int _forcedSamples;
        private float _nextSampleTime;
        private float _nextVerboseLogTime;
        private float _nextErrorLogTime;

        public OrbitDeckConfig Config { get; set; } = new OrbitDeckConfig();

        /// <summary>Dernier snapshot publié (lisible depuis n'importe quel thread).</summary>
        public TelemetrySnapshot Latest => _latest;

        /// <summary>Numéro du dernier snapshot publié ; croît à chaque changement.</summary>
        public long Sequence => Interlocked.Read(ref _sequence);

        internal OrbitDeckServer Server { get; set; }

        internal CommandQueue Commands { get; set; }

        internal IGameControls Controls { get; set; } = new Ksp1GameControls();

        private void OnDestroy() => Server?.Stop();

        private void OnApplicationQuit() => Server?.Stop();

        private void Update()
        {
            try
            {
                // Les commandes d'abord, puis une relecture immédiate (et celle de l'image suivante, car certains
                // effets du jeu ne sont visibles qu'après sa mise à jour) pour que le Stream Deck voie le résultat.
                if (Commands != null && Commands.ProcessPending(Controls) > 0)
                    _forcedSamples = 2;

                var forced = _forcedSamples > 0;
                if (forced)
                    _forcedSamples--;

                var now = Time.unscaledTime;
                if (!forced && now < _nextSampleTime)
                    return;
                _nextSampleTime = now + 1f / Config.SampleRateHz;

                Sample(now);
            }
            catch (Exception exception)
            {
                // Ne jamais laisser une exception sortir d'Update() : elle serait répétée à chaque image.
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
            var sequence = Interlocked.Increment(ref _sequence);

            if (Config.VerboseLogging && (modeChange || now >= _nextVerboseLogTime))
            {
                _nextVerboseLogTime = now + VerboseLogIntervalSeconds;
                OrbitDeckAddon.Log(Describe(sequence, next));
            }
        }

        private void LogErrorThrottled(Exception exception)
        {
            var now = Time.unscaledTime;
            if (now < _nextErrorLogTime)
                return;
            _nextErrorLogTime = now + ErrorLogIntervalSeconds;
            Debug.LogError($"[OrbitDeck] Erreur de lecture : {exception}");
        }

        private static string Describe(long sequence, TelemetrySnapshot s)
        {
            var culture = CultureInfo.InvariantCulture;
            if (s.Status == VesselStatus.NoVessel)
                return string.Format(culture, "#{0} aucun véhicule (scène : {1})", sequence, s.GameStateName ?? "?");

            return string.Format(
                culture,
                "#{0} {1} « {2} » | alt mer={3:F1} sol={4:F1} m | vitesse sol={5:F1} orbite={6:F1} cible={7} (affichée : {8}) m/s | poussée={9:P0} | SAS={10} ({11}) | RCS={12}",
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
