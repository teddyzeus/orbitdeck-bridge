using System;
using KSP.Game;
using KSP.Messages;
using KSP.Sim;
using KSP.Sim.Definitions;
using KSP.Sim.impl;
using KSP.Sim.State;
using OrbitDeck.Mod.Telemetry;
using GameAutopilotMode = KSP.Sim.AutopilotMode;
using GameSpeedMode = KSP.Sim.SpeedDisplayMode;

namespace OrbitDeck.Mod.Commands
{
    /// <summary>
    /// Exécute les commandes dans le jeu (thread principal uniquement). Reproduit les chemins et les garde-fous
    /// de la saisie clavier du jeu (FlightInputHandler) : mêmes appels, mêmes refus.
    /// </summary>
    public sealed class GameControls : IGameControls
    {
        private const float ThrottleChangeEpsilon = 1e-5f;

        public CommandResult SetThrottle(float amount, bool relative)
        {
            if (!GameAccess.TryGetFlightVessel(out var vehicle, out var vessel, out _))
                return CommandResult.NoVessel();

            if (!vessel.IsControllable)
                return CommandResult.NotControllable();

            var game = GameManager.Instance.Game;
            var timeWarp = game.ViewController.TimeWarp;
            if (timeWarp.IsWarping && !timeWarp.IsPhysicsTimeWarp && !game.ViewController.IsPaused)
                return CommandResult.Rejected("Poussée verrouillée pendant l'accélération du temps.");

            if (vessel.IsKerbalEVA)
                return CommandResult.Rejected("La poussée n'est pas disponible en EVA.");

            var allowed = relative ? vessel.HasControlForThrottleDelta() : vessel.HasControlForThrottleMinMax();
            if (!allowed)
            {
                vessel.NotifyInsufficientVesselControl();
                return CommandResult.NotControllable("Contrôle insuffisant du véhicule pour régler la poussée.");
            }

            var before = vehicle.mainThrottle;
            var target = Clamp01(relative ? before + amount : amount);

            vehicle.SetMainThrottle(target);

            if (Math.Abs(before - target) > ThrottleChangeEpsilon)
                game.Messages.Publish<ThrottleChangedMessage>();

            return CommandResult.Ok;
        }

        public CommandResult SetSas(bool? enabled)
        {
            if (!GameAccess.TryGetFlightVessel(out var vehicle, out var vessel, out _))
                return CommandResult.NoVessel();

            if (vessel.ControlStatus == VesselControlState.NoControl)
            {
                vessel.NotifyInsufficientVesselControl();
                return CommandResult.NotControllable("Contrôle insuffisant du véhicule pour utiliser le SAS.");
            }

            var current = vessel.AutopilotStatus.IsEnabled;
            var desired = enabled ?? !current;
            if (desired == current)
                return CommandResult.Ok;

            if (!vessel.SetAutopilotEnableDisable(desired))
                return CommandResult.Rejected("Le SAS n'est pas disponible sur ce véhicule.");

            // Comme la saisie clavier : maintient le groupe d'actions SAS synchronisé avec l'état du pilote automatique.
            vehicle.TriggerGroup(KSPActionGroup.SAS);
            return CommandResult.Ok;
        }

        public CommandResult SetRcs(bool? enabled)
        {
            if (!GameAccess.TryGetFlightVessel(out var vehicle, out var vessel, out _))
                return CommandResult.NoVessel();

            var current = vessel.IsRCSEnabled;
            var desired = enabled ?? !current;
            if (desired == current)
                return CommandResult.Ok;

            var state = default(VesselState);
            state.isRCSEnabled = desired;
            vessel.SetState(state, null);

            // Le jeu peut refuser (par exemple un kerbal en EVA sans jetpack utilisable).
            if (vessel.IsRCSEnabled != desired)
                return CommandResult.Rejected("Le RCS n'est pas disponible pour le moment.");

            vehicle.TriggerGroup(KSPActionGroup.RCS);
            return CommandResult.Ok;
        }

        public CommandResult SetSpeedMode(SpeedMode mode)
        {
            if (!GameAccess.TryGetFlightVessel(out var vehicle, out var vessel, out _))
                return CommandResult.NoVessel();

            if (mode == SpeedMode.Target && vessel.TargetObject == null)
                return CommandResult.Rejected("Aucune cible sélectionnée.");

            vehicle.SetSpeedDisplayMode(ToGameMode(mode));
            return CommandResult.Ok;
        }

        public CommandResult SetSasMode(SasModeKind mode)
        {
            if (!TryGetControllableVessel(out var vehicle, out var vessel, out var failure))
                return failure;

            if (vessel.ControlStatus == VesselControlState.NoControl)
            {
                vessel.NotifyInsufficientVesselControl();
                return CommandResult.NotControllable("Contrôle insuffisant du véhicule pour utiliser le SAS.");
            }

            var target = ToGameMode(mode);
            if ((mode == SasModeKind.Target || mode == SasModeKind.AntiTarget) && vessel.TargetObject == null)
                return CommandResult.Rejected("Aucune cible sélectionnée.");

            // Allume le SAS comme la touche du jeu, puis choisit le mode.
            if (!vessel.AutopilotStatus.IsEnabled)
            {
                if (!vessel.SetAutopilotEnableDisable(true))
                    return CommandResult.Rejected("Le SAS n'est pas disponible sur ce véhicule.");

                vehicle.TriggerGroup(KSPActionGroup.SAS);
            }

            var autopilot = vessel.Autopilot;
            if (autopilot == null || !autopilot.CanSetMode(target) || !vessel.SetAutopilotMode(target))
                return CommandResult.Rejected(DescribeUnavailableMode(mode));

            return CommandResult.Ok;
        }

        public CommandResult SetLights(bool? enabled) => SetActionGroup(KSPActionGroup.Lights, enabled, "phares", useSet: false);

        public CommandResult SetBrakes(bool? enabled) => SetActionGroup(KSPActionGroup.Brakes, enabled, "freins", useSet: true);

        public CommandResult SetGear(bool? enabled) => SetActionGroup(KSPActionGroup.Gear, enabled, "train d'atterrissage", useSet: false);

        /// <summary>Même chemin que les touches du jeu : le groupe d'actions est basculé (phares, train) ou réglé (freins).</summary>
        private static CommandResult SetActionGroup(KSPActionGroup group, bool? enabled, string label, bool useSet)
        {
            if (!TryGetControllableVessel(out var vehicle, out var vessel, out var failure))
                return failure;

            var state = vessel.GetActionGroupState(group);
            if (state == KSPActionGroupState.None)
                return CommandResult.Rejected($"Ce véhicule n'a pas de {label}.");

            var current = state == KSPActionGroupState.True;
            var desired = enabled ?? !current;
            if (desired == current)
                return CommandResult.Ok;

            if (useSet)
                vehicle.SetGroup(group, desired);
            else
                vehicle.TriggerGroup(group);

            return CommandResult.Ok;
        }

        // Le déclenchement d'étage passe, dans le jeu, par la gestion du lancement (séquence de pré-lancement, vol) :
        // pas encore pris en charge pour KSP 2.
        public CommandResult ActivateStage() =>
            CommandResult.Rejected("Le déclenchement d'étage n'est pas encore pris en charge dans le mod pour KSP 2.");

        private static bool TryGetControllableVessel(out VesselVehicle vehicle, out VesselComponent vessel, out CommandResult failure)
        {
            failure = null;
            if (!GameAccess.TryGetFlightVessel(out vehicle, out vessel, out _))
            {
                failure = CommandResult.NoVessel();
                return false;
            }

            if (vessel.IsKerbalEVA)
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

        private static string DescribeUnavailableMode(SasModeKind mode)
        {
            switch (mode)
            {
                case SasModeKind.Maneuver:
                    return "Aucun nœud de manœuvre.";
                case SasModeKind.Target:
                case SasModeKind.AntiTarget:
                    return "Aucune cible sélectionnée.";
                default:
                    return "Ce mode du SAS n'est pas disponible dans la situation actuelle.";
            }
        }

        private static GameAutopilotMode ToGameMode(SasModeKind mode)
        {
            switch (mode)
            {
                case SasModeKind.Prograde: return GameAutopilotMode.Prograde;
                case SasModeKind.Retrograde: return GameAutopilotMode.Retrograde;
                case SasModeKind.Normal: return GameAutopilotMode.Normal;
                case SasModeKind.Antinormal: return GameAutopilotMode.Antinormal;
                case SasModeKind.RadialIn: return GameAutopilotMode.RadialIn;
                case SasModeKind.RadialOut: return GameAutopilotMode.RadialOut;
                case SasModeKind.Target: return GameAutopilotMode.Target;
                case SasModeKind.AntiTarget: return GameAutopilotMode.AntiTarget;
                case SasModeKind.Maneuver: return GameAutopilotMode.Maneuver;
                default: return GameAutopilotMode.StabilityAssist;
            }
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        private static GameSpeedMode ToGameMode(SpeedMode mode)
        {
            switch (mode)
            {
                case SpeedMode.Orbit: return GameSpeedMode.Orbit;
                case SpeedMode.Target: return GameSpeedMode.Target;
                default: return GameSpeedMode.Surface;
            }
        }
    }
}
