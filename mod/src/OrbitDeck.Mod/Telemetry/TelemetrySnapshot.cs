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

        /// <summary>Temps du jeu (vitesse, pause, paliers) ; null si le jeu ne permet pas de le lire.</summary>
        public TimeInfo Time { get; init; }

        /// <summary>Plan de manœuvre du véhicule actif ; null si le jeu ne permet pas de le lire (un plan vide a <c>Count</c> à 0).</summary>
        public ManeuverInfo Maneuver
        {
            get => _maneuver;
            init => _maneuver = value;
        }

        private ManeuverInfo _maneuver;

        // 2.0 : blocs ajoutés après la 1.1. Tous nuls par défaut (jamais zéro) : un adaptateur qui ne les renseigne pas produit le même
        // état qu'avant (rétrocompatibilité, docs/SPEC-2.0.md §1 du dépôt du plugin).

        /// <summary>Orbite courante ; null si le jeu ne permet pas de la lire.</summary>
        public CurrentOrbitInfo Orbit { get; init; }

        /// <summary>Ressources du véhicule ; null si illisibles.</summary>
        public ResourcesInfo Resources { get; init; }

        /// <summary>Delta-V et TWR ; null si illisibles.</summary>
        public PerformanceInfo Performance { get; init; }

        /// <summary>Aides à l'atterrissage ; null si illisibles.</summary>
        public LandingInfo Landing { get; init; }

        /// <summary>Cible ; <c>None</c> s'il n'y en a pas, null si le jeu ne permet pas de le savoir.</summary>
        public TargetInfo Target { get; init; }

        /// <summary>Panneaux, antennes, échelles, parachutes ; null si illisibles.</summary>
        public SystemsInfo Systems { get; init; }

        /// <summary>Ports d'amarrage ; null si illisibles.</summary>
        public DockingInfo Docking { get; init; }

        /// <summary>Les dix groupes d'actions personnalisés ; null si illisibles.</summary>
        public ActionGroupsInfo ActionGroups { get; init; }

        /// <summary>Copie du snapshot avec un autre plan de manœuvre (le snapshot est immuable).</summary>
        public TelemetrySnapshot WithManeuver(ManeuverInfo maneuver)
        {
            var copy = (TelemetrySnapshot)MemberwiseClone();
            copy._maneuver = maneuver;
            return copy;
        }
    }
}
