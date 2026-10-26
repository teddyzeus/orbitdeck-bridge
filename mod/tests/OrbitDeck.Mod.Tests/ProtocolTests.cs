using Newtonsoft.Json.Linq;
using OrbitDeck.Mod.Protocol;
using OrbitDeck.Mod.Telemetry;

namespace OrbitDeck.Mod.Tests;

public class ProtocolMessagesTests
{
    [Fact]
    public void Hello_has_version_and_type()
    {
        var message = JObject.Parse(ProtocolMessages.Hello("0.1.0", "0.2.8.5"));

        Assert.Equal(1, (int)message["v"]!);
        Assert.Equal("hello", (string?)message["type"]);
        Assert.Equal("0.1.0", (string?)message["mod"]);
        Assert.Equal("0.2.8.5", (string?)message["game"]);
    }

    [Fact]
    public void State_in_flight_contains_all_fields()
    {
        var snapshot = new TelemetrySnapshot
        {
            Status = VesselStatus.Connected, GameStateName = "FlightView", VesselName = "Kerbal X",
            AltitudeSeaLevel = 12400.456, AltitudeSurface = 12380.123, SpeedSurface = 245.3, SpeedOrbital = 2300.14,
            SpeedTarget = 5.5, SpeedMode = SpeedMode.Orbit, Throttle = 0.5f, SasEnabled = true,
            SasMode = "Prograde", RcsEnabled = true
        };

        var message = JObject.Parse(ProtocolMessages.State(snapshot, 1042));

        Assert.Equal("state", (string?)message["type"]);
        Assert.Equal(1042, (long)message["seq"]!);
        Assert.Equal("connected", (string?)message["status"]);
        Assert.Equal("FlightView", (string?)message["gameState"]);
        Assert.Equal("Kerbal X", (string?)message["vessel"]!["name"]);
        Assert.Equal(12400.46, (double)message["altitude"]!["seaLevel"]!);
        Assert.Equal(12380.12, (double)message["altitude"]!["surface"]!);
        Assert.Equal("orbit", (string?)message["speed"]!["mode"]);
        Assert.Equal(245.3, (double)message["speed"]!["surface"]!);
        Assert.Equal(5.5, (double)message["speed"]!["target"]!);
        Assert.Equal(0.5, (double)message["throttle"]!);
        Assert.True((bool)message["sas"]!["enabled"]!);
        Assert.Equal("Prograde", (string?)message["sas"]!["mode"]);
        Assert.True((bool)message["rcs"]!["enabled"]!);
    }

    [Fact]
    public void State_without_target_has_null_target_speed()
    {
        var message = JObject.Parse(ProtocolMessages.State(Snapshots.InFlight(), 1));

        Assert.Equal(JTokenType.Null, message["speed"]!["target"]!.Type);
    }

    [Fact]
    public void State_without_vessel_omits_vessel_data()
    {
        var message = JObject.Parse(ProtocolMessages.State(TelemetrySnapshot.NoVesselIn("MainMenu"), 7));

        Assert.Equal("noVessel", (string?)message["status"]);
        Assert.Equal("MainMenu", (string?)message["gameState"]);
        Assert.Null(message["altitude"]);
        Assert.Null(message["speed"]);
        Assert.Null(message["throttle"]);
        Assert.Null(message["vessel"]);
    }

    [Fact]
    public void State_not_controllable_still_carries_data()
    {
        var snapshot = new TelemetrySnapshot { Status = VesselStatus.NotControllable, VesselName = "Bob Kerman" };

        var message = JObject.Parse(ProtocolMessages.State(snapshot, 3));

        Assert.Equal("notControllable", (string?)message["status"]);
        Assert.NotNull(message["altitude"]);
    }

    [Fact]
    public void Non_finite_numbers_become_null()
    {
        var snapshot = new TelemetrySnapshot
        {
            Status = VesselStatus.Connected, AltitudeSeaLevel = double.NaN, SpeedSurface = double.PositiveInfinity
        };

        var json = ProtocolMessages.State(snapshot, 1);
        var message = JObject.Parse(json);

        Assert.DoesNotContain("NaN", json);
        Assert.DoesNotContain("Infinity", json);
        Assert.Equal(JTokenType.Null, message["altitude"]!["seaLevel"]!.Type);
        Assert.Equal(JTokenType.Null, message["speed"]!["surface"]!.Type);
    }

    [Fact]
    public void Throttle_is_clamped_between_zero_and_one()
    {
        var high = new TelemetrySnapshot { Status = VesselStatus.Connected, Throttle = 1.7f };
        var low = new TelemetrySnapshot { Status = VesselStatus.Connected, Throttle = -0.2f };

        Assert.Equal(1.0, (double)JObject.Parse(ProtocolMessages.State(high, 1))["throttle"]!);
        Assert.Equal(0.0, (double)JObject.Parse(ProtocolMessages.State(low, 1))["throttle"]!);
    }

    [Fact]
    public void Error_includes_id_only_when_known()
    {
        var withId = JObject.Parse(ProtocolMessages.Error("c-1", ErrorCodes.NoVessel, "Aucun véhicule"));
        var withoutId = JObject.Parse(ProtocolMessages.Error(null, ErrorCodes.BadRequest, "JSON invalide"));

        Assert.Equal("c-1", (string?)withId["id"]);
        Assert.Equal("NO_VESSEL", (string?)withId["code"]);
        Assert.Null(withoutId["id"]);
        Assert.Equal("BAD_REQUEST", (string?)withoutId["code"]);
    }

    [Fact]
    public void Ack_and_pong_are_well_formed()
    {
        Assert.Equal("c-9", (string?)JObject.Parse(ProtocolMessages.Ack("c-9"))["id"]);
        Assert.Equal("pong", (string?)JObject.Parse(ProtocolMessages.Pong())["type"]);
    }

    [Fact]
    public void Messages_are_single_line_compact_json()
    {
        Assert.DoesNotContain("\n", ProtocolMessages.State(Snapshots.InFlight(), 1));
    }
}

public class ProtocolParserTests
{
    [Fact]
    public void Ping_is_valid()
    {
        var message = ProtocolParser.Parse("{\"v\":1,\"type\":\"ping\"}");

        Assert.True(message.IsValid);
        Assert.Equal(ClientMessageKind.Ping, message.Kind);
    }

    [Fact]
    public void Command_is_parsed_with_args()
    {
        var message = ProtocolParser.Parse(
            "{\"v\":1,\"type\":\"command\",\"id\":\"c-1\",\"name\":\"setThrottle\",\"args\":{\"value\":0.5}}");

        Assert.True(message.IsValid);
        Assert.Equal(ClientMessageKind.Command, message.Kind);
        Assert.Equal("c-1", message.Id);
        Assert.Equal("setThrottle", message.CommandName);
        Assert.Equal(0.5, (double)message.Args["value"]!);
    }

    [Fact]
    public void Command_without_args_gets_empty_args()
    {
        var message = ProtocolParser.Parse("{\"v\":1,\"type\":\"command\",\"id\":\"c-2\",\"name\":\"toggleSas\"}");

        Assert.True(message.IsValid);
        Assert.Empty(message.Args);
    }

    [Theory]
    [InlineData("pas du json")]
    [InlineData("")]
    [InlineData("[1,2,3]")]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData("{\"v\":1,\"type\":\"ping\"} {\"autre\":1}")]
    public void Invalid_json_is_bad_request(string json)
    {
        var message = ProtocolParser.Parse(json);

        Assert.False(message.IsValid);
        Assert.Equal(ErrorCodes.BadRequest, message.ErrorCode);
    }

    [Fact]
    public void Wrong_protocol_version_is_rejected()
    {
        var message = ProtocolParser.Parse("{\"v\":2,\"type\":\"ping\"}");

        Assert.False(message.IsValid);
        Assert.Equal(ErrorCodes.BadRequest, message.ErrorCode);
    }

    [Fact]
    public void Missing_version_is_rejected()
    {
        Assert.False(ProtocolParser.Parse("{\"type\":\"ping\"}").IsValid);
    }

    [Theory]
    [InlineData("{\"v\":1}")]
    [InlineData("{\"v\":1,\"type\":\"inconnu\"}")]
    [InlineData("{\"v\":1,\"type\":42}")]
    public void Missing_or_unknown_type_is_rejected(string json)
    {
        Assert.False(ProtocolParser.Parse(json).IsValid);
    }

    [Theory]
    [InlineData("{\"v\":1,\"type\":\"command\",\"name\":\"toggleSas\"}")]
    [InlineData("{\"v\":1,\"type\":\"command\",\"id\":\"\",\"name\":\"toggleSas\"}")]
    [InlineData("{\"v\":1,\"type\":\"command\",\"id\":5,\"name\":\"toggleSas\"}")]
    public void Command_without_valid_id_is_rejected(string json)
    {
        var message = ProtocolParser.Parse(json);

        Assert.False(message.IsValid);
        Assert.Null(message.Id);
    }

    [Fact]
    public void Command_without_name_is_rejected_but_keeps_id()
    {
        var message = ProtocolParser.Parse("{\"v\":1,\"type\":\"command\",\"id\":\"c-3\"}");

        Assert.False(message.IsValid);
        Assert.Equal("c-3", message.Id);
    }

    [Fact]
    public void Command_with_non_object_args_is_rejected_but_keeps_id()
    {
        var message = ProtocolParser.Parse("{\"v\":1,\"type\":\"command\",\"id\":\"c-4\",\"name\":\"setSas\",\"args\":[1]}");

        Assert.False(message.IsValid);
        Assert.Equal("c-4", message.Id);
    }

    [Fact]
    public void Command_id_longer_than_limit_is_rejected()
    {
        var id = new string('x', ParsedMessage.MaxIdLength + 1);

        var message = ProtocolParser.Parse($"{{\"v\":1,\"type\":\"command\",\"id\":\"{id}\",\"name\":\"toggleSas\"}}");

        Assert.False(message.IsValid);
    }

    [Fact]
    public void Deeply_nested_json_is_rejected_without_crashing()
    {
        var json = "{\"v\":1,\"type\":\"command\",\"id\":\"c\",\"name\":\"x\",\"args\":" + new string('[', 2000) + new string(']', 2000) + "}";

        var message = ProtocolParser.Parse(json);

        Assert.False(message.IsValid);
    }
}

public class ProtocolExamplesTests
{
    private static string ProtocolDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "protocol", "examples")))
            dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("Dossier protocol/ introuvable."), "protocol");
    }

    public static IEnumerable<object[]> ServerExamples() =>
        Directory.GetFiles(Path.Combine(ProtocolDir(), "examples"), "server-*.json").Select(f => new object[] { Path.GetFileName(f) });

    public static IEnumerable<object[]> ClientExamples() =>
        Directory.GetFiles(Path.Combine(ProtocolDir(), "examples"), "client-*.json").Select(f => new object[] { Path.GetFileName(f) });

    [Theory]
    [MemberData(nameof(ServerExamples))]
    public void Server_examples_match_the_schema(string file)
    {
        var example = JObject.Parse(File.ReadAllText(Path.Combine(ProtocolDir(), "examples", file)));
        var schema = JObject.Parse(File.ReadAllText(Path.Combine(ProtocolDir(), "schema", "server-message.schema.json")));

        AssertMatchesOneBranch(example, schema);
    }

    [Theory]
    [MemberData(nameof(ClientExamples))]
    public void Client_examples_match_the_schema_and_parse(string file)
    {
        var text = File.ReadAllText(Path.Combine(ProtocolDir(), "examples", file));
        var schema = JObject.Parse(File.ReadAllText(Path.Combine(ProtocolDir(), "schema", "client-message.schema.json")));

        AssertMatchesOneBranch(JObject.Parse(text), schema);
        Assert.True(ProtocolParser.Parse(text).IsValid, $"{file} devrait être accepté par le serveur.");
    }

    [Fact]
    public void Messages_built_by_the_server_match_the_schema()
    {
        var schema = JObject.Parse(File.ReadAllText(Path.Combine(ProtocolDir(), "schema", "server-message.schema.json")));

        foreach (var json in new[]
                 {
                     ProtocolMessages.Hello("0.1.0", "0.2.8.5"),
                     ProtocolMessages.State(Snapshots.InFlight(), 5),
                     ProtocolMessages.State(TelemetrySnapshot.NoVessel, 6),
                     ProtocolMessages.Ack("c-1"),
                     ProtocolMessages.Error(null, ErrorCodes.Internal, "x"),
                     ProtocolMessages.Pong()
                 })
        {
            AssertMatchesOneBranch(JObject.Parse(json), schema);
        }
    }

    // Vérification légère (sans bibliothèque de validation) : le message correspond à une branche `oneOf`
    // dont `type` est la constante attendue et dont tous les champs `required` sont présents.
    private static void AssertMatchesOneBranch(JObject message, JObject schema)
    {
        Assert.Equal(1, (int)message["v"]!);

        var type = (string?)message["type"];
        var branch = schema["oneOf"]!
            .OfType<JObject>()
            .SingleOrDefault(b => (string?)b["properties"]!["type"]!["const"] == type);

        Assert.True(branch != null, $"Aucune branche du schéma pour le type « {type} ».");

        foreach (var required in branch!["required"]!.Select(t => (string)t!))
            Assert.True(message.ContainsKey(required), $"Champ requis « {required} » absent du message « {type} ».");

        foreach (var property in ((JObject)branch["properties"]!).Properties().Where(p => message.ContainsKey(p.Name)))
        {
            if (property.Value["enum"] is JArray allowed)
                Assert.Contains(message[property.Name]!, allowed, JToken.EqualityComparer);
        }
    }
}
