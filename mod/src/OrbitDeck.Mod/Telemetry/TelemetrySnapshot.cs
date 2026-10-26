using System.Collections.Generic;

namespace System.Runtime.CompilerServices
{
    // Permet les propriétés `init` sous netstandard2.1
    internal static class IsExternalInit
    {
    }
}

namespace OrbitDeck.Mod.Telemetry
{
    /// <summary>État du véhicule actif tel que vu par le Stream Deck.</summary>
    public enum VesselStatus
    {
        /// <summary>Un véhicule contrôlable est actif.</summary>
        Connected,

        /// <summary>Aucun véhicule actif (menu, centre spatial…).</summary>
        NoVessel,

        /// <summary>Un véhicule est actif mais ne peut pas être contrôlé (pas de pilote, EVA…).</summary>
        NotControllable
    }

    /// <summary>Modes du pilote automatique (SAS) pilotables depuis le Stream Deck.</summary>
    public enum SasModeKind
    {
        StabilityAssist,
        Prograde,
        Retrograde,
        Normal,
        Antinormal,
        RadialIn,
        RadialOut,
        Target,
        AntiTarget,
        Maneuver
    }

    /// <summary>Vue du jeu en vol : le véhicule (caméra de vol) ou la carte (orbites).</summary>
    public enum ViewKind
    {
        Ship,
        Map
    }

    /// <summary>Type de vitesse affiché par le jeu.</summary>
    public enum SpeedMode
    {
        Surface,
        Orbit,
        Target
    }

    /// <summary>
    /// Photographie immuable de l'état du jeu. Ne référence aucun type du jeu : testable sans KSP 2.
    /// Altitudes en mètres, vitesses en m/s, poussée entre 0 et 1.
    /// </summary>
    public sealed class TelemetrySnapshot
    {
        public static TelemetrySnapshot NoVessel { get; } = new TelemetrySnapshot { Status = VesselStatus.NoVessel };

        /// <summary>Aucun véhicule, en précisant l'état global du jeu (diagnostic).</summary>
        public static TelemetrySnapshot NoVesselIn(string gameStateName) =>
            new TelemetrySnapshot { Status = VesselStatus.NoVessel, GameStateName = gameStateName };

        public VesselStatus Status { get; init; }

        /// <summary>État global du jeu (FlightView, MainMenu, KerbalSpaceCenter…) ; null si inconnu. Sert au diagnostic.</summary>
        public string GameStateName { get; init; }

        public string VesselName { get; init; }

        public double AltitudeSeaLevel { get; init; }

        public double AltitudeSurface { get; init; }

        public double SpeedSurface { get; init; }

        public double SpeedOrbital { get; init; }

        /// <summary>Vitesse relative à la cible ; null si aucune cible n'est sélectionnée.</summary>
        public double? SpeedTarget { get; init; }

        public SpeedMode SpeedMode { get; init; }

        public float Throttle { get; init; }

        public bool SasEnabled { get; init; }

        public string SasMode { get; init; }

        public bool RcsEnabled { get; init; }

        /// <summary>Phares allumés ; null si le jeu ne permet pas de le lire.</summary>
        public bool? LightsEnabled { get; init; }

        /// <summary>Freins actionnés ; null si le jeu ne permet pas de le lire.</summary>
        public bool? BrakesEnabled { get; init; }

        /// <summary>Train d'atterrissage sorti ; null si le jeu ne permet pas de le lire.</summary>
        public bool? GearEnabled { get; init; }

        /// <summary>Nombre d'étages restants (celui qu'affiche le bouton d'étage du jeu) ; null si le jeu ne permet pas de le lire.</summary>
        public int? StageCount { get; init; }

        /// <summary>
        /// Plus rien à déclencher : le bouton d'étage du jeu est grisé. Le seuil dépend du jeu (KSP 2 : un étage restant,
        /// KSP 1 : étage 0 atteint), d'où une information explicite plutôt que de laisser le client la déduire du compteur.
        /// </summary>
        public bool? StageExhausted { get; init; }

        /// <summary>Vue affichée (véhicule ou carte) ; null si le jeu ne permet pas de la lire.</summary>
        public ViewKind? View { get; init; }

        /// <summary>
        /// Modes du SAS sélectionnables en ce moment (les autres sont grisés dans le jeu : pas de cible, pas de nœud de
        /// manœuvre, navball hors mode orbite, SAS indisponible…). Liste vide : SAS inutilisable ; null : le jeu ne permet pas de le lire.
        /// </summary>
        public IReadOnlyList<SasModeKind> AvailableSasModes { get; init; }
    }
}
