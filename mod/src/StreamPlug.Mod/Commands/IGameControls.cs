using StreamPlug.Mod.Telemetry;

namespace StreamPlug.Mod.Commands
{
    /// <summary>
    /// Actions possibles sur le véhicule actif. Doit être appelé uniquement depuis le thread principal Unity.
    /// L'implémentation de production est <see cref="GameControls"/> ; les tests utilisent un faux.
    /// </summary>
    public interface IGameControls
    {
        /// <summary>Règle la poussée (0 à 1), ou la modifie de <paramref name="amount"/> si <paramref name="relative"/>.</summary>
        CommandResult SetThrottle(float amount, bool relative);

        /// <summary>Active ou coupe le SAS ; <paramref name="enabled"/> null = inverser.</summary>
        CommandResult SetSas(bool? enabled);

        /// <summary>Active ou coupe le RCS ; <paramref name="enabled"/> null = inverser.</summary>
        CommandResult SetRcs(bool? enabled);

        CommandResult SetSpeedMode(SpeedMode mode);
    }
}
