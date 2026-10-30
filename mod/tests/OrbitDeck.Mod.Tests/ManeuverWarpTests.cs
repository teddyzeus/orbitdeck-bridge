using Newtonsoft.Json.Linq;
using OrbitDeck.Mod.Commands;
using OrbitDeck.Mod.Protocol;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Mod.Tests;

internal sealed class FakeWarpDriver : IWarpDriver
{
    public List<string> Calls { get; } = new();

    public bool Accept { get; set; } = true;

    public bool WarpTo(double ut, double? maxRate)
    {
        Calls.Add($"warpTo {ut} {maxRate}");
        return Accept;
    }

    public void StopWarp() => Calls.Add("stop");
}

public class ManeuverWarpTrackerTests
{
    // Nœud à 10000 s, allumage à 9976 s, marge 30 s → arrêt visé à 9946 s.
    private const double NodeUt = 10_000;
    private const double Ignition = 9_976;
    private const double Margin = 30;
    private const double Stop = 9_946;

    private double _clock;
    private readonly FakeWarpDriver _driver = new();
    private readonly ManeuverWarpTracker _tracker;

    public ManeuverWarpTrackerTests()
    {
        _tracker = new ManeuverWarpTracker(() => _clock);
    }

    private static ManeuverNodeInfo Node(double ut = NodeUt, double? ignition = Ignition) => new() { Ut = ut, Dv = 100, Burn = 48, Ignition = ignition };

    private static TelemetrySnapshot Snapshot(
        double ut = 5_000,
        bool auto = false,
        string vessel = "Kerbal X",
        VesselStatus status = VesselStatus.Connected,
        ManeuverNodeInfo[]? nodes = null,
        bool withPlan = true,
        bool paused = false) => new()
    {
        Status = status,
        VesselName = vessel,
        Time = new TimeInfo { Ut = ut, Auto = auto, Paused = paused, Rates = new double[] { 1, 5, 10 } },
        Maneuver = withPlan ? new ManeuverInfo { Count = (nodes ?? new[] { Node() }).Length, Nodes = nodes ?? new[] { Node() } } : null
    };

    private CommandResult Start(TelemetrySnapshot? snapshot = null, double? maxRate = null) =>
        _tracker.Start(snapshot ?? Snapshot(), NodeUt, Margin, maxRate, _driver);

    // --- Démarrage ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Starts_the_game_warp_at_the_ignition_minus_the_margin()
    {
        var result = Start(maxRate: 1000);

        Assert.True(result.Success);
        Assert.Equal(new[] { $"warpTo {Stop} 1000" }, _driver.Calls);
        Assert.Equal(ManeuverWarpState.Running, _tracker.State);
        Assert.Equal(Stop, _tracker.Info!.Stop);
    }

    [Fact]
    public void Without_a_burn_duration_it_stops_a_minute_earlier()
    {
        var snapshot = Snapshot(nodes: new[] { Node(ignition: null) });

        Assert.True(Start(snapshot).Success);
        Assert.Equal(NodeUt - Margin - 60, _tracker.Info!.Stop);
    }

    [Fact]
    public void A_node_is_found_within_one_second()
    {
        var snapshot = Snapshot(nodes: new[] { Node(ut: NodeUt + 0.8) });

        Assert.True(Start(snapshot).Success);
    }

    [Fact]
    public void An_unknown_node_is_refused()
    {
        var result = Start(Snapshot(nodes: new[] { Node(ut: NodeUt + 50) }));

        Assert.Equal(ErrorCodes.RejectedByGame, result.ErrorCode);
        Assert.Empty(_driver.Calls);
        Assert.Equal(ManeuverWarpState.Idle, _tracker.State);
    }

    [Fact]
    public void A_stop_instant_already_passed_is_refused_and_reported_as_late()
    {
        var result = Start(Snapshot(ut: 9_950));

        Assert.Equal(ErrorCodes.RejectedByGame, result.ErrorCode);
        Assert.Empty(_driver.Calls);
        Assert.Equal(ManeuverWarpState.Late, _tracker.State);
        Assert.Null(_tracker.Info!.Stop);
    }

    [Fact]
    public void A_stop_instant_less_than_a_second_away_is_late()
    {
        Assert.False(Start(Snapshot(ut: Stop - 0.5)).Success);
        Assert.Equal(ManeuverWarpState.Late, _tracker.State);
    }

    [Theory]
    [InlineData(VesselStatus.NoVessel, ErrorCodes.NoVessel)]
    [InlineData(VesselStatus.NotControllable, ErrorCodes.NotControllable)]
    public void Refuses_without_a_controllable_vessel(VesselStatus status, string code)
    {
        var result = Start(Snapshot(status: status));

        Assert.Equal(code, result.ErrorCode);
        Assert.Empty(_driver.Calls);
    }

    [Fact]
    public void Refuses_when_the_game_gives_no_plan_or_no_time()
    {
        Assert.Equal(ErrorCodes.RejectedByGame, Start(Snapshot(withPlan: false)).ErrorCode);
        Assert.Equal(ErrorCodes.RejectedByGame, Start(new TelemetrySnapshot { Status = VesselStatus.Connected }).ErrorCode);
    }

    [Fact]
    public void Refuses_when_the_game_refuses_the_warp()
    {
        _driver.Accept = false;

        Assert.Equal(ErrorCodes.RejectedByGame, Start().ErrorCode);
        Assert.Equal(ManeuverWarpState.Idle, _tracker.State);
    }

    // --- Surveillance ------------------------------------------------------------------------------------------------

    [Fact]
    public void Stays_running_while_the_game_warps()
    {
        Start();
        _clock = 10;
        _tracker.Update(Snapshot(ut: 7_000, auto: true), _driver);

        Assert.Equal(ManeuverWarpState.Running, _tracker.State);
        Assert.Single(_driver.Calls);
    }

    [Fact]
    public void Does_not_conclude_before_the_game_has_engaged_its_warp()
    {
        Start();
        _clock = 0.5;
        _tracker.Update(Snapshot(ut: 5_001, auto: false), _driver);

        Assert.Equal(ManeuverWarpState.Running, _tracker.State);
    }

    [Fact]
    public void Arrives_when_the_game_stops_on_the_target()
    {
        Start();
        _clock = 30;
        _tracker.Update(Snapshot(ut: Stop - 0.3, auto: false), _driver);

        Assert.Equal(ManeuverWarpState.Arrived, _tracker.State);
        Assert.Equal(Stop, _tracker.Info!.Stop);
        Assert.DoesNotContain("stop", _driver.Calls);
    }

    [Fact]
    public void The_player_stopping_the_warp_early_is_a_manual_interruption()
    {
        Start();
        _tracker.NoteStopRequested();
        _clock = 30;
        _tracker.Update(Snapshot(ut: 7_000, auto: false), _driver);

        Assert.Equal(ManeuverWarpState.Interrupted, _tracker.State);
        Assert.Equal(ManeuverWarpReason.Manual, _tracker.Info!.Reason);
        Assert.DoesNotContain("stop", _driver.Calls);
    }

    [Fact]
    public void A_removed_node_interrupts_and_stops_the_warp()
    {
        Start();
        _clock = 5;
        _tracker.Update(Snapshot(ut: 7_000, auto: true, nodes: Array.Empty<ManeuverNodeInfo>()), _driver);

        Assert.Equal(ManeuverWarpReason.NodeGone, _tracker.Info!.Reason);
        Assert.Equal("stop", _driver.Calls.Last());
    }

    [Fact]
    public void A_missing_plan_counts_as_a_removed_node()
    {
        Start();
        _clock = 5;
        _tracker.Update(Snapshot(ut: 7_000, auto: true, withPlan: false), _driver);

        Assert.Equal(ManeuverWarpReason.NodeGone, _tracker.Info!.Reason);
    }

    [Fact]
    public void A_changed_vessel_interrupts_and_stops_the_warp()
    {
        Start();
        _clock = 5;
        _tracker.Update(Snapshot(ut: 7_000, auto: true, vessel: "Autre"), _driver);

        Assert.Equal(ManeuverWarpReason.Vessel, _tracker.Info!.Reason);
        Assert.Equal("stop", _driver.Calls.Last());
    }

    [Fact]
    public void A_vessel_that_is_no_longer_controllable_interrupts_and_stops_the_warp()
    {
        Start();
        _clock = 5;
        _tracker.Update(Snapshot(ut: 7_000, auto: true, status: VesselStatus.NotControllable), _driver);

        Assert.Equal(ManeuverWarpReason.Control, _tracker.Info!.Reason);
        Assert.Equal("stop", _driver.Calls.Last());
    }

    [Fact]
    public void Leaving_the_flight_scene_resets_silently()
    {
        Start();
        _clock = 5;
        _tracker.Update(TelemetrySnapshot.NoVessel, _driver);

        Assert.Equal(ManeuverWarpState.Idle, _tracker.State);
        Assert.DoesNotContain("stop", _driver.Calls);
    }

    [Fact]
    public void A_longer_burn_moves_the_stop_and_relaunches_the_warp()
    {
        Start();
        _clock = 5;
        var longer = Snapshot(ut: 7_000, auto: true, nodes: new[] { Node(ignition: Ignition - 20) });
        _tracker.Update(longer, _driver);
        _clock = 6.5; // l'instant d'arrêt est resté le même plus d'une seconde
        _tracker.Update(longer, _driver);

        Assert.Equal(ManeuverWarpState.Running, _tracker.State);
        Assert.Equal(Stop - 20, _tracker.Info!.Stop);
        Assert.Equal($"warpTo {Stop - 20} ", _driver.Calls.Last());
    }

    [Fact]
    public void A_burn_change_that_puts_the_stop_in_the_past_ends_late()
    {
        Start();
        _clock = 5;
        _tracker.Update(Snapshot(ut: 9_925, auto: true), _driver);

        Assert.Equal(ManeuverWarpState.Running, _tracker.State);
        var shifted = Snapshot(ut: 9_925, auto: true, nodes: new[] { Node(ignition: 9_950) });
        _tracker.Update(shifted, _driver);
        _clock = 7;
        _tracker.Update(shifted, _driver);
        Assert.Equal(ManeuverWarpState.Late, _tracker.State);
        Assert.Equal("stop", _driver.Calls.Last());
    }

    [Fact]
    public void An_ignition_estimate_that_flickers_from_one_frame_to_the_next_relaunches_nothing()
    {
        Start();
        for (var i = 0; i < 40; i++)
        {
            _clock += 0.016;
            var ignition = i % 2 == 0 ? NodeUt : Ignition; // l'estimation saute à chaque image
            _tracker.Update(Snapshot(ut: 7_000, auto: true, nodes: new[] { Node(ignition: ignition) }), _driver);
        }

        Assert.Single(_driver.Calls);
        Assert.Equal(ManeuverWarpState.Running, _tracker.State);
    }

    [Fact]
    public void Small_jitter_of_the_ignition_does_not_relaunch_the_warp()
    {
        Start();
        _clock = 5;
        _tracker.Update(Snapshot(ut: 7_000, auto: true, nodes: new[] { Node(ignition: Ignition + 0.3) }), _driver);

        Assert.Single(_driver.Calls);
    }

    // --- Fin et publication ------------------------------------------------------------------------------------------

    [Fact]
    public void A_final_state_is_cleared_after_five_seconds()
    {
        Start();
        _clock = 30;
        _tracker.Update(Snapshot(ut: Stop, auto: false), _driver);
        Assert.Equal(ManeuverWarpState.Arrived, _tracker.State);

        _clock = 34.9;
        _tracker.Update(Snapshot(ut: Stop), _driver);
        Assert.Equal(ManeuverWarpState.Arrived, _tracker.State);

        _clock = 35.1;
        _tracker.Update(Snapshot(ut: Stop), _driver);
        Assert.Equal(ManeuverWarpState.Idle, _tracker.State);
        Assert.Null(_tracker.Info);
        Assert.False(_tracker.Active);
    }

    [Fact]
    public void A_new_request_replaces_a_final_state()
    {
        Start(Snapshot(ut: 9_950));
        Assert.Equal(ManeuverWarpState.Late, _tracker.State);

        Assert.True(Start().Success);
        Assert.Equal(ManeuverWarpState.Running, _tracker.State);
    }

    [Fact]
    public void Decorate_adds_the_operation_to_the_plan_without_touching_the_original()
    {
        Start();
        var original = Snapshot();
        var decorated = _tracker.Decorate(original);

        Assert.Null(original.Maneuver!.Warp);
        Assert.Equal(ManeuverWarpState.Running, decorated.Maneuver!.Warp!.State);
        Assert.Equal(original.VesselName, decorated.VesselName);
        Assert.Same(original.Time, decorated.Time);
    }

    [Fact]
    public void Decorate_leaves_the_snapshot_alone_when_idle_or_without_plan()
    {
        var snapshot = Snapshot();
        Assert.Same(snapshot, _tracker.Decorate(snapshot));

        Start();
        var noPlan = Snapshot(withPlan: false);
        Assert.Same(noPlan, _tracker.Decorate(noPlan));
    }
}

public class WarpToManeuverProtocolTests
{
    private static ValidationResult Validate(string argsJson) => CommandValidator.Validate("warpToManeuver", JObject.Parse(argsJson));

    [Fact]
    public void A_valid_command_carries_its_arguments()
    {
        var result = Validate("{\"ut\": 1250000.5, \"margin\": 30, \"maxRate\": 1000}");

        Assert.True(result.IsValid);
        Assert.Equal(CommandKind.WarpToManeuver, result.Command!.Kind);
        Assert.Equal(1250000.5, result.Command.NodeUt);
        Assert.Equal(30, result.Command.Margin);
        Assert.Equal(1000, result.Command.MaxRate);
    }

    [Fact]
    public void The_ceiling_is_optional_and_null_means_none()
    {
        Assert.Null(Validate("{\"ut\": 5, \"margin\": 0}").Command!.MaxRate);
        Assert.Null(Validate("{\"ut\": 5, \"margin\": 0, \"maxRate\": null}").Command!.MaxRate);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"margin\": 30}")]
    [InlineData("{\"ut\": 5}")]
    [InlineData("{\"ut\": \"5\", \"margin\": 30}")]
    [InlineData("{\"ut\": 5, \"margin\": -1}")]
    [InlineData("{\"ut\": 5, \"margin\": 3601}")]
    [InlineData("{\"ut\": 5, \"margin\": 30, \"maxRate\": 0}")]
    [InlineData("{\"ut\": 5, \"margin\": 30, \"maxRate\": \"x\"}")]
    public void Invalid_arguments_are_refused_never_corrected(string argsJson)
    {
        var result = Validate(argsJson);

        Assert.False(result.IsValid);
        Assert.Equal(ErrorCodes.BadRequest, result.ErrorCode);
    }

    [Fact]
    public void The_command_reaches_the_game_controls()
    {
        var controls = new FakeGameControls();
        var queue = new CommandQueue();
        var pending = new PendingCommand("1", Validate("{\"ut\": 5, \"margin\": 30}").Command!, _ => { });
        queue.TryEnqueue(pending);

        queue.ProcessPending(controls);

        Assert.Equal(new[] { "warpToManeuver 5 30 " }, controls.Calls);
    }

    private static JObject Warp(ManeuverWarpInfo? warp)
    {
        var plan = new ManeuverInfo { Count = 1, Nodes = new[] { new ManeuverNodeInfo { Ut = 1, Dv = 1 } } };
        var snapshot = new TelemetrySnapshot { Status = VesselStatus.Connected, VesselName = "K", Maneuver = warp == null ? plan : plan.WithWarp(warp) };
        return JObject.Parse(ProtocolMessages.State(snapshot, 1));
    }

    [Fact]
    public void The_warp_object_is_absent_at_rest()
    {
        Assert.Null(Warp(null)["maneuver"]!["warp"]);
    }

    [Fact]
    public void A_running_warp_is_published_with_its_stop()
    {
        var warp = Warp(new ManeuverWarpInfo { State = ManeuverWarpState.Running, Stop = 1249945.94 })["maneuver"]!["warp"]!;

        Assert.Equal("running", (string?)warp["state"]);
        Assert.Equal(1249945.9, (double)warp["stop"]!);
        Assert.Null(warp["reason"]);
    }

    [Theory]
    [InlineData(ManeuverWarpReason.Manual, "manual")]
    [InlineData(ManeuverWarpReason.NodeGone, "nodeGone")]
    [InlineData(ManeuverWarpReason.Vessel, "vessel")]
    [InlineData(ManeuverWarpReason.Control, "control")]
    public void An_interruption_carries_its_reason(ManeuverWarpReason reason, string name)
    {
        var warp = Warp(new ManeuverWarpInfo { State = ManeuverWarpState.Interrupted, Reason = reason })["maneuver"]!["warp"]!;

        Assert.Equal("interrupted", (string?)warp["state"]);
        Assert.Equal(name, (string?)warp["reason"]);
    }

    [Theory]
    [InlineData(ManeuverWarpState.Arrived, "arrived")]
    [InlineData(ManeuverWarpState.Late, "late")]
    public void Final_states_are_named(ManeuverWarpState state, string name)
    {
        Assert.Equal(name, (string?)Warp(new ManeuverWarpInfo { State = state })["maneuver"]!["warp"]!["state"]);
    }

    [Fact]
    public void A_change_of_the_operation_republishes_immediately()
    {
        var plan = new ManeuverInfo { Count = 1, Nodes = new[] { new ManeuverNodeInfo { Ut = 1, Dv = 1 } } };
        TelemetrySnapshot With(ManeuverInfo maneuver) => new() { Status = VesselStatus.Connected, VesselName = "K", Maneuver = maneuver };
        var running = plan.WithWarp(new ManeuverWarpInfo { State = ManeuverWarpState.Running, Stop = 100 });

        Assert.True(SnapshotComparer.HasModeChange(With(plan), With(running)));
        Assert.True(SnapshotComparer.HasModeChange(With(running), With(plan.WithWarp(new ManeuverWarpInfo { State = ManeuverWarpState.Arrived, Stop = 100 }))));
        Assert.True(SnapshotComparer.HasModeChange(With(running), With(plan.WithWarp(new ManeuverWarpInfo { State = ManeuverWarpState.Running, Stop = 120 }))));
        Assert.False(SnapshotComparer.HasModeChange(With(running), With(plan.WithWarp(new ManeuverWarpInfo { State = ManeuverWarpState.Running, Stop = 100.01 }))));
    }
}

public class ManeuverWarpStagesTests
{
    private const double NodeUt = 10_000;
    private const double Stop = 9_946;

    private double _clock;
    private readonly FakeWarpDriver _driver = new();
    private readonly ManeuverWarpTracker _tracker;

    public ManeuverWarpStagesTests()
    {
        _tracker = new ManeuverWarpTracker(() => _clock);
    }

    private static TelemetrySnapshot Snapshot(double ut, bool auto, double rate = 1) => new()
    {
        Status = VesselStatus.Connected,
        VesselName = "Kerbal X",
        Time = new TimeInfo { Ut = ut, Auto = auto, Rate = rate, Rates = new double[] { 1, 5, 10 } },
        Maneuver = new ManeuverInfo { Count = 1, Nodes = new[] { new ManeuverNodeInfo { Ut = NodeUt, Dv = 100, Burn = 48, Ignition = 9_976 } } }
    };

    private void Begin()
    {
        Assert.True(_tracker.Start(Snapshot(5_000, false), NodeUt, 30, null, _driver).Success);
    }

    private void Tick(double seconds, double ut, bool auto, double rate = 1)
    {
        _clock += seconds;
        _tracker.Update(Snapshot(ut, auto, rate), _driver);
    }

    [Fact]
    public void When_the_game_stops_a_little_before_the_target_the_next_stage_is_launched()
    {
        Begin();
        Tick(5, 7_000, true, 1000);
        Tick(3, Stop - 480, false); // le jeu s'est arrêté à ~0,5 s réelle du dernier palier

        Assert.Equal(ManeuverWarpState.Running, _tracker.State);
        Assert.Equal(2, _driver.Calls.Count);
        Assert.StartsWith("warpTo", _driver.Calls.Last());
    }

    [Fact]
    public void Stages_chain_down_to_the_arrival()
    {
        Begin();
        Tick(5, 7_000, true, 1000);
        Tick(3, Stop - 480, false);
        Tick(2, Stop - 400, true, 50);
        Tick(3, Stop - 20, false);
        Tick(2, Stop - 10, true, 5);
        Tick(3, Stop - 2, false);

        Assert.Equal(ManeuverWarpState.Arrived, _tracker.State);
        Assert.Equal(3, _driver.Calls.Count);
        Assert.DoesNotContain("stop", _driver.Calls);
    }

    [Fact]
    public void A_requested_stop_is_a_manual_interruption_and_nothing_is_relaunched()
    {
        Begin();
        Tick(5, 7_000, true, 100);
        _tracker.NoteStopRequested();
        Tick(3, 7_800, false);

        Assert.Equal(ManeuverWarpReason.Manual, _tracker.Info!.Reason);
        Assert.Single(_driver.Calls);
    }

    [Fact]
    public void A_stage_ending_far_from_the_target_without_a_stop_request_is_relaunched()
    {
        Begin();
        Tick(5, 7_000, true, 1000);
        Tick(1.6, 7_900, false); // le jeu a coupé son warp (limite, fin d'étape) à 2000 s de l'arrêt

        Assert.Equal(ManeuverWarpState.Running, _tracker.State);
        Assert.Equal(2, _driver.Calls.Count);
    }

    [Fact]
    public void A_pause_waits_without_relaunching()
    {
        Begin();
        Tick(5, 7_000, true, 1000);
        _clock += 3;
        var paused = Snapshot(7_900, false);
        paused.Time!.GetType(); // l'instantané est immuable : on en refait un en pause
        _tracker.Update(new TelemetrySnapshot
        {
            Status = VesselStatus.Connected,
            VesselName = "Kerbal X",
            Time = new TimeInfo { Ut = 7_900, Paused = true, Rates = new double[] { 1, 5, 10 } },
            Maneuver = paused.Maneuver
        }, _driver);

        Assert.Equal(ManeuverWarpState.Running, _tracker.State);
        Assert.Single(_driver.Calls);
    }

    [Fact]
    public void A_game_that_no_longer_warps_ends_the_operation_after_a_few_relaunches()
    {
        Begin();
        for (var i = 0; i < 10 && _tracker.Running; i++)
            Tick(2, 5_000, false); // aucun temps ne s'écoule, loin de l'arrêt

        Assert.Equal(ManeuverWarpState.Interrupted, _tracker.State);
        Assert.Equal(1 + ManeuverWarpTracker.MaxIdleRelaunches, _driver.Calls.Count);
    }

    [Fact]
    public void Near_the_end_at_one_x_the_last_seconds_just_elapse()
    {
        Begin();
        Tick(5, 7_000, true, 1000);
        Tick(3, Stop - 20, false);
        Assert.Equal(2, _driver.Calls.Count);
        Tick(2, Stop - 18, false); // l'étape suivante n'a pas accéléré (1×) : on laisse passer le temps
        Assert.Equal(ManeuverWarpState.Running, _tracker.State);
        var calls = _driver.Calls.Count;

        Tick(2, Stop - 16, false);
        Assert.Equal(calls, _driver.Calls.Count);

        Tick(15, Stop - 2, false);
        Assert.Equal(ManeuverWarpState.Arrived, _tracker.State);
    }

    [Fact]
    public void The_number_of_relaunches_is_bounded()
    {
        Begin();
        for (var i = 0; i < ManeuverWarpTracker.MaxRelaunches + 5; i++)
        {
            Tick(2, 7_000, true, 1000);
            Tick(3, Stop - 400, false);
        }

        Assert.Equal(1 + ManeuverWarpTracker.MaxRelaunches, _driver.Calls.Count);
    }
}
