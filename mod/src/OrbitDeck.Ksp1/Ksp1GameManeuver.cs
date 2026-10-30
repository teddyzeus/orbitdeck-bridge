using System;
using System.Collections.Generic;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Ksp1
{
    /// <summary>Lecture du plan de manœuvre de KSP 1 (nœuds, orbites, delta-V). Thread principal uniquement.</summary>
    internal static class Ksp1GameManeuver
    {
        private const double EscapeEccentricity = 1.0;

        /// <summary>Plan du véhicule ; plan vide (Count à 0) s'il n'a aucun nœud, null si le jeu ne le fournit pas.</summary>
        public static ManeuverInfo Read(Vessel vessel, double ut)
        {
            var solver = vessel.patchedConicSolver;
            if (solver == null || solver.maneuverNodes == null)
                return null;

            var sorted = new List<ManeuverNode>();
            foreach (var item in solver.maneuverNodes)
            {
                if (item != null)
                    sorted.Add(item);
            }

            sorted.Sort((a, b) => a.UT.CompareTo(b.UT));

            if (vessel.id != _burnVessel)
            {
                _burnVessel = vessel.id;
                _burnPerDv = 0;
            }

            var nodes = new List<ManeuverNodeInfo>();
            foreach (var item in sorted)
            {
                if (nodes.Count >= ManeuverInfo.MaxNodes)
                    break;
                nodes.Add(NodeOf(item, ut));
            }

            return new ManeuverInfo
            {
                Count = sorted.Count,
                Nodes = nodes,
                Before = sorted.Count > 0 ? OrbitOf(vessel.orbit) : null,
                DvAvailable = DvAvailableOf(vessel),
                Burning = sorted.Count > 0 ? ManeuverInfo.BurningOf(ut, nodes[0], RemainingOf(vessel, sorted[0])) : null
            };
        }

        /// <summary>Estimation de combustion en secondes par m/s de delta-V, retenue de la dernière lecture valable du véhicule.</summary>
        private static double _burnPerDv;

        private static Guid _burnVessel;

        /// <summary>En deçà, la durée lue n'est pas une estimation (le jeu la remet à zéro quand les moteurs ne poussent pas).</summary>
        private const double MinBurnSeconds = 2;

        private static ManeuverNodeInfo NodeOf(ManeuverNode node, double ut)
        {
            // startBurnIn : délai avant le début de la poussée, calculé par le jeu (la moitié de la durée avant le nœud). Il retombe à
            // zéro (début de poussée = instant du nœud) par intermittence, selon l'état des moteurs : une durée nulle est ignorée et
            // on reprend la dernière estimation valable, rapportée au delta-V du nœud.
            double? ignition = null;
            double? burn = null;
            var dv = node.DeltaV.magnitude;
            try
            {
                var delay = node.startBurnIn;
                if (!double.IsNaN(delay) && !double.IsInfinity(delay) && ut + delay < node.UT)
                {
                    var estimated = 2 * (node.UT - (ut + delay));
                    if (estimated >= MinBurnSeconds)
                    {
                        burn = estimated;
                        if (dv > 0)
                            _burnPerDv = estimated / dv;
                    }
                }
            }
            catch (Exception)
            {
                // Durée de combustion illisible : on retombe sur la dernière estimation.
            }

            if (!burn.HasValue && _burnPerDv > 0 && dv > 0)
                burn = _burnPerDv * dv;
            if (burn.HasValue)
                ignition = node.UT - burn.Value / 2;

            return new ManeuverNodeInfo
            {
                Ut = node.UT,
                Dv = node.DeltaV.magnitude,
                Burn = burn,
                Ignition = ignition,
                After = OrbitOf(node.nextPatch)
            };
        }

        private static OrbitInfo OrbitOf(Orbit orbit)
        {
            if (orbit == null)
                return null;

            try
            {
                var escape = orbit.eccentricity >= EscapeEccentricity;
                var body = orbit.referenceBody;
                return new OrbitInfo
                {
                    Ap = escape ? (double?)null : orbit.ApA,
                    Pe = orbit.PeA,
                    Escape = escape,
                    Atmosphere = body != null && body.atmosphere ? body.atmosphereDepth : (double?)null
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Delta-V restant du premier nœud : la norme du vecteur de poussée du jeu (celui de la boule de navigation).</summary>
        private static double? RemainingOf(Vessel vessel, ManeuverNode node)
        {
            try
            {
                var remaining = node.GetBurnVector(vessel.orbit).magnitude;
                return double.IsNaN(remaining) || double.IsInfinity(remaining) ? (double?)null : remaining;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static double? DvAvailableOf(Vessel vessel)
        {
            try
            {
                var dv = vessel.VesselDeltaV != null ? vessel.VesselDeltaV.TotalDeltaVActual : (double?)null;
                return dv.HasValue && !double.IsNaN(dv.Value) ? dv : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
