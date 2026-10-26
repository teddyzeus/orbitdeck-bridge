using Newtonsoft.Json.Linq;
using OrbitDeck.Mod.Commands;
using OrbitDeck.Mod.Protocol;
using OrbitDeck.Mod.Server;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Mod.Tests;

/// <summary>Faux véhicule : enregistre les appels et renvoie le résultat programmé.</summary>
internal sealed class FakeGameControls : IGameControls
{
    public List<string> Calls { get; } = new();

    public CommandResult Result { get; set; } = CommandResult.Ok;

    public Exception? ThrowOnNextCall { get; set; }

    public int? ThreadId { get; private set; }

    private CommandResult Record(string call)
    {
        ThreadId = Environment.CurrentManagedThreadId;
        Calls.Add(call);
        if (ThrowOnNextCall != null)
        {
            var exception = ThrowOnNextCall;
            ThrowOnNextCall = null;
            throw exception;
        }

        return Result;
    }

    public CommandResult SetThrottle(float amount, bool relative) =>
        Record($"throttle {(relative ? "delta" : "set")} {amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}");

    public CommandResult SetSas(bool? enabled) => Record($"sas {(enabled.HasValue ? enabled.Value.ToString().ToLowerInvariant() : "toggle")}");

    public CommandResult SetRcs(bool? enabled) => Record($"rcs {(enabled.HasValue ? enabled.Value.ToString().ToLowerInvariant() : "toggle")}");

    public CommandResult SetSpeedMode(SpeedMode mode) => Record($"speed {mode}");
}

public class CommandValidatorTests
{
    private static ValidationResult Validate(string name, string argsJson = "{}") =>
        CommandValidator.Validate(name, JObject.Parse(argsJson));

    [Theory]
    [InlineData("{\"value\":0}", 0f)]
    [InlineData("{\"value\":0.5}", 0.5f)]
    [InlineData("{\"value\":1}", 1f)]
    public void SetThrottle_accepts_values_between_0_and_1(string args, float expected)
    {
        var result = Validate("setThrottle", args);

        Assert.True(result.IsValid);
        Assert.Equal(CommandKind.SetThrottle, result.Command.Kind);
        Assert.Equal(expected, result.Command.Amount);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"value\":-0.1}")]
    [InlineData("{\"value\":1.01}")]
    [InlineData("{\"value\":5}")]
    [InlineData("{\"value\":\"0.5\"}")]
    [InlineData("{\"value\":null}")]
    [InlineData("{\"value\":true}")]
    [InlineData("{\"value\":NaN}")]
    [InlineData("{\"value\":Infinity}")]
    [InlineData("{\"valeur\":0.5}")]
    public void SetThrottle_refuses_missing_or_out_of_range_values(string args)
    {
        var result = Validate("setThrottle", args);

        Assert.False(result.IsValid);
        Assert.Equal(ErrorCodes.BadRequest, result.ErrorCode);
    }

    [Theory]
    [InlineData("{\"delta\":0.1}", 0.1f)]
    [InlineData("{\"delta\":-0.1}", -0.1f)]
    [InlineData("{\"delta\":-1}", -1f)]
    [InlineData("{\"delta\":1}", 1f)]
    [InlineData("{\"delta\":0}", 0f)]
    public void AdjustThrottle_accepts_deltas_between_minus_1_and_1(string args, float expected)
    {
        var result = Validate("adjustThrottle", args);

        Assert.True(result.IsValid);
        Assert.Equal(CommandKind.AdjustThrottle, result.Command.Kind);
        Assert.Equal(expected, result.Command.Amount);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"delta\":1.5}")]
    [InlineData("{\"delta\":-2}")]
    [InlineData("{\"delta\":\"0.1\"}")]
    [InlineData("{\"value\":0.1}")]
    public void AdjustThrottle_refuses_bad_deltas(string args)
    {
        Assert.False(Validate("adjustThrottle", args).IsValid);
    }

    [Theory]
    [InlineData("setSas", CommandKind.SetSas)]
    [InlineData("setRcs", CommandKind.SetRcs)]
    public void Set_commands_require_a_boolean(string name, CommandKind kind)
    {
        var on = Validate(name, "{\"enabled\":true}");
        var off = Validate(name, "{\"enabled\":false}");

        Assert.True(on.IsValid && off.IsValid);
        Assert.Equal(kind, on.Command.Kind);
        Assert.True(on.Command.Enabled);
        Assert.False(off.Command.Enabled);
    }

    [Theory]
    [InlineData("setSas", "{}")]
    [InlineData("setSas", "{\"enabled\":1}")]
    [InlineData("setSas", "{\"enabled\":\"true\"}")]
    [InlineData("setRcs", "{\"enabled\":null}")]
    [InlineData("setRcs", "{\"on\":true}")]
    public void Set_commands_refuse_non_booleans(string name, string args)
    {
        var result = Validate(name, args);

        Assert.False(result.IsValid);
        Assert.Equal(ErrorCodes.BadRequest, result.ErrorCode);
    }

    [Theory]
    [InlineData("toggleSas", CommandKind.ToggleSas)]
    [InlineData("toggleRcs", CommandKind.ToggleRcs)]
    public void Toggle_commands_need_no_arguments(string name, CommandKind kind)
    {
        Assert.Equal(kind, Validate(name).Command.Kind);
        Assert.Equal(kind, CommandValidator.Validate(name, null).Command.Kind);
    }

    [Theory]
    [InlineData("surface", SpeedMode.Surface)]
    [InlineData("orbit", SpeedMode.Orbit)]
    [InlineData("target", SpeedMode.Target)]
    public void SetSpeedMode_accepts_known_modes(string mode, SpeedMode expected)
    {
        var result = Validate("setSpeedMode", $"{{\"mode\":\"{mode}\"}}");

        Assert.True(result.IsValid);
        Assert.Equal(expected, result.Command.Mode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"mode\":\"Surface\"}")]
    [InlineData("{\"mode\":\"orbital\"}")]
    [InlineData("{\"mode\":2}")]
    public void SetSpeedMode_refuses_unknown_modes(string args)
    {
        Assert.False(Validate("setSpeedMode", args).IsValid);
    }

    [Theory]
    [InlineData("deleteEverything")]
    [InlineData("")]
    [InlineData("SETTHROTTLE")]
    [InlineData(null)]
    public void Unknown_command_names_are_refused(string? name)
    {
        var result = CommandValidator.Validate(name!, new JObject());

        Assert.False(result.IsValid);
        Assert.Equal(ErrorCodes.UnknownCommand, result.ErrorCode);
    }

    [Fact]
    public void Very_long_unknown_name_is_truncated_in_the_message()
    {
        var result = Validate(new string('x', 500));

        Assert.True(result.ErrorMessage.Length < 100);
    }
}

public class CommandQueueTests
{
    private static GameCommand Command(CommandKind kind, float amount = 0, bool enabled = false) =>
        new() { Kind = kind, Amount = amount, Enabled = enabled };

    private static (PendingCommand pending, List<JObject> replies) Pending(string id, GameCommand command)
    {
        var replies = new List<JObject>();
        return (new PendingCommand(id, command, json => replies.Add(JObject.Parse(json))), replies);
    }

    [Fact]
    public void Successful_command_is_executed_and_acknowledged()
    {
        var queue = new CommandQueue();
        var controls = new FakeGameControls();
        var (pending, replies) = Pending("c-1", Command(CommandKind.SetThrottle, 0.75f));

        Assert.True(queue.TryEnqueue(pending));
        var processed = queue.ProcessPending(controls);

        Assert.Equal(1, processed);
        Assert.Equal(new[] { "throttle set 0.75" }, controls.Calls);
        Assert.Equal("ack", (string?)replies.Single()["type"]);
        Assert.Equal("c-1", (string?)replies.Single()["id"]);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Failed_command_gets_the_game_error()
    {
        var queue = new CommandQueue();
        var controls = new FakeGameControls { Result = CommandResult.NoVessel() };
        var (pending, replies) = Pending("c-2", Command(CommandKind.ToggleSas));

        queue.TryEnqueue(pending);
        queue.ProcessPending(controls);

        var reply = replies.Single();
        Assert.Equal("error", (string?)reply["type"]);
        Assert.Equal("c-2", (string?)reply["id"]);
        Assert.Equal("NO_VESSEL", (string?)reply["code"]);
    }

    [Theory]
    [InlineData(CommandKind.SetThrottle, 0.3f, false, "throttle set 0.3")]
    [InlineData(CommandKind.AdjustThrottle, -0.1f, false, "throttle delta -0.1")]
    [InlineData(CommandKind.SetSas, 0f, true, "sas true")]
    [InlineData(CommandKind.SetSas, 0f, false, "sas false")]
    [InlineData(CommandKind.ToggleSas, 0f, false, "sas toggle")]
    [InlineData(CommandKind.SetRcs, 0f, true, "rcs true")]
    [InlineData(CommandKind.ToggleRcs, 0f, false, "rcs toggle")]
    public void Each_command_kind_calls_the_matching_control(CommandKind kind, float amount, bool enabled, string expected)
    {
        var queue = new CommandQueue();
        var controls = new FakeGameControls();
        var (pending, _) = Pending("c", Command(kind, amount, enabled));

        queue.TryEnqueue(pending);
        queue.ProcessPending(controls);

        Assert.Equal(new[] { expected }, controls.Calls);
    }

    [Fact]
    public void Speed_mode_command_calls_set_speed_mode()
    {
        var queue = new CommandQueue();
        var controls = new FakeGameControls();
        var (pending, _) = Pending("c", new GameCommand { Kind = CommandKind.SetSpeedMode, Mode = SpeedMode.Orbit });

        queue.TryEnqueue(pending);
        queue.ProcessPending(controls);

        Assert.Equal(new[] { "speed Orbit" }, controls.Calls);
    }

    [Fact]
    public void Commands_run_in_arrival_order()
    {
        var queue = new CommandQueue();
        var controls = new FakeGameControls();
        foreach (var amount in new[] { 0.1f, 0.2f, 0.3f })
            queue.TryEnqueue(Pending("c", Command(CommandKind.SetThrottle, amount)).pending);

        queue.ProcessPending(controls);

        Assert.Equal(new[] { "throttle set 0.1", "throttle set 0.2", "throttle set 0.3" }, controls.Calls);
    }

    [Fact]
    public void At_most_max_commands_are_processed_per_call()
    {
        var queue = new CommandQueue();
        var controls = new FakeGameControls();
        for (var i = 0; i < 10; i++)
            queue.TryEnqueue(Pending("c", Command(CommandKind.ToggleRcs)).pending);

        Assert.Equal(4, queue.ProcessPending(controls, max: 4));
        Assert.Equal(6, queue.Count);
        Assert.Equal(6, queue.ProcessPending(controls, max: 100));
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Queue_is_bounded()
    {
        var queue = new CommandQueue();

        for (var i = 0; i < CommandQueue.Capacity; i++)
            Assert.True(queue.TryEnqueue(Pending("c", Command(CommandKind.ToggleRcs)).pending));

        Assert.False(queue.TryEnqueue(Pending("c", Command(CommandKind.ToggleRcs)).pending));
        Assert.Equal(CommandQueue.Capacity, queue.Count);

        queue.ProcessPending(new FakeGameControls(), max: 1);
        Assert.True(queue.TryEnqueue(Pending("c", Command(CommandKind.ToggleRcs)).pending));
    }

    [Fact]
    public void Exception_in_the_game_becomes_an_internal_error_and_does_not_stop_the_queue()
    {
        var errors = new List<string>();
        var queue = new CommandQueue(errors.Add);
        var controls = new FakeGameControls { ThrowOnNextCall = new InvalidOperationException("boum") };
        var (first, firstReplies) = Pending("c-1", Command(CommandKind.ToggleSas));
        var (second, secondReplies) = Pending("c-2", Command(CommandKind.ToggleRcs));

        queue.TryEnqueue(first);
        queue.TryEnqueue(second);
        queue.ProcessPending(controls);

        Assert.Equal("INTERNAL", (string?)firstReplies.Single()["code"]);
        Assert.Equal("ack", (string?)secondReplies.Single()["type"]);
        Assert.Single(errors);
    }

    [Fact]
    public void A_failing_reply_does_not_stop_the_queue()
    {
        var errors = new List<string>();
        var queue = new CommandQueue(errors.Add);
        var controls = new FakeGameControls();
        queue.TryEnqueue(new PendingCommand("c-1", Command(CommandKind.ToggleSas), _ => throw new InvalidOperationException("client parti")));
        var (second, secondReplies) = Pending("c-2", Command(CommandKind.ToggleRcs));
        queue.TryEnqueue(second);

        queue.ProcessPending(controls);

        Assert.Equal(2, controls.Calls.Count);
        Assert.Single(secondReplies);
        Assert.Single(errors);
    }

    [Fact]
    public void Processed_callback_sees_every_command_and_result()
    {
        var queue = new CommandQueue();
        var seen = new List<string>();
        queue.Processed = (pending, result) => seen.Add($"{pending.Id}:{result.Success}");
        var controls = new FakeGameControls();

        queue.TryEnqueue(Pending("a", Command(CommandKind.ToggleSas)).pending);
        controls.Result = CommandResult.Rejected("non");
        queue.TryEnqueue(Pending("b", Command(CommandKind.ToggleRcs)).pending);
        queue.ProcessPending(controls);
        queue.ProcessPending(controls);

        Assert.Equal(new[] { "a:False", "b:False" }, seen);
    }

    [Fact]
    public void Throwing_processed_callback_does_not_prevent_the_reply()
    {
        var queue = new CommandQueue(_ => { });
        queue.Processed = (_, _) => throw new InvalidOperationException("log cassé");
        var (pending, replies) = Pending("c", Command(CommandKind.ToggleSas));

        queue.TryEnqueue(pending);
        queue.ProcessPending(new FakeGameControls());

        Assert.Equal("ack", (string?)replies.Single()["type"]);
    }

    [Fact]
    public async Task Producers_on_other_threads_never_exceed_capacity_and_commands_run_on_the_consumer_thread()
    {
        var queue = new CommandQueue();
        var controls = new FakeGameControls();
        var accepted = 0;

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 50; i++)
            {
                if (queue.TryEnqueue(Pending("c", Command(CommandKind.ToggleRcs)).pending))
                    Interlocked.Increment(ref accepted);
            }
        })));

        Assert.Equal(CommandQueue.Capacity, accepted);
        Assert.Equal(CommandQueue.Capacity, queue.Count);

        var consumerThread = Environment.CurrentManagedThreadId;
        var processed = 0;
        while (queue.Count > 0)
            processed += queue.ProcessPending(controls);

        Assert.Equal(CommandQueue.Capacity, processed);
        Assert.Equal(consumerThread, controls.ThreadId);
    }
}

public sealed class CommandEndToEndTests : IDisposable
{
    private readonly FakeTelemetrySource _source = new();
    private readonly CommandQueue _commands = new();
    private readonly FakeGameControls _controls = new();
    private readonly WsServer _ws = new(0);
    private readonly OrbitDeckServer _server;

    public CommandEndToEndTests()
    {
        _server = new OrbitDeckServer(_ws, _source, _commands, "0.1.0", "test");
        Assert.True(_server.Start());
    }

    public void Dispose() => _server.Stop();

    private async Task<TestClient> ConnectAsync()
    {
        var client = await TestClient.ConnectAsync(_ws.Port);
        client.Expect("hello");
        client.Expect("state");
        return client;
    }

    private void RunGameFrame() => SpinWait.SpinUntil(() => _commands.ProcessPending(_controls) > 0, 3000);

    [Fact]
    public async Task Valid_command_is_queued_executed_on_the_game_side_and_acknowledged()
    {
        using var client = await ConnectAsync();

        await client.SendAsync("{\"v\":1,\"type\":\"command\",\"id\":\"c-17\",\"name\":\"setThrottle\",\"args\":{\"value\":0.5}}");
        RunGameFrame();

        var ack = client.Expect("ack");
        Assert.Equal("c-17", (string?)ack["id"]);
        Assert.Equal(new[] { "throttle set 0.5" }, _controls.Calls);
    }

    [Fact]
    public async Task Nothing_is_executed_until_the_game_thread_processes_the_queue()
    {
        using var client = await ConnectAsync();

        await client.SendAsync("{\"v\":1,\"type\":\"command\",\"id\":\"c-1\",\"name\":\"toggleSas\"}");

        Assert.True(SpinWait.SpinUntil(() => _commands.Count == 1, 3000));
        Assert.Empty(_controls.Calls);
        Assert.Null(client.Next(300));
    }

    [Fact]
    public async Task Game_refusal_reaches_the_client_with_the_command_id()
    {
        _controls.Result = CommandResult.NotControllable();
        using var client = await ConnectAsync();

        await client.SendAsync("{\"v\":1,\"type\":\"command\",\"id\":\"c-2\",\"name\":\"toggleRcs\"}");
        RunGameFrame();

        var error = client.Expect("error");
        Assert.Equal("c-2", (string?)error["id"]);
        Assert.Equal("NOT_CONTROLLABLE", (string?)error["code"]);
    }

    [Fact]
    public async Task Invalid_arguments_are_refused_without_reaching_the_game()
    {
        using var client = await ConnectAsync();

        await client.SendAsync("{\"v\":1,\"type\":\"command\",\"id\":\"c-3\",\"name\":\"setThrottle\",\"args\":{\"value\":5}}");

        var error = client.Expect("error");
        Assert.Equal("c-3", (string?)error["id"]);
        Assert.Equal("BAD_REQUEST", (string?)error["code"]);
        Assert.Equal(0, _commands.Count);
        Assert.Empty(_controls.Calls);
    }

    [Fact]
    public async Task Unknown_command_is_refused_with_its_id()
    {
        using var client = await ConnectAsync();

        await client.SendAsync("{\"v\":1,\"type\":\"command\",\"id\":\"c-4\",\"name\":\"selfDestruct\"}");

        var error = client.Expect("error");
        Assert.Equal("c-4", (string?)error["id"]);
        Assert.Equal("UNKNOWN_COMMAND", (string?)error["code"]);
    }

    [Fact]
    public async Task Each_client_gets_the_answer_to_its_own_command_only()
    {
        using var a = await ConnectAsync();
        using var b = await ConnectAsync();

        await a.SendAsync("{\"v\":1,\"type\":\"command\",\"id\":\"from-a\",\"name\":\"toggleSas\"}");
        RunGameFrame();

        Assert.Equal("from-a", (string?)a.Expect("ack")["id"]);
        Assert.Null(b.Next(300));
    }

    [Fact]
    public async Task Flooding_the_queue_returns_errors_instead_of_blocking()
    {
        using var client = await ConnectAsync();
        var total = CommandQueue.Capacity + 20;

        for (var i = 0; i < total; i++)
            await client.SendAsync($"{{\"v\":1,\"type\":\"command\",\"id\":\"f-{i}\",\"name\":\"toggleRcs\"}}");

        var busy = 0;
        for (var i = 0; i < total - CommandQueue.Capacity; i++)
        {
            var error = client.Expect("error");
            Assert.Equal("INTERNAL", (string?)error["code"]);
            busy++;
        }

        Assert.Equal(20, busy);
        Assert.Equal(CommandQueue.Capacity, _commands.Count);
    }
}
