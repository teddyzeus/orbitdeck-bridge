using System;
using System.Collections.Generic;
using KSP.Game;
using KSP.Messages;

namespace StreamPlug.Mod.Telemetry
{
    /// <summary>
    /// S'abonne aux messages du jeu (poussée, SAS, RCS) pour déclencher un échantillonnage immédiat.
    /// Se réabonne automatiquement si le centre de messages du jeu est recréé.
    /// </summary>
    public sealed class GameEventWatcher : IDisposable
    {
        private readonly List<SubscriptionHandle> _handles = new List<SubscriptionHandle>();
        private MessageCenter _center;
        private volatile bool _eventPending;

        /// <summary>Nombre d'abonnements actifs (diagnostic).</summary>
        public int SubscriptionCount => _handles.Count;

        /// <summary>Vérifie le centre de messages courant et s'abonne si besoin. Appeler à chaque Update.</summary>
        public void Refresh()
        {
            MessageCenter center = null;
            var manager = GameManager.Instance;
            if (manager != null && manager.Game != null)
                center = manager.Game.Messages;

            if (ReferenceEquals(center, _center))
                return;

            Unsubscribe();
            _center = center;
            if (_center == null)
                return;

            Subscribe<ThrottleChangedMessage>();
            Subscribe<SASEnabledMessage>();
            Subscribe<SASDisabledMessage>();
            Subscribe<SASModeChangedMessage>();
            Subscribe<RCSModeChangedMessage>();
        }

        /// <summary>Indique si un message est arrivé depuis le dernier appel, et remet l'indicateur à zéro.</summary>
        public bool ConsumeEvent()
        {
            var pending = _eventPending;
            _eventPending = false;
            return pending;
        }

        public void Dispose() => Unsubscribe();

        private void Subscribe<TMessage>() where TMessage : MessageCenterMessage
        {
            _handles.Add(_center.Subscribe<TMessage>(OnMessage));
        }

        private void OnMessage(MessageCenterMessage message)
        {
            _eventPending = true;
        }

        private void Unsubscribe()
        {
            if (_center != null)
            {
                foreach (var handle in _handles)
                {
                    try
                    {
                        var copy = handle;
                        _center.Unsubscribe(ref copy);
                    }
                    catch
                    {
                        // Centre de messages déjà détruit : rien à désabonner.
                    }
                }
            }

            _handles.Clear();
            _center = null;
        }
    }
}
