using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OrbitDeck.Mod.Protocol
{
    public enum ClientMessageKind
    {
        Ping,
        Command
    }

    /// <summary>Résultat de l'analyse d'un message client : un message valide, ou une erreur à renvoyer.</summary>
    public sealed class ParsedMessage
    {
        public const int MaxIdLength = 64;

        public bool IsValid => ErrorCode == null;

        public ClientMessageKind Kind { get; set; }

        /// <summary>Identifiant de la commande (ou de la requête en erreur, si lisible).</summary>
        public string Id { get; set; }

        public string CommandName { get; set; }

        public JObject Args { get; set; } = new JObject();

        public string ErrorCode { get; set; }

        public string ErrorMessage { get; set; }
    }

    /// <summary>Analyse et valide les messages client → serveur (protocole v1).</summary>
    public static class ProtocolParser
    {
        public static ParsedMessage Parse(string json)
        {
            JObject message;
            try
            {
                // Profondeur limitée : un message imbriqué à l'excès ne doit pas pouvoir saturer la pile.
                using (var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 16 })
                {
                    message = JToken.Load(reader) as JObject;
                    if (reader.Read())
                        return Fail(null, "Contenu inattendu après le message JSON.");
                }
            }
            catch (JsonException)
            {
                return Fail(null, "Message JSON invalide.");
            }

            if (message == null)
                return Fail(null, "Le message doit être un objet JSON.");

            var id = ReadString(message, "id");

            var version = message["v"];
            if (version == null || version.Type != JTokenType.Integer || version.Value<int>() != ProtocolMessages.Version)
                return Fail(id, $"Version de protocole non supportée (attendu : {ProtocolMessages.Version}).");

            var type = ReadString(message, "type");
            switch (type)
            {
                case "ping":
                    return new ParsedMessage { Kind = ClientMessageKind.Ping };

                case "command":
                    return ParseCommand(message, id);

                default:
                    return Fail(id, type == null ? "Champ « type » manquant." : $"Type de message inconnu : « {Truncate(type)} ».");
            }
        }

        private static ParsedMessage ParseCommand(JObject message, string id)
        {
            if (string.IsNullOrEmpty(id))
                return Fail(null, "Champ « id » manquant ou invalide.");

            if (id.Length > ParsedMessage.MaxIdLength)
                return Fail(null, $"Champ « id » trop long (maximum {ParsedMessage.MaxIdLength} caractères).");

            var name = ReadString(message, "name");
            if (string.IsNullOrEmpty(name))
                return Fail(id, "Champ « name » manquant ou invalide.");

            var argsToken = message["args"];
            if (argsToken != null && argsToken.Type != JTokenType.Null && argsToken.Type != JTokenType.Object)
                return Fail(id, "Le champ « args » doit être un objet.");

            return new ParsedMessage
            {
                Kind = ClientMessageKind.Command,
                Id = id,
                CommandName = name,
                Args = argsToken as JObject ?? new JObject()
            };
        }

        private static ParsedMessage Fail(string id, string message) =>
            new ParsedMessage { Id = id, ErrorCode = ErrorCodes.BadRequest, ErrorMessage = message };

        private static string ReadString(JObject message, string name)
        {
            var token = message[name];
            return token != null && token.Type == JTokenType.String ? token.Value<string>() : null;
        }

        private static string Truncate(string value) => value.Length <= 32 ? value : value.Substring(0, 32) + "…";
    }
}
