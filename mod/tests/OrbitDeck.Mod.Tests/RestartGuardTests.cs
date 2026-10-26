using OrbitDeck.Mod;

namespace OrbitDeck.Mod.Tests;

public sealed class RestartGuardTests
{
    [Fact]
    public void Allows_restarts_up_to_the_limit_then_refuses()
    {
        var guard = new RestartGuard(maxRestarts: 3, windowMs: 10_000);

        Assert.True(guard.TryRestart(0));
        Assert.True(guard.TryRestart(1_000));
        Assert.True(guard.TryRestart(2_000));
        Assert.False(guard.TryRestart(3_000));
    }

    [Fact]
    public void Old_restarts_leave_the_window()
    {
        var guard = new RestartGuard(maxRestarts: 2, windowMs: 10_000);

        Assert.True(guard.TryRestart(0));
        Assert.True(guard.TryRestart(1_000));
        Assert.False(guard.TryRestart(5_000));

        // Au-delà de la fenêtre, le premier redémarrage ne compte plus.
        Assert.True(guard.TryRestart(10_000));
        Assert.False(guard.TryRestart(10_500));
    }

    [Fact]
    public void A_refused_restart_is_not_counted()
    {
        var guard = new RestartGuard(maxRestarts: 1, windowMs: 10_000);

        Assert.True(guard.TryRestart(0));
        Assert.False(guard.TryRestart(1_000));
        Assert.False(guard.TryRestart(9_999));
        Assert.True(guard.TryRestart(10_000));
    }
}
