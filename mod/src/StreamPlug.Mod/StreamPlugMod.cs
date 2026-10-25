using System.IO;
using System.Reflection;
using SpaceWarp2.API.Mods;
using StreamPlug.Mod.Config;
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
        }
    }
}
