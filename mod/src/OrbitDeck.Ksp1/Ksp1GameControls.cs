using OrbitDeck.Mod.Commands;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Ksp1
{
    /// <summary>
    /// Exécution des commandes dans KSP 1. Étape K3 : les commandes sont reçues et validées par le serveur,
    /// mais pas encore exécutées (étape K4) : elles sont refusées avec un message explicite.
    /// </summary>
    public sealed class Ksp1GameControls : IGameControls
    {
        private static CommandResult NotYet() =>
            CommandResult.Rejected("Commandes non prises en charge dans cette version du mod pour KSP 1.");

        public CommandResult SetThrottle(float amount, bool relative) => NotYet();

        public CommandResult SetSas(bool? enabled) => NotYet();

        public CommandResult SetRcs(bool? enabled) => NotYet();

        public CommandResult SetSpeedMode(SpeedMode mode) => NotYet();
    }
}
