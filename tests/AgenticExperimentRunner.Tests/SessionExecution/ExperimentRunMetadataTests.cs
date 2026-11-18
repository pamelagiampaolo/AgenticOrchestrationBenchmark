using AgenticExperimentRunner.SessionExecution;
using Xunit;

public sealed class ExperimentRunMetadataTests
{
    [Fact]
    public void FindValidationError_ValidMetadata_ReturnsNull()
    {
        var metadata = new ExperimentRunMetadata(ExperimentRunKind.Official, 3, "seed", 54);

        Assert.Null(metadata.FindValidationError());
    }

    [Theory]
    [InlineData(0, "seed", 1)]
    [InlineData(1, "", 1)]
    [InlineData(1, " ", 1)]
    [InlineData(1, "seed", 0)]
    public void FindValidationError_InvalidValue_ReturnsDiagnostic(int repetition, string seed, int position)
    {
        var metadata = new ExperimentRunMetadata(ExperimentRunKind.Pilot, repetition, seed, position);

        Assert.NotNull(metadata.FindValidationError());
    }

    [Fact]
    public void FindValidationError_UndefinedKind_ReturnsDiagnostic()
    {
        var metadata = new ExperimentRunMetadata((ExperimentRunKind)99, 1, "seed", 1);

        Assert.NotNull(metadata.FindValidationError());
    }
}
