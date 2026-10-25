using System;
using KSP.Game;
using KSP.Messages;
using KSP.Sim;
using KSP.Sim.Definitions;
using KSP.Sim.State;
using StreamPlug.Mod.Telemetry;
using GameSpeedMode = KSP.Sim.SpeedDisplayMode;

namespace StreamPlug.Mod.Commands
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
