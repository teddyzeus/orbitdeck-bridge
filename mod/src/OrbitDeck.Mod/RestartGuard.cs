using System.Collections.Generic;

namespace OrbitDeck.Mod
{
    /// <summary>
    /// Limite les redémarrages automatiques du pont : si le jeu détruit l'objet du mod en boucle, mieux vaut
    /// abandonner (avec un message dans le journal) que relancer indéfiniment.
    /// </summary>
    internal sealed class RestartGuard
    {
        private readonly int _maxRestarts;
        private readonly long _windowMs;
        private readonly Queue<long> _restarts = new Queue<long>();

        public RestartGuard(int maxRestarts, long windowMs)
        {
            _maxRestarts = maxRestarts;
            _windowMs = windowMs;
        }

        /// <summary>Enregistre un redémarrage à l'instant <paramref name="nowMs"/>. Retourne false s'il y en a déjà trop dans la fenêtre.</summary>
        public bool TryRestart(long nowMs)
        {
            while (_restarts.Count > 0 && nowMs - _restarts.Peek() >= _windowMs)
                _restarts.Dequeue();

            if (_restarts.Count >= _maxRestarts)
                return false;

            _restarts.Enqueue(nowMs);
            return true;
        }
    }
}
