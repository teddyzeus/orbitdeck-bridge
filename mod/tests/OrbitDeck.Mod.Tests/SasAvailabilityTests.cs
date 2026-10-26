using Newtonsoft.Json.Linq;
using OrbitDeck.Mod.Protocol;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Mod.Tests;

public class SasAvailabilityTests
{
    [Fact]
    public void State_lists_the_selectable_sas_modes_with_the_command_names()
    {
        var snapshot = WithModes(SasModeKind.StabilityAssist, SasModeKind.Prograde, SasModeKind.RadialIn, SasModeKind.RadialOut, SasModeKind.AntiTarget, SasModeKind.Maneuver);

        var message = JObject.Parse(ProtocolMessages.State(snapshot, 1));

        var available = message["sas"]?["available"]?.Select(token => (string)token!).ToArray();
        Assert.Equal(new[] { "stabilityAssist", "prograde", "radialIn", "radialOut", "antiTarget", "maneuver" }, available);
    }

    [Fact]
    public void An_empty_list_means_the_sas_is_unusable()
    {
        var message = JObject.Parse(ProtocolMessages.State(WithModes(), 1));

        var available = Assert.IsType<JArray>(message["sas"]?["available"]);
        Assert.Empty(available);
    }

    [Fact]
    public void State_omits_the_list_when_the_game_does_not_say()
    {
        var message = JObject.Parse(ProtocolMessages.State(Snapshots.InFlight(), 1));

        Assert.NotNull(message["sas"]);
        Assert.Null(message["sas"]?["available"]);
    }

    [Fact]
    public void Every_sas_mode_has_a_distinct_name_accepted_by_the_command_validator()
    {
        var names = new HashSet<string>();
        foreach (SasModeKind mode in Enum.GetValues(typeof(SasModeKind)))
        {
            var name = ProtocolMessages.SasModeName(mode);
            Assert.True(names.Add(name), $"nom en double : {name}");

            var result = OrbitDeck.Mod.Commands.CommandValidator.Validate("setSasMode", new JObject { ["mode"] = name });
            Assert.True(result.IsValid, $"« {name} » refusé par la commande setSasMode");
        }
    }

    [Fact]
    public void A_change_in_the_available_modes_is_a_mode_change()
    {
        var before = WithModes(SasModeKind.StabilityAssist, SasModeKind.Prograde);
        var after = WithModes(SasModeKind.StabilityAssist, SasModeKind.Prograde, SasModeKind.Maneuver);

        Assert.True(SnapshotComparer.HasModeChange(before, after));
        Assert.True(SnapshotComparer.HasChanged(before, after));
    }

    [Fact]
    public void The_same_modes_in_a_new_list_are_not_a_change()
    {
        var before = WithModes(SasModeKind.StabilityAssist, SasModeKind.Prograde);
        var after = WithModes(SasModeKind.StabilityAssist, SasModeKind.Prograde);

        Assert.False(SnapshotComparer.HasChanged(before, after));
    }

    [Fact]
    public void Unknown_and_empty_lists_differ()
    {
        Assert.True(SnapshotComparer.HasModeChange(WithModes(), Snapshots.InFlight()));
        Assert.True(SnapshotComparer.HasModeChange(Snapshots.InFlight(), WithModes()));
    }

    private static TelemetrySnapshot WithModes(params SasModeKind[] modes) => new()
    {
        Status = VesselStatus.Connected,
        VesselName = "Kerbal X",
        AvailableSasModes = modes
    };
}
