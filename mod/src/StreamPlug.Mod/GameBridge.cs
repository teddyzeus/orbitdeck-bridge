using System;
using KSP.Game;
using KSP.Sim.impl;
using UnityEngine;
using ILogger = ReduxLib.Logging.ILogger;

namespace StreamPlug.Mod
{
    /// <summary>
    /// Seul code qui touche l'API du jeu. Tourne sur le thread principal Unity.
    /// T1 : journalise l'altitude du véhicule actif une fois par seconde.
    /// </summary>
    public class GameBridge : MonoBehaviour
    {
        private const float LogIntervalSeconds = 1f;

        private float _nextLogTime;

        public ILogger Logger { get; set; }

        private void Update()
        {
            if (Time.unscaledTime < _nextLogTime)
                return;
            _nextLogTime = Time.unscaledTime + LogIntervalSeconds;

            try
            {
                LogAltitude();
            }
            catch (Exception exception)
            {
                // Ne jamais laisser une exception sortir d'Update() (cf. SPECS-TECHNIQUES §4.4)
                Logger?.LogError($"[StreamPlug] Erreur de lecture : {exception}");
            }
        }

        private void LogAltitude()
        {
            var manager = GameManager.Instance;
            if (manager == null || manager.Game == null || manager.Game.ViewController == null)
                return;

            VesselVehicle vehicle = manager.Game.ViewController.ActiveVesselVehicle;
            if (vehicle == null)
                return;

            Logger?.LogInfo(
                $"[StreamPlug] altitude mer={vehicle.AltitudeFromSeaLevel:F1} m, " +
                $"surface={vehicle.AltitudeFromSurface:F1} m, vitesse sol={vehicle.SurfaceSpeed:F1} m/s");
        }
    }
}
