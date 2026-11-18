using System.Text.Json;
using AgenticExperimentRunner.ExperimentPlanning;
using AgenticExperimentRunner.SessionExecution;
using Xunit;

public sealed class ExperimentPlanTests
{
    private const string Seed = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void Generate_ValidSeed_ProducesBalancedCompletePlan()
    {
        ExperimentPlan plan = ExperimentPlanGenerator.Generate(Seed);

        Assert.Equal(54, plan.Slots.Count);
        Assert.Equal(Enumerable.Range(1, 54), plan.Slots.Select(slot => slot.OrderPosition));
        Assert.Equal(54, plan.Slots.Select(slot => slot.SlotId).Distinct().Count());
        Assert.Equal(18, plan.Slots.Select(slot => slot.BlockPosition).Distinct().Count());
        Assert.All(plan.Slots, slot => Assert.Equal(ExperimentRunKind.Official, slot.RunKind));
        Assert.All(
            plan.Slots.GroupBy(slot => slot.BlockPosition),
            block =>
            {
                Assert.Equal(3, block.Count());
                Assert.Single(block.Select(slot => (slot.TaskId, slot.Repetition)).Distinct());
                Assert.Equal(
                    ["hierarchical-adaptive", "hierarchical-static", "single-agent"],
                    block.Select(slot => slot.Architecture).Order(StringComparer.Ordinal));
            });
        Assert.Empty(ExperimentPlanValidator.FindErrors(plan));
    }

    [Fact]
    public void Generate_SameSeed_ProducesByteIdenticalJson()
    {
        string first = JsonSerializer.Serialize(ExperimentPlanGenerator.Generate(Seed), SessionRecord.SerializerOptions);
        string second = JsonSerializer.Serialize(ExperimentPlanGenerator.Generate(Seed), SessionRecord.SerializerOptions);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Generate_Sha256GitObjectId_IsAccepted()
    {
        const string sha256Seed = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        Assert.Equal(54, ExperimentPlanGenerator.Generate(sha256Seed).Slots.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void Generate_InvalidSeed_IsRejected(string seed)
    {
        Assert.Throws<ArgumentException>(() => ExperimentPlanGenerator.Generate(seed));
    }

    [Fact]
    public void Validator_TamperedPlan_ReportsViolations()
    {
        ExperimentPlan valid = ExperimentPlanGenerator.Generate(Seed);
        ExperimentPlanSlot first = valid.Slots[0];
        var tampered = valid with
        {
            Seed = "invalid",
            Slots = [first, .. valid.Slots.Skip(1).Select(slot => slot with { OrderPosition = slot.OrderPosition + 1 })],
        };

        IReadOnlyList<string> errors = ExperimentPlanValidator.FindErrors(tampered);

        Assert.Contains(errors, error => error.Contains("posizioni globali", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("seed", StringComparison.Ordinal));
    }

    [Fact]
    public void Validator_MissingSlots_ReportsViolation()
    {
        var malformed = new ExperimentPlan(
            ExperimentPlan.CurrentSchemaVersion,
            Seed,
            ExperimentPlan.CurrentAlgorithm,
            null!);

        Assert.Contains(
            ExperimentPlanValidator.FindErrors(malformed),
            error => error.Contains("slot", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Command_ValidSeed_WritesValidatedJson()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = await GenerateExperimentPlanCommand.ExecuteAsync(["--seed", Seed], output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        ExperimentPlan plan = JsonSerializer.Deserialize<ExperimentPlan>(output.ToString(), SessionRecord.SerializerOptions)!;
        Assert.Empty(ExperimentPlanValidator.FindErrors(plan));
    }

    [Theory]
    [InlineData()]
    [InlineData("--seed", "not-a-sha256")]
    [InlineData("--other", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Command_InvalidArguments_ReturnsUsageError(params string[] arguments)
    {
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = await GenerateExperimentPlanCommand.ExecuteAsync(arguments, output, error);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Errore negli argomenti:", error.ToString());
    }
}
