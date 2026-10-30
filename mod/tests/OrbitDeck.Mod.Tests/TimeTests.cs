using Newtonsoft.Json.Linq;
using OrbitDeck.Mod.Commands;
using OrbitDeck.Mod.Protocol;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Mod.Tests;

public class TimeTests
{
    private static readonly double[] RailsRates = { 1, 5, 10, 50, 100, 1000, 10000, 100000 };

    private static TimeInfo SampleTime(Func<TimeInfo, TimeInfo>? change = null)
    {
        var time = new TimeInfo { Ut = 1000, Rate = 100, Index = 4, Rates = RailsRates, Mode = WarpMode.Rails, Pausable = true };
        return change?.Invoke(time) ?? time;
    }

    private static TelemetrySnapshot WithTime(TimeInfo? time) => new()
    {
        Status = VesselStatus.Connected,
        VesselName = "Kerbal X",
        Time = time
    };

    // --- Sérialisation -----------------------------------------------------------------------------------------------

    [Fact]
    public void State_omits_time_when_the_game_does_not_provide_it()
    {
        var message = JObject.Parse(ProtocolMessages.State(Snapshots.InFlight(), 1));

        Assert.Null(message["time"]);
    }

    [Fact]
    public void State_omits_time_without_a_vessel()
    {
        var message = JObject.Parse(ProtocolMessages.State(new TelemetrySnapshot { Status = VesselStatus.NoVessel, Time = SampleTime() }, 1));

        Assert.Null(message["time"]);
    }

    [Fact]
    public void State_contains_the_time_with_its_steps()
    {
        var time = JObject.Parse(ProtocolMessages.State(WithTime(SampleTime()), 1))["time"]!;

        Assert.Equal(1000, (double)time["ut"]!);
        Assert.Equal(100, (double)time["rate"]!);
        Assert.Equal(4, (int)time["index"]!);
        Assert.Equal("rails", (string?)time["mode"]);
        Assert.False((bool)time["paused"]!);
        Assert.Equal(RailsRates, time["rates"]!.Select(rate => (double)rate).ToArray());
    }

    [Fact]
    public void Optional_time_fields_are_absent_when_unknown()
    {
        var time = (JObject)JObject.Parse(ProtocolMessages.State(WithTime(new TimeInfo { Rates = RailsRates }), 1))["time"]!;

        Assert.Null(time["maxIndex"]);
        Assert.Null(time["limit"]);
        Assert.Null(time["day"]);
        Assert.Null(time["auto"]);
        Assert.Null(time["pausable"]);
    }

    [Fact]
    public void Optional_time_fields_are_written_when_known()
    {
        var info = SampleTime(t => new TimeInfo
        {
            Ut = t.Ut, Rate = t.Rate, Index = t.Index, Rates = t.Rates, Mode = WarpMode.Physics, Paused = true,
            Pausable = true, MaxIndex = 3, Limit = WarpLimit.Atmosphere, Auto = true, DayLength = 21600
        });

        var time = JObject.Parse(ProtocolMessages.State(WithTime(info), 1))["time"]!;

        Assert.Equal("physics", (string?)time["mode"]);
        Assert.True((bool)time["paused"]!);
        Assert.True((bool)time["pausable"]!);
        Assert.Equal(3, (int)time["maxIndex"]!);
        Assert.Equal("atmosphere", (string?)time["limit"]);
        Assert.True((bool)time["auto"]!);
        Assert.Equal(21600, (double)time["day"]!);
    }

    [Theory]
    [InlineData(WarpLimit.Atmosphere, "atmosphere")]
    [InlineData(WarpLimit.Altitude, "altitude")]
    [InlineData(WarpLimit.Thrust, "thrust")]
    [InlineData(WarpLimit.Ground, "ground")]
    [InlineData(WarpLimit.Proximity, "proximity")]
    [InlineData(WarpLimit.Other, "other")]
    public void Limits_have_stable_names(WarpLimit limit, string expected)
    {
        Assert.Equal(expected, ProtocolMessages.WarpLimitName(limit));
    }

    [Fact]
    public void Time_stays_far_below_the_message_size_limit()
    {
        var many = Enumerable.Range(1, 40).Select(i => i * 123456.789).ToArray();
        var message = ProtocolMessages.State(WithTime(SampleTime(t => new TimeInfo { Ut = 1e9, Rate = 100000, Index = 39, Rates = many, MaxIndex = 39, Limit = WarpLimit.Other, DayLength = 21600 })), 99999);

        Assert.True(System.Text.Encoding.UTF8.GetByteCount(message) < 2048);
    }

    // --- Republication -----------------------------------------------------------------------------------------------

    [Fact]
    public void The_advancing_game_time_alone_is_not_a_change()
    {
        var previous = WithTime(SampleTime());
        var current = WithTime(SampleTime(t => new TimeInfo { Ut = t.Ut + 50, Rate = t.Rate, Index = t.Index, Rates = t.Rates, Mode = t.Mode, Pausable = t.Pausable }));

        Assert.False(SnapshotComparer.HasChanged(previous, current));
    }

    [Theory]
    [InlineData("index")]
    [InlineData("rate")]
    [InlineData("paused")]
    [InlineData("maxIndex")]
    [InlineData("limit")]
    [InlineData("auto")]
    [InlineData("mode")]
    public void A_change_of_step_pause_or_limit_is_a_mode_change(string what)
    {
        var baseTime = SampleTime();
        var changed = what switch
        {
            "index" => new TimeInfo { Ut = 1000, Rate = 100, Index = 5, Rates = RailsRates, Pausable = true },
            "rate" => new TimeInfo { Ut = 1000, Rate = 50, Index = 4, Rates = RailsRates, Pausable = true },
            "paused" => new TimeInfo { Ut = 1000, Rate = 100, Index = 4, Rates = RailsRates, Pausable = true, Paused = true },
            "maxIndex" => new TimeInfo { Ut = 1000, Rate = 100, Index = 4, Rates = RailsRates, Pausable = true, MaxIndex = 4 },
            "limit" => new TimeInfo { Ut = 1000, Rate = 100, Index = 4, Rates = RailsRates, Pausable = true, Limit = WarpLimit.Thrust },
            "auto" => new TimeInfo { Ut = 1000, Rate = 100, Index = 4, Rates = RailsRates, Pausable = true, Auto = true },
            _ => new TimeInfo { Ut = 1000, Rate = 100, Index = 4, Rates = RailsRates, Pausable = true, Mode = WarpMode.Physics }
        };

        Assert.True(SnapshotComparer.HasModeChange(WithTime(baseTime), WithTime(changed)));
    }

    [Fact]
    public void Time_appearing_or_disappearing_is_a_mode_change()
    {
        Assert.True(SnapshotComparer.HasModeChange(WithTime(null), WithTime(SampleTime())));
        Assert.True(SnapshotComparer.HasModeChange(WithTime(SampleTime()), WithTime(null)));
        Assert.False(SnapshotComparer.HasModeChange(WithTime(null), WithTime(null)));
    }

    // --- Paliers -----------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1, 0)]
    [InlineData(100, 4)]
    [InlineData(100000, 7)]
    [InlineData(7, -1)]
    public void A_rate_is_found_in_the_steps(double rate, int expected)
    {
        Assert.Equal(expected, TimeInfo.IndexOfRate(RailsRates, rate));
    }

    // --- Commandes ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("increaseWarp", CommandKind.IncreaseWarp)]
    [InlineData("decreaseWarp", CommandKind.DecreaseWarp)]
    [InlineData("stopWarp", CommandKind.StopWarp)]
    [InlineData("togglePause", CommandKind.TogglePause)]
    public void Simple_time_commands_take_no_arguments(string name, CommandKind kind)
    {
        var result = CommandValidator.Validate(name, new JObject());

        Assert.True(result.IsValid);
        Assert.Equal(kind, result.Command.Kind);
    }

    [Theory]
    [InlineData("{\"rate\":100}", 100)]
    [InlineData("{\"rate\":0.5}", 0.5)]
    [InlineData("{\"rate\":1}", 1)]
    public void SetWarpRate_accepts_a_positive_factor(string args, double expected)
    {
        var result = CommandValidator.Validate("setWarpRate", JObject.Parse(args));

        Assert.True(result.IsValid);
        Assert.Equal(CommandKind.SetWarpRate, result.Command.Kind);
        Assert.Equal(expected, result.Command.Rate);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"rate\":0}")]
    [InlineData("{\"rate\":-5}")]
    [InlineData("{\"rate\":\"fast\"}")]
    [InlineData("{\"rate\":null}")]
    public void SetWarpRate_refuses_a_missing_or_invalid_factor(string args)
    {
        var result = CommandValidator.Validate("setWarpRate", JObject.Parse(args));

        Assert.False(result.IsValid);
        Assert.Equal(ErrorCodes.BadRequest, result.ErrorCode);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void SetPause_accepts_a_boolean(string value, bool expected)
    {
        var result = CommandValidator.Validate("setPause", JObject.Parse($"{{\"paused\":{value}}}"));

        Assert.True(result.IsValid);
        Assert.Equal(CommandKind.SetPause, result.Command.Kind);
        Assert.Equal(expected, result.Command.Enabled);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"paused\":1}")]
    [InlineData("{\"paused\":\"yes\"}")]
    public void SetPause_refuses_a_missing_or_invalid_value(string args)
    {
        var result = CommandValidator.Validate("setPause", JObject.Parse(args));

        Assert.False(result.IsValid);
        Assert.Equal(ErrorCodes.BadRequest, result.ErrorCode);
    }

    [Theory]
    [InlineData(CommandKind.IncreaseWarp, 0, false, "warp up")]
    [InlineData(CommandKind.DecreaseWarp, 0, false, "warp down")]
    [InlineData(CommandKind.StopWarp, 0, false, "warp stop")]
    [InlineData(CommandKind.SetWarpRate, 100, false, "warp rate 100")]
    [InlineData(CommandKind.SetPause, 0, true, "pause true")]
    [InlineData(CommandKind.SetPause, 0, false, "pause false")]
    [InlineData(CommandKind.TogglePause, 0, false, "pause toggle")]
    public void Commands_reach_the_game_controls(CommandKind kind, double rate, bool enabled, string expectedCall)
    {
        var queue = new CommandQueue();
        var controls = new FakeGameControls();

        queue.TryEnqueue(new PendingCommand("a", new GameCommand { Kind = kind, Rate = rate, Enabled = enabled }, _ => { }));
        queue.ProcessPending(controls);

        Assert.Equal(new[] { expectedCall }, controls.Calls);
    }

    [Fact]
    public void Time_commands_are_not_idempotent_only_when_they_move_a_step()
    {
        // increaseWarp / decreaseWarp avancent d'un palier à chaque envoi : deux commandes = deux appels au jeu.
        var queue = new CommandQueue();
        var controls = new FakeGameControls();

        queue.TryEnqueue(new PendingCommand("a", new GameCommand { Kind = CommandKind.IncreaseWarp }, _ => { }));
        queue.TryEnqueue(new PendingCommand("b", new GameCommand { Kind = CommandKind.IncreaseWarp }, _ => { }));
        queue.ProcessPending(controls);

        Assert.Equal(new[] { "warp up", "warp up" }, controls.Calls);
    }
}
