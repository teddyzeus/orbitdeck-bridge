using UnityEngine;

namespace OrbitDeck.Ksp1
{
    /// <summary>
    /// Point d'entrée du mod, chargé une seule fois au démarrage du jeu (étape K1 : vérifie seulement que le mod
    /// est chargé et que l'API du jeu est accessible).
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class OrbitDeckAddon : MonoBehaviour
    {
        private void Awake()
        {
            Log($"Mod chargé (v{typeof(OrbitDeckAddon).Assembly.GetName().Version}).");
            Log($"Jeu : KSP {Versioning.GetVersionString()}, Unity {Application.unityVersion}.");
            DontDestroyOnLoad(gameObject);
        }

        internal static void Log(string message)
        {
            Debug.Log($"[OrbitDeck] {message}");
        }
    }

    /// <summary>À chaque entrée en vol : lit le véhicule actif (étape K1 : preuve que l'API répond).</summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class OrbitDeckFlightProbe : MonoBehaviour
    {
        private void Start()
        {
            var vessel = FlightGlobals.ActiveVessel;
            if (vessel == null)
            {
                OrbitDeckAddon.Log("Scène de vol sans véhicule actif.");
                return;
            }

            OrbitDeckAddon.Log(
                $"Vol : « {vessel.vesselName} », altitude mer {vessel.altitude:F0} m, "
                + $"contrôlable : {vessel.IsControllable}.");
        }
    }
}
