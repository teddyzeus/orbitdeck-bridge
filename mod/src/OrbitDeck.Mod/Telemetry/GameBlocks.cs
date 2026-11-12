using System;
using System.Collections.Generic;
using KSP.Game;
using KSP.Modules;
using KSP.Sim.DeltaV;
using KSP.Sim.impl;
using KSP.Sim.ResourceSystem;

namespace OrbitDeck.Mod.Telemetry
{
    /// <summary>
    /// Lecture des blocs 2.0 de KSP 2 : ressources, performance, atterrissage, cible, systèmes, amarrage. Thread principal uniquement.
    /// Chaque méthode peut lever une exception : l'appelant (<c>SafeReader</c>) omet alors le bloc sans toucher au reste de l'état.
    /// </summary>
    internal static class GameBlocks
    {
        private static readonly string[] ResourceNames = { "LiquidFuel", "Methane", "Methalox", "Oxidizer", "ElectricCharge", "MonoPropellant" };

        private static readonly ApproachRate Approach = new ApproachRate();
        private static string _approachTarget;

        public static ResourcesInfo Resources(VesselComponent vessel)
        {
            var group = vessel.SimulationObject?.PartOwner?.ContainerGroup;
            var database = GameManager.Instance?.Game?.ResourceDefinitionDatabase;
            if (group == null || database == null)
                return null;

            var tally = new ResourceTally();
            foreach (var name in ResourceNames)
            {
                ResourceDefinitionID id;
                try
                {
                    id = database.GetResourceIDFromName(name);
                }
                catch (Exception)
                {
                    continue;
                }

                tally.Add(name, group.GetResourceStoredUnits(id), group.GetResourceCapacityUnits(id));
            }

            // « Étage courant » : non lu pour l'instant (le jeu ne donne pas de ressources par étage sans parcourir l'étagement).
            return new ResourcesInfo { Vessel = tally.ToSet() };
        }

        public static PerformanceInfo Performance(VesselComponent vessel)
        {
            var deltaV = vessel.VesselDeltaV;
            if (deltaV == null)
                return null;

            DeltaVStageInfo current = null;
            if (deltaV.StageInfo != null)
            {
                foreach (var info in deltaV.StageInfo)
                {
                    if (info == null || info.PayloadStage || info.EnginesInStage == null || info.EnginesInStage.Count == 0)
                        continue;
                    if (current == null || info.Stage > current.Stage)
                        current = info;
                }
            }

            double? Finite(double value) => double.IsNaN(value) || double.IsInfinity(value) ? (double?)null : value;
            var hasActiveEngine = current?.EnginesActiveInStage != null && current.EnginesActiveInStage.Count > 0;
            return new PerformanceInfo
            {
                Dv = Finite(deltaV.TotalDeltaVActual),
                DvStage = current != null ? Finite(current.DeltaVActual) : null,
                Twr = hasActiveEngine ? Finite(current.TWRActual) : null
            };
        }

        public static LandingInfo Landing(VesselComponent vessel, double ut)
        {
            double? radar = vessel.AltitudeFromTerrain;
            if (radar.HasValue && (double.IsNaN(radar.Value) || radar.Value < 0))
                radar = null;

            var vspeed = vessel.VerticalSrfSpeed;
            var body = vessel.Orbit?.referenceBody;
            double? gravity = body != null && body.radius > 0 ? body.gravParameter / (body.radius * body.radius) : (double?)null;
            return new LandingInfo
            {
                VSpeed = vspeed,
                HSpeed = vessel.HorizontalSrfSpeed,
                Radar = radar,
                ImpactUt = ImpactEstimator.ImpactUt(ut, radar, vspeed, gravity)
            };
        }

        public static TargetInfo Target(VesselComponent vessel, double ut)
        {
            if (!vessel.HasTargetObject || vessel.TargetObject == null)
            {
                Approach.Reset();
                _approachTarget = null;
                return new TargetInfo { None = true };
            }

            var target = vessel.TargetObject;
            var distance = KSP.Sim.Position.Distance(vessel.SimulationObject.Position, target.Position);
            if (_approachTarget != target.Name)
            {
                Approach.Reset();
                _approachTarget = target.Name;
            }

            return new TargetInfo { Name = target.Name, Distance = distance, Rel = Approach.Update(ut, distance) };
        }

        public static SystemsInfo Systems(VesselComponent vessel)
        {
            var owner = vessel.SimulationObject?.PartOwner;
            if (owner == null)
                return null;

            var solar = new DeployTally();
            var antennas = new DeployTally();
            var chutes = new ChuteTally();
            foreach (var part in owner.Parts)
            {
                if (SolarData(part, out var panel))
                    AddDeployable(solar, panel);
                else if (part.TryGetModule<PartComponentModule_DataTransmitter>(out _) && part.TryGetModuleData<PartComponentModule_Deployable, Data_Deployable>(out var antenna))
                    AddDeployable(antennas, antenna);

                if (part.TryGetModuleData<PartComponentModule_Parachute, Data_Parachute>(out var chute))
                    chutes.Add(ChuteOf(chute));
            }

            return new SystemsInfo
            {
                Solar = solar.ToCategory(),
                Antennas = antennas.ToCategory(),
                Ladders = null, // KSP 2 n'a pas d'échelle commandable : catégorie omise.
                Parachutes = chutes.ToCategory()
            };
        }

        /// <summary>Les dix groupes d'actions personnalisés du jeu, dans l'ordre (rang 0 = groupe 1).</summary>
        public static readonly KSP.Sim.KSPActionGroup[] CustomGroups =
        {
            KSP.Sim.KSPActionGroup.Custom01, KSP.Sim.KSPActionGroup.Custom02, KSP.Sim.KSPActionGroup.Custom03, KSP.Sim.KSPActionGroup.Custom04, KSP.Sim.KSPActionGroup.Custom05,
            KSP.Sim.KSPActionGroup.Custom06, KSP.Sim.KSPActionGroup.Custom07, KSP.Sim.KSPActionGroup.Custom08, KSP.Sim.KSPActionGroup.Custom09, KSP.Sim.KSPActionGroup.Custom10
        };

        /// <summary>État des groupes d'actions personnalisés ; `None` du jeu = aucune pièce dans le groupe.</summary>
        public static ActionGroupsInfo ActionGroups(VesselComponent vessel)
        {
            var custom = new ActionGroupState[ActionGroupsInfo.Count];
            for (var i = 0; i < custom.Length; i++)
            {
                switch (vessel.GetActionGroupState(CustomGroups[i]))
                {
                    case KSP.Sim.KSPActionGroupState.True:
                        custom[i] = ActionGroupState.On;
                        break;
                    case KSP.Sim.KSPActionGroupState.Mixed:
                        custom[i] = ActionGroupState.Mixed;
                        break;
                    case KSP.Sim.KSPActionGroupState.None:
                        custom[i] = ActionGroupState.None;
                        break;
                    default:
                        custom[i] = ActionGroupState.Off;
                        break;
                }
            }

            return new ActionGroupsInfo { Custom = custom };
        }

        public static DockingInfo Docking(VesselComponent vessel)
        {
            var owner = vessel.SimulationObject?.PartOwner;
            if (owner == null)
                return null;

            var tally = new DockTally();
            foreach (var part in owner.Parts)
            {
                if (!part.TryGetModule<PartComponentModule_DockingNode>(out var node))
                    continue;

                switch (node.GetCurrentState())
                {
                    case Data_DockingNode.DockingState.Docked:
                        tally.Add(DockingPortState.Docked);
                        break;
                    case Data_DockingNode.DockingState.Acquire_Dockee:
                    case Data_DockingNode.DockingState.Acquire_Docker:
                        tally.Add(DockingPortState.Approach);
                        break;
                    case Data_DockingNode.DockingState.Ready:
                        tally.Add(DockingPortState.Armed);
                        break;
                    default:
                        tally.Add(DockingPortState.Disarmed);
                        break;
                }
            }

            // Pas d'armement commandable pour l'instant : la fonction « Amarrage » est en lecture seule.
            return tally.ToInfo(false);
        }

        /// <summary>Données de déploiement du panneau solaire d'une pièce (recherchées par le module solaire, puis par le module déployable).</summary>
        internal static bool SolarData(PartComponent part, out Data_Deployable data)
        {
            data = null;
            if (!part.TryGetModule<PartComponentModule_SolarPanel>(out _))
                return false;

            return part.TryGetModuleData<PartComponentModule_SolarPanel, Data_Deployable>(out data)
                   || part.TryGetModuleData<PartComponentModule_Deployable, Data_Deployable>(out data);
        }

        private static void AddDeployable(DeployTally tally, Data_Deployable data)
        {
            DeployPhase phase;
            if (data.IsExtended)
                phase = DeployPhase.Extended;
            else if (data.IsRetracted)
                phase = DeployPhase.Retracted;
            else if (data.IsExtending || data.IsRetracting)
                phase = DeployPhase.Moving;
            else
                phase = DeployPhase.Broken;

            tally.Add(phase, data.retractable);
        }

        private static ChutePhase ChuteOf(Data_Parachute data)
        {
            switch (data.deployState.GetValue())
            {
                case Data_Parachute.DeploymentStates.CUT: return ChutePhase.Cut;
                case Data_Parachute.DeploymentStates.ARMED:
                case Data_Parachute.DeploymentStates.SEMIDEPLOYED:
                case Data_Parachute.DeploymentStates.DEPLOYED: return ChutePhase.Deployed;
                default: return ChutePhase.Ready;
            }
        }
    }
}
