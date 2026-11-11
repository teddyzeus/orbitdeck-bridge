using System;
using KSP.Sim.impl;

namespace OrbitDeck.Mod.Telemetry
{
    /// <summary>Lecture de l'orbite courante de KSP 2 (hors manœuvre). Thread principal uniquement.</summary>
    internal static class GameOrbit
    {
        /// <summary>Orbite courante du véhicule ; null si le jeu ne la fournit pas.</summary>
        public static CurrentOrbitInfo Read(VesselComponent vessel, double ut)
        {
            var orbit = vessel.Orbit;
            if (orbit == null)
                return null;

            var body = orbit.referenceBody;
            return CurrentOrbitInfo.FromGame(
                ut,
                orbit.eccentricity,
                orbit.ApoapsisArl,
                orbit.PeriapsisArl,
                orbit.period,
                orbit.inclination,
                orbit.TimeToAp,
                orbit.TimeToPe,
                body?.bodyName,
                body != null && body.hasAtmosphere ? body.atmosphereDepth : (double?)null);
        }
    }
}
