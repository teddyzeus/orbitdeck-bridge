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

            var viewController = manager.Game.ViewController;
            VesselVehicle vehicle = viewController.ActiveVesselVehicle;
            if (vehicle == null || !viewController.TryGetActiveSimVessel(out VesselComponent vessel, true) || vessel == null)
                return TelemetrySnapshot.NoVessel;

            var autopilot = vessel.Autopilot;

            return new TelemetrySnapshot
            {
                Status = vessel.IsControllable ? VesselStatus.Connected : VesselStatus.NotControllable,
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
