using System;
using System.Collections.Generic;

namespace OrbitDeck.Mod.Telemetry
{
    /// <summary>Orbite d'après le jeu, réduite à ce qu'affiche une touche. Ne référence aucun type du jeu.</summary>
    public sealed class OrbitInfo
    {
        /// <summary>Altitude de l'apoapsis au-dessus du corps (m) ; null pour une orbite ouverte ou si inconnue.</summary>
        public double? Ap { get; init; }

        /// <summary>Altitude du périapsis au-dessus du corps (m) ; null si inconnue.</summary>
        public double? Pe { get; init; }

        /// <summary>Orbite ouverte (hyperbolique) : le véhicule quitte le corps.</summary>
        public bool Escape { get; init; }

        /// <summary>Hauteur de l'atmosphère du corps (m) ; null sans atmosphère ou si inconnue.</summary>
        public double? Atmosphere { get; init; }

        public static bool Same(OrbitInfo a, OrbitInfo b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null)
                return false;

            return a.Escape == b.Escape
                   && Near(a.Ap, b.Ap, ManeuverInfo.AltitudeTolerance)
                   && Near(a.Pe, b.Pe, ManeuverInfo.AltitudeTolerance)
                   && Near(a.Atmosphere, b.Atmosphere, ManeuverInfo.AltitudeTolerance);
        }

        internal static bool Near(double? a, double? b, double tolerance)
        {
            if (a.HasValue != b.HasValue)
                return false;
            return !a.HasValue || Math.Abs(a.Value - b.Value) <= tolerance;
        }
    }

    /// <summary>Un nœud de manœuvre planifié.</summary>
    public sealed class ManeuverNodeInfo
    {
        /// <summary>Instant du nœud, en secondes de jeu. C'est aussi son identifiant pour les commandes.</summary>
        public double Ut { get; init; }

        /// <summary>Delta-V requis (m/s).</summary>
        public double Dv { get; init; }

        /// <summary>Durée de combustion estimée (s) ; null si inconnue.</summary>
        public double? Burn { get; init; }

        /// <summary>Instant d'allumage recommandé ; null si inconnu.</summary>
        public double? Ignition { get; init; }

        /// <summary>Orbite après le nœud ; null si inconnue.</summary>
        public OrbitInfo After { get; init; }

        public static bool Same(ManeuverNodeInfo a, ManeuverNodeInfo b)
        {
            return Math.Abs(a.Ut - b.Ut) <= ManeuverInfo.TimeTolerance
                   && Math.Abs(a.Dv - b.Dv) <= ManeuverInfo.DvTolerance
                   && OrbitInfo.Near(a.Burn, b.Burn, ManeuverInfo.TimeTolerance)
                   && OrbitInfo.Near(a.Ignition, b.Ignition, ManeuverInfo.TimeTolerance)
                   && OrbitInfo.Same(a.After, b.After);
        }
    }

    /// <summary>Combustion en cours du premier nœud.</summary>
    public sealed class BurnInfo
    {
        /// <summary>Delta-V restant (m/s) ; null si inconnu.</summary>
        public double? Remaining { get; init; }

        /// <summary>Progression de 0 à 1 ; null si inconnue.</summary>
        public double? Progress { get; init; }
    }

    /// <summary>Plan de manœuvre du véhicule actif. Ne référence aucun type du jeu.</summary>
    public sealed class ManeuverInfo
    {
        /// <summary>Nombre de nœuds retenus dans l'état : au plus huit, du plus proche au plus lointain.</summary>
        public const int MaxNodes = 8;

        public const double TimeTolerance = 0.05;
        public const double DvTolerance = 0.05;
        public const double AltitudeTolerance = 0.5;
        public const double DvAvailableTolerance = 0.5;
        public const double RemainingTolerance = 0.1;
        public const double ProgressTolerance = 0.005;

        /// <summary>Nombre de nœuds planifiés (peut dépasser <see cref="Nodes"/>).</summary>
        public int Count { get; init; }

        public IReadOnlyList<ManeuverNodeInfo> Nodes { get; init; } = Array.Empty<ManeuverNodeInfo>();

        /// <summary>Orbite actuelle du véhicule ; null si inconnue.</summary>
        public OrbitInfo Before { get; init; }

        /// <summary>Delta-V du véhicule (m/s) ; null si inconnu.</summary>
        public double? DvAvailable { get; init; }

        /// <summary>Combustion du premier nœud en cours ; null sinon.</summary>
        public BurnInfo Burning { get; init; }

        /// <summary>Warp jusqu'à la manœuvre en cours ou récemment terminé ; null au repos.</summary>
        public ManeuverWarpInfo Warp => _warp;

        private ManeuverWarpInfo _warp;

        /// <summary>Copie du plan avec l'opération de warp donnée.</summary>
        public ManeuverInfo WithWarp(ManeuverWarpInfo warp)
        {
            var copy = (ManeuverInfo)MemberwiseClone();
            copy._warp = warp;
            return copy;
        }

        public static bool Same(ManeuverInfo a, ManeuverInfo b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null)
                return false;

            if (a.Count != b.Count || a.Nodes.Count != b.Nodes.Count || !OrbitInfo.Same(a.Before, b.Before)
                || !OrbitInfo.Near(a.DvAvailable, b.DvAvailable, DvAvailableTolerance) || !SameBurning(a.Burning, b.Burning)
                || !ManeuverWarpInfo.Same(a.Warp, b.Warp))
                return false;

            for (var i = 0; i < a.Nodes.Count; i++)
            {
                if (!ManeuverNodeInfo.Same(a.Nodes[i], b.Nodes[i]))
                    return false;
            }

            return true;
        }

        private static bool SameBurning(BurnInfo a, BurnInfo b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null)
                return false;

            return OrbitInfo.Near(a.Remaining, b.Remaining, RemainingTolerance)
                   && OrbitInfo.Near(a.Progress, b.Progress, ProgressTolerance);
        }

        /// <summary>
        /// Combustion du premier nœud : seulement si l'allumage est passé et que le nœud existe encore. Null si le jeu ne donne
        /// ni le restant ni la progression.
        /// </summary>
        public static BurnInfo BurningOf(double ut, ManeuverNodeInfo first, double? remaining)
        {
            if (first == null || !first.Ignition.HasValue || ut < first.Ignition.Value || first.Dv <= 0 || !remaining.HasValue)
                return null;

            var left = Math.Max(0, remaining.Value);
            return new BurnInfo { Remaining = left, Progress = Math.Max(0, Math.Min(1, 1 - left / first.Dv)) };
        }
    }
}
