using StreamPlug.Mod.Protocol;
using StreamPlug.Mod.Telemetry;

namespace StreamPlug.Mod.Commands
{
    public enum CommandKind
    {
        SetThrottle,
        AdjustThrottle,
        SetSas,
        ToggleSas,
        SetRcs,
        ToggleRcs,
        SetSpeedMode
    }

    /// <summary>Commande validée, prête à être exécutée sur le thread principal du jeu.</summary>
    public sealed class GameCommand
    {
        public CommandKind Kind { get; set; }

        /// <summary>Poussée visée (SetThrottle, entre 0 et 1) ou variation (AdjustThrottle, entre -1 et 1).</summary>
        public float Amount { get; set; }

        /// <summary>État visé pour SetSas et SetRcs.</summary>
        public bool Enabled { get; set; }

        public SpeedMode Mode { get; set; }
    }

    /// <summary>Résultat de l'exécution d'une commande dans le jeu.</summary>
    public sealed class CommandResult
    {
        public static CommandResult Ok { get; } = new CommandResult();

        public bool Success => ErrorCode == null;

        public string ErrorCode { get; private set; }

        public string Message { get; private set; }

        public static CommandResult Fail(string errorCode, string message) =>
            new CommandResult { ErrorCode = errorCode, Message = message };

        public static CommandResult NoVessel() => Fail(ErrorCodes.NoVessel, "Aucun véhicule actif.");

        public static CommandResult NotControllable(string message = "Le véhicule n'est pas contrôlable.") =>
            Fail(ErrorCodes.NotControllable, message);

        public static CommandResult Rejected(string message) => Fail(ErrorCodes.RejectedByGame, message);
    }
}
