using System;
using System.Collections.Generic;

namespace OrbitDeck.Mod.Telemetry
{
    // Logique commune à KSP 1 et KSP 2 pour les blocs 2.0 (ressources, systèmes, amarrage, cible) : sans type du jeu, donc testée une fois.

    /// <summary>Famille de ressource suivie par OrbitDeck selon son nom interne dans le jeu.</summary>
    public enum ResourceKind
    {
        None,
        Fuel,
        Oxidizer,
        Electric,
        Monoprop
    }

    public static class ResourceKinds
    {
        /// <summary>
        /// Nom interne du jeu → famille. Carburant : « LiquidFuel » (KSP 1 et 2) et « Methalox » (KSP 2, qui mélange carburant et comburant) ;
        /// les autres ressources (ergols solides, xénon, minerai…) ne sont pas suivies.
        /// </summary>
        public static ResourceKind Of(string name)
        {
            switch ((name ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "liquidfuel":
                case "methane":
                case "methalox":
                    return ResourceKind.Fuel;
                case "oxidizer":
                    return ResourceKind.Oxidizer;
                case "electriccharge":
                    return ResourceKind.Electric;
                case "monopropellant":
                case "monoprop":
                    return ResourceKind.Monoprop;
                default:
                    return ResourceKind.None;
            }
        }
    }

    /// <summary>Somme des ressources d'un ensemble de pièces ; une ressource sans capacité n'existe pas sur le véhicule (null).</summary>
    public sealed class ResourceTally
    {
        private readonly double[] _amount = new double[5];
        private readonly double[] _max = new double[5];

        public void Add(string name, double amount, double max)
        {
            var kind = ResourceKinds.Of(name);
            if (kind == ResourceKind.None || double.IsNaN(amount) || double.IsNaN(max) || max <= 0)
                return;

            _amount[(int)kind] += Math.Max(0, Math.Min(amount, max));
            _max[(int)kind] += max;
        }

        private ResourceAmount Of(ResourceKind kind) => _max[(int)kind] > 0 ? new ResourceAmount { Amount = _amount[(int)kind], Max = _max[(int)kind] } : null;

        public ResourceSet ToSet() => new ResourceSet
        {
            Fuel = Of(ResourceKind.Fuel),
            Oxidizer = Of(ResourceKind.Oxidizer),
            Electric = Of(ResourceKind.Electric),
            Monoprop = Of(ResourceKind.Monoprop)
        };
    }

    /// <summary>État d'un module déployable (panneau, antenne, échelle) vu par l'adaptateur du jeu.</summary>
    public enum DeployPhase
    {
        Retracted,
        Extended,
        Moving,
        Broken
    }

    /// <summary>Fusion des modules d'une catégorie en un état unique (docs/SPEC-2.0-TECH.md §3.3 du dépôt du plugin).</summary>
    public sealed class DeployTally
    {
        private int _count;
        private int _extended;
        private int _retracted;
        private int _moving;
        private bool _anyRetractable;

        public void Add(DeployPhase phase, bool retractable)
        {
            if (phase == DeployPhase.Broken)
                return;

            _count++;
            _anyRetractable |= retractable;
            switch (phase)
            {
                case DeployPhase.Extended: _extended++; break;
                case DeployPhase.Retracted: _retracted++; break;
                default: _moving++; break;
            }
        }

        public SystemCategory ToCategory()
        {
            if (_count == 0)
                return new SystemCategory { Count = 0 };

            SystemState state;
            if (_moving > 0)
                state = SystemState.Moving;
            else if (_extended == _count)
                state = SystemState.Extended;
            else if (_retracted == _count)
                state = SystemState.Retracted;
            else
                state = SystemState.Mixed;

            return new SystemCategory { Count = _count, State = state, Retractable = _anyRetractable };
        }
    }

    public enum ChutePhase
    {
        Ready,
        Deployed,
        Cut
    }

    public sealed class ChuteTally
    {
        private int _count;
        private int _ready;
        private int _deployed;
        private int _cut;

        public void Add(ChutePhase phase)
        {
            _count++;
            switch (phase)
            {
                case ChutePhase.Ready: _ready++; break;
                case ChutePhase.Deployed: _deployed++; break;
                default: _cut++; break;
            }
        }

        public SystemCategory ToCategory()
        {
            if (_count == 0)
                return new SystemCategory { Count = 0 };

            SystemState state;
            if (_ready == _count)
                state = SystemState.Ready;
            else if (_deployed == _count)
                state = SystemState.Deployed;
            else if (_cut == _count)
                state = SystemState.Cut;
            else
                state = SystemState.Mixed;

            return new SystemCategory { Count = _count, State = state };
        }
    }

    /// <summary>Fusion des ports d'amarrage : le « plus avancé » des états l'emporte (amarré &gt; approche &gt; armé &gt; désarmé).</summary>
    public sealed class DockTally
    {
        private int _ports;
        private DockingPortState _best = DockingPortState.Disarmed;
        private bool _docked;

        public void Add(DockingPortState state)
        {
            _ports++;
            _docked |= state == DockingPortState.Docked;
            if (state > _best)
                _best = state;
        }

        /// <param name="armable">Le jeu permet d'armer et désarmer les ports (faux sur KSP 1).</param>
        public DockingInfo ToInfo(bool armable) => _ports == 0
            ? new DockingInfo { Ports = 0 }
            : new DockingInfo { Ports = _ports, State = _best, Armable = armable, CanUndock = _docked };
    }

    /// <summary>
    /// Vitesse d'approche d'une cible estimée par différence de distance entre deux lectures (négative quand on s'approche) :
    /// utilisée quand le jeu ne fournit pas de vitesse relative signée. Lissée pour ne pas trembler.
    /// </summary>
    public sealed class ApproachRate
    {
        private const double MinDelta = 0.05;
        private const double Smoothing = 0.5;
        private double? _lastDistance;
        private double _lastUt;
        private double? _rate;

        public double? Update(double ut, double distance)
        {
            if (_lastDistance == null || ut < _lastUt)
            {
                _lastDistance = distance;
                _lastUt = ut;
                _rate = null;
                return null;
            }

            var dt = ut - _lastUt;
            if (dt < MinDelta)
                return _rate;

            var raw = (distance - _lastDistance.Value) / dt;
            _rate = _rate == null ? raw : _rate + Smoothing * (raw - _rate);
            _lastDistance = distance;
            _lastUt = ut;
            return _rate;
        }

        public void Reset()
        {
            _lastDistance = null;
            _rate = null;
        }
    }

    /// <summary>Estimation de l'instant d'impact sans freinage ni poussée (alerte prudente, docs/SPEC-2.0-TECH.md §3.3).</summary>
    public static class ImpactEstimator
    {
        /// <param name="ut">Instant courant.</param>
        /// <param name="radar">Altitude au-dessus du sol (m).</param>
        /// <param name="vspeed">Vitesse verticale (m/s), négative en descente.</param>
        /// <param name="gravity">Gravité de surface (m/s²).</param>
        public static double? ImpactUt(double ut, double? radar, double? vspeed, double? gravity)
        {
            if (radar == null || vspeed == null || gravity == null || vspeed.Value >= 0 || radar.Value < 0 || gravity.Value <= 0)
                return null;

            // radar + vspeed·t − g·t²/2 = 0 (vspeed négative), racine positive.
            var discriminant = vspeed.Value * vspeed.Value + 2 * gravity.Value * radar.Value;
            var t = (vspeed.Value + Math.Sqrt(discriminant)) / gravity.Value;
            return t >= 0 && !double.IsNaN(t) && !double.IsInfinity(t) ? ut + t : (double?)null;
        }
    }
}
