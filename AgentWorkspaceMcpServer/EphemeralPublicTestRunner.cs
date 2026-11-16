using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AgentWorkspaceMcpServer;

/// <summary>
/// Compone una copia effimera del progetto e vi esegue la suite pubblica controllata.
/// </summary>
public sealed class EphemeralPublicTestRunner
{
    private static readonly string[] T6CompatibleCommonTestNames =
    [
        "lists only product data ordered by product identifier",
        "returns the available quantity for P-001", "returns the available quantity for P-002",
        "returns the available quantity for P-003", "exposes a stable error code for an unknown product",
        "copies initial and returned product data", "keeps data independent between instances",
        "lists orders by textual identifier", "does not consume an identifier when creation fails",
        "rejects an empty order", "rejects an unknown product", "rejects invalid quantity 0",
        "rejects invalid quantity -1", "rejects invalid quantity 1.5",
        "rejects invalid quantity 9007199254740992", "rejects invalid quantity NaN",
        "rejects invalid quantity Infinity", "blocks a second confirmation without decrementing stock again",
        "reports ORDER_NOT_FOUND when attempting to read an unknown order",
        "reports ORDER_NOT_FOUND when attempting to confirm an unknown order",
        "keeps counters, orders, and stock isolated between instances",
    ];
    private readonly string agentRoot;
    private readonly string workerRoot;
    private readonly string nodePath;
    private readonly string vitestEntryPoint;
    private readonly string auditPath;
    private readonly TimeSpan timeout;
    private readonly string sourceDirectoryName;
    private readonly string includePattern;
    private readonly string operation;
    private readonly string suiteLabel;
    private readonly string[] excludedPatterns;
    private readonly string? testNamePattern;

    /// <summary>
    /// Configura le sorgenti della composizione e la toolchain fidata.
    /// </summary>
    public EphemeralPublicTestRunner(
        string agentRoot,
        string workerRoot,
        string nodePath,
        string vitestEntryPoint,
        string auditPath,
        TimeSpan timeout,
        string taskId)
        : this(
            agentRoot,
            workerRoot,
            nodePath,
            vitestEntryPoint,
            auditPath,
            timeout,
            "public-reference",
            "tests/public/**/*.test.ts",
            "public-tests",
            "pubblici",
            ["tests/reserved/**"],
            CreateTestNamePattern(taskId))
    {
    }

    internal EphemeralPublicTestRunner(
        string agentRoot,
        string workerRoot,
        string nodePath,
        string vitestEntryPoint,
        string auditPath,
        TimeSpan timeout,
        string sourceDirectoryName,
        string includePattern,
        string operation,
        string suiteLabel,
        string[] excludedPatterns,
        string? testNamePattern = null)
    {
        this.agentRoot = RequireDirectory(agentRoot, nameof(agentRoot));
        this.workerRoot = RequireAbsolutePath(workerRoot, nameof(workerRoot));
        this.nodePath = RequireFile(nodePath, nameof(nodePath));
        this.vitestEntryPoint = RequireFile(vitestEntryPoint, nameof(vitestEntryPoint));
        this.auditPath = RequireAbsolutePath(auditPath, nameof(auditPath));
        this.timeout = timeout > TimeSpan.Zero
            ? timeout
            : throw new ArgumentException("Il timeout deve essere positivo.", nameof(timeout));
        this.sourceDirectoryName = sourceDirectoryName;
        this.includePattern = includePattern;
        this.operation = operation;
        this.suiteLabel = suiteLabel;
        this.excludedPatterns = excludedPatterns;
        this.testNamePattern = testNamePattern;

        RequireDirectory(Path.Combine(this.agentRoot, "workspace"), "workspace");
        RequireDirectory(Path.Combine(this.agentRoot, sourceDirectoryName), sourceDirectoryName);
        if (IsWithin(this.workerRoot, this.agentRoot) || IsWithin(this.auditPath, this.agentRoot))
        {
            throw new ArgumentException("Worker e audit devono essere esterni alla root MCP dell'agente.");
        }
    }

    /// <summary>
    /// Crea una nuova composizione, esegue Vitest, verifica i test pubblici e rimuove la composizione.
    /// </summary>
    public async Task<ControlledCommandResult> RunAsync(CancellationToken cancellationToken = default)
    {
        string suiteSource = Path.Combine(agentRoot, sourceDirectoryName);
        string suiteHashBefore = ComputeTreeHash(suiteSource);
        string invocationRoot = Path.Combine(workerRoot, Guid.NewGuid().ToString("N"));
        string project = Path.Combine(invocationRoot, "project");
        string configDirectory = Path.Combine(invocationRoot, "config");
        string configPath = Path.Combine(configDirectory, $"vitest.{operation}.generated.mjs");
        string outputDirectory = Path.Combine(invocationRoot, "output");
        string reportPath = Path.Combine(outputDirectory, "vitest-results.json");

        Directory.CreateDirectory(project);
        Directory.CreateDirectory(configDirectory);
        Directory.CreateDirectory(outputDirectory);
        try
        {
            CopyTree(Path.Combine(agentRoot, "workspace"), project, rejectCollisions: true);
            CopyTree(suiteSource, project, rejectCollisions: true);
            await File.WriteAllTextAsync(
                configPath,
                BuildVitestConfiguration(),
                Encoding.UTF8,
                cancellationToken);

            var runner = new ControlledPublicTestRunner(
                nodePath,
                vitestEntryPoint,
                configPath,
                project,
                auditPath,
                timeout,
                operation,
                testNamePattern);
            ControlledCommandResult result = await runner.RunAsync(cancellationToken);
            result = AttachValidatedReport(result, reportPath, invocationRoot);
            string suiteHashAfter = ComputeTreeHash(suiteSource);
            if (!string.Equals(suiteHashBefore, suiteHashAfter, StringComparison.Ordinal))
            {
                return result with
                {
                    Status = "infrastructure-error",
                    StandardError = $"Integrita dei test {suiteLabel} violata durante l'esecuzione."
                };
            }

            return result;
        }
        finally
        {
            if (Directory.Exists(invocationRoot))
            {
                Directory.Delete(invocationRoot, recursive: true);
            }
        }
    }

    private string BuildVitestConfiguration()
    {
        return $"export default {{ test: {{ environment: 'node', include: ['{includePattern}'], exclude: [{string.Join(", ", excludedPatterns.Select(pattern => $"'{pattern}'"))}], outputFile: '../output/vitest-results.json' }} }};\n";
    }

    private static string? CreateTestNamePattern(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId) || taskId.Length != 2
            || taskId[0] is not ('T' or 't') || taskId[1] is < '1' or > '6')
        {
            throw new ArgumentException($"Task non valido per la suite pubblica: '{taskId}'.", nameof(taskId));
        }

        if (!taskId.Equals("T6", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string commonNames = string.Join("|", T6CompatibleCommonTestNames);
        return $"(?:{commonNames}|T6-P0[1-5]\\b)";
    }

    private ControlledCommandResult AttachValidatedReport(
        ControlledCommandResult result,
        string reportPath,
        string invocationRoot)
    {
        string sanitizedOutput = Sanitize(result.StandardOutput, invocationRoot);
        string sanitizedError = Sanitize(result.StandardError, invocationRoot);
        if (!File.Exists(reportPath))
        {
            return result with
            {
                Status = "infrastructure-error",
                StandardOutput = sanitizedOutput,
                StandardError = $"Vitest non ha prodotto il report JSON dei test {suiteLabel} atteso."
            };
        }

        try
        {
            using JsonDocument report = JsonDocument.Parse(File.ReadAllText(reportPath));
            JsonElement root = report.RootElement;
            var counts = new PublicTestCounts(
                RequiredNonNegative(root, "numTotalTests"),
                RequiredNonNegative(root, "numPassedTests"),
                RequiredNonNegative(root, "numFailedTests"),
                RequiredNonNegative(root, "numPendingTests"),
                RequiredNonNegative(root, "numTotalTestSuites"),
                RequiredNonNegative(root, "numPassedTestSuites"),
                RequiredNonNegative(root, "numFailedTestSuites"),
                RequiredNonNegative(root, "numPendingTestSuites"));
            if (counts.Passed + counts.Failed + counts.Pending != counts.Total)
            {
                throw new InvalidDataException("I conteggi dei test non sono coerenti.");
            }

            return result with
            {
                StandardOutput = sanitizedOutput,
                StandardError = sanitizedError,
                Tests = counts
            };
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or InvalidDataException)
        {
            return result with
            {
                Status = "infrastructure-error",
                StandardOutput = sanitizedOutput,
                StandardError = $"Report Vitest dei test {suiteLabel} non valido: {exception.Message}",
                Tests = null
            };
        }
    }

    private static int RequiredNonNegative(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement property)
            || !property.TryGetInt32(out int value)
            || value < 0)
        {
            throw new InvalidDataException($"Proprieta mancante o non valida: {propertyName}.");
        }

        return value;
    }

    private string Sanitize(string diagnostic, string invocationRoot)
    {
        var replacements = new[]
        {
            (invocationRoot, "<worker>"),
            (agentRoot, "<agent-root>"),
            (Path.GetDirectoryName(vitestEntryPoint)!, "<toolchain>"),
            (nodePath, "<node>")
        };
        foreach ((string path, string placeholder) in replacements.OrderByDescending(item => item.Item1.Length))
        {
            diagnostic = diagnostic.Replace(path, placeholder, StringComparison.OrdinalIgnoreCase);
        }

        return diagnostic;
    }

    private static void CopyTree(string sourceRoot, string destinationRoot, bool rejectCollisions)
    {
        foreach (string directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            RejectLink(directory);
            string relativePath = Path.GetRelativePath(sourceRoot, directory);
            string destination = Path.Combine(destinationRoot, relativePath);
            if (File.Exists(destination))
            {
                throw new IOException($"Collisione nella composizione: '{relativePath}'.");
            }

            Directory.CreateDirectory(destination);
        }

        foreach (string file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            RejectLink(file);
            string relativePath = Path.GetRelativePath(sourceRoot, file);
            string destination = Path.Combine(destinationRoot, relativePath);
            if (rejectCollisions && File.Exists(destination))
            {
                throw new IOException($"Collisione nella composizione: '{relativePath}'.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: false);
        }
    }

    private static string ComputeTreeHash(string root)
    {
        using IncrementalHash aggregate = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal))
        {
            RejectLink(file);
            string relativePath = Path.GetRelativePath(root, file).Replace('\\', '/');
            aggregate.AppendData(Encoding.UTF8.GetBytes(relativePath));
            aggregate.AppendData([0]);
            aggregate.AppendData(SHA256.HashData(File.ReadAllBytes(file)));
        }

        return Convert.ToHexString(aggregate.GetHashAndReset()).ToLowerInvariant();
    }

    private static void RejectLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Collegamento non ammesso nella composizione: '{path}'.");
        }
    }

    private static string RequireFile(string path, string parameterName)
    {
        string fullPath = RequireAbsolutePath(path, parameterName);
        return File.Exists(fullPath)
            ? fullPath
            : throw new FileNotFoundException("Il file configurato non esiste.", fullPath);
    }

    private static string RequireDirectory(string path, string parameterName)
    {
        string fullPath = RequireAbsolutePath(path, parameterName);
        return Directory.Exists(fullPath)
            ? fullPath
            : throw new DirectoryNotFoundException($"La directory configurata non esiste: '{fullPath}'.");
    }

    private static string RequireAbsolutePath(string path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("Il percorso deve essere assoluto.", parameterName);
        }

        return Path.GetFullPath(path);
    }

    private static bool IsWithin(string candidate, string directory)
    {
        string prefix = Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
