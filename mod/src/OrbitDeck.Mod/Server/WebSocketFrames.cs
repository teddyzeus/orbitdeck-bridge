using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OrbitDeck.Mod.Server
{
    internal sealed class WebSocketFrame
    {
        public bool Fin { get; set; }

        public int Opcode { get; set; }

        public byte[] Payload { get; set; }
    }

    /// <summary>Violation du protocole WebSocket : la connexion doit être fermée avec <see cref="CloseCode"/>.</summary>
    internal sealed class WebSocketProtocolException : Exception
    {
        public WebSocketProtocolException(int closeCode, string message) : base(message)
        {
            CloseCode = closeCode;
        }

        public int CloseCode { get; }
    }

    /// <summary>Encodage et décodage des trames WebSocket (RFC 6455 §5).</summary>
    internal static class WebSocketFrames
    {
        public const int Continuation = 0x0;
        public const int Text = 0x1;
        public const int Binary = 0x2;
        public const int Close = 0x8;
        public const int Ping = 0x9;
        public const int Pong = 0xA;

        public const int CloseNormal = 1000;
        public const int CloseGoingAway = 1001;
        public const int CloseProtocolError = 1002;
        public const int CloseUnsupportedData = 1003;
        public const int CloseNoStatus = 1005;
        public const int CloseInvalidPayload = 1007;
        public const int ClosePolicyViolation = 1008;
        public const int CloseMessageTooBig = 1009;

        /// <summary>Lit une trame envoyée par un client (obligatoirement masquée).</summary>
        public static async Task<WebSocketFrame> ReadAsync(Stream stream, int maxPayload, CancellationToken cancellationToken)
        {
            var header = new byte[2];
            await ReadExactAsync(stream, header, 2, cancellationToken).ConfigureAwait(false);

            var fin = (header[0] & 0x80) != 0;
            if ((header[0] & 0x70) != 0)
                throw new WebSocketProtocolException(CloseProtocolError, "Bits RSV non supportés.");

            var opcode = header[0] & 0x0F;
            var masked = (header[1] & 0x80) != 0;
            long length = header[1] & 0x7F;

            if (!masked)
                throw new WebSocketProtocolException(CloseProtocolError, "Trame client non masquée.");

            if (length == 126)
            {
                var ext = new byte[2];
                await ReadExactAsync(stream, ext, 2, cancellationToken).ConfigureAwait(false);
                length = (ext[0] << 8) | ext[1];
            }
            else if (length == 127)
            {
                var ext = new byte[8];
                await ReadExactAsync(stream, ext, 8, cancellationToken).ConfigureAwait(false);
                length = 0;
                for (var i = 0; i < 8; i++)
                    length = (length << 8) | ext[i];
                if (length < 0)
                    throw new WebSocketProtocolException(CloseMessageTooBig, "Longueur de trame invalide.");
            }

            var isControl = opcode >= Close;
            if (isControl && (!fin || length > 125))
                throw new WebSocketProtocolException(CloseProtocolError, "Trame de contrôle invalide.");

            if (length > maxPayload)
                throw new WebSocketProtocolException(CloseMessageTooBig, "Message trop grand.");

            var mask = new byte[4];
            await ReadExactAsync(stream, mask, 4, cancellationToken).ConfigureAwait(false);

            var payload = new byte[length];
            await ReadExactAsync(stream, payload, (int)length, cancellationToken).ConfigureAwait(false);
            for (var i = 0; i < payload.Length; i++)
                payload[i] ^= mask[i & 3];

            return new WebSocketFrame { Fin = fin, Opcode = opcode, Payload = payload };
        }

        /// <summary>Construit une trame serveur (non masquée, FIN positionné).</summary>
        public static byte[] Build(int opcode, byte[] payload)
        {
            payload = payload ?? new byte[0];
            int headerLength;
            if (payload.Length < 126)
                headerLength = 2;
            else if (payload.Length <= ushort.MaxValue)
                headerLength = 4;
            else
                headerLength = 10;

            var frame = new byte[headerLength + payload.Length];
            frame[0] = (byte)(0x80 | opcode);

            if (headerLength == 2)
            {
                frame[1] = (byte)payload.Length;
            }
            else if (headerLength == 4)
            {
                frame[1] = 126;
                frame[2] = (byte)(payload.Length >> 8);
                frame[3] = (byte)payload.Length;
            }
            else
            {
                frame[1] = 127;
                long len = payload.Length;
                for (var i = 0; i < 8; i++)
                    frame[2 + i] = (byte)(len >> (8 * (7 - i)));
            }

            Buffer.BlockCopy(payload, 0, frame, headerLength, payload.Length);
            return frame;
        }

        public static byte[] BuildText(string text) => Build(Text, Encoding.UTF8.GetBytes(text));

        public static byte[] BuildClose(int code, string reason)
        {
            var reasonBytes = Encoding.UTF8.GetBytes(reason ?? string.Empty);
            if (reasonBytes.Length > 123)
                Array.Resize(ref reasonBytes, 123);

            var payload = new byte[2 + reasonBytes.Length];
            payload[0] = (byte)(code >> 8);
            payload[1] = (byte)code;
            Buffer.BlockCopy(reasonBytes, 0, payload, 2, reasonBytes.Length);
            return Build(Close, payload);
        }

        private static async Task ReadExactAsync(Stream stream, byte[] buffer, int count, CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < count)
            {
                var read = await stream.ReadAsync(buffer, offset, count - offset, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    throw new EndOfStreamException("Connexion fermée par le client.");
                offset += read;
            }
        }
    }
}
