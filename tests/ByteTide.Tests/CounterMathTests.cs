using ByteTide.Core;
using Xunit;

namespace ByteTide.Tests;

public class CounterMathTests
{
    [Fact]
    public void Delta_NormalGrowth_IsDifference()
    {
        Assert.Equal(150, CounterMath.Delta(100, 250));
    }

    [Fact]
    public void Delta_CounterReset_KeepsBytesSinceReset()
    {
        // Wi-Fi reconnect / PPP redial: counter drops back to a small number.
        Assert.Equal(300, CounterMath.Delta(5_000_000, 300));
    }

    [Fact]
    public void Delta_NoChange_IsZero()
    {
        Assert.Equal(0, CounterMath.Delta(42, 42));
    }
}
