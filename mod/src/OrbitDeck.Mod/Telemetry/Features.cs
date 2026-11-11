namespace OrbitDeck.Mod.Telemetry
{
    /// <summary>
    /// Familles que le mod 2.0 sait lire ou commander, annoncées dans « hello » (docs/SPEC-2.0-TECH.md §2.2 du dépôt du plugin).
    /// Chaque lot de la 2.0 ajoute la sienne quand elle est réalisée sur les deux jeux.
    /// </summary>
    public static class Features
    {
        public static readonly string[] Announced = { "orbit", "resources", "performance", "landing", "target", "systems", "docking", "quickSave" };
    }
}
