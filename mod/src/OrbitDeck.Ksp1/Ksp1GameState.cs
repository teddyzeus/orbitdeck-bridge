using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Ksp1
{
    /// <summary>Lecture de l'état de KSP 1 (thread principal Unity uniquement).</summary>
    public sealed class Ksp1GameState : IGameState
    {
        public TelemetrySnapshot Read()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return TelemetrySnapshot.NoVesselIn(HighLogic.LoadedScene.ToString());

            var vessel = FlightGlobals.ActiveVessel;
            if (vessel == null)
                return TelemetrySnapshot.NoVesselIn(HighLogic.LoadedScene.ToString());

            var autopilot = vessel.Autopilot;

            return new TelemetrySnapshot
            {
                Status = vessel.IsControllable ? VesselStatus.Connected : VesselStatus.NotControllable,
                GameStateName = HighLogic.LoadedScene.ToString(),
                VesselName = vessel.GetDisplayName(),
                AltitudeSeaLevel = vessel.altitude,
                AltitudeSurface = vessel.radarAltitude,
                SpeedSurface = vessel.srfSpeed,
                SpeedOrbital = vessel.obt_speed,
                SpeedTarget = vessel.targetObject != null ? FlightGlobals.ship_tgtSpeed : (double?)null,
                SpeedMode = ToSpeedMode(FlightGlobals.speedDisplayMode),
                Throttle = FlightInputHandler.state.mainThrottle,
                SasEnabled = autopilot != null && autopilot.Enabled,
                SasMode = autopilot != null ? autopilot.Mode.ToString() : null,
                RcsEnabled = vessel.ActionGroups[KSPActionGroup.RCS],
                LightsEnabled = vessel.ActionGroups[KSPActionGroup.Light],
                BrakesEnabled = vessel.ActionGroups[KSPActionGroup.Brakes],
                GearEnabled = vessel.ActionGroups[KSPActionGroup.Gear]
            };
        }

        private static SpeedMode ToSpeedMode(FlightGlobals.SpeedDisplayModes mode)
        {
            switch (mode)
            {
                case FlightGlobals.SpeedDisplayModes.Orbit: return SpeedMode.Orbit;
                case FlightGlobals.SpeedDisplayModes.Target: return SpeedMode.Target;
                default: return SpeedMode.Surface;
            }
        }
    }
}
