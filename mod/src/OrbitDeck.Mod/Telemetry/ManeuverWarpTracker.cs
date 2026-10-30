using System;
using OrbitDeck.Mod.Commands;

namespace OrbitDeck.Mod.Telemetry
{
    /// <summary>État de l'opération « warp jusqu'à la manœuvre ».</summary>
    public enum ManeuverWarpState
    {
        /// <summary>Rien en cours.</summary>
        Idle,

        /// <summary>Le jeu warpe vers l'instant d'arrêt.</summary>
        Running,

        /// <summary>Arrêté à 1× à l'instant d'arrêt, avant l'allumage.</summary>
        Arrived,

        /// <summary>La demande a été refusée : l'instant d'arrêt était déjà passé.</summary>
        Late,

        /// <summary>L'opération a été interrompue ; voir <see cref="ManeuverWarpReason"/>.</summary>
        Interrupted
    }

    public enum ManeuverWarpReason
    {
        /// <summary>Le joueur a repris la main (arrêt du warp, changement de palier).</summary>
        Manual,

        /// <summary>Le nœud visé n'existe plus (supprimé ou déplacé).</summary>
        NodeGone,

        /// <summary>Le véhicule actif a changé.</summary>
        Vessel,

        /// <summary>Le véhicule n'est plus contrôlable.</summary>
        Control
    }

    /// <summary>Opération en cours telle que le plugin la voit.</summary>
    public sealed class ManeuverWarpInfo
    {
        public ManeuverWarpState State { get; init; }

        /// <summary>Instant d'arrêt visé ; null si l'opération n'en a pas.</summary>
        public double? Stop { get; init; }

        public ManeuverWarpReason? Reason { get; init; }

        public static bool Same(ManeuverWarpInfo a, ManeuverWarpInfo b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null)
                return false;

            return a.State == b.State && a.Reason == b.Reason && OrbitInfo.Near(a.Stop, b.Stop, ManeuverInfo.TimeTolerance);
        }
    }

    /// <summary>Ce que le suivi demande au jeu. Les adaptateurs l'implémentent ; les tests le simulent.</summary>
    public interface IWarpDriver
    {
        /// <summary>Lance le warp automatique du jeu jusqu'à <paramref name="ut"/> ; <paramref name="maxRate"/> plafonne le palier. Reprend de la pause au besoin.</summary>
        bool WarpTo(double ut, double? maxRate);

        /// <summary>Retour à 1× immédiat et annulation du warp automatique.</summary>
        void StopWarp();
    }

    /// <summary>
    /// Suit un « warp jusqu'à la manœuvre ». Le jeu fait le warp (il ralentit tout seul à l'approche de l'instant) ; ce suivi
    /// ne commande jamais un palier. Il lance l'opération, la surveille et en rend compte. Ne référence aucun type du jeu.
    /// </summary>
    public sealed class ManeuverWarpTracker
    {
        /// <summary>Délai pendant lequel un état final (arrivé, trop tard, interrompu) reste publié (s).</summary>
        public const double FinalStateSeconds = 5;

        /// <summary>Le jeu met un instant à engager le warp automatique : on ne conclut à une reprise en main qu'après ce délai (s).</summary>
        public const double EngageGraceSeconds = 1.5;

        /// <summary>À cet écart de l'instant d'arrêt (s de jeu) ou moins, l'opération est arrivée.</summary>
        public const double ArrivalTolerance = 5;

        /// <summary>
        /// Le warp automatique du jeu s'arrête de lui-même un peu avant l'instant visé (environ une demi-seconde réelle au dernier
        /// palier). En deçà de cet écart (s de jeu) ou de 1,5 × le dernier palier, c'est la fin d'une étape, pas une reprise en main.
        /// </summary>
        public const double LegEndSeconds = 30;

        /// <summary>Durée pendant laquelle un nouvel instant d'arrêt doit rester stable avant d'être pris en compte (s).</summary>
        public const double StopSettleSeconds = 1;

        /// <summary>Au-dessus de ce facteur observé, l'étape a vraiment accéléré le temps.</summary>
        public const double MinWarpedRate = 1.5;

        /// <summary>Relances successives sans accélération du temps avant d'abandonner.</summary>
        public const int MaxIdleRelaunches = 3;

        /// <summary>Nombre maximal d'étapes de warp relancées pour une opération.</summary>
        public const int MaxRelaunches = 20;

        /// <summary>Un nœud est reconnu à son instant, à cette tolérance près (s).</summary>
        public const double NodeTolerance = 1;

        /// <summary>Avance prise sur l'arrêt quand la durée de combustion est inconnue (s).</summary>
        public const double UnknownBurnLead = 60;

        private readonly Func<double> _clock;

        private ManeuverWarpState _state;
        private ManeuverWarpReason? _reason;
        private double _nodeUt;
        private double _margin;
        private double? _maxRate;
        private double _stop;
        private string _vessel;
        private double _startedAt;
        private double _legMaxRate;
        private int _relaunches;
        private int _idleRelaunches;
        private bool _stopRequested;
        private double _pendingStop = double.NaN;
        private double _pendingSince;
        private double _lastUt = double.NaN;
        private double _lastClock;
        private double _finalUntil;

        /// <param name="clock">Horloge en secondes qui ne s'arrête ni en pause ni en warp.</param>
        public ManeuverWarpTracker(Func<double> clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <summary>Journal des étapes de l'opération (lancement, fin, relance) ; rien si null.</summary>
        public Action<string> Log { get; set; }

        public ManeuverWarpState State => _state;

        /// <summary>Une opération est en cours ou son résultat est encore publié.</summary>
        public bool Active => _state != ManeuverWarpState.Idle;

        /// <summary>L'opération surveille le jeu à chaque image.</summary>
        public bool Running => _state == ManeuverWarpState.Running;

        /// <summary>Opération à publier ; null au repos.</summary>
        public ManeuverWarpInfo Info
        {
            get
            {
                if (_state == ManeuverWarpState.Idle)
                    return null;

                return new ManeuverWarpInfo
                {
                    State = _state,
                    Stop = _state == ManeuverWarpState.Late ? (double?)null : _stop,
                    Reason = _state == ManeuverWarpState.Interrupted ? _reason : null
                };
            }
        }

        /// <summary>Plan de l'état courant complété de l'opération en cours.</summary>
        public TelemetrySnapshot Decorate(TelemetrySnapshot snapshot)
        {
            if (snapshot?.Maneuver == null || _state == ManeuverWarpState.Idle)
                return snapshot;

            return snapshot.WithManeuver(snapshot.Maneuver.WithWarp(Info));
        }

        /// <summary>
        /// Lance l'opération pour le nœud d'instant <paramref name="nodeUt"/>. Retourne le refus à renvoyer au client, ou
        /// <see cref="CommandResult.Ok"/> une fois le jeu en route.
        /// </summary>
        public CommandResult Start(TelemetrySnapshot snapshot, double nodeUt, double margin, double? maxRate, IWarpDriver driver)
        {
            if (snapshot == null || snapshot.Status == VesselStatus.NoVessel)
                return CommandResult.NoVessel();
            if (snapshot.Status != VesselStatus.Connected)
                return CommandResult.NotControllable();
            if (snapshot.Time == null || snapshot.Maneuver == null)
                return CommandResult.Rejected("Le jeu ne fournit pas le plan de manœuvre.");

            var node = FindNode(snapshot.Maneuver, nodeUt);
            if (node == null)
                return CommandResult.Rejected("Ce nœud de manœuvre n'existe plus.");

            var stop = StopOf(node, margin);
            if (stop <= snapshot.Time.Ut + 1)
            {
                Finish(ManeuverWarpState.Late, null, stop);
                return CommandResult.Rejected("L'instant d'arrêt est déjà passé.");
            }

            if (!driver.WarpTo(stop, maxRate))
                return CommandResult.Rejected("Le jeu refuse le warp en ce moment.");

            _state = ManeuverWarpState.Running;
            _reason = null;
            _nodeUt = node.Ut;
            _margin = margin;
            _maxRate = maxRate;
            _stop = stop;
            _vessel = snapshot.VesselName;
            _startedAt = _clock();
            _legMaxRate = 1;
            _relaunches = 0;
            _idleRelaunches = 0;
            _stopRequested = false;
            _pendingStop = double.NaN;
            _lastUt = double.NaN;
            Log?.Invoke($"Warp vers le nœud ut={node.Ut:F1} : arrêt visé {stop:F1}, ut actuel {snapshot.Time.Ut:F1} (reste {stop - snapshot.Time.Ut:F1} s de jeu), marge {margin:F0} s, plafond {(maxRate.HasValue ? maxRate.Value.ToString("F0") + "×" : "aucun")}.");
            return CommandResult.Ok;
        }

        /// <summary>À appeler à chaque image avec une lecture fraîche de l'état du jeu.</summary>
        public void Update(TelemetrySnapshot snapshot, IWarpDriver driver)
        {
            var now = _clock();
            if (_state == ManeuverWarpState.Idle)
                return;

            if (_state != ManeuverWarpState.Running)
            {
                if (now >= _finalUntil)
                    _state = ManeuverWarpState.Idle;
                return;
            }

            // Le jeu n'est plus en vol (menu, centre spatial) : plus rien à suivre, sans message ni commande.
            if (snapshot == null || snapshot.Status == VesselStatus.NoVessel)
            {
                Reset();
                return;
            }

            if (snapshot.VesselName != _vessel)
            {
                Cancel(driver, ManeuverWarpReason.Vessel);
                return;
            }

            if (snapshot.Status != VesselStatus.Connected)
            {
                Cancel(driver, ManeuverWarpReason.Control);
                return;
            }

            var node = snapshot.Maneuver == null ? null : FindNode(snapshot.Maneuver, _nodeUt);
            if (node == null)
            {
                Cancel(driver, ManeuverWarpReason.NodeGone);
                return;
            }

            var time = snapshot.Time;
            if (time == null)
                return;

            // Durée de combustion modifiée (delta-V changé) : l'instant d'arrêt suit, le warp est relancé vers le nouveau.
            // Un instant d'arrêt qui change d'une image à l'autre (estimation de combustion instable) ne relance rien : il doit rester
            // le même un court instant avant d'être pris en compte.
            var stop = StopOf(node, _margin);
            if (Math.Abs(stop - _stop) <= 0.5)
            {
                _pendingStop = double.NaN;
            }
            else if (double.IsNaN(_pendingStop) || Math.Abs(stop - _pendingStop) > 0.5)
            {
                _pendingStop = stop;
                _pendingSince = now;
            }
            else if (now - _pendingSince >= StopSettleSeconds)
            {
                _pendingStop = double.NaN;
                if (stop <= time.Ut + 1)
                {
                    driver.StopWarp();
                    Finish(ManeuverWarpState.Late, null, stop);
                    return;
                }

                if (driver.WarpTo(stop, _maxRate))
                {
                    Log?.Invoke($"Warp vers le nœud relancé : arrêt visé {_stop:F1} → {stop:F1}.");
                    _stop = stop;
                }
            }

            // Vitesse réelle du temps de jeu sur cette étape (le facteur lu peut retarder sur la montée de palier).
            if (!double.IsNaN(_lastUt) && now > _lastClock + 1e-3)
                _legMaxRate = Math.Max(_legMaxRate, (time.Ut - _lastUt) / (now - _lastClock));
            _lastUt = time.Ut;
            _lastClock = now;

            if (time.Auto)
            {
                _legMaxRate = Math.Max(_legMaxRate, time.Rate);
                return;
            }

            if (now - _startedAt < EngageGraceSeconds)
                return;

            // Le warp automatique est terminé.
            var remaining = _stop - time.Ut;
            if (remaining <= ArrivalTolerance)
            {
                Log?.Invoke($"Warp automatique du jeu terminé à ut={time.Ut:F1} (arrêt visé {_stop:F1}, écart {-remaining:F1} s).");
                Finish(ManeuverWarpState.Arrived, null, _stop);
                return;
            }

            // Arrêt demandé par le joueur (touche de retour à 1× ou annulation de la touche de warp) : on ne relance rien.
            if (_stopRequested)
            {
                Log?.Invoke($"Warp arrêté à ut={time.Ut:F1} sur demande, à {remaining:F1} s de l'arrêt visé {_stop:F1}.");
                Finish(ManeuverWarpState.Interrupted, ManeuverWarpReason.Manual, _stop);
                return;
            }

            // En pause, il n'y a rien à relancer : on attend la reprise.
            if (time.Paused)
                return;

            // Le jeu coupe son warp avant l'instant visé (fin d'une étape, ou limite qu'il impose) : on relance pour l'étape suivante
            // (palier plus bas). Dans la dernière minute à 1×, il ne reste qu'à laisser passer le temps.
            var warped = _legMaxRate > MinWarpedRate;
            if (!warped && remaining <= LegEndSeconds * 2)
                return;

            if (warped)
                _idleRelaunches = 0;
            else if (++_idleRelaunches > MaxIdleRelaunches)
            {
                Log?.Invoke($"Le jeu ne warpe plus à {remaining:F1} s de l'arrêt visé {_stop:F1} : opération abandonnée.");
                Finish(ManeuverWarpState.Interrupted, ManeuverWarpReason.Manual, _stop);
                return;
            }

            if (_relaunches < MaxRelaunches && driver.WarpTo(_stop, _maxRate))
            {
                _relaunches++;
                Log?.Invoke($"Étape de warp terminée à ut={time.Ut:F1} ({_legMaxRate:F0}×), reste {remaining:F1} s : étape suivante ({_relaunches}).");
                _legMaxRate = 1;
                _startedAt = now;
            }
            else
            {
                Finish(ManeuverWarpState.Interrupted, ManeuverWarpReason.Manual, _stop);
            }
        }

        /// <summary>Le joueur (ou le plugin) a ordonné le retour à 1× : une fin de warp qui suit n'est pas une étape du jeu.</summary>
        public void NoteStopRequested()
        {
            if (_state == ManeuverWarpState.Running)
                _stopRequested = true;
        }

        private static ManeuverNodeInfo FindNode(ManeuverInfo maneuver, double ut)
        {
            foreach (var node in maneuver.Nodes)
            {
                if (Math.Abs(node.Ut - ut) <= NodeTolerance)
                    return node;
            }

            return null;
        }

        private static double StopOf(ManeuverNodeInfo node, double margin) =>
            node.Ignition.HasValue ? node.Ignition.Value - margin : node.Ut - margin - UnknownBurnLead;

        private void Cancel(IWarpDriver driver, ManeuverWarpReason reason)
        {
            driver.StopWarp();
            Finish(ManeuverWarpState.Interrupted, reason, _stop);
        }

        private void Finish(ManeuverWarpState state, ManeuverWarpReason? reason, double stop)
        {
            Log?.Invoke($"Warp vers le nœud terminé : {state}{(reason.HasValue ? " (" + reason.Value + ")" : string.Empty)}, arrêt visé {stop:F1}.");
            _state = state;
            _reason = reason;
            _stop = stop;
            _finalUntil = _clock() + FinalStateSeconds;
        }

        private void Reset()
        {
            _state = ManeuverWarpState.Idle;
            _reason = null;
        }
    }
}
