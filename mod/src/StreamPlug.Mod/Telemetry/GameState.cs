using KSP.Game;
using KSP.Sim.impl;
using GameSpeedMode = KSP.Sim.SpeedDisplayMode;

namespace StreamPlug.Mod.Telemetry
{
    /// <summary>Lecture de l'état du jeu via l'API de KSP 2 Redux (thread principal uniquement).</summary>
    public sealed class GameState : IGameState
    {
        public TelemetrySnapshot Read()
        {
            var manager = GameManager.Instance;
            if (manager == null || manager.Game == null || manager.Game.ViewController == null)
                return TelemetrySnapshot.NoVessel;

            // Hors vol (menu, centre spatial, éditeur…), le jeu peut conserver le dernier véhicule en mémoire :
            // l'état global du jeu fait foi.
            var stateConfig = manager.Game.GlobalGameState?.GetGameState();
            var gameStateName = stateConfig?.GameState.ToString();
            if (stateConfig != null && !stateConfig.IsFlightMode)
                return TelemetrySnapshot.NoVesselIn(gameStateName);

            var viewController = manager.Game.ViewController;
            VesselVehicle vehicle = viewController.ActiveVesselVehicle;
            if (vehicle == null || !viewController.TryGetActiveSimVessel(out VesselComponent vessel, true) || vessel == null)
                return TelemetrySnapshot.NoVesselIn(gameStateName);

            var autopilot = vessel.Autopilot;

            return new TelemetrySnapshot
            {
                Status = vessel.IsControllable ? VesselStatus.Connected : VesselStatus.NotControllable,
                GameStateName = gameStateName,
                VesselName = vessel.DisplayName,
                AltitudeSeaLevel = vehicle.AltitudeFromSeaLevel,
                AltitudeSurface = vehicle.AltitudeFromSurface,
                SpeedSurface = vehicle.SurfaceSpeed,
                SpeedOrbital = vehicle.OrbitalSpeed,
                SpeedTarget = vessel.TargetObject != null ? vehicle.TargetSpeed : (double?)null,
                SpeedMode = ToSpeedMode(vehicle.SpeedDisplayMode),
                Throttle = vehicle.mainThrottle,
                SasEnabled = autopilot != null && autopilot.Enabled,
                SasMode = autopilot != null ? autopilot.AutopilotMode.ToString() : null,
                RcsEnabled = vessel.IsRCSEnabled
            };
        }

        private static SpeedMode ToSpeedMode(GameSpeedMode mode)
        {
            switch (mode)
            {
                case GameSpeedMode.Orbit: return SpeedMode.Orbit;
                case GameSpeedMode.Target: return SpeedMode.Target;
                default: return SpeedMode.Surface;
            }
        }
    }
}
