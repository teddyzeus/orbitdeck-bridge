using System;
using System.Collections.Generic;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Ksp1
{
    /// <summary>Lecture de l'état de KSP 1 (thread principal Unity uniquement).</summary>
    public sealed class Ksp1GameState : IGameState
    {
        private static readonly SafeReader Safe = new SafeReader(() => UnityEngine.Time.unscaledTime, null);

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
                Maneuver = ManeuverOf(vessel, time),
                // 2.0 : chaque nouveau bloc est lu à part ; une panne l'omet sans toucher au reste de l'état.
                Orbit = time == null ? null : Safe.Read("orbit", () => Ksp1GameOrbit.Read(vessel, time.Ut)),
                // Blocs lourds (parcours des pièces) : au plus une lecture par seconde.
                Resources = Safe.Read("resources", () => SlowResources.Get(() => Ksp1GameBlocks.Resources(vessel))),
                Systems = Safe.Read("systems", () => SlowSystems.Get(() => Ksp1GameBlocks.Systems(vessel))),
                Docking = Safe.Read("docking", () => SlowDocking.Get(() => Ksp1GameBlocks.Docking(vessel))),
                Performance = Safe.Read("performance", () => SlowPerformance.Get(() => Ksp1GameBlocks.Performance(vessel))),
                Landing = time == null ? null : Safe.Read("landing", () => Ksp1GameBlocks.Landing(vessel, time.Ut)),
                Target = Safe.Read("target", () => Ksp1GameBlocks.Target(vessel))
            };
        }

        private static readonly SlowRead<ResourcesInfo> SlowResources = new SlowRead<ResourcesInfo>(() => UnityEngine.Time.unscaledTime);
        private static readonly SlowRead<SystemsInfo> SlowSystems = new SlowRead<SystemsInfo>(() => UnityEngine.Time.unscaledTime);
        private static readonly SlowRead<DockingInfo> SlowDocking = new SlowRead<DockingInfo>(() => UnityEngine.Time.unscaledTime);
        private static readonly SlowRead<PerformanceInfo> SlowPerformance = new SlowRead<PerformanceInfo>(() => UnityEngine.Time.unscaledTime, 0.25);

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
