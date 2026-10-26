using System.IO;
using System.Reflection;
using SpaceWarp2.API.Mods;
using OrbitDeck.Mod.Commands;
using OrbitDeck.Mod.Config;
using OrbitDeck.Mod.Server;
using UnityEngine;

namespace OrbitDeck.Mod
{
    /// <summary>Point d'entrée du mod, chargé par le chargeur de mods de Redux.</summary>
    public class OrbitDeckMod : GeneralMod
    {
        private GameObject _host;

        public override void OnInitialized()
        {
            var modFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var config = OrbitDeckConfig.Load(
                Path.Combine(modFolder ?? ".", OrbitDeckConfig.FileName),
                message => SWLogger.LogWarning($"[OrbitDeck] {message}"));

            SWLogger.LogInfo(
                $"[OrbitDeck] Mod chargé (port {config.Port}, {config.SampleRateHz} Hz, logs détaillés : {config.VerboseLogging}).");

            _host = new GameObject("OrbitDeck");
            Object.DontDestroyOnLoad(_host);
            var bridge = _host.AddComponent<GameBridge>();
            bridge.Logger = SWLogger;
            bridge.Config = config;

            var webSocket = new WsServer(
                config.Port,
                verbose: config.VerboseLogging,
                logInfo: message => SWLogger.LogInfo($"[OrbitDeck] {message}"),
                logError: message => SWLogger.LogError($"[OrbitDeck] {message}"));

            var commands = new CommandQueue(message => SWLogger.LogError($"[OrbitDeck] {message}"));
            if (config.VerboseLogging)
            {
                commands.Processed = (pending, result) => SWLogger.LogInfo(
                    $"[OrbitDeck] commande {pending.Id} {pending.Command.Kind} → "
                    + (result.Success ? "ok" : $"{result.ErrorCode} ({result.Message})"));
            }

            bridge.Commands = commands;

            var server = new OrbitDeckServer(
                webSocket,
                bridge,
                commands,
                modVersion: Assembly.GetExecutingAssembly().GetName().Version.ToString(3),
                gameVersion: Application.version,
                logError: message => SWLogger.LogError($"[OrbitDeck] {message}"));

            if (server.Start())
                bridge.Server = server;
            else
                SWLogger.LogError($"[OrbitDeck] Le serveur n'a pas pu démarrer (port {config.Port}). Le mod continue sans Stream Deck.");
        }
    }
}
