using System;
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
