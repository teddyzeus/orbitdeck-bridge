namespace StreamPlug.Mod.Telemetry
{
    /// <summary>Source du dernier snapshot publié. Lisible depuis n'importe quel thread.</summary>
    public interface ITelemetrySource
    {
        /// <summary>Dernier snapshot publié ; jamais null.</summary>
        TelemetrySnapshot Latest { get; }

        /// <summary>Numéro du dernier snapshot ; croît à chaque changement publié.</summary>
        long Sequence { get; }
    }
}
