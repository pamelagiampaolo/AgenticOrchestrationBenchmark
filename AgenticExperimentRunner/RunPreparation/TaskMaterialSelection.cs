using System.Text.RegularExpressions;

namespace AgenticExperimentRunner.RunPreparation;

/// <summary>
/// Area di destinazione di un file della baseline.
/// </summary>
public enum MaterialDestination
{
    /// <summary>Sorgenti e configurazioni modificabili dall'agente.</summary>
    Workspace,

    /// <summary>Test pubblici consegnati in sola lettura.</summary>
    PublicReference,

    /// <summary>File non consegnato all'agente (test pubblici di altri task).</summary>
    Excluded,
}

/// <summary>
/// Destinazione di un file della baseline.
/// </summary>
/// <param name="Destination">Area di destinazione.</param>
/// <param name="RelativePath">
/// Percorso relativo all'area di destinazione, con separatore <c>/</c>; per i file
/// esclusi coincide con il percorso relativo alla sorgente.
/// </param>
public sealed record MaterialRoute(MaterialDestination Destination, string RelativePath);

/// <summary>
/// Regole, prive di accesso al file system, che stabiliscono quali materiali di un
/// task raggiungono l'agente e dove.
/// </summary>
/// <remarks>
/// Si espongono soltanto le suite pubbliche comuni e quella del task selezionato
/// (decisione dell'utente dell'8 novembre 2026). Il prefisso <c>tests/public/</c>
/// è conservato in <c>public-reference/</c> perché gli import relativi dei test
/// restino validi quando il worker sovrappone i test a una copia della workspace.
/// </remarks>
public static partial class TaskMaterialSelection
{
    private const string PublicTestsPrefix = "tests/public/";

    /// <summary>
    /// Verifica e normalizza l'identificatore di un task (<c>T1</c>, <c>T2</c>, ...).
    /// </summary>
    /// <param name="taskId">Identificatore fornito dall'utente.</param>
    /// <returns>L'identificatore in forma canonica, con <c>T</c> maiuscola.</returns>
    /// <exception cref="RunPreparationException">Il formato non è <c>T</c> seguito da un numero da 1 a 99.</exception>
    public static string NormalizeTaskId(string? taskId)
    {
        if (taskId is null || !TaskIdPattern().IsMatch(taskId))
        {
            throw new RunPreparationException(
                $"Task non valido: '{taskId}'. Il formato atteso è 'T' seguito da un numero (per esempio T1).");
        }

        return "T" + taskId[1..];
    }

    /// <summary>
    /// Percorso, relativo alla radice del repository, della specifica pubblica del task.
    /// </summary>
    /// <param name="taskId">Identificatore canonico del task.</param>
    /// <returns>Per esempio <c>docs/benchmark/T1-specifica-pubblica.md</c>.</returns>
    public static string TaskSpecificationSourcePath(string taskId) =>
        $"docs/benchmark/{taskId}-specifica-pubblica.md";

    /// <summary>
    /// Directory dei test pubblici del task, relativa alla sorgente della baseline.
    /// </summary>
    /// <param name="taskId">Identificatore canonico del task.</param>
    /// <returns>Per esempio <c>tests/public/t1</c>.</returns>
    public static string TaskPublicTestsDirectory(string taskId) =>
        PublicTestsPrefix + taskId.ToLowerInvariant();

    /// <summary>
    /// Stabilisce dove va un file della baseline.
    /// </summary>
    /// <param name="sourceRelativePath">
    /// Percorso del file relativo alla sorgente della baseline, già autorizzato da
    /// <see cref="WorkspacePathPolicy"/>.
    /// </param>
    /// <param name="taskId">Identificatore canonico del task selezionato.</param>
    /// <returns>La destinazione del file.</returns>
    /// <exception cref="RunPreparationException">
    /// Il file è in una sottocartella di <c>tests/public/</c> che non corrisponde a
    /// un task: la struttura non è prevista e non si sa se vada esposta.
    /// </exception>
    public static MaterialRoute Route(string sourceRelativePath, string taskId)
    {
        if (!sourceRelativePath.StartsWith(PublicTestsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return new MaterialRoute(MaterialDestination.Workspace, sourceRelativePath);
        }

        string insidePublicTests = sourceRelativePath[PublicTestsPrefix.Length..];
        int separatorIndex = insidePublicTests.IndexOf('/', StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            // File direttamente in tests/public/: suite comune a tutti i task.
            return new MaterialRoute(MaterialDestination.PublicReference, sourceRelativePath);
        }

        string taskDirectory = insidePublicTests[..separatorIndex];
        if (string.Equals(taskDirectory, taskId, StringComparison.OrdinalIgnoreCase))
        {
            return new MaterialRoute(MaterialDestination.PublicReference, sourceRelativePath);
        }

        if (TaskIdPattern().IsMatch(taskDirectory))
        {
            return new MaterialRoute(MaterialDestination.Excluded, sourceRelativePath);
        }

        throw new RunPreparationException(
            $"Struttura dei test pubblici non prevista: '{sourceRelativePath}'. In tests/public/ sono ammessi solo file comuni e cartelle tN.");
    }

    [GeneratedRegex(@"^[Tt][1-9][0-9]?\z")]
    private static partial Regex TaskIdPattern();
}
