using System.IO;
using System.Reflection;
using OrbitDeck.Mod.Commands;
using OrbitDeck.Mod.Config;
using OrbitDeck.Mod.Server;
using UnityEngine;

namespace OrbitDeck.Ksp1
{
    /// <summary>
    /// Point d'entrée du mod, chargé une seule fois au démarrage du jeu : lit la configuration, démarre le serveur
    /// WebSocket local et crée le composant qui échantillonne le jeu.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class OrbitDeckAddon : MonoBehaviour
    {
        private void Awake()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var modFolder = Path.GetDirectoryName(assembly.Location);
            var config = OrbitDeckConfig.Load(
                Path.Combine(modFolder ?? ".", OrbitDeckConfig.FileName),
                message => Debug.LogWarning($"[OrbitDeck] {message}"));

            Log($"Mod chargé (v{assembly.GetName().Version.ToString(3)}, port {config.Port}, {config.SampleRateHz} Hz, logs détaillés : {config.VerboseLogging}).");
            Log($"Jeu : KSP {Versioning.GetVersionString()}, Unity {Application.unityVersion}.");
            DontDestroyOnLoad(gameObject);

            var bridge = gameObject.AddComponent<Ksp1Bridge>();
            bridge.Config = config;

            var webSocket = new WsServer(
                config.Port,
                verbose: config.VerboseLogging,
                logInfo: Log,
                logError: message => Debug.LogError($"[OrbitDeck] {message}"));

            var commands = new CommandQueue(message => Debug.LogError($"[OrbitDeck] {message}"));
            if (config.VerboseLogging)
            {
                commands.Processed = (pending, result) => Log(
                    $"commande {pending.Id} {pending.Command.Kind} → "
                    + (result.Success ? "ok" : $"{result.ErrorCode} ({result.Message})"));
            }

            bridge.Commands = commands;

            var server = new OrbitDeckServer(
                webSocket,
                bridge,
                commands,
                modVersion: assembly.GetName().Version.ToString(3),
                gameVersion: Versioning.GetVersionString(),
                logError: message => Debug.LogError($"[OrbitDeck] {message}"));

            if (server.Start())
                bridge.Server = server;
            else
                Debug.LogError($"[OrbitDeck] Le serveur n'a pas pu démarrer (port {config.Port}). Le mod continue sans Stream Deck.");
        }

        internal static void Log(string message)
        {
            Debug.Log($"[OrbitDeck] {message}");
        }
    }
}
