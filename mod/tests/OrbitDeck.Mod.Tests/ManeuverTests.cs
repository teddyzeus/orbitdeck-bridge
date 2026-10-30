using System.Text;
using Newtonsoft.Json.Linq;
using OrbitDeck.Mod.Protocol;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Mod.Tests;

public class ManeuverTests
{
    private static ManeuverNodeInfo Node(double ut = 1000, double dv = 100, double? burn = 20, double? ignition = 990) =>
        new() { Ut = ut, Dv = dv, Burn = burn, Ignition = ignition, After = new OrbitInfo { Ap = 200_000, Pe = 180_000 } };

    private static ManeuverInfo Plan(params ManeuverNodeInfo[] nodes) => new()
    {
        Count = nodes.Length,
        Nodes = nodes,
        Before = nodes.Length > 0 ? new OrbitInfo { Ap = 85_000, Pe = 84_000 } : null,
        DvAvailable = 2800
    };

    private static TelemetrySnapshot WithManeuver(ManeuverInfo? maneuver) => new()
    {
        Status = VesselStatus.Connected,
        VesselName = "Kerbal X",
        Maneuver = maneuver
    };

    private static JObject StateOf(ManeuverInfo? maneuver) => JObject.Parse(ProtocolMessages.State(WithManeuver(maneuver), 1));

    // --- Sérialisation -----------------------------------------------------------------------------------------------

    [Fact]
    public void State_omits_maneuver_when_the_game_does_not_provide_it()
    {
        Assert.Null(StateOf(null)["maneuver"]);
    }

    [Fact]
    public void State_omits_maneuver_without_a_vessel()
    {
        var message = JObject.Parse(ProtocolMessages.State(new TelemetrySnapshot { Status = VesselStatus.NoVessel, Maneuver = Plan(Node()) }, 1));

        Assert.Null(message["maneuver"]);
    }

    [Fact]
    public void An_empty_plan_is_present_with_a_zero_count()
    {
        var maneuver = (JObject)StateOf(new ManeuverInfo { Count = 0 })["maneuver"]!;

        Assert.Equal(0, (int)maneuver["count"]!);
        Assert.Empty((JArray)maneuver["nodes"]!);
        Assert.Null(maneuver["before"]);
        Assert.Null(maneuver["burning"]);
    }

    [Fact]
    public void A_node_is_serialized_with_its_orbit()
    {
        var maneuver = (JObject)StateOf(Plan(Node(1250000.04, 1243.46, 48.24, 1249975.9)))["maneuver"]!;
        var node = (JObject)maneuver["nodes"]![0]!;

        Assert.Equal(1, (int)maneuver["count"]!);
        Assert.Equal(1250000.0, (double)node["ut"]!);
        Assert.Equal(1243.5, (double)node["dv"]!);
        Assert.Equal(48.2, (double)node["burn"]!);
        Assert.Equal(1249975.9, (double)node["ignition"]!);
        Assert.Equal(200_000, (double)node["after"]!["ap"]!);
        Assert.Equal(180_000, (double)node["after"]!["pe"]!);
        Assert.Equal(85_000, (double)maneuver["before"]!["ap"]!);
        Assert.Equal(2800, (double)maneuver["dvAvailable"]!);
    }

    [Fact]
    public void Unknown_fields_are_omitted_never_written_as_zero()
    {
        var plan = new ManeuverInfo { Count = 1, Nodes = new[] { new ManeuverNodeInfo { Ut = 1000, Dv = 50 } } };
        var node = (JObject)StateOf(plan)["maneuver"]!["nodes"]![0]!;

        Assert.Null(node["burn"]);
        Assert.Null(node["ignition"]);
        Assert.Null(node["after"]);
        Assert.Null(StateOf(plan)["maneuver"]!["dvAvailable"]);
    }

    [Fact]
    public void An_open_orbit_has_no_apoapsis_and_is_flagged_escape()
    {
        var plan = Plan(new ManeuverNodeInfo { Ut = 1000, Dv = 50, After = new OrbitInfo { Escape = true, Pe = 30_000 } });
        var after = (JObject)StateOf(plan)["maneuver"]!["nodes"]![0]!["after"]!;

        Assert.Null(after["ap"]);
        Assert.True((bool)after["escape"]!);
        Assert.Equal(30_000, (double)after["pe"]!);
    }

    [Fact]
    public void A_closed_orbit_has_no_escape_field_and_carries_the_atmosphere_when_there_is_one()
    {
        var plan = Plan(new ManeuverNodeInfo { Ut = 1000, Dv = 50, After = new OrbitInfo { Ap = 1, Pe = 2, Atmosphere = 70_000 } });
        var after = (JObject)StateOf(plan)["maneuver"]!["nodes"]![0]!["after"]!;

        Assert.Null(after["escape"]);
        Assert.Equal(70_000, (double)after["atmosphere"]!);
    }

    [Fact]
    public void At_most_eight_nodes_are_sent_but_the_count_stays_real()
    {
        var nodes = Enumerable.Range(0, 12).Select(i => Node(1000 + i * 100)).ToArray();
        var maneuver = (JObject)StateOf(new ManeuverInfo { Count = 12, Nodes = nodes })["maneuver"]!;

        Assert.Equal(12, (int)maneuver["count"]!);
        Assert.Equal(8, ((JArray)maneuver["nodes"]!).Count);
    }

    [Fact]
    public void The_worst_case_state_stays_under_the_message_limit()
    {
        var orbit = new OrbitInfo { Ap = 123_456_789_012, Pe = -123_456_789, Atmosphere = 70_000 };
        var nodes = Enumerable.Range(0, 8)
            .Select(i => new ManeuverNodeInfo { Ut = 123_456_789.1 + i, Dv = 12_345.6, Burn = 9_999.9, Ignition = 123_456_789.1, After = orbit })
            .ToArray();
        var time = new TimeInfo { Ut = 123_456_789.1, Rate = 100000, Index = 7, Rates = new double[] { 1, 5, 10, 50, 100, 1000, 10000, 100000 }, Pausable = true, DayLength = 21600 };
        var snapshot = new TelemetrySnapshot
        {
            Status = VesselStatus.Connected,
            VesselName = new string('K', 80),
            Time = time,
            Maneuver = new ManeuverInfo
            {
                Count = 8,
                Nodes = nodes,
                Before = orbit,
                DvAvailable = 12_345.6,
                Burning = new BurnInfo { Remaining = 12_345.6, Progress = 0.123 }
            }
        };

        Assert.True(Encoding.UTF8.GetByteCount(ProtocolMessages.State(snapshot, 1)) < 4096);
    }

    [Fact]
    public void The_burning_object_carries_only_what_is_known()
    {
        var plan = new ManeuverInfo { Count = 1, Nodes = new[] { Node() }, Burning = new BurnInfo { Remaining = 312.04, Progress = 0.7396 } };
        var burning = (JObject)StateOf(plan)["maneuver"]!["burning"]!;

        Assert.Equal(312.0, (double)burning["remaining"]!);
        Assert.Equal(0.74, (double)burning["progress"]!);
    }

    // --- Combustion --------------------------------------------------------------------------------------------------

    [Fact]
    public void There_is_no_burn_before_the_ignition()
    {
        Assert.Null(ManeuverInfo.BurningOf(980, Node(ignition: 990), 100));
    }

    [Fact]
    public void The_burn_starts_at_the_ignition_and_progress_follows_the_remaining_delta_v()
    {
        var burning = ManeuverInfo.BurningOf(995, Node(dv: 100, ignition: 990), 25);

        Assert.NotNull(burning);
        Assert.Equal(25, burning!.Remaining);
        Assert.Equal(0.75, burning.Progress!.Value, 6);
    }

    [Fact]
    public void There_is_no_burn_without_an_ignition_a_node_or_a_remaining_value()
    {
        Assert.Null(ManeuverInfo.BurningOf(995, Node(ignition: null), 25));
        Assert.Null(ManeuverInfo.BurningOf(995, null!, 25));
        Assert.Null(ManeuverInfo.BurningOf(995, Node(), null));
    }

    [Fact]
    public void A_negative_remaining_value_is_clamped_to_a_finished_burn()
    {
        var burning = ManeuverInfo.BurningOf(995, Node(dv: 100), -3);

        Assert.Equal(0, burning!.Remaining);
        Assert.Equal(1, burning.Progress);
    }

    // --- Republication -----------------------------------------------------------------------------------------------

    [Fact]
    public void An_identical_plan_does_not_trigger_a_republication()
    {
        Assert.False(SnapshotComparer.HasModeChange(WithManeuver(Plan(Node())), WithManeuver(Plan(Node()))));
    }

    [Fact]
    public void Adding_or_removing_a_node_triggers_a_republication()
    {
        Assert.True(SnapshotComparer.HasModeChange(WithManeuver(Plan()), WithManeuver(Plan(Node()))));
        Assert.True(SnapshotComparer.HasModeChange(WithManeuver(Plan(Node())), WithManeuver(Plan())));
        Assert.True(SnapshotComparer.HasModeChange(WithManeuver(null), WithManeuver(Plan())));
    }

    [Fact]
    public void Moving_or_resizing_a_node_triggers_a_republication()
    {
        var before = WithManeuver(Plan(Node()));

        Assert.True(SnapshotComparer.HasModeChange(before, WithManeuver(Plan(Node(ut: 1010)))));
        Assert.True(SnapshotComparer.HasModeChange(before, WithManeuver(Plan(Node(dv: 101)))));
        Assert.True(SnapshotComparer.HasModeChange(before, WithManeuver(Plan(Node(ignition: 985)))));
        Assert.True(SnapshotComparer.HasModeChange(before, WithManeuver(Plan(Node(burn: null)))));
    }

    [Fact]
    public void Float_noise_below_the_tolerances_does_not_republish()
    {
        var before = WithManeuver(Plan(Node()));

        Assert.False(SnapshotComparer.HasModeChange(before, WithManeuver(Plan(Node(ut: 1000.01, dv: 100.01, ignition: 990.01)))));
    }

    [Fact]
    public void The_available_delta_v_republishes_only_beyond_half_a_meter_per_second()
    {
        var before = WithManeuver(new ManeuverInfo { Count = 1, Nodes = new[] { Node() }, DvAvailable = 2800 });

        Assert.False(SnapshotComparer.HasModeChange(before, WithManeuver(new ManeuverInfo { Count = 1, Nodes = new[] { Node() }, DvAvailable = 2800.4 })));
        Assert.True(SnapshotComparer.HasModeChange(before, WithManeuver(new ManeuverInfo { Count = 1, Nodes = new[] { Node() }, DvAvailable = 2801 })));
    }

    [Fact]
    public void The_burn_progress_republishes_beyond_its_thresholds()
    {
        ManeuverInfo With(double remaining, double progress) => new()
        {
            Count = 1,
            Nodes = new[] { Node() },
            Burning = new BurnInfo { Remaining = remaining, Progress = progress }
        };

        Assert.False(SnapshotComparer.HasModeChange(WithManeuver(With(100, 0.5)), WithManeuver(With(100.05, 0.502))));
        Assert.True(SnapshotComparer.HasModeChange(WithManeuver(With(100, 0.5)), WithManeuver(With(99.8, 0.5))));
        Assert.True(SnapshotComparer.HasModeChange(WithManeuver(With(100, 0.5)), WithManeuver(With(100, 0.51))));
        Assert.True(SnapshotComparer.HasModeChange(WithManeuver(With(100, 0.5)), WithManeuver(new ManeuverInfo { Count = 1, Nodes = new[] { Node() } })));
    }
}
