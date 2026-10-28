using System;
using System.Collections.Generic;
using System.Reflection;
using KSP.Game;
using KSP.Sim.impl;

namespace OrbitDeck.Mod.Telemetry
{
    /// <summary>Lecture du temps de KSP 2 (instant, paliers de warp, pause), partagée par la lecture et les commandes. Thread principal uniquement.</summary>
    internal static class GameTime
    {
        /// <summary>Dernier indice de palier du warp physique si le jeu ne le dit pas (paliers 1×, 2×, 3×, 4×).</summary>
        private const int DefaultMaxPhysicsIndex = 3;

        private static int? _maxPhysicsIndex;

        public static bool TryGetTimeWarp(out GameInstance game, out TimeWarp timeWarp)
        {
            game = null;
            timeWarp = null;

            var manager = GameManager.Instance;
            if (manager == null || manager.Game == null || manager.Game.ViewController == null)
                return false;

            game = manager.Game;
            timeWarp = game.ViewController.TimeWarp;
            return timeWarp != null;
        }

        public static TimeInfo Read(GameInstance game, TimeWarp timeWarp)
        {
            var physics = timeWarp.IsPhysicsTimeWarp;
            var rates = RatesOf(timeWarp, physics);
            var index = Math.Max(0, Math.Min(timeWarp.CurrentRateIndex, Math.Max(0, rates.Count - 1)));
            var paused = game.ViewController.IsPaused;

            int? maxIndex = null;
            WarpLimit? limit = null;
            try
            {
                maxIndex = Math.Min(timeWarp.GetMaxRateIndex(false, out _), Math.Max(0, rates.Count - 1));
                // Le jeu ne dit pas pourquoi : on sait seulement que le palier suivant existe et qu'il est refusé.
                if (!paused && maxIndex < rates.Count - 1 && index >= maxIndex)
                    limit = WarpLimit.Other;
            }
            catch (Exception)
            {
                // Palier maximal illisible : laissé inconnu.
            }

            return new TimeInfo
            {
                Ut = game.ViewController.universalTime,
                Rate = timeWarp.CurrentRate,
                Index = index,
                Rates = rates,
                Mode = physics ? WarpMode.Physics : WarpMode.Rails,
                Paused = paused,
                Pausable = true,
                MaxIndex = maxIndex,
                Limit = limit,
                Auto = timeWarp.IsAutoWarpEngaged
            };
        }

        /// <summary>
        /// Met le jeu en pause ou le reprend par l'interrupteur de temps de l'univers (celui de la touche Échap), pas par
        /// <c>TimeWarp.SetIsPaused</c> qui ne fait que relayer l'état. Retourne faux si le jeu n'a pas changé d'état.
        /// </summary>
        public static bool SetPaused(GameInstance game, bool paused)
        {
            if (game.UniverseModel.IsTimePaused != paused)
                game.UniverseModel.SetTimePaused(paused, true);

            return game.UniverseModel.IsTimePaused == paused;
        }

        /// <summary>Facteurs de temps du mode courant, du plus lent au plus rapide.</summary>
        public static IReadOnlyList<double> RatesOf(TimeWarp timeWarp, bool physics)
        {
            var levels = timeWarp.GetWarpRates();
            var count = levels == null ? 0 : levels.Length;
            if (physics)
                count = Math.Min(count, MaxPhysicsIndex() + 1);

            var rates = new double[count];
            for (var i = 0; i < count; i++)
                rates[i] = levels[i].TimeScaleFactor;

            return rates;
        }

        private static int MaxPhysicsIndex()
        {
            if (_maxPhysicsIndex.HasValue)
                return _maxPhysicsIndex.Value;

            try
            {
                var field = typeof(TimeWarp).GetField("MAX_PHYSICS_WARP_RATE_INDEX", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                _maxPhysicsIndex = field != null ? Convert.ToInt32(field.GetValue(null)) : DefaultMaxPhysicsIndex;
            }
            catch (Exception)
            {
                _maxPhysicsIndex = DefaultMaxPhysicsIndex;
            }

            return _maxPhysicsIndex.Value;
        }
    }
}
