using Newtonsoft.Json.Linq;
using OrbitDeck.Mod.Commands;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Mod.Tests;

/// <summary>Logique commune 2.0 (ressources, systèmes, amarrage, impact) et nouvelles commandes.</summary>
public class SystemsTests
{
    [Theory]
    [InlineData("LiquidFuel", ResourceKind.Fuel)]
    [InlineData("Methalox", ResourceKind.Fuel)]
    [InlineData("Methane", ResourceKind.Fuel)]
    [InlineData("Oxidizer", ResourceKind.Oxidizer)]
    [InlineData("ElectricCharge", ResourceKind.Electric)]
    [InlineData("MonoPropellant", ResourceKind.Monoprop)]
    [InlineData("SolidFuel", ResourceKind.None)]
    [InlineData("Xenon", ResourceKind.None)]
    [InlineData(null, ResourceKind.None)]
    public void Resource_names_are_classified(string? name, ResourceKind expected) => Assert.Equal(expected, ResourceKinds.Of(name));

    [Fact]
    public void Tally_sums_by_kind_and_leaves_missing_resources_null()
    {
        var tally = new ResourceTally();
        tally.Add("LiquidFuel", 100, 200);
        tally.Add("LiquidFuel", 50, 100);
        tally.Add("ElectricCharge", 0, 400);
        tally.Add("SolidFuel", 10, 10);
        var set = tally.ToSet();

        Assert.Equal(150, set.Fuel!.Amount);
        Assert.Equal(300, set.Fuel.Max);
        Assert.Equal(0, set.Electric!.Amount);
        Assert.Null(set.Oxidizer);
        Assert.Null(set.Monoprop);
    }

    [Fact]
    public void Tally_clamps_amount_to_capacity()
    {
        var tally = new ResourceTally();
        tally.Add("Oxidizer", 500, 100);
        Assert.Equal(100, tally.ToSet().Oxidizer!.Amount);
    }

    [Fact]
    public void Deployables_merge_into_one_state()
    {
        var all = new DeployTally();
        all.Add(DeployPhase.Extended, true);
        all.Add(DeployPhase.Extended, true);
        Assert.Equal(SystemState.Extended, all.ToCategory().State);

        var mixed = new DeployTally();
        mixed.Add(DeployPhase.Extended, true);
        mixed.Add(DeployPhase.Retracted, true);
        Assert.Equal(SystemState.Mixed, mixed.ToCategory().State);

        var moving = new DeployTally();
        moving.Add(DeployPhase.Extended, true);
        moving.Add(DeployPhase.Moving, true);
        Assert.Equal(SystemState.Moving, moving.ToCategory().State);

        var fixedPanels = new DeployTally();
        fixedPanels.Add(DeployPhase.Extended, false);
        Assert.False(fixedPanels.ToCategory().Retractable);

        var none = new DeployTally().ToCategory();
        Assert.Equal(0, none.Count);
        Assert.Null(none.State);
    }

    [Fact]
    public void Broken_modules_are_ignored()
    {
        var tally = new DeployTally();
        tally.Add(DeployPhase.Broken, true);
        Assert.Equal(0, tally.ToCategory().Count);
    }

    [Fact]
    public void Parachutes_merge_into_one_state()
    {
        var ready = new ChuteTally();
        ready.Add(ChutePhase.Ready);
        ready.Add(ChutePhase.Ready);
        Assert.Equal(SystemState.Ready, ready.ToCategory().State);

        var mixed = new ChuteTally();
        mixed.Add(ChutePhase.Ready);
        mixed.Add(ChutePhase.Deployed);
        Assert.Equal(SystemState.Mixed, mixed.ToCategory().State);

        var cut = new ChuteTally();
        cut.Add(ChutePhase.Cut);
        Assert.Equal(SystemState.Cut, cut.ToCategory().State);
    }

    [Fact]
    public void Docking_takes_the_most_advanced_state()
    {
        var tally = new DockTally();
        tally.Add(DockingPortState.Armed);
        tally.Add(DockingPortState.Docked);
        tally.Add(DockingPortState.Disarmed);
        var info = tally.ToInfo(false);

        Assert.Equal(3, info.Ports);
        Assert.Equal(DockingPortState.Docked, info.State);
        Assert.True(info.CanUndock);
        Assert.False(info.Armable);
        Assert.Equal(0, new DockTally().ToInfo(false).Ports);
    }

    [Fact]
    public void Approach_rate_is_negative_when_closing_in()
    {
        var rate = new ApproachRate();
        Assert.Null(rate.Update(0, 1000));
        Assert.Equal(-10, rate.Update(1, 990)!.Value, 6);
        Assert.True(rate.Update(2, 980)!.Value < 0);
        rate.Reset();
        Assert.Null(rate.Update(3, 970));
    }

    [Fact]
    public void Impact_is_computed_only_when_descending_with_known_ground()
    {
        // 100 m de haut, -10 m/s, g = 10 : environ 3,58 s.
        var ut = ImpactEstimator.ImpactUt(1000, 100, -10, 10);
        Assert.NotNull(ut);
        Assert.Equal(1003.583, ut!.Value, 2);

        Assert.Null(ImpactEstimator.ImpactUt(1000, 100, 5, 10));
        Assert.Null(ImpactEstimator.ImpactUt(1000, null, -10, 10));
        Assert.Null(ImpactEstimator.ImpactUt(1000, 100, -10, null));
        Assert.Equal(1000, ImpactEstimator.ImpactUt(1000, 0, -10, 10)!.Value, 6);
    }

    [Theory]
    [InlineData("setSolarPanels", CommandKind.SetSolarPanels)]
    [InlineData("setAntennas", CommandKind.SetAntennas)]
    [InlineData("setLadders", CommandKind.SetLadders)]
    [InlineData("setDockingArmed", CommandKind.SetDockingArmed)]
    public void Boolean_commands_are_validated(string name, CommandKind kind)
    {
        var ok = CommandValidator.Validate(name, JObject.Parse("{\"enabled\":true}"));
        Assert.True(ok.IsValid);
        Assert.Equal(kind, ok.Command!.Kind);
        Assert.True(ok.Command.Enabled);

        Assert.False(CommandValidator.Validate(name, JObject.Parse("{}")).IsValid);
        Assert.False(CommandValidator.Validate(name, JObject.Parse("{\"enabled\":\"yes\"}")).IsValid);
    }

    [Theory]
    [InlineData("deployParachutes", CommandKind.DeployParachutes)]
    [InlineData("undock", CommandKind.Undock)]
    [InlineData("quickSave", CommandKind.QuickSave)]
    [InlineData("quickLoad", CommandKind.QuickLoad)]
    public void Argument_free_commands_are_validated(string name, CommandKind kind)
    {
        var result = CommandValidator.Validate(name, new JObject());
        Assert.True(result.IsValid);
        Assert.Equal(kind, result.Command!.Kind);
    }

    [Fact]
    public void New_commands_reach_the_game_controls()
    {
        var controls = new FakeGameControls();
        foreach (var (name, args) in new[] { ("setSolarPanels", "{\"enabled\":false}"), ("deployParachutes", "{}"), ("quickSave", "{}"), ("undock", "{}") })
        {
            var command = CommandValidator.Validate(name, JObject.Parse(args)).Command!;
            Assert.True(CommandQueue.Execute(command, controls).Success);
        }

        Assert.Equal(new[] { "solar False", "parachutes", "quicksave", "undock" }, controls.Calls);
    }
}
