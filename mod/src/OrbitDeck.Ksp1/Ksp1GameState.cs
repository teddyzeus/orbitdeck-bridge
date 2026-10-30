using System;
using System.Collections.Generic;
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
            // « ActivateNextStage » déclenche l'étage currentStage - 1 : c'est le numéro du prochain étage (les étages de KSP 1
            // vont jusqu'à 0, d'où le « - 1 »), celui qu'affiche la pile d'étagement.
            var stageCount = Math.Max(0, vessel.currentStage - 1);
            var time = TimeOf();

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
                GearEnabled = vessel.ActionGroups[KSPActionGroup.Gear],
                StageCount = stageCount,
                // Sur KSP 1, l'étage 0 est le dernier : la touche ne se grise que lorsqu'il n'y en a plus.
                StageExhausted = vessel.currentStage <= 0,
                View = MapView.MapIsEnabled ? ViewKind.Map : ViewKind.Ship,
                AvailableSasModes = AvailableSasModesOf(autopilot),
                Time = time,
                Maneuver = ManeuverOf(vessel, time)
            };
        }

        /// <summary>Plan de manœuvre ; null si illisible ou sans temps de jeu (le reste de l'état reste publié).</summary>
        private static ManeuverInfo ManeuverOf(Vessel vessel, TimeInfo time)
        {
            if (time == null)
                return null;

            try
            {
                return Ksp1GameManeuver.Read(vessel, time.Ut);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Temps du jeu ; null si illisible (le reste de l'état reste publié).</summary>
        private static TimeInfo TimeOf()
        {
            try
            {
                return Ksp1GameTime.TryGetFlight(out var timeWarp) ? Ksp1GameTime.Read(timeWarp) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Modes du SAS sélectionnables : les mêmes vérifications que la commande « setSasMode » (SAS utilisable avec ce pilote ou
        /// cette sonde, puis ce que le jeu accepte : cible, nœud de manœuvre, navball en mode orbite…).
        /// </summary>
        private static IReadOnlyList<SasModeKind> AvailableSasModesOf(VesselAutopilot autopilot)
        {
            var available = new List<SasModeKind>();
            if (autopilot == null || !autopilot.SAS.CanEngageSAS())
                return available;

            foreach (SasModeKind mode in Enum.GetValues(typeof(SasModeKind)))
            {
                if (autopilot.CanSetMode(Ksp1GameControls.ToGameMode(mode)))
                    available.Add(mode);
            }

            return available;
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
