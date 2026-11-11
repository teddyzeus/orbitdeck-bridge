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

        /// <summary>Affiche la vue du véhicule ou la carte ; <paramref name="view"/> null = basculer.</summary>
        CommandResult SetView(ViewKind? view);

        /// <summary>Passe au palier de temps suivant (depuis la pause : reprend à 1× puis monte d'un palier).</summary>
        CommandResult IncreaseWarp();

        /// <summary>Passe au palier de temps précédent.</summary>
        CommandResult DecreaseWarp();

        /// <summary>Retour immédiat à 1× : annule un warp automatique et sort de la pause. Fonctionne toujours.</summary>
        CommandResult StopWarp();

        /// <summary>Fixe le palier dont le facteur vaut <paramref name="rate"/> dans le mode courant.</summary>
        CommandResult SetWarpRate(double rate);

        /// <summary>Met le jeu en pause ou le reprend ; <paramref name="paused"/> null = inverser.</summary>
        CommandResult SetPaused(bool? paused);

        /// <summary>Warpe jusqu'à <paramref name="margin"/> secondes avant l'allumage du nœud d'instant <paramref name="nodeUt"/>.</summary>
        CommandResult WarpToManeuver(double nodeUt, double margin, double? maxRate);

        // 2.0 : systèmes du véhicule et sauvegarde rapide (ajouts à la fin, rien d'existant modifié).

        /// <summary>Déploie ou rentre tous les panneaux solaires rétractables.</summary>
        CommandResult SetSolarPanels(bool enabled);

        /// <summary>Déploie ou rentre toutes les antennes déployables.</summary>
        CommandResult SetAntennas(bool enabled);

        /// <summary>Sort ou rentre les échelles (KSP 1 seulement).</summary>
        CommandResult SetLadders(bool enabled);

        /// <summary>Déploie les parachutes prêts. Irréversible.</summary>
        CommandResult DeployParachutes();

        /// <summary>Arme ou désarme les ports d'amarrage (refusé quand le jeu ne le permet pas).</summary>
        CommandResult SetDockingArmed(bool enabled);

        /// <summary>Sépare le véhicule du port amarré. Irréversible.</summary>
        CommandResult Undock();

        CommandResult QuickSave();

        /// <summary>Recharge la sauvegarde rapide. Irréversible (perte de progression).</summary>
        CommandResult QuickLoad();
    }
}
