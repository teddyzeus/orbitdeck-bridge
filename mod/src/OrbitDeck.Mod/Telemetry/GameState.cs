using System;
using System.Collections.Generic;
using OrbitDeck.Mod.Commands;
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
            var stageCount = StageCountOf(vessel);

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
                GearEnabled = GroupState(vessel, KSP.Sim.KSPActionGroup.Gear),
                StageCount = stageCount,
                // Comme le bouton d'étage du jeu, grisé quand il ne reste qu'un étage.
                StageExhausted = stageCount.HasValue ? stageCount.Value <= 1 : (bool?)null,
                AvailableSasModes = AvailableSasModesOf(vessel),
                View = GameAccess.IsMapView(gameStateName) ? ViewKind.Map : ViewKind.Ship
            };
        }

        /// <summary>État d'un groupe d'actions : null quand le véhicule n'a aucune pièce dans ce groupe (pas de phares, pas de train…).</summary>
        private static bool? GroupState(KSP.Sim.impl.VesselComponent vessel, KSP.Sim.KSPActionGroup group)
        {
            var state = vessel.GetActionGroupState(group);
            return state == KSP.Sim.KSPActionGroupState.None ? (bool?)null : state == KSP.Sim.KSPActionGroupState.True;
        }

        /// <summary>
        /// Modes du SAS sélectionnables : les mêmes vérifications que la commande « setSasMode » (une cible pour les modes
        /// cible, puis ce que le pilote automatique du jeu accepte : nœud de manœuvre, navball en mode orbite…).
        /// </summary>
        private static IReadOnlyList<SasModeKind> AvailableSasModesOf(KSP.Sim.impl.VesselComponent vessel)
        {
            var autopilot = vessel.Autopilot;
            var available = new List<SasModeKind>();
            if (autopilot == null)
                return available;

            foreach (SasModeKind mode in Enum.GetValues(typeof(SasModeKind)))
            {
                if ((mode == SasModeKind.Target || mode == SasModeKind.AntiTarget) && vessel.TargetObject == null)
                    continue;

                if (autopilot.CanSetMode(GameControls.ToGameMode(mode)))
                    available.Add(mode);
            }

            return available;
        }

        /// <summary>Nombre d'étages restants du véhicule ; null s'il n'a pas de pile d'étagement lisible.</summary>
        private static int? StageCountOf(KSP.Sim.impl.VesselComponent vessel)
        {
            var staging = vessel.SimulationObject?.Staging;
            return staging != null && staging.HaveStages ? staging.StageCount : (int?)null;
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
