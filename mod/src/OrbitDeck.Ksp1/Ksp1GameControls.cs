using System;
using KSP.UI.Screens;
using OrbitDeck.Mod.Commands;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Ksp1
{
    /// <summary>
    /// Exécute les commandes dans KSP 1 (thread principal uniquement). Reproduit les chemins et les garde-fous de la
    /// saisie clavier du jeu (FlightInputHandler) : mêmes appels, mêmes verrous, mêmes refus.
    /// </summary>
    public sealed class Ksp1GameControls : IGameControls
    {
        private const float ThrottleChangeEpsilon = 1e-5f;

        public CommandResult SetThrottle(float amount, bool relative)
        {
            if (!TryGetControllableVessel(out var vessel, out var failure))
                return failure;

            // Mêmes verrous que le clavier : touches de poussée (relatif) ou Z / X (absolu).
            if (!InputLockManager.IsUnlocked(relative ? ControlTypes.THROTTLE : ControlTypes.THROTTLE_CUT_MAX))
                return CommandResult.Rejected("La poussée est verrouillée par le jeu.");

            if (TimeWarp.CurrentRateIndex > 0 && TimeWarp.WarpMode == TimeWarp.Modes.HIGH)
                return CommandResult.Rejected("Poussée verrouillée pendant l'accélération du temps.");

            // Le gestionnaire d'entrées du jeu relit cette valeur à chaque image (c'est ce qu'écrivent aussi Z et X).
            var before = FlightInputHandler.state.mainThrottle;
            var target = Clamp01(relative ? before + amount : amount);
            if (Math.Abs(before - target) > ThrottleChangeEpsilon)
                FlightInputHandler.state.mainThrottle = target;

            return CommandResult.Ok;
        }

        public CommandResult SetSas(bool? enabled)
        {
            if (!TryGetControllableVessel(out var vessel, out var failure))
                return failure;

            if (!InputLockManager.IsUnlocked(ControlTypes.SAS) || vessel.ActionControlBlocked(KSPActionGroup.SAS))
                return CommandResult.Rejected("Le SAS est verrouillé par le jeu.");

            var current = vessel.ActionGroups[KSPActionGroup.SAS];
            var desired = enabled ?? !current;
            if (desired == current)
                return CommandResult.Ok;

            if (desired && !vessel.Autopilot.SAS.CanEngageSAS())
                return CommandResult.Rejected("Le SAS n'est pas disponible sur ce véhicule (pilote ou sonde adaptée requis).");

            // Comme la touche T du jeu.
            FlightInputHandler.state.killRot = !FlightInputHandler.state.killRot;
            vessel.ActionGroups.ToggleGroup(KSPActionGroup.SAS);

            return vessel.ActionGroups[KSPActionGroup.SAS] == desired
                ? CommandResult.Ok
                : CommandResult.Rejected("Le jeu n'a pas changé l'état du SAS.");
        }

        public CommandResult SetRcs(bool? enabled)
        {
            if (!TryGetControllableVessel(out var vessel, out var failure))
                return failure;

            if (!InputLockManager.IsUnlocked(ControlTypes.RCS) || vessel.ActionControlBlocked(KSPActionGroup.RCS))
                return CommandResult.Rejected("Le RCS est verrouillé par le jeu.");

            var current = vessel.ActionGroups[KSPActionGroup.RCS];
            var desired = enabled ?? !current;
            if (desired == current)
                return CommandResult.Ok;

            // Comme la touche R du jeu.
            vessel.ActionGroups.ToggleGroup(KSPActionGroup.RCS);
            FlightInputHandler.fetch.rcslock = !FlightInputHandler.fetch.rcslock;

            return vessel.ActionGroups[KSPActionGroup.RCS] == desired
                ? CommandResult.Ok
                : CommandResult.Rejected("Le jeu n'a pas changé l'état du RCS.");
        }

        public CommandResult SetSasMode(SasModeKind mode)
        {
            if (!TryGetControllableVessel(out var vessel, out var failure))
                return failure;

            if (!InputLockManager.IsUnlocked(ControlTypes.SAS) || vessel.ActionControlBlocked(KSPActionGroup.SAS))
                return CommandResult.Rejected("Le SAS est verrouillé par le jeu.");

            var autopilot = vessel.Autopilot;
            if (!autopilot.SAS.CanEngageSAS())
                return CommandResult.Rejected("Le SAS n'est pas disponible sur ce véhicule (pilote ou sonde adaptée requis).");

            var target = ToGameMode(mode);
            if (!autopilot.CanSetMode(target))
                return CommandResult.Rejected(DescribeUnavailable(mode));

            // Allume le SAS comme la touche T, puis choisit le mode tout de suite : sinon le jeu le démarre en mode
            // « stabilité » à l'image suivante et écrase notre choix.
            if (!vessel.ActionGroups[KSPActionGroup.SAS])
            {
                FlightInputHandler.state.killRot = !FlightInputHandler.state.killRot;
                vessel.ActionGroups.ToggleGroup(KSPActionGroup.SAS);
                if (!vessel.ActionGroups[KSPActionGroup.SAS])
                    return CommandResult.Rejected("Le jeu n'a pas allumé le SAS.");
            }

            var applied = autopilot.Enabled ? autopilot.SetMode(target) : autopilot.Enable(target);
            return applied && autopilot.Enabled && autopilot.Mode == target
                ? CommandResult.Ok
                : CommandResult.Rejected("Le jeu n'a pas appliqué ce mode du SAS.");
        }

        public CommandResult ActivateStage()
        {
            if (!TryGetControllableVessel(out var vessel, out var failure))
                return failure;

            // Mêmes garde-fous que la touche Espace : verrou des étages (Maj + L), puis blocage du groupe d'actions « étage ».
            if (!InputLockManager.IsUnlocked(ControlTypes.STAGING))
                return CommandResult.Rejected("Le déclenchement des étages est verrouillé par le jeu (Maj + L).");

            if (!vessel.ActionControlBlocked(KSPActionGroup.Stage))
                StageManager.ActivateNextStage();

            vessel.ActionGroups.ToggleGroup(KSPActionGroup.Stage);
            return CommandResult.Ok;
        }

        public CommandResult SetView(ViewKind? view)
        {
            if (!TryGetVessel(out _, out var failure))
                return failure;

            // Comme la touche M du jeu : verrou de bascule, puis entrée ou sortie de la vue carte.
            if (!InputLockManager.IsUnlocked(ControlTypes.MAP_TOGGLE) || MapView.fetch == null)
                return CommandResult.Rejected("La vue carte est verrouillée par le jeu.");

            var current = MapView.MapIsEnabled;
            var desired = view.HasValue ? view.Value == ViewKind.Map : !current;
            if (desired == current)
                return CommandResult.Ok;

            if (desired)
                MapView.EnterMapView();
            else
                MapView.ExitMapView();

            return CommandResult.Ok;
        }

        public CommandResult SetLights(bool? enabled) =>
            SetActionGroup(KSPActionGroup.Light, ControlTypes.GROUP_LIGHTS, enabled, "Les phares", useSet: false);

        public CommandResult SetBrakes(bool? enabled) =>
            SetActionGroup(KSPActionGroup.Brakes, ControlTypes.GROUP_BRAKES, enabled, "Les freins", useSet: true);

        public CommandResult SetGear(bool? enabled) =>
            SetActionGroup(KSPActionGroup.Gear, ControlTypes.GROUP_GEARS, enabled, "Le train d'atterrissage", useSet: false);

        /// <summary>Même chemin que les touches du jeu : verrou de saisie, blocage du groupe, puis bascule du groupe d'actions.</summary>
        private static CommandResult SetActionGroup(KSPActionGroup group, ControlTypes lockType, bool? enabled, string label, bool useSet)
        {
            if (!TryGetControllableVessel(out var vessel, out var failure))
                return failure;

            if (!InputLockManager.IsUnlocked(lockType) || vessel.ActionControlBlocked(group))
                return CommandResult.Rejected($"{label} : commande verrouillée par le jeu.");

            var current = vessel.ActionGroups[group];
            var desired = enabled ?? !current;
            if (desired == current)
                return CommandResult.Ok;

            // Les freins sont tenus par la touche (SetGroup) ; les phares et le train basculent (ToggleGroup).
            if (useSet)
                vessel.ActionGroups.SetGroup(group, desired);
            else
                vessel.ActionGroups.ToggleGroup(group);

            return vessel.ActionGroups[group] == desired
                ? CommandResult.Ok
                : CommandResult.Rejected($"{label} : le jeu n'a pas changé l'état.");
        }

        private static string DescribeUnavailable(SasModeKind mode)
        {
            switch (mode)
            {
                case SasModeKind.Target:
                case SasModeKind.AntiTarget:
                    return "Aucune cible sélectionnée.";
                case SasModeKind.Maneuver:
                    return "Aucun nœud de manœuvre.";
                case SasModeKind.Normal:
                case SasModeKind.Antinormal:
                case SasModeKind.RadialIn:
                case SasModeKind.RadialOut:
                    return FlightGlobals.speedDisplayMode != FlightGlobals.SpeedDisplayModes.Orbit
                        ? "Ce mode du SAS n'est disponible qu'avec le navball en mode orbite."
                        : "Ce mode du SAS n'est pas disponible dans la situation actuelle.";
                default:
                    return "Ce mode du SAS n'est pas disponible dans la situation actuelle.";
            }
        }

        internal static VesselAutopilot.AutopilotMode ToGameMode(SasModeKind mode)
        {
            switch (mode)
            {
                case SasModeKind.Prograde: return VesselAutopilot.AutopilotMode.Prograde;
                case SasModeKind.Retrograde: return VesselAutopilot.AutopilotMode.Retrograde;
                case SasModeKind.Normal: return VesselAutopilot.AutopilotMode.Normal;
                case SasModeKind.Antinormal: return VesselAutopilot.AutopilotMode.Antinormal;
                case SasModeKind.RadialIn: return VesselAutopilot.AutopilotMode.RadialIn;
                case SasModeKind.RadialOut: return VesselAutopilot.AutopilotMode.RadialOut;
                case SasModeKind.Target: return VesselAutopilot.AutopilotMode.Target;
                case SasModeKind.AntiTarget: return VesselAutopilot.AutopilotMode.AntiTarget;
                case SasModeKind.Maneuver: return VesselAutopilot.AutopilotMode.Maneuver;
                default: return VesselAutopilot.AutopilotMode.StabilityAssist;
            }
        }

        public CommandResult SetSpeedMode(SpeedMode mode)
        {
            if (!TryGetVessel(out var vessel, out var failure))
                return failure;

            if (mode == SpeedMode.Target && vessel.targetObject == null)
                return CommandResult.Rejected("Aucune cible sélectionnée.");

            FlightGlobals.SetSpeedMode(ToGameMode(mode));
            return CommandResult.Ok;
        }

        private static bool TryGetVessel(out Vessel vessel, out CommandResult failure)
        {
            vessel = HighLogic.LoadedSceneIsFlight ? FlightGlobals.ActiveVessel : null;
            failure = vessel == null ? CommandResult.NoVessel() : null;
            return vessel != null;
        }

        /// <summary>Véhicule actif, pilotable : pas de kerbal en EVA, avec un pilote ou une sonde en état de contrôle.</summary>
        private static bool TryGetControllableVessel(out Vessel vessel, out CommandResult failure)
        {
            if (!TryGetVessel(out vessel, out failure))
                return false;

            if (vessel.isEVA)
            {
                failure = CommandResult.Rejected("Cette commande n'est pas disponible en EVA.");
                return false;
            }

            if (!vessel.IsControllable)
            {
                failure = CommandResult.NotControllable();
                return false;
            }

            return true;
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        private static FlightGlobals.SpeedDisplayModes ToGameMode(SpeedMode mode)
        {
            switch (mode)
            {
                case SpeedMode.Orbit: return FlightGlobals.SpeedDisplayModes.Orbit;
                case SpeedMode.Target: return FlightGlobals.SpeedDisplayModes.Target;
                default: return FlightGlobals.SpeedDisplayModes.Surface;
            }
        }
    }
}
