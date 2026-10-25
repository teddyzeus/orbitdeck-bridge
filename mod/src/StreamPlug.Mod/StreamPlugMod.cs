using SpaceWarp2.API.Mods;
using UnityEngine;

namespace StreamPlug.Mod
{
    /// <summary>Point d'entrée du mod, chargé par le chargeur de mods de Redux.</summary>
    public class StreamPlugMod : GeneralMod
    {
        private GameObject _host;

        public override void OnInitialized()
        {
            SWLogger.LogInfo("[StreamPlug] Mod chargé.");

            _host = new GameObject("StreamPlug");
            Object.DontDestroyOnLoad(_host);
            var bridge = _host.AddComponent<GameBridge>();
            bridge.Logger = SWLogger;
        }
    }
}
