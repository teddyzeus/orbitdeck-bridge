using System.Globalization;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using OrbitDeck.Mod.Commands;
using OrbitDeck.Mod.Protocol;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Mod.Tests;

/// <summary>
/// FILET DE NON-RÉGRESSION 0.5 → 2.0 (docs/PLAN-2.0.md du dépôt du plugin, phase P0 ; SPEC-2.0.md §1).
///
/// Les fichiers de <c>Compat05/</c> ont été produits PAR LE CODE DU MOD 0.5.0 et ne sont JAMAIS régénérés : le mod 2.0 doit publier,
/// pour un état sans nouveau bloc, exactement les mêmes octets, et valider les commandes 1.1 exactement de la même façon.
/// Un écart est une régression, ou une dérogation à demander à l'utilisateur. Génération initiale seulement :
/// <c>COMPAT_GENERATE=1 dotnet test --filter Compat05</c> (refuse d'écraser un fichier existant).
/// </summary>
public class Compat05Tests
{
    private static string Dir([CallerFilePath] string path = "") => Path.Combine(Path.GetDirectoryName(path)!, "Compat05");

    private static readonly bool Generate = Environment.GetEnvironmentVariable("COMPAT_GENERATE") == "1";

    /// <summary>Compare <paramref name="actual"/> au fichier de référence ; en génération initiale, l'écrit (jamais par-dessus un existant).</summary>
    private static void Check(string name, string actual)
    {
        var file = Path.Combine(Dir(), name);
        if (Generate)
        {
            Assert.False(File.Exists(file), $"{name} est déjà figé : il n'est jamais régénéré.");
            Directory.CreateDirectory(Dir());
            File.WriteAllText(file, actual, new System.Text.UTF8Encoding(false));
            return;
        }

        Assert.True(File.Exists(file), $"référence manquante : {name}");
        // Fins de ligne normalisées : git peut convertir les fichiers de référence selon la machine.
        Assert.Equal(File.ReadAllText(file).Replace("\r\n", "\n"), actual.Replace("\r\n", "\n"));
    }

    private static readonly OrbitInfo OrbitAfter = new() { Ap = 200000, Pe = 180000, Atmosphere = 70000 };
    private static readonly OrbitInfo OrbitBefore = new() { Ap = 85000, Pe = 84000, Atmosphere = 70000 };

    private static ManeuverNodeInfo Node(double ut = 1600, OrbitInfo? after = null) =>
        new() { Ut = ut, Dv = 1243.5, Burn = 48, Ignition = ut - 24, After = after ?? OrbitAfter };

    private static TimeInfo Rails() => new()
    {
        Ut = 1000.04, Rate = 5, Index = 1, Rates = new double[] { 1, 5, 10, 50, 100, 1000, 10000, 100000 },
        Mode = WarpMode.Rails, Paused = false, Pausable = true, MaxIndex = 6, DayLength = 21600
    };

    private static TelemetrySnapshot Flight(Action<Builder>? configure = null)
    {
        var b = new Builder();
        configure?.Invoke(b);
        return new TelemetrySnapshot
        {
            Status = b.Status, GameStateName = "FlightView", VesselName = "Kerbal X",
            AltitudeSeaLevel = 12400.456, AltitudeSurface = 12380.123, SpeedSurface = 245.3, SpeedOrbital = 2300.14,
            SpeedTarget = b.TargetSpeed, SpeedMode = b.SpeedMode, Throttle = 0.5f, SasEnabled = true, SasMode = "Prograde", RcsEnabled = false,
            LightsEnabled = b.Lights, BrakesEnabled = b.Brakes, GearEnabled = b.Gear, StageCount = b.Stage, StageExhausted = b.Exhausted,
            View = b.View, AvailableSasModes = b.SasModes, Time = b.Time, Maneuver = b.Maneuver
        };
    }

    private sealed class Builder
    {
        public VesselStatus Status = VesselStatus.Connected;
        public double? TargetSpeed;
        public SpeedMode SpeedMode = SpeedMode.Surface;
        public bool? Lights, Brakes, Gear, Exhausted;
        public int? Stage;
        public ViewKind? View;
        public IReadOnlyList<SasModeKind>? SasModes;
        public TimeInfo? Time;
        public ManeuverInfo? Maneuver;
    }

    private static readonly SasModeKind[] AllSas =
    {
        SasModeKind.StabilityAssist, SasModeKind.Prograde, SasModeKind.Retrograde, SasModeKind.Normal, SasModeKind.Antinormal,
        SasModeKind.RadialIn, SasModeKind.RadialOut, SasModeKind.Target, SasModeKind.Maneuver
    };

    private static ManeuverInfo Plan(Func<ManeuverInfo, ManeuverInfo>? decorate = null)
    {
        var plan = new ManeuverInfo { Count = 1, Nodes = new[] { Node() }, Before = OrbitBefore, DvAvailable = 2800 };
        return decorate?.Invoke(plan) ?? plan;
    }

    /// <summary>États de référence : tout ce que publiait le mod 0.5 (et rien de plus).</summary>
    private static IEnumerable<(string Name, TelemetrySnapshot Snapshot)> Reference()
    {
        yield return ("no-vessel", TelemetrySnapshot.NoVesselIn("MainMenu"));
        yield return ("no-vessel-unnamed", TelemetrySnapshot.NoVessel);
        yield return ("bare", Snapshots.InFlight());
        yield return ("not-controllable", Flight(b => b.Status = VesselStatus.NotControllable));
        yield return ("switches", Flight(b => { b.Lights = true; b.Brakes = false; b.Gear = true; b.View = ViewKind.Map; b.Stage = 3; b.Exhausted = false; }));
        yield return ("exhausted", Flight(b => { b.Stage = 1; b.Exhausted = true; }));
        yield return ("sas-available", Flight(b => b.SasModes = AllSas));
        yield return ("sas-none", Flight(b => b.SasModes = Array.Empty<SasModeKind>()));
        yield return ("target-speed", Flight(b => { b.SpeedMode = SpeedMode.Target; b.TargetSpeed = 12.3; }));
        yield return ("orbit-speed", Flight(b => b.SpeedMode = SpeedMode.Orbit));
        yield return ("time-rails", Flight(b => b.Time = Rails()));
        yield return ("time-physics", Flight(b => b.Time = new TimeInfo { Ut = 5.5, Rate = 2, Index = 1, Rates = new double[] { 1, 2, 3, 4 }, Mode = WarpMode.Physics, MaxIndex = 3 }));
        yield return ("time-paused-limited", Flight(b => b.Time = new TimeInfo { Ut = 1000, Rate = 100, Index = 4, Rates = new double[] { 1, 5, 10, 50, 100, 1000 }, Mode = WarpMode.Rails, Paused = true, Pausable = true, MaxIndex = 4, Limit = WarpLimit.Atmosphere, Auto = true }));
        yield return ("time-bare", Flight(b => b.Time = new TimeInfo { Ut = 12, Rates = new double[] { 1 } }));
        yield return ("maneuver-one", Flight(b => b.Maneuver = Plan()));
        yield return ("maneuver-none", Flight(b => b.Maneuver = new ManeuverInfo { Count = 0, DvAvailable = 2800 }));
        yield return ("maneuver-many", Flight(b => b.Maneuver = new ManeuverInfo
        {
            Count = 11, Nodes = Enumerable.Range(0, 8).Select(i => Node(1600 + i * 1000)).ToArray(), Before = OrbitBefore, DvAvailable = 2800.123
        }));
        yield return ("maneuver-escape", Flight(b => b.Maneuver = Plan(p => new ManeuverInfo { Count = 1, Nodes = new[] { Node(1600, new OrbitInfo { Escape = true, Pe = 50000 }) }, Before = OrbitBefore })));
        yield return ("maneuver-burning", Flight(b => b.Maneuver = new ManeuverInfo { Count = 1, Nodes = new[] { Node() }, Before = OrbitBefore, DvAvailable = 2800, Burning = new BurnInfo { Remaining = 312.04, Progress = 0.7432 } }));
        yield return ("maneuver-burning-partial", Flight(b => b.Maneuver = new ManeuverInfo { Count = 1, Nodes = new[] { new ManeuverNodeInfo { Ut = 1600, Dv = 10 } }, Burning = new BurnInfo { Remaining = 3 } }));
        yield return ("maneuver-warp-running", Flight(b => b.Maneuver = Plan(p => p.WithWarp(new ManeuverWarpInfo { State = ManeuverWarpState.Running, Stop = 1500 }))));
        yield return ("maneuver-warp-interrupted", Flight(b => b.Maneuver = Plan(p => p.WithWarp(new ManeuverWarpInfo { State = ManeuverWarpState.Interrupted, Reason = ManeuverWarpReason.NodeGone }))));
        yield return ("full", Flight(b =>
        {
            b.Lights = true; b.Brakes = true; b.Gear = false; b.View = ViewKind.Ship; b.Stage = 2; b.Exhausted = false; b.SasModes = AllSas;
            b.SpeedMode = SpeedMode.Target; b.TargetSpeed = 3.21; b.Time = Rails(); b.Maneuver = Plan(p => p.WithWarp(new ManeuverWarpInfo { State = ManeuverWarpState.Arrived, Stop = 1500 }));
        }));
    }

    [Fact]
    public void States_are_byte_for_byte_those_of_mod_0_5()
    {
        foreach (var (name, snapshot) in Reference())
            Check($"state-{name}.json", ProtocolMessages.State(snapshot, 1042));
    }

    [Fact]
    public void Server_messages_are_byte_for_byte_those_of_mod_0_5()
    {
        Check("hello.json", ProtocolMessages.Hello("0.5.0", "0.2.8.5.103184"));
        Check("ack.json", ProtocolMessages.Ack("cmd-1"));
        Check("error.json", ProtocolMessages.Error("cmd-2", ErrorCodes.RejectedByGame, "refusé"));
        Check("error-no-id.json", ProtocolMessages.Error(null, ErrorCodes.BadRequest, "x"));
        Check("pong.json", ProtocolMessages.Pong());
    }

    [Fact]
    public void Existing_state_has_the_same_snapshot_change_decisions()
    {
        // Les décisions de republication (seuils, changements discrets) font partie du contrat : même résultat qu'en 0.5.
        var rows = new List<string>();
        var states = Reference().ToList();
        foreach (var (nameA, a) in states)
        foreach (var (nameB, b) in states)
            rows.Add(FormattableString.Invariant($"{nameA}>{nameB}: mode={SnapshotComparer.HasModeChange(a, b)} discrete={SnapshotComparer.HasDiscreteChange(a, b)} changed={SnapshotComparer.HasChanged(a, b)}"));

        Check("comparer.txt", string.Join("\n", rows) + "\n");
    }

    private static readonly (string Name, string Args)[] Commands =
    {
        ("setThrottle", "{\"value\":0.5}"), ("setThrottle", "{\"value\":5}"), ("setThrottle", "{}"), ("setThrottle", "{\"value\":\"x\"}"),
        ("adjustThrottle", "{\"delta\":-0.1}"), ("adjustThrottle", "{\"delta\":2}"),
        ("setSas", "{\"enabled\":true}"), ("setSas", "{\"enabled\":1}"), ("toggleSas", "{}"),
        ("setRcs", "{\"enabled\":false}"), ("toggleRcs", "{}"),
        ("setSpeedMode", "{\"mode\":\"orbit\"}"), ("setSpeedMode", "{\"mode\":\"nope\"}"),
        ("setSasMode", "{\"mode\":\"prograde\"}"), ("setSasMode", "{\"mode\":\"antiTarget\"}"), ("setSasMode", "{\"mode\":\"maneuver\"}"), ("setSasMode", "{\"mode\":\"x\"}"),
        ("setLights", "{\"enabled\":true}"), ("toggleLights", "{}"), ("setBrakes", "{\"enabled\":true}"), ("toggleBrakes", "{}"),
        ("setGear", "{\"enabled\":false}"), ("toggleGear", "{}"),
        ("activateStage", "{}"),
        ("setView", "{\"view\":\"map\"}"), ("setView", "{\"view\":\"x\"}"), ("toggleView", "{}"),
        ("increaseWarp", "{}"), ("decreaseWarp", "{}"), ("stopWarp", "{}"),
        ("setWarpRate", "{\"rate\":100}"), ("setWarpRate", "{\"rate\":0}"), ("setWarpRate", "{\"rate\":-1}"),
        ("setPause", "{\"paused\":true}"), ("setPause", "{}"), ("togglePause", "{}"),
        ("warpToManeuver", "{\"ut\":1600.5,\"margin\":30}"), ("warpToManeuver", "{\"ut\":1600.5,\"margin\":30,\"maxRate\":1000}"),
        ("warpToManeuver", "{\"ut\":1,\"margin\":4000}"), ("warpToManeuver", "{\"margin\":30}"), ("warpToManeuver", "{\"ut\":1,\"margin\":0,\"maxRate\":0}"),
        ("unknownCommand", "{}"), ("", "{}"),
        ("setThrottle", "null"),
    };

    [Fact]
    public void Commands_are_validated_exactly_as_in_mod_0_5()
    {
        var rows = new List<string>();
        foreach (var (name, args) in Commands)
        {
            var parsed = args == "null" ? null : JObject.Parse(args);
            var result = CommandValidator.Validate(name, parsed!);
            var c = result.Command;
            rows.Add(result.IsValid
                ? string.Create(CultureInfo.InvariantCulture, $"{name} {args} => OK kind={c.Kind} amount={c.Amount} enabled={c.Enabled} rate={c.Rate} nodeUt={c.NodeUt} margin={c.Margin} maxRate={(c.MaxRate.HasValue ? c.MaxRate.Value.ToString(CultureInfo.InvariantCulture) : "-")} mode={c.Mode} sas={c.SasMode} view={c.View}")
                : $"{name} {args} => {result.ErrorCode}: {result.ErrorMessage}");
        }

        Check("commands.txt", string.Join("\n", rows) + "\n");
    }

    [Fact]
    public void Existing_command_kinds_keep_their_values()
    {
        // Les valeurs numériques de l'énumération ne bougent pas : les nouvelles commandes se placent à la suite.
        var rows = Enum.GetValues<CommandKind>().Select(k => $"{(int)k} {k}").Take(24);
        Check("command-kinds.txt", string.Join("\n", rows) + "\n");
    }
}
