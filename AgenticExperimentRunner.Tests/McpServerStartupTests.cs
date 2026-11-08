using McpReadOnlyPrototype;
using Xunit;

namespace AgenticExperimentRunner.Tests;

public sealed class McpServerStartupTests
{
    [Fact]
    public void ConfigurationPolicy_HandlesUnauthorizedAccess()
    {
        Assert.True(StartupExceptionPolicy.IsConfigurationError(
            new UnauthorizedAccessException("accesso negato")));
    }

    [Fact]
    public void ConfigurationPolicy_DoesNotHideUnexpectedFailures()
    {
        Assert.False(StartupExceptionPolicy.IsConfigurationError(
            new InvalidOperationException("errore inatteso")));
    }
}
