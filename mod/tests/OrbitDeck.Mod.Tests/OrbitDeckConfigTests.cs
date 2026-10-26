using OrbitDeck.Mod.Config;

namespace OrbitDeck.Mod.Tests;

public sealed class OrbitDeckConfigTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "orbitdeck-tests-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _warnings = new();

    public OrbitDeckConfigTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, true);

    private string ConfigPath => Path.Combine(_dir, OrbitDeckConfig.FileName);

    [Fact]
    public void Missing_file_gives_defaults_and_creates_the_file()
    {
        var config = OrbitDeckConfig.Load(ConfigPath, _warnings.Add);

        Assert.Equal(8765, config.Port);
        Assert.Equal(10, config.SampleRateHz);
        Assert.False(config.VerboseLogging);
        Assert.True(File.Exists(ConfigPath));
        Assert.Empty(_warnings);
    }

    [Fact]
    public void Existing_file_is_read()
    {
        File.WriteAllText(ConfigPath, "{ \"port\": 9000, \"sampleRateHz\": 20, \"verboseLogging\": true }");

        var config = OrbitDeckConfig.Load(ConfigPath, _warnings.Add);

        Assert.Equal(9000, config.Port);
        Assert.Equal(20, config.SampleRateHz);
        Assert.True(config.VerboseLogging);
        Assert.Empty(_warnings);
    }

    [Fact]
    public void Partial_file_keeps_defaults_for_missing_values()
    {
        File.WriteAllText(ConfigPath, "{ \"port\": 9001 }");

        var config = OrbitDeckConfig.Load(ConfigPath, _warnings.Add);

        Assert.Equal(9001, config.Port);
        Assert.Equal(10, config.SampleRateHz);
    }

    [Fact]
    public void Invalid_json_gives_defaults_and_a_warning()
    {
        File.WriteAllText(ConfigPath, "{ ceci n'est pas du json");

        var config = OrbitDeckConfig.Load(ConfigPath, _warnings.Add);

        Assert.Equal(8765, config.Port);
        Assert.Single(_warnings);
    }

    [Theory]
    [InlineData(80, 1024)]
    [InlineData(70000, 65535)]
    public void Port_is_clamped(int value, int expected)
    {
        File.WriteAllText(ConfigPath, $"{{ \"port\": {value} }}");

        var config = OrbitDeckConfig.Load(ConfigPath, _warnings.Add);

        Assert.Equal(expected, config.Port);
        Assert.Single(_warnings);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(500, 60)]
    public void Sample_rate_is_clamped(int value, int expected)
    {
        File.WriteAllText(ConfigPath, $"{{ \"sampleRateHz\": {value} }}");

        var config = OrbitDeckConfig.Load(ConfigPath, _warnings.Add);

        Assert.Equal(expected, config.SampleRateHz);
        Assert.Single(_warnings);
    }
}
