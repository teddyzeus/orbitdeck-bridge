using System;
using System.Globalization;
using OrbitDeck.Mod.Telemetry;
using UnityEngine;

namespace OrbitDeck.Ksp1
{
    /// <summary>
    /// Point d'entrée du mod, chargé une seule fois au démarrage du jeu. Étape K2 : échantillonne l'état du jeu
    /// et l'écrit dans le journal (pas encore de serveur ni de commandes).
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class OrbitDeckAddon : MonoBehaviour
    {
        private const float SampleIntervalSeconds = 0.1f;
        private const float HeartbeatLogSeconds = 2f;
        private const float ErrorLogSeconds = 5f;

        private readonly IGameState _gameState = new Ksp1GameState();
        private TelemetrySnapshot _last;
        private float _nextSample;
        private float _nextLog;
        private float _nextErrorLog;

        private void Awake()
        {
            Log($"Mod chargé (v{typeof(OrbitDeckAddon).Assembly.GetName().Version}).");
            Log($"Jeu : KSP {Versioning.GetVersionString()}, Unity {Application.unityVersion}.");
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            var now = Time.unscaledTime;
            if (now < _nextSample)
                return;
            _nextSample = now + SampleIntervalSeconds;

            try
            {
                var snapshot = _gameState.Read();
                var modeChange = SnapshotComparer.HasModeChange(_last, snapshot);
                if (modeChange || (now >= _nextLog && SnapshotComparer.HasChanged(_last, snapshot)))
                {
                    _nextLog = now + HeartbeatLogSeconds;
                    Log(Describe(snapshot));
                    _last = snapshot;
                }
            }
            catch (Exception exception)
            {
                // Ne jamais laisser une exception sortir d'Update() : elle serait répétée à chaque image.
                if (now >= _nextErrorLog)
                {
                    _nextErrorLog = now + ErrorLogSeconds;
                    Debug.LogError($"[OrbitDeck] Erreur de lecture : {exception}");
                }
            }
        }

        private static string Describe(TelemetrySnapshot s)
        {
            if (s.Status == VesselStatus.NoVessel)
                return $"AUCUN VÉHICULE (scène : {s.GameStateName ?? "?"})";

            var inv = CultureInfo.InvariantCulture;
            var vessel = FlightGlobals.ActiveVessel;
            return string.Format(inv,
                "{0} « {1} » | alt mer={2:F1} m sol(radar)={3:F1} m sol(terrain)={4:F1} m | vitesse({5}) sol={6:F1} orb={7:F1} cible={8} | gaz={9:P0} | SAS={10}({11}) | RCS={12}",
                s.Status, s.VesselName, s.AltitudeSeaLevel, s.AltitudeSurface,
                vessel != null ? vessel.heightFromTerrain : float.NaN,
                s.SpeedMode, s.SpeedSurface, s.SpeedOrbital,
                s.SpeedTarget.HasValue ? s.SpeedTarget.Value.ToString("F1", inv) : "-",
                s.Throttle, s.SasEnabled ? "on" : "off", s.SasMode ?? "-", s.RcsEnabled ? "on" : "off");
        }

        internal static void Log(string message)
        {
            Debug.Log($"[OrbitDeck] {message}");
        }
    }
}
