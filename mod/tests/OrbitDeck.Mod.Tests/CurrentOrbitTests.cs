using Newtonsoft.Json.Linq;
using OrbitDeck.Mod.Protocol;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Mod.Tests;

/// <summary>Orbite courante (F1) : construction commune à KSP 1 et KSP 2 depuis les valeurs brutes du jeu, annonce de la famille.</summary>
public class CurrentOrbitTests
{
    private static CurrentOrbitInfo Closed(double ut = 1000) =>
        CurrentOrbitInfo.FromGame(ut, 0.01, 85000, 84000, 2540.3, 28.5, 600, 1900, "Kerbin", 70000);

    [Fact]
    public void A_closed_orbit_keeps_everything_and_turns_delays_into_absolute_instants()
    {
        var orbit = Closed(1000);

        Assert.False(orbit.Escape);
        Assert.Equal(85000, orbit.Ap);
        Assert.Equal(84000, orbit.Pe);
        Assert.Equal(2540.3, orbit.Period);
        Assert.Equal(28.5, orbit.Inc);
        Assert.Equal(1600, orbit.ApUt);
        Assert.Equal(2900, orbit.PeUt);
        Assert.Equal("Kerbin", orbit.Body);
        Assert.Equal(70000, orbit.Atmosphere);
    }

    [Fact]
    public void An_open_orbit_has_no_apoapsis_period_or_time_to_apoapsis()
    {
        var orbit = CurrentOrbitInfo.FromGame(1000, 1.2, double.NaN, 50000, double.NaN, 10, double.NaN, 300, "Kerbin", null);

        Assert.True(orbit.Escape);
        Assert.Null(orbit.Ap);
        Assert.Null(orbit.Period);
        Assert.Null(orbit.ApUt);
        Assert.Equal(50000, orbit.Pe);
        Assert.Equal(1300, orbit.PeUt);
        Assert.Null(orbit.Atmosphere);
    }

    [Fact]
    public void An_exactly_parabolic_orbit_is_open()
    {
        Assert.True(CurrentOrbitInfo.FromGame(0, 1.0, 1, 1, 1, 1, 1, 1, "X", null).Escape);
        Assert.False(CurrentOrbitInfo.FromGame(0, 0.9999, 1, 1, 1, 1, 1, 1, "X", null).Escape);
    }

    [Fact]
    public void Non_finite_values_are_unknown_never_zero()
    {
        var orbit = CurrentOrbitInfo.FromGame(1000, 0.1, double.PositiveInfinity, double.NaN, double.NaN, double.NegativeInfinity, double.NaN, double.NaN, null, double.NaN);

        Assert.Null(orbit.Ap);
        Assert.Null(orbit.Pe);
        Assert.Null(orbit.Period);
        Assert.Null(orbit.Inc);
        Assert.Null(orbit.ApUt);
        Assert.Null(orbit.PeUt);
        Assert.Null(orbit.Body);
        Assert.Null(orbit.Atmosphere);
    }

    [Fact]
    public void A_passage_already_made_is_unknown()
    {
        var orbit = CurrentOrbitInfo.FromGame(1000, 0.1, 1, 1, 1, 1, -5, 100, "K", null);

        Assert.Null(orbit.ApUt);
        Assert.Equal(1100, orbit.PeUt);
    }

    [Fact]
    public void A_body_without_atmosphere_has_none_and_an_empty_name_is_unknown()
    {
        var orbit = CurrentOrbitInfo.FromGame(0, 0.1, 1, 1, 1, 1, 1, 1, "", 0);

        Assert.Null(orbit.Atmosphere);
        Assert.Null(orbit.Body);
    }

    [Fact]
    public void The_state_carries_the_orbit_without_touching_the_maneuver_orbit()
    {
        var snapshot = new TelemetrySnapshot
        {
            Status = VesselStatus.Connected, VesselName = "X",
            Maneuver = new ManeuverInfo { Count = 0 },
            Orbit = Closed()
        };
        var message = JObject.Parse(ProtocolMessages.State(snapshot, 1));

        Assert.Equal(1600.0, (double)message["orbit"]!["apUt"]!);
        Assert.Null(message["maneuver"]!["before"]);
    }

    [Fact]
    public void Hello_announces_the_orbit_family_once_the_mod_provides_it()
    {
        var message = JObject.Parse(ProtocolMessages.Hello("0.6.0", "x", Features.Announced));

        Assert.Contains("orbit", message["features"]!.Select(t => (string?)t));
    }

    [Fact]
    public void The_orbit_is_republished_when_it_changes_beyond_the_thresholds_only()
    {
        TelemetrySnapshot With(CurrentOrbitInfo orbit) => new() { Status = VesselStatus.Connected, VesselName = "X", Orbit = orbit };

        Assert.False(SnapshotComparer.HasChanged(With(Closed(1000)), With(Closed(1000.0))));
        Assert.True(SnapshotComparer.HasChanged(With(Closed(1000)), With(Closed(1010))));
    }
}
