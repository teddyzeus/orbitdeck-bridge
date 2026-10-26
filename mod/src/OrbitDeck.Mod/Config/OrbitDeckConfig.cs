using System;
using System.IO;
using Newtonsoft.Json;

namespace OrbitDeck.Mod.Config
{
    /// <summary>Configuration du mod, lue dans orbitdeck-config.json (dossier du mod).</summary>
    public sealed class OrbitDeckConfig
    {
        public const string FileName = "orbitdeck-config.json";

        public const int MinPort = 1024;
        public const int MaxPort = 65535;
        public const int MinSampleRateHz = 1;
        public const int MaxSampleRateHz = 60;

        [JsonProperty("port")]
        public int Port { get; set; } = 18765;

        [JsonProperty("sampleRateHz")]
        public int SampleRateHz { get; set; } = 10;

        [JsonProperty("verboseLogging")]
        public bool VerboseLogging { get; set; }

        /// <summary>
        /// Charge la configuration. Fichier absent : valeurs par défaut (et fichier créé).
        /// Fichier illisible : valeurs par défaut et avertissement. Valeurs hors bornes : ramenées dans les bornes.
        /// </summary>
        public static OrbitDeckConfig Load(string path, Action<string> warn)
        {
            var config = new OrbitDeckConfig();

            try
            {
                if (File.Exists(path))
                    JsonConvert.PopulateObject(File.ReadAllText(path), config);
                else
                    File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
            }
            catch (Exception exception)
            {
                warn?.Invoke($"Configuration illisible ({path}), valeurs par défaut utilisées : {exception.Message}");
                config = new OrbitDeckConfig();
            }

            config.Clamp(warn);
            return config;
        }

        private void Clamp(Action<string> warn)
        {
            var port = Math.Min(MaxPort, Math.Max(MinPort, Port));
            if (port != Port)
            {
                warn?.Invoke($"port {Port} hors bornes [{MinPort}, {MaxPort}], remplacé par {port}.");
                Port = port;
            }

            var rate = Math.Min(MaxSampleRateHz, Math.Max(MinSampleRateHz, SampleRateHz));
            if (rate != SampleRateHz)
            {
                warn?.Invoke($"sampleRateHz {SampleRateHz} hors bornes [{MinSampleRateHz}, {MaxSampleRateHz}], remplacé par {rate}.");
                SampleRateHz = rate;
            }
        }
    }
}
