using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Ksp1
{
    /// <summary>Lecture de l'orbite courante de KSP 1 (hors manœuvre). Thread principal uniquement.</summary>
    internal static class Ksp1GameOrbit
    {
        /// <summary>Orbite courante du véhicule ; null si le jeu ne la fournit pas.</summary>
        public static CurrentOrbitInfo Read(Vessel vessel, double ut)
        {
            var orbit = vessel.orbit;
            if (orbit == null)
                return null;

            var body = orbit.referenceBody;
            return CurrentOrbitInfo.FromGame(
                ut,
                orbit.eccentricity,
                orbit.ApA,
                orbit.PeA,
                orbit.period,
                orbit.inclination,
                orbit.timeToAp,
                orbit.timeToPe,
                body?.bodyName,
                body != null && body.atmosphere ? body.atmosphereDepth : (double?)null);
        }
    }
}
