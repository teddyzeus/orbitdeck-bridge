using System;
using System.Collections.Generic;

namespace OrbitDeck.Mod.Telemetry
{
    /// <summary>
    /// Décide si un snapshot diffère assez du précédent pour être republié.
    /// Seuils définis dans SPECS-TECHNIQUES §4.5.
    /// </summary>
    public static class SnapshotComparer
    {
        public const double AltitudeThreshold = 0.5;
        public const double SpeedThreshold = 0.05;
        public const float ThrottleThreshold = 0.005f;

        /// <summary>
        /// Changement d'un état « tout ou rien » (statut, véhicule, mode de vitesse, SAS, RCS).
        /// Contrairement à la poussée, ces changements sont rares : ils sont toujours signalés immédiatement.
        /// </summary>
        public static bool HasModeChange(TelemetrySnapshot previous, TelemetrySnapshot current)
        {
            if (previous == null || current == null)
                return !ReferenceEquals(previous, current);

            return previous.Status != current.Status
                   || previous.GameStateName != current.GameStateName
                   || previous.VesselName != current.VesselName
                   || previous.SpeedMode != current.SpeedMode
                   || previous.SasEnabled != current.SasEnabled
                   || previous.SasMode != current.SasMode
                   || previous.RcsEnabled != current.RcsEnabled
                   || previous.LightsEnabled != current.LightsEnabled
                   || previous.BrakesEnabled != current.BrakesEnabled
                   || previous.GearEnabled != current.GearEnabled
                   || previous.StageCount != current.StageCount
                   || previous.View != current.View
                   || !SameModes(previous.AvailableSasModes, current.AvailableSasModes);
        }

        /// <summary>Changement d'un état discret : <see cref="HasModeChange"/> ou poussée au-delà du seuil.</summary>
        public static bool HasDiscreteChange(TelemetrySnapshot previous, TelemetrySnapshot current)
        {
            if (HasModeChange(previous, current))
                return true;

            if (previous == null || current == null)
                return false;

            return Math.Abs(previous.Throttle - current.Throttle) > ThrottleThreshold;
        }

        /// <summary>Changement discret ou variation continue (altitude, vitesses) au-delà des seuils.</summary>
        public static bool HasChanged(TelemetrySnapshot previous, TelemetrySnapshot current)
        {
            if (HasDiscreteChange(previous, current))
                return true;

            if (previous == null || current == null)
                return false;

            return Differs(previous.AltitudeSeaLevel, current.AltitudeSeaLevel, AltitudeThreshold)
                   || Differs(previous.AltitudeSurface, current.AltitudeSurface, AltitudeThreshold)
                   || Differs(previous.SpeedSurface, current.SpeedSurface, SpeedThreshold)
                   || Differs(previous.SpeedOrbital, current.SpeedOrbital, SpeedThreshold)
                   || DiffersNullable(previous.SpeedTarget, current.SpeedTarget, SpeedThreshold);
        }

        private static bool SameModes(IReadOnlyList<SasModeKind> a, IReadOnlyList<SasModeKind> b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null || a.Count != b.Count)
                return false;

            for (var i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }

        private static bool Differs(double a, double b, double threshold) => Math.Abs(a - b) > threshold;

        private static bool DiffersNullable(double? a, double? b, double threshold)
        {
            if (a.HasValue != b.HasValue)
                return true;
            return a.HasValue && Differs(a.Value, b.Value, threshold);
        }
    }
}
