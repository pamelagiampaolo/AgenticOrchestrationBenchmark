using AgenticExperimentRunner.RunPreparation;
using Xunit;

public sealed class TaskMaterialSelectionTests
{
    [Theory]
    [InlineData("src/index.ts", MaterialDestination.Workspace)]
    [InlineData("package.json", MaterialDestination.Workspace)]
    [InlineData("vitest.public.t2.config.ts", MaterialDestination.Workspace)]
    [InlineData("tests/other.test.ts", MaterialDestination.Workspace)]
    [InlineData("tests/public/order-management.test.ts", MaterialDestination.PublicReference)]
    [InlineData("tests/public/t1/find.test.ts", MaterialDestination.PublicReference)]
    [InlineData("tests/public/t1/nested/deep.test.ts", MaterialDestination.PublicReference)]
    [InlineData("Tests/Public/T1/find.test.ts", MaterialDestination.PublicReference)]
    [InlineData("tests/public/t2/receive.test.ts", MaterialDestination.Excluded)]
    [InlineData("tests/public/t11/later.test.ts", MaterialDestination.Excluded)]
    [InlineData("Tests/Public/T3/upper.test.ts", MaterialDestination.Excluded)]
    public void Route_ForTaskT1_SelectsDestinationAndKeepsPath(string sourcePath, MaterialDestination expected)
    {
        MaterialRoute route = TaskMaterialSelection.Route(sourcePath, "T1");

        Assert.Equal(expected, route.Destination);
        Assert.Equal(sourcePath, route.RelativePath);
    }

    [Theory]
    [InlineData("tests/public/helpers/setup.ts")]
    [InlineData("tests/public/t1-extra/file.ts")]
    [InlineData("tests/public/t0/file.ts")]
    public void Route_UnexpectedPublicTestsDirectory_Throws(string sourcePath)
    {
        Assert.Throws<RunPreparationException>(() => TaskMaterialSelection.Route(sourcePath, "T1"));
    }

    [Theory]
    [InlineData("T1", "T1")]
    [InlineData("t6", "T6")]
    [InlineData("T12", "T12")]
    public void NormalizeTaskId_AcceptsTaskIdentifiers(string taskId, string expected)
    {
        Assert.Equal(expected, TaskMaterialSelection.NormalizeTaskId(taskId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("T0")]
    [InlineData("T")]
    [InlineData("T100")]
    [InlineData("1")]
    [InlineData("T1 ")]
    [InlineData("T1\n")]
    [InlineData("T1/..")]
    public void NormalizeTaskId_RejectsInvalidIdentifiers(string? taskId)
    {
        Assert.Throws<RunPreparationException>(() => TaskMaterialSelection.NormalizeTaskId(taskId));
    }

    [Fact]
    public void TaskPaths_FollowRepositoryConventions()
    {
        Assert.Equal("docs/benchmark/T3-specifica-pubblica.md", TaskMaterialSelection.TaskSpecificationSourcePath("T3"));
        Assert.Equal("tests/public/t3", TaskMaterialSelection.TaskPublicTestsDirectory("T3"));
    }
}
