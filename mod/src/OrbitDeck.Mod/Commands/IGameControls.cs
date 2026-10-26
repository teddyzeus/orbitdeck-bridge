using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Mod.Commands
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

        /// <summary>Choisit le mode du SAS (l'active au besoin).</summary>
        CommandResult SetSasMode(SasModeKind mode);

        /// <summary>Allume ou éteint les phares ; <paramref name="enabled"/> null = inverser.</summary>
        CommandResult SetLights(bool? enabled);

        /// <summary>Actionne ou relâche les freins ; <paramref name="enabled"/> null = inverser.</summary>
        CommandResult SetBrakes(bool? enabled);

        /// <summary>Sort ou rentre le train d'atterrissage ; <paramref name="enabled"/> null = inverser.</summary>
        CommandResult SetGear(bool? enabled);

        /// <summary>Déclenche l'étage suivant (touche Espace du jeu). Action irréversible : non idempotente.</summary>
        CommandResult ActivateStage();
    }
}
