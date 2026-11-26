using Newtonsoft.Json.Linq;
using OrbitDeck.Mod.Commands;
using OrbitDeck.Mod.Protocol;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Mod.Tests;

/// <summary>Groupes d'actions personnalisés (les dix boutons « Actions » de KSP) : commande, bloc d'état, annonce.</summary>
public class ActionGroupsTests
{
    private static ValidationResult Validate(string argsJson) => CommandValidator.Validate("toggleActionGroup", JObject.Parse(argsJson));

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void A_group_from_1_to_10_is_accepted(int group)
    {
        var result = Validate($"{{\"group\":{group}}}");

        Assert.True(result.IsValid);
        Assert.Equal(CommandKind.ToggleActionGroup, result.Command!.Kind);
        Assert.Equal(group, result.Command.Group);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"group\":0}")]
    [InlineData("{\"group\":11}")]
    [InlineData("{\"group\":-1}")]
    [InlineData("{\"group\":1.5}")]
    [InlineData("{\"group\":\"3\"}")]
    [InlineData("{\"group\":null}")]
    [InlineData("{\"group\":true}")]
    public void Anything_else_is_refused_never_corrected(string args)
    {
        var result = Validate(args);

        Assert.False(result.IsValid);
        Assert.Equal(ErrorCodes.BadRequest, result.ErrorCode);
    }

    [Fact]
    public void The_command_reaches_the_game_controls_with_its_group()
    {
        var controls = new FakeGameControls();
        var command = Validate("{\"group\":3}").Command!;

        Assert.True(CommandQueue.Execute(command, controls).Success);

        Assert.Equal(new[] { "actiongroup 3" }, controls.Calls);
    }

    [Fact]
    public void The_new_command_comes_after_all_the_existing_ones()
    {
        // Le filet de compat 0.5 prend les premières valeurs de l'énumération : une commande ajoutée doit être à la fin.
        var values = Enum.GetValues<CommandKind>();

        Assert.Equal(CommandKind.ToggleActionGroup, values[^1]);
        Assert.Equal(CommandKind.QuickLoad, values[^2]);
    }

    private static ActionGroupsInfo Groups(params (int group, ActionGroupState state)[] set)
    {
        var custom = Enumerable.Repeat(ActionGroupState.Off, ActionGroupsInfo.Count).ToArray();
        foreach (var (group, state) in set)
            custom[group - 1] = state;
        return new ActionGroupsInfo { Custom = custom };
    }

    private static JObject State(ActionGroupsInfo? groups) =>
        JObject.Parse(ProtocolMessages.State(new TelemetrySnapshot { Status = VesselStatus.Connected, VesselName = "X", ActionGroups = groups }, 1));

    [Fact]
    public void The_state_carries_the_ten_groups_in_order()
    {
        var message = State(Groups((1, ActionGroupState.On), (3, ActionGroupState.Mixed), (10, ActionGroupState.None)));

        var custom = message["actionGroups"]!["custom"]!.Select(t => (string?)t).ToArray();
        Assert.Equal(new[] { "on", "off", "mixed", "off", "off", "off", "off", "off", "off", "none" }, custom);
    }

    [Fact]
    public void The_block_is_omitted_when_unreadable_and_the_rest_of_the_state_stays()
    {
        var message = State(null);

        Assert.Null(message["actionGroups"]);
        Assert.Equal("state", (string?)message["type"]);
    }

    [Fact]
    public void Comparing_two_snapshots_notices_a_changed_group_only()
    {
        TelemetrySnapshot Snap(ActionGroupsInfo g) => new() { Status = VesselStatus.Connected, VesselName = "X", ActionGroups = g };

        Assert.False(SnapshotComparer.HasChanged(Snap(Groups()), Snap(Groups())));
        Assert.True(SnapshotComparer.HasChanged(Snap(Groups()), Snap(Groups((2, ActionGroupState.On)))));
        Assert.True(SnapshotComparer.HasChanged(Snap(Groups((2, ActionGroupState.On))), Snap(Groups((2, ActionGroupState.Mixed)))));
        Assert.True(SnapshotComparer.HasChanged(new TelemetrySnapshot { Status = VesselStatus.Connected, VesselName = "X" }, Snap(Groups())));
    }

    [Fact]
    public void Hello_announces_the_action_groups()
    {
        var message = JObject.Parse(ProtocolMessages.Hello("0.6.0", "x", Features.Announced));

        Assert.Contains("actionGroups", message["features"]!.Select(t => (string?)t));
    }
}
