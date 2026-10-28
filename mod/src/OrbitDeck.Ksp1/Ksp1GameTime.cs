using System;
using System.Reflection;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Ksp1
{
    /// <summary>Lecture du temps de KSP 1 (instant, paliers de warp, pause), partagée par la lecture et les commandes. Thread principal uniquement.</summary>
    internal static class Ksp1GameTime
    {
        /// <summary>Durée d'un jour du jeu : 6 h sur Kerbin, 24 h avec le calendrier terrestre.</summary>
        private const double KerbinDay = 6 * 3600;

        private const double EarthDay = 24 * 3600;

        public static bool TryGetFlight(out TimeWarp timeWarp)
        {
            timeWarp = TimeWarp.fetch;
            return HighLogic.LoadedSceneIsFlight && FlightGlobals.ActiveVessel != null && timeWarp != null;
        }

        public static bool IsPhysics(TimeWarp timeWarp) => TimeWarp.WarpMode == TimeWarp.Modes.LOW;

        /// <summary>Facteurs de temps du mode courant, du plus lent au plus rapide.</summary>
        public static double[] RatesOf(TimeWarp timeWarp)
        {
            var source = IsPhysics(timeWarp) ? timeWarp.physicsWarpRates : timeWarp.warpRates;
            var rates = new double[source == null ? 0 : source.Length];
            for (var i = 0; i < rates.Length; i++)
                rates[i] = source[i];

            return rates;
        }

        public static TimeInfo Read(TimeWarp timeWarp)
        {
            var physics = IsPhysics(timeWarp);
            var rates = RatesOf(timeWarp);
            var index = Math.Max(0, Math.Min(TimeWarp.CurrentRateIndex, Math.Max(0, rates.Length - 1)));

            int? maxIndex = null;
            WarpLimit? limit = null;
            if (!physics && rates.Length > 0)
                maxIndex = MaxRailsIndex(timeWarp, rates.Length - 1, out limit);

            var paused = FlightDriver.Pause;
            if (paused || maxIndex == null || index < maxIndex || maxIndex >= rates.Length - 1)
                limit = null;

            return new TimeInfo
            {
                Ut = Planetarium.GetUniversalTime(),
                Rate = TimeWarp.CurrentRate,
                Index = index,
                Rates = rates,
                Mode = physics ? WarpMode.Physics : WarpMode.Rails,
                Paused = paused,
                Pausable = true,
                MaxIndex = maxIndex,
                Limit = limit,
                Auto = AutoWarpEngaged(timeWarp),
                DayLength = GameSettings.KERBIN_TIME ? KerbinDay : EarthDay
            };
        }

        /// <summary>Plus haut palier sur rails autorisé maintenant (altitude, atmosphère, poussée…), et pourquoi le suivant est refusé.</summary>
        private static int MaxRailsIndex(TimeWarp timeWarp, int top, out WarpLimit? limit)
        {
            limit = null;
            var vessel = FlightGlobals.ActiveVessel;

            var byAltitude = top;
            try
            {
                byAltitude = Math.Min(top, timeWarp.GetMaxRateForAltitude(vessel.altitude, vessel.mainBody));
            }
            catch (Exception)
            {
                // Limite d'altitude illisible : seule la vérification d'état est appliquée.
            }

            var byState = MaxOnRails(timeWarp, top, out var reason);
            var max = Math.Min(byAltitude, byState);
            if (max >= top)
                return top;

            limit = byState < byAltitude ? ToLimit(reason) : WarpLimit.Altitude;
            return max;
        }

        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static FieldInfo _autoWarpField;
        private static MethodInfo _maxOnRails;
        private static bool _resolved;

        private static void Resolve()
        {
            if (_resolved)
                return;

            _resolved = true;
            _autoWarpField = typeof(TimeWarp).GetField("autoWarpEngaged", AnyInstance);
            _maxOnRails = typeof(TimeWarp).GetMethod("getMaxOnRailsRateIdx", AnyInstance);
        }

        /// <summary>Un warp automatique (WarpTo) est en cours. Membre non public du jeu : faux s'il est introuvable.</summary>
        public static bool AutoWarpEngaged(TimeWarp timeWarp)
        {
            Resolve();
            return _autoWarpField != null && _autoWarpField.GetValue(timeWarp) is bool engaged && engaged;
        }

        /// <summary>Plus haut palier sur rails que le jeu autorise vers <paramref name="top"/>, avec la raison s'il est plus bas. Membre non public du jeu.</summary>
        private static int MaxOnRails(TimeWarp timeWarp, int top, out ClearToSaveStatus reason)
        {
            reason = ClearToSaveStatus.CLEAR;
            Resolve();
            if (_maxOnRails == null)
                return top;

            var arguments = new object[] { top, false, ClearToSaveStatus.CLEAR };
            var result = _maxOnRails.Invoke(timeWarp, arguments);
            reason = (ClearToSaveStatus)arguments[2];
            return result is int index ? index : top;
        }

        private static WarpLimit ToLimit(ClearToSaveStatus reason)
        {
            switch (reason)
            {
                case ClearToSaveStatus.NOT_IN_ATMOSPHERE: return WarpLimit.Atmosphere;
                case ClearToSaveStatus.NOT_WHILE_THROTTLED_UP:
                case ClearToSaveStatus.NOT_UNDER_ACCELERATION: return WarpLimit.Thrust;
                case ClearToSaveStatus.NOT_WHILE_MOVING_OVER_SURFACE:
                case ClearToSaveStatus.NOT_WHILE_ABOUT_TO_CRASH: return WarpLimit.Ground;
                case ClearToSaveStatus.ORBIT_EVENT_IMMINENT: return WarpLimit.Proximity;
                default: return WarpLimit.Other;
            }
        }
    }
}
