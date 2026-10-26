using GameSpeedMode = KSP.Sim.SpeedDisplayMode;

namespace OrbitDeck.Mod.Telemetry
{
    /// <summary>Lecture de l'état du jeu via l'API de KSP 2 Redux (thread principal uniquement).</summary>
    public sealed class GameState : IGameState
    {
        public TelemetrySnapshot Read()
        {
            if (!GameAccess.TryGetFlightVessel(out var vehicle, out var vessel, out var gameStateName))
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
                RcsEnabled = vessel.IsRCSEnabled,
                LightsEnabled = GroupState(vessel, KSP.Sim.KSPActionGroup.Lights),
                BrakesEnabled = GroupState(vessel, KSP.Sim.KSPActionGroup.Brakes),
                GearEnabled = GroupState(vessel, KSP.Sim.KSPActionGroup.Gear)
            };
        }

        /// <summary>État d'un groupe d'actions : null quand le véhicule n'a aucune pièce dans ce groupe (pas de phares, pas de train…).</summary>
        private static bool? GroupState(KSP.Sim.impl.VesselComponent vessel, KSP.Sim.KSPActionGroup group)
        {
            var state = vessel.GetActionGroupState(group);
            return state == KSP.Sim.KSPActionGroupState.None ? (bool?)null : state == KSP.Sim.KSPActionGroupState.True;
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
