using System.IO;
using System.Reflection;
using SpaceWarp2.API.Mods;
using StreamPlug.Mod.Commands;
using StreamPlug.Mod.Config;
using StreamPlug.Mod.Server;
using UnityEngine;

namespace StreamPlug.Mod
{
    /// <summary>Point d'entrée du mod, chargé par le chargeur de mods de Redux.</summary>
    public class StreamPlugMod : GeneralMod
    {
        private GameObject _host;

        public override void OnInitialized()
        {
            var modFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var config = StreamPlugConfig.Load(
                Path.Combine(modFolder ?? ".", StreamPlugConfig.FileName),
                message => SWLogger.LogWarning($"[StreamPlug] {message}"));

            SWLogger.LogInfo(
                $"[StreamPlug] Mod chargé (port {config.Port}, {config.SampleRateHz} Hz, logs détaillés : {config.VerboseLogging}).");

            _host = new GameObject("StreamPlug");
            Object.DontDestroyOnLoad(_host);
            var bridge = _host.AddComponent<GameBridge>();
            bridge.Logger = SWLogger;
            bridge.Config = config;

            var webSocket = new WsServer(
                config.Port,
                verbose: config.VerboseLogging,
                logInfo: message => SWLogger.LogInfo($"[StreamPlug] {message}"),
                logError: message => SWLogger.LogError($"[StreamPlug] {message}"));

            var commands = new CommandQueue(message => SWLogger.LogError($"[StreamPlug] {message}"));
            if (config.VerboseLogging)
            {
                commands.Processed = (pending, result) => SWLogger.LogInfo(
                    $"[StreamPlug] commande {pending.Id} {pending.Command.Kind} → "
                    + (result.Success ? "ok" : $"{result.ErrorCode} ({result.Message})"));
            }

            bridge.Commands = commands;

            var server = new StreamPlugServer(
                webSocket,
                bridge,
                commands,
                modVersion: Assembly.GetExecutingAssembly().GetName().Version.ToString(3),
                gameVersion: Application.version,
                logError: message => SWLogger.LogError($"[StreamPlug] {message}"));

            if (server.Start())
                bridge.Server = server;
            else
                SWLogger.LogError($"[StreamPlug] Le serveur n'a pas pu démarrer (port {config.Port}). Le mod continue sans Stream Deck.");
        }
    }
}
