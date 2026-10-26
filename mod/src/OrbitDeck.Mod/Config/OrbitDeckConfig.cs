using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

        public int Port { get; set; } = 18765;

        public int SampleRateHz { get; set; } = 10;

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
                    config.Apply(JObject.Parse(File.ReadAllText(path)));
                else
                    File.WriteAllText(path, config.ToJson());
            }
            catch (Exception exception)
            {
                warn?.Invoke($"Configuration illisible ({path}), valeurs par défaut utilisées : {exception.Message}");
                config = new OrbitDeckConfig();
            }

            config.Clamp(warn);
            return config;
        }

        // Lecture par l'arbre JSON (JObject) et non par la sérialisation automatique (JsonConvert) : cette dernière
        // charge System.Data, absent de KSP 1.
        private void Apply(JObject json)
        {
            Port = ReadInt(json, "port", Port);
            SampleRateHz = ReadInt(json, "sampleRateHz", SampleRateHz);
            VerboseLogging = ReadBool(json, "verboseLogging", VerboseLogging);
        }

        private string ToJson() =>
            new JObject { ["port"] = Port, ["sampleRateHz"] = SampleRateHz, ["verboseLogging"] = VerboseLogging }
                .ToString(Formatting.Indented);

        private static int ReadInt(JObject json, string name, int fallback)
        {
            var token = json[name];
            if (token == null)
                return fallback;
            if (token.Type != JTokenType.Integer)
                throw new FormatException($"« {name} » doit être un nombre entier.");
            return checked((int)token.Value<long>());
        }

        private static bool ReadBool(JObject json, string name, bool fallback)
        {
            var token = json[name];
            if (token == null)
                return fallback;
            if (token.Type != JTokenType.Boolean)
                throw new FormatException($"« {name} » doit être vrai ou faux.");
            return token.Value<bool>();
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
