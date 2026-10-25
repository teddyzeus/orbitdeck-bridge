using System;
using System.Collections.Concurrent;
using System.Threading;
using StreamPlug.Mod.Protocol;

namespace StreamPlug.Mod.Commands
{
    /// <summary>Commande en attente d'exécution, avec le moyen de répondre au client qui l'a envoyée.</summary>
    public sealed class PendingCommand
    {
        public PendingCommand(string id, GameCommand command, Action<string> reply)
        {
            Id = id;
            Command = command;
            Reply = reply;
        }

        public string Id { get; }

        public GameCommand Command { get; }

        /// <summary>Envoie un message JSON au client d'origine (peut être appelé depuis n'importe quel thread).</summary>
        public Action<string> Reply { get; }
    }

    /// <summary>
    /// Pont entre le thread réseau (qui valide et met en file) et le thread Unity (qui exécute).
    /// La file est bornée : un client qui inonde le serveur reçoit des erreurs, le jeu n'est pas ralenti.
    /// </summary>
    public sealed class CommandQueue
    {
        public const int Capacity = 64;
        public const int DefaultMaxPerFrame = 16;

        private readonly ConcurrentQueue<PendingCommand> _queue = new ConcurrentQueue<PendingCommand>();
        private readonly Action<string> _logError;
        private int _count;

        public CommandQueue(Action<string> logError = null)
        {
            _logError = logError;
        }

        public int Count => Volatile.Read(ref _count);

        /// <summary>Appelé après chaque commande exécutée (journalisation). Ne doit pas lever d'exception.</summary>
        public Action<PendingCommand, CommandResult> Processed { get; set; }

        /// <summary>Met la commande en file. Retourne false si la file est pleine.</summary>
        public bool TryEnqueue(PendingCommand command)
        {
            if (Interlocked.Increment(ref _count) > Capacity)
            {
                Interlocked.Decrement(ref _count);
                return false;
            }

            _queue.Enqueue(command);
            return true;
        }

        /// <summary>
        /// Exécute les commandes en attente (au plus <paramref name="max"/>) et répond à chaque client.
        /// À appeler depuis le thread principal Unity. Retourne le nombre de commandes traitées.
        /// </summary>
        public int ProcessPending(IGameControls controls, int max = DefaultMaxPerFrame)
        {
            var processed = 0;
            while (processed < max && _queue.TryDequeue(out var pending))
            {
                Interlocked.Decrement(ref _count);
                processed++;

                CommandResult result;
                try
                {
                    result = Execute(pending.Command, controls);
                }
                catch (Exception exception)
                {
                    _logError?.Invoke($"Erreur pendant l'exécution de « {pending.Command.Kind} » : {exception}");
                    result = CommandResult.Fail(ErrorCodes.Internal, "Erreur interne pendant l'exécution de la commande.");
                }

                try
                {
                    Processed?.Invoke(pending, result);
                }
                catch (Exception exception)
                {
                    _logError?.Invoke($"Erreur dans le suivi de la commande {pending.Id} : {exception.Message}");
                }

                try
                {
                    pending.Reply(result.Success
                        ? ProtocolMessages.Ack(pending.Id)
                        : ProtocolMessages.Error(pending.Id, result.ErrorCode, result.Message));
                }
                catch (Exception exception)
                {
                    _logError?.Invoke($"Réponse impossible pour la commande {pending.Id} : {exception.Message}");
                }
            }

            return processed;
        }

        internal static CommandResult Execute(GameCommand command, IGameControls controls)
        {
            switch (command.Kind)
            {
                case CommandKind.SetThrottle: return controls.SetThrottle(command.Amount, false);
                case CommandKind.AdjustThrottle: return controls.SetThrottle(command.Amount, true);
                case CommandKind.SetSas: return controls.SetSas(command.Enabled);
                case CommandKind.ToggleSas: return controls.SetSas(null);
                case CommandKind.SetRcs: return controls.SetRcs(command.Enabled);
                case CommandKind.ToggleRcs: return controls.SetRcs(null);
                case CommandKind.SetSpeedMode: return controls.SetSpeedMode(command.Mode);
                default: return CommandResult.Fail(ErrorCodes.UnknownCommand, "Commande non supportée.");
            }
        }
    }
}
