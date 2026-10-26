using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Mod.Tests;

public class SnapshotComparerTests
{
    private static TelemetrySnapshot Base() => new()
    {
        Status = VesselStatus.Connected,
        VesselName = "Kerbal X",
        AltitudeSeaLevel = 1000,
        AltitudeSurface = 990,
        SpeedSurface = 100,
        SpeedOrbital = 200,
        SpeedTarget = null,
        SpeedMode = SpeedMode.Surface,
        Throttle = 0.5f,
        SasEnabled = true,
        SasMode = "StabilityAssist",
        RcsEnabled = false
    };

    [Fact]
    public void Identical_snapshots_are_not_changed()
    {
        Assert.False(SnapshotComparer.HasChanged(Base(), Base()));
        Assert.False(SnapshotComparer.HasDiscreteChange(Base(), Base()));
    }

    [Fact]
    public void First_snapshot_is_always_a_change()
    {
        Assert.True(SnapshotComparer.HasChanged(null, Base()));
    }

    [Fact]
    public void Two_nulls_are_not_a_change()
    {
        Assert.False(SnapshotComparer.HasChanged(null, null));
    }

    [Theory]
    [InlineData(0.4, false)]
    [InlineData(0.6, true)]
    [InlineData(-0.6, true)]
    public void Altitude_change_respects_threshold(double delta, bool expected)
    {
        var next = new TelemetrySnapshot
        {
            Status = VesselStatus.Connected, VesselName = "Kerbal X", AltitudeSeaLevel = 1000 + delta,
            AltitudeSurface = 990, SpeedSurface = 100, SpeedOrbital = 200, SpeedMode = SpeedMode.Surface,
            Throttle = 0.5f, SasEnabled = true, SasMode = "StabilityAssist"
        };
        Assert.Equal(expected, SnapshotComparer.HasChanged(Base(), next));
        Assert.False(SnapshotComparer.HasDiscreteChange(Base(), next));
    }

    [Theory]
    [InlineData(0.04, false)]
    [InlineData(0.06, true)]
    public void Speed_change_respects_threshold(double delta, bool expected)
    {
        var next = new TelemetrySnapshot
        {
            Status = VesselStatus.Connected, VesselName = "Kerbal X", AltitudeSeaLevel = 1000,
            AltitudeSurface = 990, SpeedSurface = 100 + delta, SpeedOrbital = 200, SpeedMode = SpeedMode.Surface,
            Throttle = 0.5f, SasEnabled = true, SasMode = "StabilityAssist"
        };
        Assert.Equal(expected, SnapshotComparer.HasChanged(Base(), next));
    }

    [Theory]
    [InlineData(0.003f, false)]
    [InlineData(0.01f, true)]
    public void Throttle_change_respects_threshold_and_is_discrete(float delta, bool expected)
    {
        var next = new TelemetrySnapshot
        {
            Status = VesselStatus.Connected, VesselName = "Kerbal X", AltitudeSeaLevel = 1000,
            AltitudeSurface = 990, SpeedSurface = 100, SpeedOrbital = 200, SpeedMode = SpeedMode.Surface,
            Throttle = 0.5f + delta, SasEnabled = true, SasMode = "StabilityAssist"
        };
        Assert.Equal(expected, SnapshotComparer.HasChanged(Base(), next));
        Assert.Equal(expected, SnapshotComparer.HasDiscreteChange(Base(), next));
    }

    [Fact]
    public void Sas_rcs_status_and_speed_mode_are_discrete_changes()
    {
        var sasOff = new TelemetrySnapshot
        {
            Status = VesselStatus.Connected, VesselName = "Kerbal X", AltitudeSeaLevel = 1000, AltitudeSurface = 990,
            SpeedSurface = 100, SpeedOrbital = 200, SpeedMode = SpeedMode.Surface, Throttle = 0.5f,
            SasEnabled = false, SasMode = "StabilityAssist"
        };
        var rcsOn = new TelemetrySnapshot
        {
            Status = VesselStatus.Connected, VesselName = "Kerbal X", AltitudeSeaLevel = 1000, AltitudeSurface = 990,
            SpeedSurface = 100, SpeedOrbital = 200, SpeedMode = SpeedMode.Surface, Throttle = 0.5f,
            SasEnabled = true, SasMode = "StabilityAssist", RcsEnabled = true
        };
        var orbitMode = new TelemetrySnapshot
        {
            Status = VesselStatus.Connected, VesselName = "Kerbal X", AltitudeSeaLevel = 1000, AltitudeSurface = 990,
            SpeedSurface = 100, SpeedOrbital = 200, SpeedMode = SpeedMode.Orbit, Throttle = 0.5f,
            SasEnabled = true, SasMode = "StabilityAssist"
        };
        var notControllable = new TelemetrySnapshot
        {
            Status = VesselStatus.NotControllable, VesselName = "Kerbal X", AltitudeSeaLevel = 1000, AltitudeSurface = 990,
            SpeedSurface = 100, SpeedOrbital = 200, SpeedMode = SpeedMode.Surface, Throttle = 0.5f,
            SasEnabled = true, SasMode = "StabilityAssist"
        };

        Assert.True(SnapshotComparer.HasDiscreteChange(Base(), sasOff));
        Assert.True(SnapshotComparer.HasDiscreteChange(Base(), rcsOn));
        Assert.True(SnapshotComparer.HasDiscreteChange(Base(), orbitMode));
        Assert.True(SnapshotComparer.HasDiscreteChange(Base(), notControllable));
    }

    [Fact]
    public void Game_state_change_is_a_mode_change()
    {
        var inFlight = TelemetrySnapshot.NoVesselIn("FlightView");
        var inMenu = TelemetrySnapshot.NoVesselIn("MainMenu");

        Assert.True(SnapshotComparer.HasModeChange(inFlight, inMenu));
        Assert.False(SnapshotComparer.HasChanged(inMenu, TelemetrySnapshot.NoVesselIn("MainMenu")));
    }

    [Fact]
    public void Throttle_alone_is_not_a_mode_change()
    {
        var next = new TelemetrySnapshot
        {
            Status = VesselStatus.Connected, VesselName = "Kerbal X", AltitudeSeaLevel = 1000, AltitudeSurface = 990,
            SpeedSurface = 100, SpeedOrbital = 200, SpeedMode = SpeedMode.Surface, Throttle = 0.9f,
            SasEnabled = true, SasMode = "StabilityAssist"
        };
        Assert.False(SnapshotComparer.HasModeChange(Base(), next));
        Assert.True(SnapshotComparer.HasDiscreteChange(Base(), next));
    }

    [Fact]
    public void Sas_toggle_is_a_mode_change()
    {
        var next = new TelemetrySnapshot
        {
            Status = VesselStatus.Connected, VesselName = "Kerbal X", AltitudeSeaLevel = 1000, AltitudeSurface = 990,
            SpeedSurface = 100, SpeedOrbital = 200, SpeedMode = SpeedMode.Surface, Throttle = 0.5f,
            SasEnabled = false, SasMode = "StabilityAssist"
        };
        Assert.True(SnapshotComparer.HasModeChange(Base(), next));
    }

    [Fact]
    public void Target_speed_appearing_is_a_change()
    {
        var withTarget = new TelemetrySnapshot
        {
            Status = VesselStatus.Connected, VesselName = "Kerbal X", AltitudeSeaLevel = 1000, AltitudeSurface = 990,
            SpeedSurface = 100, SpeedOrbital = 200, SpeedTarget = 5.0, SpeedMode = SpeedMode.Surface, Throttle = 0.5f,
            SasEnabled = true, SasMode = "StabilityAssist"
        };
        Assert.True(SnapshotComparer.HasChanged(Base(), withTarget));
        Assert.True(SnapshotComparer.HasChanged(withTarget, Base()));
    }

    [Fact]
    public void Going_to_no_vessel_is_a_change()
    {
        Assert.True(SnapshotComparer.HasChanged(Base(), TelemetrySnapshot.NoVessel));
        Assert.True(SnapshotComparer.HasChanged(TelemetrySnapshot.NoVessel, Base()));
        Assert.False(SnapshotComparer.HasChanged(TelemetrySnapshot.NoVessel, TelemetrySnapshot.NoVessel));
    }
}
