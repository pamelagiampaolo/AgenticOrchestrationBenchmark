using AgenticExperimentRunner.SessionExecution;
using Xunit;

public sealed class ExperimentProtocolTests
{
    [Fact]
    public void FindModelValidationError_RequiredModel_ReturnsNull()
    {
        Assert.Null(ExperimentProtocol.FindModelValidationError(ExperimentProtocol.RequiredModel));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("claude-opus-5-5")]
    [InlineData("Claude-Sonnet-5-5")]
    [InlineData("sonnet")]
    public void FindModelValidationError_OtherValue_ReturnsDiagnostic(string? model)
    {
        string? error = ExperimentProtocol.FindModelValidationError(model);

        Assert.NotNull(error);
        Assert.Contains(ExperimentProtocol.RequiredModel, error);
    }
}
