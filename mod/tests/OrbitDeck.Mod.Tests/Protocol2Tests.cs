using System.Text;
using Newtonsoft.Json.Linq;
using OrbitDeck.Mod.Protocol;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Mod.Tests;

/// <summary>Protocole 2.0 : nouveaux blocs de l'état, annonce des familles, seuils de republication, lecture lente et isolée.</summary>
public class Protocol2Tests
{
    private static TelemetrySnapshot With(Func<TelemetrySnapshot, TelemetrySnapshot>? _ = null) => Snapshots.InFlight();

    private static TelemetrySnapshot Flight(
        CurrentOrbitInfo? orbit = null, ResourcesInfo? resources = null, PerformanceInfo? performance = null, LandingInfo? landing = null,
        TargetInfo? target = null, SystemsInfo? systems = null, DockingInfo? docking = null) => new()
    {
        Status = VesselStatus.Connected, GameStateName = "FlightView", VesselName = "Kerbal X",
        AltitudeSeaLevel = 1000, AltitudeSurface = 990, SpeedSurface = 100, SpeedOrbital = 200, SpeedMode = SpeedMode.Surface,
        Throttle = 0.5f, SasEnabled = true, SasMode = "StabilityAssist",
        Orbit = orbit!, Resources = resources!, Performance = performance!, Landing = landing!, Target = target!, Systems = systems!, Docking = docking!
    };

    private static JObject State(TelemetrySnapshot snapshot) => JObject.Parse(ProtocolMessages.State(snapshot, 1));

    [Fact]
    public void Without_new_blocks_the_state_has_none_of_them()
    {
        var message = State(Snapshots.InFlight());

        foreach (var name in new[] { "orbit", "resources", "performance", "landing", "target", "systems", "docking" })
            Assert.False(message.ContainsKey(name), name);
    }

    [Fact]
    public void New_blocks_come_after_maneuver_in_a_fixed_order()
    {
        var snapshot = new TelemetrySnapshot
        {
            Status = VesselStatus.Connected, VesselName = "X",
            Maneuver = new ManeuverInfo { Count = 0 },
            Orbit = new CurrentOrbitInfo { Ap = 1 }, Resources = new ResourcesInfo { Vessel = new ResourceSet() }, Performance = new PerformanceInfo { Dv = 1 },
            Landing = new LandingInfo { Radar = 1 }, Target = new TargetInfo { None = true }, Systems = new SystemsInfo(), Docking = new DockingInfo { Ports = 0 }
        };

        var names = State(snapshot).Properties().Select(p => p.Name).ToList();

        Assert.Equal(new[] { "maneuver", "orbit", "resources", "performance", "landing", "target", "systems", "docking" }, names.Skip(names.IndexOf("maneuver")));
    }

    [Fact]
    public void Current_orbit_is_published_with_instants_and_omits_unknowns()
    {
        var orbit = new CurrentOrbitInfo { Ap = 85000.4, Pe = 84000.6, Period = 2540.34, Inc = 28.456, ApUt = 1234800.04, PeUt = 1236100.0, Body = "Kerbin", Atmosphere = 70000 };
        var message = State(Flight(orbit: orbit))["orbit"]!;

        Assert.Equal(85000, (double)message["ap"]!);
        Assert.Equal(84001, (double)message["pe"]!);
        Assert.Equal(2540.3, (double)message["period"]!);
        Assert.Equal(28.46, (double)message["inc"]!);
        Assert.Equal(1234800.0, (double)message["apUt"]!);
        Assert.Equal("Kerbin", (string?)message["body"]);
        Assert.Equal(70000, (double)message["atmosphere"]!);
        Assert.Null(message["escape"]);

        var open = State(Flight(orbit: new CurrentOrbitInfo { Pe = 50000, Escape = true, Body = "Kerbin" }))["orbit"]!;
        Assert.Null(open["ap"]);
        Assert.Null(open["period"]);
        Assert.True((bool)open["escape"]!);
    }

    [Fact]
    public void Resources_flatten_the_vessel_and_nest_the_stage_and_omit_missing_resources()
    {
        var resources = new ResourcesInfo
        {
            Vessel = new ResourceSet { Fuel = new ResourceAmount { Amount = 1240.04, Max = 2000 }, Electric = new ResourceAmount { Amount = 0, Max = 400 } },
            Stage = new ResourceSet { Fuel = new ResourceAmount { Amount = 600, Max = 900 } }
        };
        var message = State(Flight(resources: resources))["resources"]!;

        Assert.Equal(1240.0, (double)message["fuel"]!["amount"]!);
        Assert.Equal(2000.0, (double)message["fuel"]!["max"]!);
        Assert.Equal(0.0, (double)message["electric"]!["amount"]!);
        Assert.Null(message["oxidizer"]);
        Assert.Null(message["monoprop"]);
        Assert.Equal(600.0, (double)message["stage"]!["fuel"]!["amount"]!);
        Assert.Null(message["stage"]!["electric"]);
    }

    [Fact]
    public void Performance_landing_and_target_publish_only_what_is_known()
    {
        Assert.Equal(new[] { "dv", "dvStage" }, State(Flight(performance: new PerformanceInfo { Dv = 2800.04, DvStage = 940 }))["performance"]!.Children<JProperty>().Select(p => p.Name));
        Assert.Equal(1.42, (double)State(Flight(performance: new PerformanceInfo { Twr = 1.4249 }))["performance"]!["twr"]!);

        var landing = State(Flight(landing: new LandingInfo { VSpeed = -12.444, HSpeed = 3.1, Radar = 85.24 }))["landing"]!;
        Assert.Equal(-12.44, (double)landing["vspeed"]!);
        Assert.Equal(85.2, (double)landing["radar"]!);
        Assert.Null(landing["impactUt"]);

        Assert.True((bool)State(Flight(target: new TargetInfo { None = true }))["target"]!["none"]!);
        var target = State(Flight(target: new TargetInfo { Name = "Station", Distance = 1840.04, Rel = -3.2 }))["target"]!;
        Assert.Equal("Station", (string?)target["name"]);
        Assert.Equal(-3.2, (double)target["rel"]!);
        Assert.Null(target["none"]);
    }

    [Fact]
    public void Systems_distinguish_no_module_from_unreadable()
    {
        var systems = new SystemsInfo
        {
            Solar = new SystemCategory { Count = 4, State = SystemState.Extended },
            Ladders = new SystemCategory { Count = 0 },
            Parachutes = new SystemCategory { Count = 3, State = SystemState.Ready },
            Antennas = new SystemCategory { Count = 2, State = SystemState.Retracted, Retractable = false }
        };
        var message = State(Flight(systems: systems))["systems"]!;

        Assert.Equal("extended", (string?)message["solar"]!["state"]);
        Assert.Equal(0, (int)message["ladders"]!["count"]!);
        Assert.Null(message["ladders"]!["state"]);
        Assert.Equal("ready", (string?)message["parachutes"]!["state"]);
        Assert.False((bool)message["antennas"]!["retractable"]!);
        Assert.Null(message["solar"]!["retractable"]);

        var unreadable = State(Flight(systems: new SystemsInfo { Solar = new SystemCategory { Count = 1, State = SystemState.Mixed } }))["systems"]!;
        Assert.Null(unreadable["antennas"]);
    }

    [Fact]
    public void Docking_publishes_the_port_state_and_capabilities()
    {
        var message = State(Flight(docking: new DockingInfo { Ports = 2, State = DockingPortState.Armed, Armable = true, CanUndock = false }))["docking"]!;

        Assert.Equal(2, (int)message["ports"]!);
        Assert.Equal("armed", (string?)message["state"]);
        Assert.True((bool)message["armable"]!);
        Assert.False((bool)message["canUndock"]!);
        Assert.Equal(0, (int)State(Flight(docking: new DockingInfo { Ports = 0 }))["docking"]!["ports"]!);
    }

    [Fact]
    public void The_worst_case_state_stays_well_under_4096_bytes()
    {
        var node = new ManeuverNodeInfo { Ut = 1600.5, Dv = 1243.5, Burn = 48, Ignition = 1576.5, After = new OrbitInfo { Ap = 200000, Pe = 180000, Atmosphere = 70000 } };
        var set = new ResourceSet
        {
            Fuel = new ResourceAmount { Amount = 123456.7, Max = 234567.8 }, Oxidizer = new ResourceAmount { Amount = 123456.7, Max = 234567.8 },
            Electric = new ResourceAmount { Amount = 12345.6, Max = 23456.7 }, Monoprop = new ResourceAmount { Amount = 123.4, Max = 234.5 }
        };
        var snapshot = new TelemetrySnapshot
        {
            Status = VesselStatus.Connected, GameStateName = "FlightView", VesselName = "Kerbal X", AltitudeSeaLevel = 12400.45, AltitudeSurface = 12380.12,
            SpeedSurface = 245.3, SpeedOrbital = 2300.14, SpeedTarget = 5.5, SpeedMode = SpeedMode.Target, Throttle = 0.5f, SasEnabled = true, SasMode = "Prograde",
            RcsEnabled = true, LightsEnabled = true, BrakesEnabled = true, GearEnabled = true, StageCount = 12, StageExhausted = false, View = ViewKind.Map,
            AvailableSasModes = Enum.GetValues<SasModeKind>(),
            Time = new TimeInfo { Ut = 123456789.1, Rate = 100000, Index = 7, Rates = new double[] { 1, 5, 10, 50, 100, 1000, 10000, 100000 }, Mode = WarpMode.Rails, Paused = true, Pausable = true, MaxIndex = 7, Limit = WarpLimit.Atmosphere, Auto = true, DayLength = 21600 },
            Maneuver = new ManeuverInfo { Count = 8, Nodes = Enumerable.Range(0, 8).Select(i => node).ToArray(), Before = node.After, DvAvailable = 99999.9, Burning = new BurnInfo { Remaining = 1234.5, Progress = 0.123 } },
            Orbit = new CurrentOrbitInfo { Ap = 1234567890, Pe = 1234567890, Period = 123456.7, Inc = 123.45, ApUt = 123456789.1, PeUt = 123456789.1, Body = "Eeloo-Moon-Longname", Atmosphere = 70000 },
            Resources = new ResourcesInfo { Vessel = set, Stage = set },
            Performance = new PerformanceInfo { Dv = 99999.9, DvStage = 99999.9, Twr = 12.34 },
            Landing = new LandingInfo { VSpeed = -1234.56, HSpeed = 1234.56, Radar = 12345.6, ImpactUt = 123456789.1 },
            Target = new TargetInfo { Name = "A very long target vessel name for a station", Distance = 1234567.8, Rel = -1234.56 },
            Systems = new SystemsInfo
            {
                Solar = new SystemCategory { Count = 99, State = SystemState.Mixed, Retractable = false }, Antennas = new SystemCategory { Count = 99, State = SystemState.Moving, Retractable = false },
                Ladders = new SystemCategory { Count = 99, State = SystemState.Extended, Retractable = false }, Parachutes = new SystemCategory { Count = 99, State = SystemState.Deployed }
            },
            Docking = new DockingInfo { Ports = 99, State = DockingPortState.Approach, Armable = true, CanUndock = true }
        };

        Assert.True(Encoding.UTF8.GetByteCount(ProtocolMessages.State(snapshot, 123456789)) < 4096);
    }

    [Fact]
    public void Hello_without_features_is_exactly_the_one_of_mod_0_5()
    {
        Assert.Equal(ProtocolMessages.Hello("0.5.0", "0.2.8.5"), ProtocolMessages.Hello("0.5.0", "0.2.8.5", null));
        Assert.Equal(ProtocolMessages.Hello("0.5.0", "0.2.8.5"), ProtocolMessages.Hello("0.5.0", "0.2.8.5", Array.Empty<string>()));
    }

    [Fact]
    public void Hello_announces_the_features()
    {
        var message = JObject.Parse(ProtocolMessages.Hello("0.6.0", "0.2.8.5", new[] { "orbit", "quickSave" }));

        Assert.Equal(1, (int)message["v"]!);
        Assert.Equal("0.6.0", (string?)message["mod"]);
        Assert.Equal(new[] { "orbit", "quickSave" }, message["features"]!.Select(t => (string?)t));
    }

    [Fact]
    public void Existing_decisions_are_unchanged_when_no_new_block_is_present()
    {
        // Deux états identiques sans nouveau bloc ne se republient pas ; aucun nouveau bloc ne change cette décision.
        Assert.False(SnapshotComparer.HasChanged(Snapshots.InFlight(), Snapshots.InFlight()));
    }

    [Fact]
    public void Each_new_block_is_republished_only_beyond_its_threshold()
    {
        static bool Changed(TelemetrySnapshot a, TelemetrySnapshot b) => SnapshotComparer.HasChanged(a, b);

        Assert.False(Changed(Flight(orbit: new CurrentOrbitInfo { Ap = 100, ApUt = 1000 }), Flight(orbit: new CurrentOrbitInfo { Ap = 100.4, ApUt = 1000.9 })));
        Assert.True(Changed(Flight(orbit: new CurrentOrbitInfo { Ap = 100 }), Flight(orbit: new CurrentOrbitInfo { Ap = 100.6 })));
        Assert.True(Changed(Flight(orbit: new CurrentOrbitInfo { ApUt = 1000 }), Flight(orbit: new CurrentOrbitInfo { ApUt = 1001.2 })));
        Assert.True(Changed(Flight(), Flight(orbit: new CurrentOrbitInfo { Ap = 1 })));

        ResourcesInfo Fuel(double amount) => new() { Vessel = new ResourceSet { Fuel = new ResourceAmount { Amount = amount, Max = 1000 } } };
        Assert.False(Changed(Flight(resources: Fuel(500)), Flight(resources: Fuel(501.5))));
        Assert.True(Changed(Flight(resources: Fuel(500)), Flight(resources: Fuel(503))));

        Assert.False(Changed(Flight(performance: new PerformanceInfo { Dv = 100, Twr = 1.00 }), Flight(performance: new PerformanceInfo { Dv = 100.4, Twr = 1.005 })));
        Assert.True(Changed(Flight(performance: new PerformanceInfo { Twr = 1.00 }), Flight(performance: new PerformanceInfo { Twr = 1.02 })));

        Assert.False(Changed(Flight(landing: new LandingInfo { VSpeed = -10, Radar = 100 }), Flight(landing: new LandingInfo { VSpeed = -10.04, Radar = 100.4 })));
        Assert.True(Changed(Flight(landing: new LandingInfo { VSpeed = -10 }), Flight(landing: new LandingInfo { VSpeed = -10.2 })));

        Assert.True(Changed(Flight(target: new TargetInfo { None = true }), Flight(target: new TargetInfo { Name = "S", Distance = 5 })));
        Assert.False(Changed(Flight(target: new TargetInfo { Name = "S", Distance = 5, Rel = -1 }), Flight(target: new TargetInfo { Name = "S", Distance = 5.4, Rel = -1.04 })));

        SystemsInfo Solar(SystemState state) => new() { Solar = new SystemCategory { Count = 2, State = state } };
        Assert.False(Changed(Flight(systems: Solar(SystemState.Extended)), Flight(systems: Solar(SystemState.Extended))));
        Assert.True(Changed(Flight(systems: Solar(SystemState.Extended)), Flight(systems: Solar(SystemState.Moving))));

        Assert.True(Changed(Flight(docking: new DockingInfo { Ports = 1, State = DockingPortState.Armed }), Flight(docking: new DockingInfo { Ports = 1, State = DockingPortState.Docked })));
    }

    [Fact]
    public void A_slow_read_reads_once_per_period_and_on_demand()
    {
        var now = 0.0;
        var reads = 0;
        var slow = new SlowRead<string>(() => now, 1.0);

        Assert.Equal("1", slow.Get(() => (++reads).ToString()));
        now = 0.5;
        Assert.Equal("1", slow.Get(() => (++reads).ToString()));
        now = 1.0;
        Assert.Equal("2", slow.Get(() => (++reads).ToString()));
        now = 1.2;
        Assert.Equal("3", slow.Get(() => (++reads).ToString(), force: true));
        slow.Reset();
        Assert.Equal("4", slow.Get(() => (++reads).ToString()));
        Assert.Equal(4, reads);
    }

    [Fact]
    public void A_failing_slow_read_waits_a_period_before_trying_again_and_rethrows()
    {
        var now = 0.0;
        var attempts = 0;
        var slow = new SlowRead<string>(() => now, 1.0);
        string Fail() { attempts++; throw new InvalidOperationException("boom"); }

        Assert.Throws<InvalidOperationException>(() => slow.Get(Fail));
        now = 0.4;
        Assert.Null(slow.Get(Fail));
        Assert.Equal(1, attempts);
        now = 1.1;
        Assert.Throws<InvalidOperationException>(() => slow.Get(Fail));
        Assert.Equal(2, attempts);
    }

    [Fact]
    public void A_failing_block_is_omitted_and_logged_once_per_period_and_never_stops_the_other_blocks()
    {
        var now = 0.0;
        var log = new List<string>();
        var safe = new SafeReader(() => now, log.Add, 5.0);

        var orbit = safe.Read<CurrentOrbitInfo>("orbit", () => throw new InvalidOperationException("boom"));
        var performance = safe.Read("performance", () => new PerformanceInfo { Dv = 10 });
        now = 1;
        safe.Read<CurrentOrbitInfo>("orbit", () => throw new InvalidOperationException("boom"));
        safe.Read<ResourcesInfo>("resources", () => throw new InvalidOperationException("boom"));
        now = 6;
        safe.Read<CurrentOrbitInfo>("orbit", () => throw new InvalidOperationException("boom"));

        Assert.Null(orbit);
        Assert.NotNull(performance);
        Assert.Equal(3, log.Count);
        Assert.Contains("orbit", log[0]);
        Assert.Contains("resources", log[1]);

        // Le reste de l'état part comme avant, avec le bloc sain et sans le bloc en panne.
        var message = State(Flight(orbit: orbit, performance: performance));
        Assert.Null(message["orbit"]);
        Assert.Equal(10, (double)message["performance"]!["dv"]!);
        Assert.Equal("connected", (string?)message["status"]);
    }
}
