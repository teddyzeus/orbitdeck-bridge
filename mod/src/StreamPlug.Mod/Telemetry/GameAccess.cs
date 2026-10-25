using KSP.Game;
using KSP.Sim.impl;

namespace StreamPlug.Mod.Telemetry
{
    /// <summary>Accès commun au véhicule actif, partagé par la lecture (GameState) et les commandes (GameControls).</summary>
    internal static class GameAccess
    {
        /// <summary>
        /// Retourne le véhicule actif si le joueur est en vol. Hors vol (menu, centre spatial, éditeur…), le jeu peut
        /// conserver le dernier véhicule en mémoire : l'état global du jeu fait foi.
        /// </summary>
        /// <param name="gameStateName">État global du jeu (FlightView, MainMenu…), même quand aucun véhicule n'est actif.</param>
        public static bool TryGetFlightVessel(out VesselVehicle vehicle, out VesselComponent vessel, out string gameStateName)
        {
            vehicle = null;
            vessel = null;
            gameStateName = null;

            var manager = GameManager.Instance;
            if (manager == null || manager.Game == null || manager.Game.ViewController == null)
                return false;

            var stateConfig = manager.Game.GlobalGameState?.GetGameState();
            gameStateName = stateConfig?.GameState.ToString();
            if (stateConfig != null && !stateConfig.IsFlightMode)
                return false;

            var viewController = manager.Game.ViewController;
            var activeVehicle = viewController.ActiveVesselVehicle;
            if (activeVehicle == null || !viewController.TryGetActiveSimVessel(out var activeVessel, true) || activeVessel == null)
                return false;

            vehicle = activeVehicle;
            vessel = activeVessel;
            return true;
        }
    }
}
