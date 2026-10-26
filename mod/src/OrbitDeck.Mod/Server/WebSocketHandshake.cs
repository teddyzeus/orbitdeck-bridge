using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OrbitDeck.Mod.Server
{
    internal sealed class HandshakeRequest
    {
        public string Method { get; set; }

        public string Path { get; set; }

        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>Poignée de main HTTP → WebSocket (RFC 6455 §4), avec contrôles de sécurité locaux.</summary>
    internal static class WebSocketHandshake
    {
        public const int MaxRequestBytes = 8192;

        private const string AcceptGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        public static string ComputeAccept(string key)
        {
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(Encoding.ASCII.GetBytes(key + AcceptGuid));
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>Lit la requête HTTP jusqu'à la ligne vide. Retourne null si elle est invalide ou trop grande.</summary>
        public static async Task<HandshakeRequest> ReadRequestAsync(Stream stream, CancellationToken cancellationToken)
        {
            var bytes = new List<byte>(512);
            var one = new byte[1];

            while (bytes.Count < MaxRequestBytes)
            {
                var read = await stream.ReadAsync(one, 0, 1, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    return null;

                bytes.Add(one[0]);
                var n = bytes.Count;
                if (n >= 4 && bytes[n - 4] == '\r' && bytes[n - 3] == '\n' && bytes[n - 2] == '\r' && bytes[n - 1] == '\n')
                    return Parse(Encoding.ASCII.GetString(bytes.ToArray()));
            }

            return null;
        }

        internal static HandshakeRequest Parse(string text)
        {
            var lines = text.Split(new[] { "\r\n" }, StringSplitOptions.None);
            var requestLine = lines[0].Split(' ');
            if (requestLine.Length < 3)
                return null;

            var request = new HandshakeRequest { Method = requestLine[0], Path = requestLine[1] };
            for (var i = 1; i < lines.Length; i++)
            {
                if (lines[i].Length == 0)
                    break;
                var colon = lines[i].IndexOf(':');
                if (colon <= 0)
                    return null;
                request.Headers[lines[i].Substring(0, colon).Trim()] = lines[i].Substring(colon + 1).Trim();
            }

            return request;
        }

        /// <summary>Valide la requête. Retourne la réponse HTTP complète à envoyer ; <paramref name="accepted"/> indique si la connexion est acceptée.</summary>
        public static string Validate(HandshakeRequest request, int port, out bool accepted)
        {
            accepted = false;

            if (request == null || request.Method != "GET")
                return ErrorResponse(400, "Bad Request");

            var host = request.Header("Host");
            if (host == null)
                return ErrorResponse(400, "Bad Request");

            // Protège contre le « DNS rebinding » : seul l'accès local direct est accepté.
            if (!string.Equals(host, "127.0.0.1:" + port, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(host, "localhost:" + port, StringComparison.OrdinalIgnoreCase))
                return ErrorResponse(403, "Forbidden");

            // Les navigateurs envoient toujours Origin ; notre plugin Node n'en envoie pas.
            if (request.Header("Origin") != null)
                return ErrorResponse(403, "Forbidden");

            var upgrade = request.Header("Upgrade");
            var connection = request.Header("Connection");
            if (upgrade == null || upgrade.IndexOf("websocket", StringComparison.OrdinalIgnoreCase) < 0
                || connection == null || connection.IndexOf("upgrade", StringComparison.OrdinalIgnoreCase) < 0)
                return ErrorResponse(426, "Upgrade Required");

            if (request.Header("Sec-WebSocket-Version") != "13")
                return "HTTP/1.1 426 Upgrade Required\r\nSec-WebSocket-Version: 13\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";

            var key = request.Header("Sec-WebSocket-Key");
            if (string.IsNullOrWhiteSpace(key))
                return ErrorResponse(400, "Bad Request");

            accepted = true;
            return "HTTP/1.1 101 Switching Protocols\r\n"
                   + "Upgrade: websocket\r\n"
                   + "Connection: Upgrade\r\n"
                   + "Sec-WebSocket-Accept: " + ComputeAccept(key) + "\r\n\r\n";
        }

        internal static string ErrorResponse(int code, string reason) =>
            "HTTP/1.1 " + code + " " + reason + "\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
    }
}
