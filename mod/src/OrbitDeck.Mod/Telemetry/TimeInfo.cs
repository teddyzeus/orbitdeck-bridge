using System;
using System.Collections.Generic;

namespace OrbitDeck.Mod.Telemetry
{
    /// <summary>Mode d'accélération du temps : avec simulation complète (physique) ou sur rails.</summary>
    public enum WarpMode
    {
        Rails,
        Physics
    }

    /// <summary>Raison pour laquelle le palier suivant est refusé en ce moment.</summary>
    public enum WarpLimit
    {
        Atmosphere,
        Altitude,
        Thrust,
        Ground,
        Proximity,
        Other
    }

    /// <summary>
    /// Temps du jeu : instant, vitesse du temps et pause. Ne référence aucun type du jeu.
    /// Les paliers sont ceux du jeu, dans le mode courant, du plus lent au plus rapide.
    /// </summary>
    public sealed class TimeInfo
    {
        /// <summary>Instant de jeu, en secondes.</summary>
        public double Ut { get; init; }

        /// <summary>Facteur de temps appliqué en ce moment (1 = normal). Diffère du palier demandé pendant une montée.</summary>
        public double Rate { get; init; } = 1;

        /// <summary>Position du palier demandé dans <see cref="Rates"/>.</summary>
        public int Index { get; init; }

        public IReadOnlyList<double> Rates { get; init; } = Array.Empty<double>();

        public WarpMode Mode { get; init; }

        public bool Paused { get; init; }

        /// <summary>Le mod sait mettre le jeu en pause.</summary>
        public bool Pausable { get; init; }

        /// <summary>Plus haut palier autorisé maintenant ; null si inconnu.</summary>
        public int? MaxIndex { get; init; }

        /// <summary>Raison du refus du palier suivant ; null si aucune ou inconnue.</summary>
        public WarpLimit? Limit { get; init; }

        /// <summary>Un warp automatique (jusqu'à un instant) est en cours.</summary>
        public bool Auto { get; init; }

        /// <summary>Durée d'un jour du jeu en secondes ; null si inconnue.</summary>
        public double? DayLength { get; init; }

        /// <summary>Position du palier qui a pour facteur <paramref name="rate"/> dans <see cref="Rates"/> ; -1 s'il n'existe pas.</summary>
        public static int IndexOfRate(IReadOnlyList<double> rates, double rate)
        {
            for (var i = 0; i < rates.Count; i++)
            {
                if (Math.Abs(rates[i] - rate) <= Math.Max(1e-6, Math.Abs(rate) * 1e-6))
                    return i;
            }

            return -1;
        }

        /// <summary>Même contenu, hors l'instant de jeu (qui avance en permanence et ne justifie pas une republication).</summary>
        public static bool SameExceptUt(TimeInfo a, TimeInfo b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null)
                return false;

            if (a.Index != b.Index || a.Mode != b.Mode || a.Paused != b.Paused || a.Pausable != b.Pausable
                || a.MaxIndex != b.MaxIndex || a.Limit != b.Limit || a.Auto != b.Auto || a.DayLength != b.DayLength
                || Math.Abs(a.Rate - b.Rate) > 1e-6 * Math.Max(1, Math.Abs(a.Rate)) || a.Rates.Count != b.Rates.Count)
                return false;

            for (var i = 0; i < a.Rates.Count; i++)
            {
                if (a.Rates[i] != b.Rates[i])
                    return false;
            }

            return true;
        }
    }
}
