using System;
using System.Collections.Generic;

namespace OrbitDeck.Mod.Telemetry
{
    /// <summary>
    /// Lecture lourde (parcours des pièces) limitée en fréquence : entre deux lectures, le dernier résultat est renvoyé.
    /// L'horloge est injectée pour les tests (docs/SPEC-2.0-TECH.md §3.4 du dépôt du plugin).
    /// </summary>
    public sealed class SlowRead<T> where T : class
    {
        private readonly Func<double> _clock;
        private readonly double _period;
        private double _next = double.NegativeInfinity;
        private T _last;

        public SlowRead(Func<double> clock, double periodSeconds = 1.0)
        {
            _clock = clock;
            _period = periodSeconds;
        }

        /// <summary>
        /// Résultat courant : relit si la période est écoulée ou si <paramref name="force"/> (événement du jeu : étagement, amarrage…).
        /// Si la lecture lève une exception, la prochaine tentative attend aussi une période (pas de tempête d'erreurs) et l'exception est relancée.
        /// </summary>
        public T Get(Func<T> read, bool force = false)
        {
            var now = _clock();
            if (!force && now < _next)
                return _last;

            _next = now + _period;
            try
            {
                _last = read();
            }
            catch
            {
                _last = null;
                throw;
            }

            return _last;
        }

        public void Reset()
        {
            _next = double.NegativeInfinity;
            _last = null;
        }
    }

    /// <summary>
    /// Lecture d'un nouveau bloc de télémétrie isolée des autres : une exception omet le bloc (résultat null) et se journalise au plus
    /// une fois par période et par bloc. Un bug de lecture 2.0 ne peut donc pas dégrader une fonction de la 1.1 (rétrocompatibilité).
    /// </summary>
    public sealed class SafeReader
    {
        private readonly Func<double> _clock;
        private readonly Action<string> _log;
        private readonly double _logPeriod;
        private readonly Dictionary<string, double> _nextLog = new Dictionary<string, double>();

        public SafeReader(Func<double> clock, Action<string> log, double logPeriodSeconds = 5.0)
        {
            _clock = clock;
            _log = log;
            _logPeriod = logPeriodSeconds;
        }

        public T Read<T>(string block, Func<T> read) where T : class
        {
            try
            {
                return read();
            }
            catch (Exception exception)
            {
                var now = _clock();
                if (_log != null && (!_nextLog.TryGetValue(block, out var next) || now >= next))
                {
                    _nextLog[block] = now + _logPeriod;
                    _log("[OrbitDeck] Lecture « " + block + " » impossible : " + exception.Message);
                }

                return null;
            }
        }
    }
}
