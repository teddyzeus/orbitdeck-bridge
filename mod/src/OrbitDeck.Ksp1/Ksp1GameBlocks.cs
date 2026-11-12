using System;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Ksp1
{
    /// <summary>
    /// Lecture des blocs 2.0 de KSP 1 : ressources, performance, atterrissage, cible, systèmes, amarrage. Thread principal uniquement.
    /// Une exception omet le bloc (voir <c>SafeReader</c>) sans toucher au reste de l'état.
    /// </summary>
    internal static class Ksp1GameBlocks
    {
        public static ResourcesInfo Resources(Vessel vessel)
        {
            var tally = new ResourceTally();
            foreach (var part in vessel.parts)
            {
                foreach (PartResource resource in part.Resources)
                    tally.Add(resource.resourceName, resource.amount, resource.maxAmount);
            }

            return new ResourcesInfo { Vessel = tally.ToSet() };
        }

        public static PerformanceInfo Performance(Vessel vessel)
        {
            var deltaV = vessel.VesselDeltaV;
            if (deltaV == null)
                return null;

            double? Finite(double value) => double.IsNaN(value) || double.IsInfinity(value) ? (double?)null : value;
            var stage = deltaV.GetStage(vessel.currentStage);
            return new PerformanceInfo
            {
                Dv = Finite(deltaV.TotalDeltaVActual),
                DvStage = stage != null ? Finite(stage.deltaVActual) : null,
                Twr = stage != null && stage.TWRActual > 0 ? Finite(stage.TWRActual) : null
            };
        }

        public static LandingInfo Landing(Vessel vessel, double ut)
        {
            double? radar = vessel.heightFromTerrain >= 0 ? vessel.heightFromTerrain : (double?)null;
            var body = vessel.mainBody;
            double? gravity = body != null && body.Radius > 0 ? body.gravParameter / (body.Radius * body.Radius) : (double?)null;
            var vspeed = vessel.verticalSpeed;
            return new LandingInfo
            {
                VSpeed = vspeed,
                HSpeed = vessel.horizontalSrfSpeed,
                Radar = radar,
                ImpactUt = ImpactEstimator.ImpactUt(ut, radar, vspeed, gravity)
            };
        }

        public static TargetInfo Target(Vessel vessel)
        {
            var target = vessel.targetObject;
            if (target == null)
                return new TargetInfo { None = true };

            var offset = target.GetTransform().position - vessel.transform.position;
            var distance = (double)offset.magnitude;
            double? rel = null;
            if (distance > 1e-3)
            {
                var direction = offset / (float)distance;
                var velocity = FlightGlobals.ship_tgtVelocity;
                // Vitesse relative du véhicule par rapport à la cible : on s'approche quand elle est dirigée vers la cible.
                rel = -(velocity.x * direction.x + velocity.y * direction.y + velocity.z * direction.z);
            }

            return new TargetInfo { Name = target.GetDisplayName(), Distance = distance, Rel = rel };
        }

        public static SystemsInfo Systems(Vessel vessel)
        {
            var solar = new DeployTally();
            var antennas = new DeployTally();
            var ladders = new DeployTally();
            var chutes = new ChuteTally();
            foreach (var part in vessel.parts)
            {
                foreach (PartModule module in part.Modules)
                {
                    if (module is ModuleDeployableSolarPanel panel)
                        solar.Add(PhaseOf(panel.deployState), panel.retractable);
                    else if (module is ModuleDeployableAntenna antenna)
                        antennas.Add(PhaseOf(antenna.deployState), antenna.retractable);
                    else if (module is RetractableLadder ladder)
                        ladders.Add(LadderPhase(ladder), true);
                    else if (module is ModuleParachute chute)
                        chutes.Add(ChuteOf(chute.deploymentState));
                }
            }

            return new SystemsInfo { Solar = solar.ToCategory(), Antennas = antennas.ToCategory(), Ladders = ladders.ToCategory(), Parachutes = chutes.ToCategory() };
        }

        /// <summary>Les dix groupes d'actions personnalisés du jeu, dans l'ordre (rang 0 = groupe 1).</summary>
        public static readonly KSPActionGroup[] CustomGroups =
        {
            KSPActionGroup.Custom01, KSPActionGroup.Custom02, KSPActionGroup.Custom03, KSPActionGroup.Custom04, KSPActionGroup.Custom05,
            KSPActionGroup.Custom06, KSPActionGroup.Custom07, KSPActionGroup.Custom08, KSPActionGroup.Custom09, KSPActionGroup.Custom10
        };

        /// <summary>État des groupes d'actions personnalisés (KSP 1 ne dit pas si un groupe est vide : seulement allumé ou éteint).</summary>
        public static ActionGroupsInfo ActionGroups(Vessel vessel)
        {
            var custom = new ActionGroupState[ActionGroupsInfo.Count];
            for (var i = 0; i < custom.Length; i++)
                custom[i] = vessel.ActionGroups[CustomGroups[i]] ? ActionGroupState.On : ActionGroupState.Off;
            return new ActionGroupsInfo { Custom = custom };
        }

        public static DockingInfo Docking(Vessel vessel)
        {
            var tally = new DockTally();
            foreach (var part in vessel.parts)
            {
                foreach (PartModule module in part.Modules)
                {
                    if (module is ModuleDockingNode node)
                        tally.Add(DockOf(node));
                }
            }

            // KSP 1 n'a pas d'armement de port : un port « prêt » est déjà actif (la fonction reste en lecture seule).
            return tally.ToInfo(false);
        }

        internal static DockingPortState DockOf(ModuleDockingNode node)
        {
            var state = node.state ?? string.Empty;
            if (state.StartsWith("Docked", StringComparison.Ordinal) || state.StartsWith("PreAttached", StringComparison.Ordinal))
                return DockingPortState.Docked;
            if (state.StartsWith("Acquire", StringComparison.Ordinal))
                return DockingPortState.Approach;
            if (state.StartsWith("Ready", StringComparison.Ordinal))
                return DockingPortState.Armed;
            return DockingPortState.Disarmed;
        }

        internal static DeployPhase PhaseOf(ModuleDeployablePart.DeployState state)
        {
            switch (state)
            {
                case ModuleDeployablePart.DeployState.EXTENDED: return DeployPhase.Extended;
                case ModuleDeployablePart.DeployState.RETRACTED: return DeployPhase.Retracted;
                case ModuleDeployablePart.DeployState.BROKEN: return DeployPhase.Broken;
                default: return DeployPhase.Moving;
            }
        }

        internal static DeployPhase LadderPhase(RetractableLadder ladder)
        {
            var state = (ladder.StateName ?? string.Empty).ToLowerInvariant();
            if (state.Contains("extended"))
                return DeployPhase.Extended;
            if (state.Contains("retracted"))
                return DeployPhase.Retracted;
            return DeployPhase.Moving;
        }

        private static ChutePhase ChuteOf(ModuleParachute.deploymentStates state)
        {
            switch (state)
            {
                case ModuleParachute.deploymentStates.CUT: return ChutePhase.Cut;
                // « ACTIVE » : le déploiement est demandé, le parachute s'ouvrira quand les conditions du jeu seront réunies.
                case ModuleParachute.deploymentStates.ACTIVE:
                case ModuleParachute.deploymentStates.SEMIDEPLOYED:
                case ModuleParachute.deploymentStates.DEPLOYED: return ChutePhase.Deployed;
                default: return ChutePhase.Ready;
            }
        }
    }
}
