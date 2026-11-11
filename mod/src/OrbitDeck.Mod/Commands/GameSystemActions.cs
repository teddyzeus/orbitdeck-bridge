using System;
using KSP.Game;
using KSP.Modules;
using KSP.Sim.impl;

namespace OrbitDeck.Mod.Commands
{
    /// <summary>
    /// Commandes 2.0 de KSP 2 (panneaux, antennes, parachutes, amarrage, sauvegarde rapide). Thread principal uniquement.
    /// Même chemin que le jeu : on agit sur les modules de pièces du véhicule actif, jamais sur un autre véhicule.
    /// </summary>
    internal static class GameSystemActions
    {
        private static T Behaviour<T>(PartComponent part) where T : UnityEngine.Component
        {
            var behaviour = GameManager.Instance?.Game?.ViewController?.GetBehaviorIfLoaded(part);
            return behaviour != null ? behaviour.GetComponent<T>() : null;
        }

        public static CommandResult SetDeployables(bool solar, bool enabled)
        {
            if (!Controllable(out var vessel, out var failure))
                return failure;

            var found = 0;
            foreach (var part in vessel.SimulationObject.PartOwner.Parts)
            {
                Data_Deployable data = null;
                if (solar)
                {
                    OrbitDeck.Mod.Telemetry.GameBlocks.SolarData(part, out data);
                }
                else if (part.TryGetModule<PartComponentModule_DataTransmitter>(out _))
                {
                    part.TryGetModuleData<PartComponentModule_Deployable, Data_Deployable>(out data);
                }

                if (data == null)
                    continue;

                found++;
                if (!enabled && !data.retractable)
                    continue;

                // Même chemin que le bouton du jeu : la propriété « déployé » du module, que le comportement de la pièce applique.
                data.toggleExtend.SetValue(enabled);
            }

            return found > 0 ? CommandResult.Ok : CommandResult.Rejected(solar ? "Aucun panneau solaire." : "Aucune antenne déployable.");
        }

        public static CommandResult DeployParachutes()
        {
            if (!Controllable(out var vessel, out var failure))
                return failure;

            var done = 0;
            foreach (var part in vessel.SimulationObject.PartOwner.Parts)
            {
                if (!part.TryGetModuleData<PartComponentModule_Parachute, Data_Parachute>(out var data))
                    continue;

                var state = data.deployState.GetValue();
                if (state != Data_Parachute.DeploymentStates.STOWED)
                    continue;

                var module = Behaviour<Module_Parachute>(part);
                if (module != null)
                    module.DeployNow();
                else
                    data.armedToggle.SetValue(true);
                done++;
            }

            return done > 0 ? CommandResult.Ok : CommandResult.Rejected("Aucun parachute prêt à se déployer.");
        }

        public static CommandResult Undock()
        {
            if (!Controllable(out var vessel, out var failure))
                return failure;

            foreach (var part in vessel.SimulationObject.PartOwner.Parts)
            {
                if (!part.TryGetModule<PartComponentModule_DockingNode>(out var node) || !node.CanUndock())
                    continue;

                var module = Behaviour<Module_DockingNode>(part);
                if (module == null)
                    continue;

                // « Undock » n'est pas publique dans le jeu : on appelle la même méthode que l'action de la pièce.
                var method = typeof(Module_DockingNode).GetMethod("Undock", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (method == null)
                    return CommandResult.Rejected("Désamarrage indisponible.");

                method.Invoke(module, null);
                return CommandResult.Ok;
            }

            return CommandResult.Rejected("Rien n'est amarré.");
        }

        public static CommandResult QuickSave()
        {
            var manager = GameManager.Instance?.Game?.SaveLoadManager;
            if (manager == null)
                return CommandResult.Rejected("Sauvegarde indisponible.");

            manager.ForceQuickSave();
            return CommandResult.Ok;
        }

        public static CommandResult QuickLoad()
        {
            var manager = GameManager.Instance?.Game?.SaveLoadManager;
            if (manager == null)
                return CommandResult.Rejected("Chargement indisponible.");

            var method = typeof(SaveLoadManager).GetMethod("QuickLoadInternal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (method == null)
                return CommandResult.Rejected("Chargement indisponible.");

            method.Invoke(manager, null);
            return CommandResult.Ok;
        }

        private static bool Controllable(out VesselComponent vessel, out CommandResult failure)
        {
            failure = null;
            if (!OrbitDeck.Mod.Telemetry.GameAccess.TryGetFlightVessel(out _, out vessel, out _))
            {
                failure = CommandResult.NoVessel();
                return false;
            }

            if (!vessel.IsControllable)
            {
                failure = CommandResult.NotControllable();
                return false;
            }

            return true;
        }
    }
}
