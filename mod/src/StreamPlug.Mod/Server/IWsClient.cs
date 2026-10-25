namespace StreamPlug.Mod.Server
{
    /// <summary>Client WebSocket connecté au serveur local. Utilisable depuis n'importe quel thread.</summary>
    public interface IWsClient
    {
        long Id { get; }

        string RemoteEndPoint { get; }

        /// <summary>Met un message texte en file d'envoi. Un client trop lent est déconnecté.</summary>
        void Send(string text);

        /// <summary>Demande la fermeture propre de la connexion.</summary>
        void Close(int code, string reason);
    }
}
