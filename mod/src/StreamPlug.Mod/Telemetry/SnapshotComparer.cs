using System;

namespace StreamPlug.Mod.Telemetry
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

        /// <summary>Changement d'un état « discret » (statut, SAS, RCS, mode, poussée…) à signaler immédiatement.</summary>
        public static bool HasDiscreteChange(TelemetrySnapshot previous, TelemetrySnapshot current)
        {
            if (previous == null || current == null)
                return !ReferenceEquals(previous, current);

            return previous.Status != current.Status
                   || previous.VesselName != current.VesselName
                   || previous.SpeedMode != current.SpeedMode
                   || previous.SasEnabled != current.SasEnabled
                   || previous.SasMode != current.SasMode
                   || previous.RcsEnabled != current.RcsEnabled
                   || Math.Abs(previous.Throttle - current.Throttle) > ThrottleThreshold;
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

        private static bool Differs(double a, double b, double threshold) => Math.Abs(a - b) > threshold;

        private static bool DiffersNullable(double? a, double? b, double threshold)
        {
            if (a.HasValue != b.HasValue)
                return true;
            return a.HasValue && Differs(a.Value, b.Value, threshold);
        }
    }
}
