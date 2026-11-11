using System;

namespace OrbitDeck.Mod.Telemetry
{
    // Modèle du protocole 2.0 (docs/SPEC-2.0-TECH.md §2.3 du dépôt du plugin). Aucun type du jeu : testable sans KSP.
    // Chaque valeur inconnue est nulle (jamais zéro) ; un bloc entier nul est absent de l'état. Les « Same » portent les seuils de
    // republication : deux photographies qui diffèrent de moins que le seuil ne déclenchent pas un nouvel état.

    /// <summary>Orbite courante du véhicule, avec ou sans nœud de manœuvre (indépendante de <see cref="ManeuverInfo.Before"/>).</summary>
    public sealed class CurrentOrbitInfo
    {
        public const double AltitudeTolerance = 0.5;
        public const double PeriodTolerance = 0.1;
        public const double InclinationTolerance = 0.05;
        public const double TimeTolerance = 1.0;

        /// <summary>Altitude de l'apoapsis (m) ; null pour une orbite ouverte ou si inconnue.</summary>
        public double? Ap { get; init; }

        public double? Pe { get; init; }

        /// <summary>Période (s) ; null pour une orbite ouverte.</summary>
        public double? Period { get; init; }

        /// <summary>Inclinaison (degrés).</summary>
        public double? Inc { get; init; }

        /// <summary>Instants de jeu des prochains passages à l'apoapsis et au périapsis.</summary>
        public double? ApUt { get; init; }

        public double? PeUt { get; init; }

        /// <summary>Corps de référence.</summary>
        public string Body { get; init; }

        public bool Escape { get; init; }

        /// <summary>Hauteur de l'atmosphère du corps (m) ; null sans atmosphère ou si inconnue.</summary>
        public double? Atmosphere { get; init; }

        private static double? Finite(double? value) => value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value) ? value : null;

        /// <summary>
        /// Construit l'orbite courante depuis les valeurs brutes d'un jeu (partagé par KSP 1 et KSP 2, donc testé une fois) : valeurs non
        /// finies = inconnues ; orbite ouverte (excentricité de 1 ou plus) = ni apoapsis, ni période, ni temps avant l'apoapsis ;
        /// les « temps avant » deviennent des instants absolus (<paramref name="ut"/> + délai), un délai négatif (passage déjà fait) est inconnu.
        /// </summary>
        public static CurrentOrbitInfo FromGame(double ut, double eccentricity, double? ap, double? pe, double? period, double? inclination,
            double? timeToAp, double? timeToPe, string body, double? atmosphere)
        {
            var escape = eccentricity >= 1.0;
            double? Instant(double? delay) => Finite(delay) is double d && d >= 0 ? ut + d : (double?)null;

            return new CurrentOrbitInfo
            {
                Ap = escape ? null : Finite(ap),
                Pe = Finite(pe),
                Period = escape ? null : Finite(period),
                Inc = Finite(inclination),
                ApUt = escape ? null : Instant(timeToAp),
                PeUt = Instant(timeToPe),
                Body = string.IsNullOrEmpty(body) ? null : body,
                Escape = escape,
                Atmosphere = Finite(atmosphere) is double a && a > 0 ? a : (double?)null
            };
        }

        public static bool Same(CurrentOrbitInfo a, CurrentOrbitInfo b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null)
                return false;

            return a.Escape == b.Escape && a.Body == b.Body
                   && OrbitInfo.Near(a.Ap, b.Ap, AltitudeTolerance) && OrbitInfo.Near(a.Pe, b.Pe, AltitudeTolerance)
                   && OrbitInfo.Near(a.Period, b.Period, PeriodTolerance) && OrbitInfo.Near(a.Inc, b.Inc, InclinationTolerance)
                   && OrbitInfo.Near(a.ApUt, b.ApUt, TimeTolerance) && OrbitInfo.Near(a.PeUt, b.PeUt, TimeTolerance)
                   && OrbitInfo.Near(a.Atmosphere, b.Atmosphere, AltitudeTolerance);
        }
    }

    /// <summary>Quantité d'une ressource et capacité (unités du jeu).</summary>
    public sealed class ResourceAmount
    {
        /// <summary>Un changement de moins de 0,2 % de la capacité ne se republie pas.</summary>
        public const double Fraction = 0.002;

        public double Amount { get; init; }

        public double Max { get; init; }

        public static bool Same(ResourceAmount a, ResourceAmount b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null)
                return false;

            var tolerance = Math.Max(1e-6, Math.Max(a.Max, b.Max) * Fraction);
            return Math.Abs(a.Amount - b.Amount) <= tolerance && Math.Abs(a.Max - b.Max) <= tolerance;
        }
    }

    /// <summary>Les quatre ressources suivies ; une ressource nulle n'existe pas sur le véhicule (≠ vide, qui a <c>Amount</c> à 0).</summary>
    public sealed class ResourceSet
    {
        public ResourceAmount Fuel { get; init; }

        public ResourceAmount Oxidizer { get; init; }

        public ResourceAmount Electric { get; init; }

        public ResourceAmount Monoprop { get; init; }

        public static bool Same(ResourceSet a, ResourceSet b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null)
                return false;

            return ResourceAmount.Same(a.Fuel, b.Fuel) && ResourceAmount.Same(a.Oxidizer, b.Oxidizer)
                   && ResourceAmount.Same(a.Electric, b.Electric) && ResourceAmount.Same(a.Monoprop, b.Monoprop);
        }
    }

    /// <summary>Ressources du véhicule entier et de l'étage courant.</summary>
    public sealed class ResourcesInfo
    {
        public ResourceSet Vessel { get; init; }

        /// <summary>Étage courant ; null si inconnu.</summary>
        public ResourceSet Stage { get; init; }

        public static bool Same(ResourcesInfo a, ResourcesInfo b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null)
                return false;

            return ResourceSet.Same(a.Vessel, b.Vessel) && ResourceSet.Same(a.Stage, b.Stage);
        }
    }

    /// <summary>Delta-V et rapport poussée / poids du véhicule.</summary>
    public sealed class PerformanceInfo
    {
        public const double DvTolerance = 0.5;
        public const double TwrTolerance = 0.01;

        public double? Dv { get; init; }

        public double? DvStage { get; init; }

        /// <summary>TWR de l'étage courant ; null sans moteur actif.</summary>
        public double? Twr { get; init; }

        public static bool Same(PerformanceInfo a, PerformanceInfo b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null)
                return false;

            return OrbitInfo.Near(a.Dv, b.Dv, DvTolerance) && OrbitInfo.Near(a.DvStage, b.DvStage, DvTolerance) && OrbitInfo.Near(a.Twr, b.Twr, TwrTolerance);
        }
    }

    /// <summary>Aides à l'atterrissage.</summary>
    public sealed class LandingInfo
    {
        public const double SpeedTolerance = 0.05;
        public const double AltitudeTolerance = 0.5;
        public const double TimeTolerance = 1.0;

        /// <summary>Vitesse verticale (m/s), la descente est négative.</summary>
        public double? VSpeed { get; init; }

        public double? HSpeed { get; init; }

        /// <summary>Altitude au-dessus du sol (m) ; null hors de portée.</summary>
        public double? Radar { get; init; }

        /// <summary>Instant d'impact estimé ; null s'il n'y a pas d'impact prévu.</summary>
        public double? ImpactUt { get; init; }

        public static bool Same(LandingInfo a, LandingInfo b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null)
                return false;

            return OrbitInfo.Near(a.VSpeed, b.VSpeed, SpeedTolerance) && OrbitInfo.Near(a.HSpeed, b.HSpeed, SpeedTolerance)
                   && OrbitInfo.Near(a.Radar, b.Radar, AltitudeTolerance) && OrbitInfo.Near(a.ImpactUt, b.ImpactUt, TimeTolerance);
        }
    }

    /// <summary>Cible du véhicule : <c>None</c> quand le jeu fournit l'information et qu'il n'y a pas de cible.</summary>
    public sealed class TargetInfo
    {
        public const double DistanceTolerance = 0.5;
        public const double SpeedTolerance = 0.05;

        public bool None { get; init; }

        public string Name { get; init; }

        public double? Distance { get; init; }

        /// <summary>Vitesse relative (m/s), négative quand on s'approche.</summary>
        public double? Rel { get; init; }

        public static bool Same(TargetInfo a, TargetInfo b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null)
                return false;

            return a.None == b.None && a.Name == b.Name
                   && OrbitInfo.Near(a.Distance, b.Distance, DistanceTolerance) && OrbitInfo.Near(a.Rel, b.Rel, SpeedTolerance);
        }
    }

    /// <summary>État d'une catégorie de modules déployables (panneaux, antennes, échelles) ou des parachutes.</summary>
    public enum SystemState
    {
        Extended,
        Retracted,
        Mixed,
        Moving,
        Ready,
        Deployed,
        Cut
    }

    /// <summary>Une catégorie de modules : <c>Count</c> à 0 = aucun module de ce type.</summary>
    public sealed class SystemCategory
    {
        public int Count { get; init; }

        /// <summary>Null si la catégorie est vide ou si l'état est illisible.</summary>
        public SystemState? State { get; init; }

        /// <summary>Faux : aucun module de la catégorie ne se rétracte ; null : inconnu ou sans objet.</summary>
        public bool? Retractable { get; init; }

        public static bool Same(SystemCategory a, SystemCategory b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null)
                return false;

            return a.Count == b.Count && a.State == b.State && a.Retractable == b.Retractable;
        }
    }

    /// <summary>Panneaux solaires, antennes, échelles et parachutes ; une catégorie nulle est illisible sur ce jeu.</summary>
    public sealed class SystemsInfo
    {
        public SystemCategory Solar { get; init; }

        public SystemCategory Antennas { get; init; }

        public SystemCategory Ladders { get; init; }

        public SystemCategory Parachutes { get; init; }

        public static bool Same(SystemsInfo a, SystemsInfo b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null)
                return false;

            return SystemCategory.Same(a.Solar, b.Solar) && SystemCategory.Same(a.Antennas, b.Antennas)
                   && SystemCategory.Same(a.Ladders, b.Ladders) && SystemCategory.Same(a.Parachutes, b.Parachutes);
        }
    }

    public enum DockingPortState
    {
        Disarmed,
        Armed,
        Approach,
        Docked
    }

    /// <summary>Ports d'amarrage du véhicule actif.</summary>
    public sealed class DockingInfo
    {
        /// <summary>Nombre de ports ; 0 = aucun.</summary>
        public int Ports { get; init; }

        public DockingPortState? State { get; init; }

        /// <summary>Le jeu permet d'armer et de désarmer le port (faux sur KSP 1).</summary>
        public bool? Armable { get; init; }

        public bool? CanUndock { get; init; }

        public static bool Same(DockingInfo a, DockingInfo b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null)
                return false;

            return a.Ports == b.Ports && a.State == b.State && a.Armable == b.Armable && a.CanUndock == b.CanUndock;
        }
    }
}
