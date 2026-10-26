using Newtonsoft.Json.Linq;
using OrbitDeck.Mod.Protocol;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Mod.Tests;

public class StageCountTests
{
    [Fact]
    public void State_contains_the_stage_count_when_the_game_provides_it()
    {
        var snapshot = new TelemetrySnapshot { Status = VesselStatus.Connected, VesselName = "Kerbal X", StageCount = 6 };

        var message = JObject.Parse(ProtocolMessages.State(snapshot, 1));

        Assert.Equal(6, (int?)message["stage"]?["count"]);
    }

    [Fact]
    public void State_omits_the_stage_when_the_game_does_not_provide_it()
    {
        var message = JObject.Parse(ProtocolMessages.State(Snapshots.InFlight(), 1));

        Assert.Null(message["stage"]);
    }

    [Fact]
    public void State_keeps_a_stage_count_of_zero()
    {
        var snapshot = new TelemetrySnapshot { Status = VesselStatus.Connected, VesselName = "Kerbal X", StageCount = 0 };

        var message = JObject.Parse(ProtocolMessages.State(snapshot, 1));

        Assert.Equal(0, (int?)message["stage"]?["count"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void State_contains_whether_the_stages_are_exhausted(bool exhausted)
    {
        var snapshot = new TelemetrySnapshot { Status = VesselStatus.Connected, VesselName = "Kerbal X", StageCount = 1, StageExhausted = exhausted };

        var message = JObject.Parse(ProtocolMessages.State(snapshot, 1));

        Assert.Equal(1, (int?)message["stage"]?["count"]);
        Assert.Equal(exhausted, (bool?)message["stage"]?["exhausted"]);
    }

    [Fact]
    public void State_omits_exhausted_when_the_game_does_not_say()
    {
        var snapshot = new TelemetrySnapshot { Status = VesselStatus.Connected, VesselName = "Kerbal X", StageCount = 3 };

        var message = JObject.Parse(ProtocolMessages.State(snapshot, 1));

        Assert.Equal(3, (int?)message["stage"]?["count"]);
        Assert.Null(message["stage"]?["exhausted"]);
    }

    [Theory]
    [InlineData(null, 5)]
    [InlineData(5, 4)]
    [InlineData(5, null)]
    public void Changing_the_stage_count_counts_as_a_mode_change(int? before, int? after)
    {
        var previous = WithStages(before);
        var current = WithStages(after);

        Assert.True(SnapshotComparer.HasModeChange(previous, current));
        Assert.True(SnapshotComparer.HasChanged(previous, current));
    }

    [Fact]
    public void An_unchanged_stage_count_is_not_a_change()
    {
        Assert.False(SnapshotComparer.HasChanged(WithStages(5), WithStages(5)));
    }

    private static TelemetrySnapshot WithStages(int? count) => new()
    {
        Status = VesselStatus.Connected,
        VesselName = "Kerbal X",
        StageCount = count
    };
}
