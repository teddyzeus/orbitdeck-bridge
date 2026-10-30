using System;
using System.Collections.Generic;
using KSP.Sim.impl;
using KSP.Sim.Maneuver;

namespace OrbitDeck.Mod.Telemetry
{
    /// <summary>Lecture du plan de manœuvre de KSP 2 (nœuds, orbites, delta-V). Thread principal uniquement.</summary>
    internal static class GameManeuver
    {
        /// <summary>Au-delà de ce nombre d'orbites ouvertes, l'excentricité fait foi : 1 ou plus, l'orbite ne se referme pas.</summary>
        private const double EscapeEccentricity = 1.0;

        /// <summary>Plan du véhicule ; plan vide (Count à 0) s'il n'a aucun nœud, null si le jeu ne le fournit pas.</summary>
        public static ManeuverInfo Read(VesselComponent vessel, double ut)
        {
            var simulation = vessel.SimulationObject;
            if (simulation == null || !simulation.TryFindComponent<ManeuverPlanComponent>(out var plan) || plan == null)
                return null;

            var data = plan.GetNodes();
            var sorted = new List<ManeuverNodeData>();
            if (data != null)
            {
                foreach (var item in data)
                {
                    if (item != null)
                        sorted.Add(item);
                }
            }

            sorted.Sort((a, b) => a.Time.CompareTo(b.Time));

            var nodes = new List<ManeuverNodeInfo>();
            foreach (var item in sorted)
            {
                if (nodes.Count >= ManeuverInfo.MaxNodes)
                    break;
                nodes.Add(NodeOf(item));
            }

            return new ManeuverInfo
            {
                Count = sorted.Count,
                Nodes = nodes,
                Before = sorted.Count > 0 ? OrbitOf(vessel.Orbit) : null,
                DvAvailable = DvAvailableOf(vessel),
                // Progression de la combustion : l'accès à ManeuverProvider n'est pas établi (vérification T-5), donc rien.
                Burning = null
            };
        }

        private static ManeuverNodeInfo NodeOf(ManeuverNodeData node)
        {
            // BurnDuration est lue comme la durée totale de la poussée (vérification T-4) : le jeu allume à la moitié avant le nœud.
            double? burn = node.BurnDuration > 0 ? node.BurnDuration : (double?)null;
            return new ManeuverNodeInfo
            {
                Ut = node.Time,
                Dv = node.BurnRequiredDV,
                Burn = burn,
                Ignition = burn.HasValue ? node.Time - burn.Value / 2 : (double?)null,
                After = OrbitOf(node.ManeuverTrajectoryPatch)
            };
        }

        private static OrbitInfo OrbitOf(PatchedConicsOrbit orbit)
        {
            if (orbit == null)
                return null;

            try
            {
                var escape = orbit.eccentricity >= EscapeEccentricity;
                var body = orbit.referenceBody;
                return new OrbitInfo
                {
                    Ap = escape ? (double?)null : orbit.ApoapsisArl,
                    Pe = orbit.PeriapsisArl,
                    Escape = escape,
                    Atmosphere = body != null && body.hasAtmosphere ? body.atmosphereDepth : (double?)null
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static double? DvAvailableOf(VesselComponent vessel)
        {
            try
            {
                var dv = vessel.VesselDeltaV?.TotalDeltaVActual;
                return dv.HasValue && !double.IsNaN(dv.Value) ? dv : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
