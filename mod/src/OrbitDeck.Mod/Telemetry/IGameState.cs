namespace OrbitDeck.Mod.Telemetry
{
    /// <summary>
    /// Accès à l'état du jeu. Doit être appelé uniquement depuis le thread principal Unity.
    /// L'implémentation de production est <see cref="GameState"/> ; les tests utilisent un faux.
    /// </summary>
    public interface IGameState
    {
        /// <summary>Lit l'état courant. Ne renvoie jamais null ; peut lever une exception si le jeu est dans un état inattendu.</summary>
        TelemetrySnapshot Read();
    }
}
