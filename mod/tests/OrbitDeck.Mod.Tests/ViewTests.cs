using Newtonsoft.Json.Linq;
using OrbitDeck.Mod.Commands;
using OrbitDeck.Mod.Protocol;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Mod.Tests;

public class ViewTests
{
    [Theory]
    [InlineData(ViewKind.Ship, "ship")]
    [InlineData(ViewKind.Map, "map")]
    public void State_contains_the_current_view(ViewKind view, string expected)
    {
        var snapshot = new TelemetrySnapshot { Status = VesselStatus.Connected, VesselName = "Kerbal X", View = view };

        var message = JObject.Parse(ProtocolMessages.State(snapshot, 1));

        Assert.Equal(expected, (string?)message["view"]);
    }

    [Fact]
    public void State_omits_the_view_when_the_game_does_not_provide_it()
    {
        var message = JObject.Parse(ProtocolMessages.State(Snapshots.InFlight(), 1));

        Assert.Null(message["view"]);
    }

    [Theory]
    [InlineData(null, ViewKind.Ship)]
    [InlineData(ViewKind.Ship, ViewKind.Map)]
    [InlineData(ViewKind.Map, ViewKind.Ship)]
    [InlineData(ViewKind.Map, null)]
    public void Changing_the_view_counts_as_a_mode_change(ViewKind? before, ViewKind? after)
    {
        var previous = WithView(before);
        var current = WithView(after);

        Assert.True(SnapshotComparer.HasModeChange(previous, current));
        Assert.True(SnapshotComparer.HasChanged(previous, current));
    }

    [Fact]
    public void An_unchanged_view_is_not_a_change()
    {
        Assert.False(SnapshotComparer.HasChanged(WithView(ViewKind.Map), WithView(ViewKind.Map)));
    }

    [Theory]
    [InlineData("ship", ViewKind.Ship)]
    [InlineData("map", ViewKind.Map)]
    public void SetView_accepts_a_known_view(string view, ViewKind expected)
    {
        var result = CommandValidator.Validate("setView", JObject.Parse($"{{\"view\":\"{view}\"}}"));

        Assert.True(result.IsValid);
        Assert.Equal(CommandKind.SetView, result.Command.Kind);
        Assert.Equal(expected, result.Command.View);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"view\":\"cockpit\"}")]
    [InlineData("{\"view\":true}")]
    public void SetView_refuses_a_missing_or_unknown_view(string args)
    {
        var result = CommandValidator.Validate("setView", JObject.Parse(args));

        Assert.False(result.IsValid);
        Assert.Equal(ErrorCodes.BadRequest, result.ErrorCode);
    }

    [Fact]
    public void ToggleView_takes_no_arguments()
    {
        var result = CommandValidator.Validate("toggleView", new JObject());

        Assert.True(result.IsValid);
        Assert.Equal(CommandKind.ToggleView, result.Command.Kind);
    }

    [Theory]
    [InlineData(CommandKind.SetView, ViewKind.Map, "view map")]
    [InlineData(CommandKind.SetView, ViewKind.Ship, "view ship")]
    [InlineData(CommandKind.ToggleView, ViewKind.Ship, "view toggle")]
    public void Commands_reach_the_game_controls(CommandKind kind, ViewKind view, string expectedCall)
    {
        var queue = new CommandQueue();
        var controls = new FakeGameControls();

        queue.TryEnqueue(new PendingCommand("a", new GameCommand { Kind = kind, View = view }, _ => { }));
        queue.ProcessPending(controls);

        Assert.Equal(new[] { expectedCall }, controls.Calls);
    }

    private static TelemetrySnapshot WithView(ViewKind? view) => new()
    {
        Status = VesselStatus.Connected,
        VesselName = "Kerbal X",
        View = view
    };
}
