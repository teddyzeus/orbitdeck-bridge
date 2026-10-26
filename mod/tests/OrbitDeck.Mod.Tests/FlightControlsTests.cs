using Newtonsoft.Json.Linq;
using OrbitDeck.Mod.Commands;
using OrbitDeck.Mod.Protocol;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Mod.Tests;

/// <summary>Modes du SAS, phares, freins et train d'atterrissage (ajoutés après la première version du protocole).</summary>
public class FlightControlsValidatorTests
{
    private static ValidationResult Validate(string name, string argsJson = "{}") =>
        CommandValidator.Validate(name, JObject.Parse(argsJson));

    [Theory]
    [InlineData("stabilityAssist", SasModeKind.StabilityAssist)]
    [InlineData("prograde", SasModeKind.Prograde)]
    [InlineData("retrograde", SasModeKind.Retrograde)]
    [InlineData("normal", SasModeKind.Normal)]
    [InlineData("antinormal", SasModeKind.Antinormal)]
    [InlineData("radialIn", SasModeKind.RadialIn)]
    [InlineData("radialOut", SasModeKind.RadialOut)]
    [InlineData("target", SasModeKind.Target)]
    [InlineData("antiTarget", SasModeKind.AntiTarget)]
    [InlineData("maneuver", SasModeKind.Maneuver)]
    public void SetSasMode_accepts_every_mode(string name, SasModeKind expected)
    {
        var result = Validate("setSasMode", $"{{\"mode\":\"{name}\"}}");

        Assert.True(result.IsValid);
        Assert.Equal(CommandKind.SetSasMode, result.Command.Kind);
        Assert.Equal(expected, result.Command.SasMode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"mode\":\"Prograde\"}")]
    [InlineData("{\"mode\":\"autre\"}")]
    [InlineData("{\"mode\":3}")]
    [InlineData("{\"mode\":null}")]
    public void SetSasMode_refuses_unknown_or_malformed_modes(string args)
    {
        var result = Validate("setSasMode", args);

        Assert.False(result.IsValid);
        Assert.Equal(ErrorCodes.BadRequest, result.ErrorCode);
    }

    [Theory]
    [InlineData("setLights", CommandKind.SetLights)]
    [InlineData("setBrakes", CommandKind.SetBrakes)]
    [InlineData("setGear", CommandKind.SetGear)]
    public void Set_commands_require_a_boolean(string name, CommandKind kind)
    {
        var accepted = Validate(name, "{\"enabled\":true}");
        Assert.True(accepted.IsValid);
        Assert.Equal(kind, accepted.Command.Kind);
        Assert.True(accepted.Command.Enabled);

        Assert.Equal(ErrorCodes.BadRequest, Validate(name, "{}").ErrorCode);
        Assert.Equal(ErrorCodes.BadRequest, Validate(name, "{\"enabled\":\"oui\"}").ErrorCode);
        Assert.Equal(ErrorCodes.BadRequest, Validate(name, "{\"enabled\":1}").ErrorCode);
    }

    [Theory]
    [InlineData("toggleLights", CommandKind.ToggleLights)]
    [InlineData("toggleBrakes", CommandKind.ToggleBrakes)]
    [InlineData("toggleGear", CommandKind.ToggleGear)]
    public void Toggle_commands_take_no_arguments(string name, CommandKind kind)
    {
        var result = Validate(name);

        Assert.True(result.IsValid);
        Assert.Equal(kind, result.Command.Kind);
    }
}

public class FlightControlsQueueTests
{
    [Theory]
    [InlineData(CommandKind.SetLights, true, "lights true")]
    [InlineData(CommandKind.ToggleLights, false, "lights toggle")]
    [InlineData(CommandKind.SetBrakes, false, "brakes false")]
    [InlineData(CommandKind.ToggleBrakes, false, "brakes toggle")]
    [InlineData(CommandKind.SetGear, true, "gear true")]
    [InlineData(CommandKind.ToggleGear, false, "gear toggle")]
    public void Each_new_command_kind_calls_the_matching_control(CommandKind kind, bool enabled, string expected)
    {
        var queue = new CommandQueue();
        var controls = new FakeGameControls();
        var pending = new PendingCommand("c", new GameCommand { Kind = kind, Enabled = enabled }, _ => { });

        queue.TryEnqueue(pending);
        queue.ProcessPending(controls);

        Assert.Equal(new[] { expected }, controls.Calls);
    }

    [Fact]
    public void Sas_mode_command_calls_set_sas_mode()
    {
        var queue = new CommandQueue();
        var controls = new FakeGameControls();
        var pending = new PendingCommand("c", new GameCommand { Kind = CommandKind.SetSasMode, SasMode = SasModeKind.Retrograde }, _ => { });

        queue.TryEnqueue(pending);
        queue.ProcessPending(controls);

        Assert.Equal(new[] { "sasmode Retrograde" }, controls.Calls);
    }
}

public class FlightControlsStateTests
{
    [Fact]
    public void State_contains_lights_brakes_and_gear_when_the_game_provides_them()
    {
        var snapshot = new TelemetrySnapshot
        {
            Status = VesselStatus.Connected,
            VesselName = "Kerbal X",
            LightsEnabled = true,
            BrakesEnabled = false,
            GearEnabled = true
        };

        var message = JObject.Parse(ProtocolMessages.State(snapshot, 1));

        Assert.True((bool?)message["lights"]?["enabled"]);
        Assert.False((bool?)message["brakes"]?["enabled"]);
        Assert.True((bool?)message["gear"]?["enabled"]);
    }

    [Fact]
    public void State_omits_the_fields_the_game_does_not_provide()
    {
        var message = JObject.Parse(ProtocolMessages.State(Snapshots.InFlight(), 1));

        Assert.Null(message["lights"]);
        Assert.Null(message["brakes"]);
        Assert.Null(message["gear"]);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Changing_lights_brakes_or_gear_counts_as_a_mode_change(bool lights, bool brakes, bool gear)
    {
        var before = Snapshots.InFlight();
        var after = new TelemetrySnapshot
        {
            Status = before.Status,
            GameStateName = before.GameStateName,
            VesselName = before.VesselName,
            SpeedMode = before.SpeedMode,
            SasEnabled = before.SasEnabled,
            SasMode = before.SasMode,
            RcsEnabled = before.RcsEnabled,
            LightsEnabled = lights ? true : (bool?)null,
            BrakesEnabled = brakes ? true : (bool?)null,
            GearEnabled = gear ? true : (bool?)null
        };

        Assert.True(SnapshotComparer.HasModeChange(before, after));
    }
}

public class StageCommandTests
{
    [Fact]
    public void ActivateStage_takes_no_arguments()
    {
        var result = CommandValidator.Validate("activateStage", new JObject());

        Assert.True(result.IsValid);
        Assert.Equal(CommandKind.ActivateStage, result.Command.Kind);
    }

    [Fact]
    public void ActivateStage_calls_the_game_once_per_command()
    {
        var queue = new CommandQueue();
        var controls = new FakeGameControls();

        queue.TryEnqueue(new PendingCommand("a", new GameCommand { Kind = CommandKind.ActivateStage }, _ => { }));
        queue.TryEnqueue(new PendingCommand("b", new GameCommand { Kind = CommandKind.ActivateStage }, _ => { }));
        queue.ProcessPending(controls);

        Assert.Equal(new[] { "stage", "stage" }, controls.Calls);
    }
}
